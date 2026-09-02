/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Chromium
{
    /// <summary>Public <see cref="IBrowser"/> wrapping <see cref="CRBrowser"/>.</summary>
    internal sealed partial class ChromiumBrowser : IBrowser, IHasDefaultUserAgent, IHasPlaywrightLogger, IHasLaunchProxy, IHasTracesDir, IHasArtifactsDir
    {
        private static readonly string[] DefaultTracingCategories =
        {
            "-*",
            "devtools.timeline",
            "v8.execute",
            "disabled-by-default-devtools.timeline",
            "disabled-by-default-devtools.timeline.frame",
            "toplevel",
            "blink.console",
            "blink.user_timing",
            "latencyInfo",
            "disabled-by-default-devtools.timeline.stack",
            "disabled-by-default-v8.cpu_profiler",
            "disabled-by-default-v8.cpu_profiler.hires",
        };

        private readonly CRBrowser _crBrowser;
        private readonly ConcurrentDictionary<CRBrowserContext, ChromiumBrowserContext> _contexts = new();
        private readonly string _downloadsPath;
        private CRSession _tracingClient;
        private string _tracingPath;
        private bool _tracingRecording;

        internal ChromiumBrowser(CRBrowser crBrowser, string downloadsPath = null, IPlaywrightLogger logger = null)
        {
            _crBrowser = crBrowser ?? throw new ArgumentNullException(nameof(crBrowser));
            _downloadsPath = downloadsPath;
            Logger = logger;
            _crBrowser.Disconnected += (_, _) => Disconnected?.Invoke(this, this);
        }

        /// <inheritdoc/>
        public event EventHandler<IBrowser> Disconnected;

        /// <inheritdoc/>
        public event EventHandler<IBrowserContext> Context;

        /// <inheritdoc/>
        public IReadOnlyList<IBrowserContext> Contexts
        {
            get
            {
                List<IBrowserContext> contexts = new List<IBrowserContext>();
                foreach (CRBrowserContext crCtx in _crBrowser.Contexts)
                {
                    contexts.Add(GetOrCreateContext(crCtx));
                }

                return contexts;
            }
        }

        /// <inheritdoc/>
        public bool IsConnected => _crBrowser.IsConnected;

        /// <inheritdoc/>
        public string Version => _crBrowser.Version;

        string IHasDefaultUserAgent.DefaultUserAgent => _crBrowser.UserAgent;

        /// <inheritdoc/>
        public IBrowserType BrowserType => BrowserTypeInfo.Chromium;

        /// <inheritdoc/>
        public IPlaywrightLogger Logger { get; set; }

        /// <inheritdoc/>
        Proxy IHasLaunchProxy.LaunchProxy => LaunchProxy;

        /// <inheritdoc/>
        string IHasTracesDir.TracesDir { get; set; }

        /// <inheritdoc/>
        string IHasArtifactsDir.ArtifactsDir { get; set; }

        /// <summary>
        /// Launch-level <c>proxy</c> from <c>browserType.launch</c>.
        /// </summary>
        internal Proxy LaunchProxy { get; set; }

        /// <inheritdoc/>
        public async Task CloseAsync(string reason = default)
        {
            foreach (IBrowserContext context in Contexts)
            {
                if (context is ChromiumBrowserContext instance)
                {
                    instance.RecordCloseReason(reason);
                    instance.CleanupDownloadsOnBrowserClose();
                    try
                    {
                        await VideoRecorder.FlushAsync(instance).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger?.Log("browser", PlaywrightLogSeverity.Error, "Video flush during browser close failed: " + ex.Message);
                    }

                    instance.NotifyClosedFromBrowser();
                }
            }

            await _crBrowser.CloseAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<ICDPSession> NewBrowserCDPSessionAsync()
        {
            CRSession session = await _crBrowser.AttachToBrowserTargetAsync().ConfigureAwait(false);
            return new CRCDPSession(session, _crBrowser.Connection.RootSession);
        }

        /// <inheritdoc/>
        public async Task StartTracingAsync(IPage page = default, string path = default, bool screenshots = default, IEnumerable<string> categories = default)
        {
            if (_tracingRecording)
            {
                throw new PlaywrightNativeException("Cannot start recording trace while already recording trace.");
            }

            Page chromiumPage = page as Page;
            if (page != null && chromiumPage == null)
            {
                throw new PlaywrightNativeException("startTracing requires a Chromium page.");
            }

            CRSession client = chromiumPage != null
                ? chromiumPage.CrPage.Session
                : _crBrowser.Connection.RootSession;

            List<string> cats = categories == null
                ? new List<string>(DefaultTracingCategories)
                : new List<string>(categories);
            if (screenshots)
            {
                cats.Add("disabled-by-default-devtools.screenshot");
            }

            _tracingClient = client;
            _tracingPath = path;
            _tracingRecording = true;
            await client.SendAsync("Tracing.start", new
            {
                transferMode = "ReturnAsStream",
                categories = string.Join(",", cats),
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<byte[]> StopTracingAsync()
        {
            if (_tracingClient == null)
            {
                throw new PlaywrightNativeException("Tracing was not started.");
            }

            CRSession client = _tracingClient;
            TaskCompletionSource<JsonElement?> complete = new(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnMessage(string method, JsonElement? parameters)
            {
                if (method == "Tracing.tracingComplete")
                {
                    complete.TrySetResult(parameters);
                }
            }

            client.MessageReceived += OnMessage;
            try
            {
                Task end = client.SendAsync("Tracing.end");
                await Task.WhenAll(complete.Task, end).ConfigureAwait(false);
            }
            finally
            {
                client.MessageReceived -= OnMessage;
            }

            JsonElement? completeParams = await complete.Task.ConfigureAwait(false);
            string handle = null;
            if (completeParams.HasValue
                && completeParams.Value.TryGetProperty("stream", out JsonElement streamElement))
            {
                handle = streamElement.GetString();
            }

            byte[] buffer = await ReadProtocolStreamAsync(client, handle).ConfigureAwait(false);
            _tracingRecording = false;
            _tracingClient = null;
            string outputPath = _tracingPath;
            _tracingPath = null;
            if (!string.IsNullOrEmpty(outputPath))
            {
                string directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllBytes(outputPath, buffer);
            }

            return buffer;
        }

        /// <inheritdoc/>
        public async Task<IBrowserContext> NewContextAsync(BrowserContextOptions options)
        {
            IBrowserContext context = options == null
                ? await NewContextAsync().ConfigureAwait(false)
                : await NewContextAsync(
                extraHTTPHeaders: options.ExtraHTTPHeaders,
                userAgent: options.UserAgent,
                viewportSize: options.Viewport,
                locale: options.Locale,
                timezoneId: options.TimezoneId,
                offline: options.Offline,
                colorScheme: options.ColorScheme,
                hasTouch: options.HasTouch,
                bypassCSP: options.BypassCSP,
                geolocation: options.Geolocation,
                permissions: options.Permissions,
                ignoreHTTPSErrors: options.IgnoreHTTPSErrors,
                javaScriptEnabled: options.JavaScriptEnabled,
                deviceScaleFactor: options.DeviceScaleFactor,
                isMobile: options.IsMobile,
                httpCredentials: options.HttpCredentials,
                screenSize: options.ScreenSize,
                acceptDownloads: options.AcceptDownloads,
                storageState: options.StorageState,
                storageStatePath: options.StorageStatePath,
                proxy: options.Proxy,
                recordHarPath: options.RecordHarPath,
                recordHarOmitContent: options.RecordHarOmitContent,
                recordHarUrl: options.RecordHarUrl,
                baseURL: options.BaseURL,
                recordHarMode: options.RecordHarMode,
                serviceWorkers: options.ServiceWorkers,
                reducedMotion: options.ReducedMotion,
                forcedColors: options.ForcedColors,
                contrast: options.Contrast,
                recordHarContent: options.RecordHarContent,
                recordHarUrlRegex: options.RecordHarUrlRegex,
                recordVideoDir: options.RecordVideoDir,
                recordVideoSize: options.RecordVideoSize,
                strictSelectors: options.StrictSelectors,
                clientCertificates: options.ClientCertificates).ConfigureAwait(false);

            if (context is IHasPlaywrightLogger has)
            {
                has.Logger = options?.Logger ?? Logger;
            }

            return context;
        }

        /// <inheritdoc/>
        public async Task<IBrowserContext> NewContextAsync(
            bool? acceptDownloads = default,
            bool? bypassCSP = default,
            ColorScheme colorScheme = default,
            float? deviceScaleFactor = default,
            IEnumerable<KeyValuePair<string, string>> extraHTTPHeaders = default,
            Geolocation geolocation = default,
            bool? hasTouch = default,
            HttpCredentials httpCredentials = default,
            bool? ignoreHTTPSErrors = default,
            bool? isMobile = default,
            bool? javaScriptEnabled = default,
            string locale = default,
            bool? offline = default,
            IEnumerable<string> permissions = default,
            Proxy proxy = default,
            bool? recordHarOmitContent = default,
            string recordHarPath = default,
            string recordVideoDir = default,
            RecordVideoSize recordVideoSize = default,
            ScreenSize screenSize = default,
            string storageState = default,
            string storageStatePath = default,
            string timezoneId = default,
            string userAgent = default,
            ViewportSize viewportSize = default,
            string recordHarUrl = default,
            string baseURL = default,
            HarMode recordHarMode = default,
            ServiceWorkerPolicy serviceWorkers = default,
            ReducedMotion reducedMotion = default,
            ForcedColors forcedColors = default,
            Contrast contrast = default,
            HarContentPolicy recordHarContent = default,
            Regex recordHarUrlRegex = default,
            bool? strictSelectors = default,
            IEnumerable<ClientCertificate> clientCertificates = default)
        {
            return await PlaywrightApiLog.RunAsync(Logger, "browser.newContext", async () =>
            {
                // Official browserContext.ts: context._options.proxy || browser.options.proxy
                proxy ??= LaunchProxy;
                BrowserContextOptionGuard.ThrowIfNullViewportConflicts(viewportSize, deviceScaleFactor, isMobile);
                BrowserContextOptionGuard.ThrowIfInvalidProxy(proxy);
                ClientCertificatesProxy certsProxy = ClientCertificatesProxy.TryStart(
                    clientCertificates,
                    ignoreHTTPSErrors == true,
                    proxy,
                    out Proxy browserProxy);
                LocaleHandshakeProxy handshake = certsProxy == null
                    ? LocaleHandshakeProxy.TryStart(locale, browserProxy, out browserProxy)
                    : null;
                CRBrowserContext crCtx;
                try
                {
                    crCtx = await _crBrowser.NewContextAsync(browserProxy).ConfigureAwait(false);
                }
                catch
                {
                    handshake?.Dispose();
                    certsProxy?.Dispose();
                    throw;
                }

                ChromiumBrowserContext instance = GetOrCreateContext(crCtx);
                instance.AttachLocaleHandshake(handshake);
                instance.AttachClientCertificatesProxy(certsProxy, proxy);
                instance.AttachClientCertificates(clientCertificates);
                instance.BaseURL = baseURL;
                instance.StrictSelectors = strictSelectors == true;
                instance.ConfigureEmulation(
                    viewportSize,
                    userAgent,
                    extraHTTPHeaders,
                    locale,
                    timezoneId,
                    offline,
                    colorScheme,
                    hasTouch,
                    bypassCSP,
                    geolocation,
                    permissions,
                    ignoreHTTPSErrors,
                    javaScriptEnabled,
                    deviceScaleFactor,
                    isMobile,
                    httpCredentials,
                    screenSize,
                    acceptDownloads,
                    reducedMotion,
                    forcedColors,
                    contrast);
                await instance.ApplyDownloadBehaviorAsync().ConfigureAwait(false);
                await StorageStateHelper.ApplyAsync(instance, storageState, storageStatePath).ConfigureAwait(false);
                HarRecorder.Start(instance, recordHarPath, recordHarOmitContent, recordHarUrl, recordHarMode, recordHarContent, recordHarUrlRegex);
                VideoRecorder.Start(instance, recordVideoDir, recordVideoSize, viewportSize);
                await ServiceWorkerPolicyHelper.ApplyAsync(instance, serviceWorkers).ConfigureAwait(false);
                Context?.Invoke(this, instance);
                instance.Logger = Logger;
                return instance;
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IPage> NewPageAsync(
            bool? acceptDownloads = default,
            bool? bypassCSP = default,
            ColorScheme colorScheme = default,
            float? deviceScaleFactor = default,
            IEnumerable<KeyValuePair<string, string>> extraHTTPHeaders = default,
            Geolocation geolocation = default,
            bool? hasTouch = default,
            HttpCredentials httpCredentials = default,
            bool? ignoreHTTPSErrors = default,
            bool? isMobile = default,
            bool? javaScriptEnabled = default,
            string locale = default,
            bool? offline = default,
            IEnumerable<string> permissions = default,
            Proxy proxy = default,
            bool? recordHarOmitContent = default,
            string recordHarPath = default,
            string recordVideoDir = default,
            RecordVideoSize recordVideoSize = default,
            ScreenSize screenSize = default,
            string storageState = default,
            string storageStatePath = default,
            string timezoneId = default,
            string userAgent = default,
            ViewportSize viewportSize = default,
            string recordHarUrl = default,
            string baseURL = default,
            HarMode recordHarMode = default,
            ServiceWorkerPolicy serviceWorkers = default,
            ReducedMotion reducedMotion = default,
            ForcedColors forcedColors = default,
            Contrast contrast = default,
            HarContentPolicy recordHarContent = default,
            Regex recordHarUrlRegex = default,
            bool? strictSelectors = default,
            IEnumerable<ClientCertificate> clientCertificates = default)
        {
            IBrowserContext context = await NewContextAsync(
                extraHTTPHeaders: extraHTTPHeaders,
                userAgent: userAgent,
                viewportSize: viewportSize,
                locale: locale,
                timezoneId: timezoneId,
                offline: offline,
                colorScheme: colorScheme,
                hasTouch: hasTouch,
                bypassCSP: bypassCSP,
                geolocation: geolocation,
                permissions: permissions,
                ignoreHTTPSErrors: ignoreHTTPSErrors,
                javaScriptEnabled: javaScriptEnabled,
                deviceScaleFactor: deviceScaleFactor,
                isMobile: isMobile,
                httpCredentials: httpCredentials,
                screenSize: screenSize,
                acceptDownloads: acceptDownloads,
                storageState: storageState,
                storageStatePath: storageStatePath,
                proxy: proxy,
                recordHarPath: recordHarPath,
                recordHarOmitContent: recordHarOmitContent,
                recordHarUrl: recordHarUrl,
                baseURL: baseURL,
                recordHarMode: recordHarMode,
                serviceWorkers: serviceWorkers,
                reducedMotion: reducedMotion,
                forcedColors: forcedColors,
                contrast: contrast,
                recordHarContent: recordHarContent,
                recordHarUrlRegex: recordHarUrlRegex,
                strictSelectors: strictSelectors,
                clientCertificates: clientCertificates).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            if (page is Page instance)
            {
                instance.OwnedContext = context;
            }

            if (context is ChromiumBrowserContext owned)
            {
                owned.OwnedByBrowserNewPage = true;
            }

            return page;
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            await _crBrowser.DisposeAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Persistent context created by <c>LaunchPersistentContextAsync</c>.
        /// </summary>
        /// <returns>The default context.</returns>
        internal IBrowserContext PersistentContext()
        {
            CRBrowserContext context = _crBrowser.DefaultContext;
            if (context == null)
            {
                throw new PlaywrightNativeException("Browser was not launched as a persistent context.");
            }

            return GetOrCreateContext(context);
        }

        private static async Task<byte[]> ReadProtocolStreamAsync(CRSession session, string handle)
        {
            if (string.IsNullOrEmpty(handle))
            {
                return Array.Empty<byte>();
            }

            List<byte> chunks = new List<byte>();
            while (true)
            {
                JsonElement? chunk = await session.SendAsync("IO.read", new { handle }).ConfigureAwait(false);
                if (!chunk.HasValue)
                {
                    break;
                }

                JsonElement chunkValue = chunk.Value;
                string data = chunkValue.TryGetProperty("data", out JsonElement dataElement)
                    ? dataElement.GetString() ?? string.Empty
                    : string.Empty;
                bool base64Encoded = chunkValue.TryGetProperty("base64Encoded", out JsonElement encodedElement)
                    && encodedElement.GetBoolean();
                byte[] bytes = base64Encoded ? Convert.FromBase64String(data) : Encoding.UTF8.GetBytes(data);
                if (bytes.Length > 0)
                {
                    chunks.AddRange(bytes);
                }

                if (chunkValue.TryGetProperty("eof", out JsonElement eofElement) && eofElement.GetBoolean())
                {
                    try
                    {
                        await session.SendAsync("IO.close", new { handle }).ConfigureAwait(false);
                    }
                    catch (PlaywrightNativeException)
                    {
                    }

                    break;
                }
            }

            return chunks.ToArray();
        }

        private ChromiumBrowserContext GetOrCreateContext(CRBrowserContext crCtx)
            => _contexts.GetOrAdd(crCtx, ctx =>
            {
                ChromiumBrowserContext instance = new(ctx, this);
                instance.UseLaunchDownloadsPath(_downloadsPath);
                return instance;
            });

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task<BrowserBindResult> IBrowser.BindAsync(string title, BrowserBindOptions options) => Task.FromResult<BrowserBindResult>(default!);

        Task IBrowser.CloseAsync(BrowserCloseOptions options) => CloseAsync();

        Task<IBrowserContext> IBrowser.NewContextAsync(BrowserNewContextOptions options)
            => NewContextAsync(MicrosoftOptionsBridge.ToBrowserContextOptions(options));

        Task<IPage> IBrowser.NewPageAsync(BrowserNewPageOptions options)
        {
            BrowserContextOptions sharpOptions = MicrosoftOptionsBridge.ToBrowserContextOptions(options);
            if (sharpOptions == null)
            {
                return NewPageAsync();
            }

            return BrowserCompatExtensions.NewPageAsync(this, sharpOptions);
        }

        Task IBrowser.UnbindAsync() => Task.CompletedTask;
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
