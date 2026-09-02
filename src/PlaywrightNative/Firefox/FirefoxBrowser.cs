/*
 * Copyright (c) 2020 Darío Kondratiuk
 * Modifications copyright (c) Microsoft Corporation. 
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Firefox
{
    /// <summary>
    /// Public <see cref="IBrowser"/> wrapping <see cref="FFBrowser"/>.
    /// Initial implementation — lifecycle wired; full surface deferred.
    /// </summary>
    internal sealed partial class FirefoxBrowser : IBrowser
    {
        private readonly FFBrowser _browser;

        internal FirefoxBrowser(FFBrowser browser)
        {
            _browser = browser ?? throw new ArgumentNullException(nameof(browser));
        }

        /// <inheritdoc/>
        public event EventHandler<IBrowser> Disconnected
        {
            add => throw NotImplementedHelper.ForMethod(nameof(Disconnected));
            remove => throw NotImplementedHelper.ForMethod(nameof(Disconnected));
        }

        /// <inheritdoc/>
        public event EventHandler<IBrowserContext> Context;

        /// <inheritdoc/>
        public IReadOnlyList<IBrowserContext> Contexts
            => throw NotImplementedHelper.ForMethod(nameof(Contexts));

        /// <inheritdoc/>
        public bool IsConnected => _browser.IsConnected;

        /// <inheritdoc/>
        public string Version => _browser.Version;

        /// <inheritdoc/>
        public IBrowserType BrowserType => BrowserTypeInfo.Firefox;

        /// <inheritdoc/>
        public Task CloseAsync(string reason = default)
        {
            _ = reason;
            return _browser.CloseAsync();
        }

        /// <inheritdoc/>
        public Task<ICDPSession> NewBrowserCDPSessionAsync()
            => throw new PlaywrightNativeException("CDP sessions are only supported in Chromium.");

        /// <inheritdoc/>
        public Task StartTracingAsync(IPage page = default, string path = default, bool screenshots = default, IEnumerable<string> categories = default)
            => throw new PlaywrightNativeException("startTracing is only supported in Chromium.");

        /// <inheritdoc/>
        public Task<byte[]> StopTracingAsync()
            => throw new PlaywrightNativeException("stopTracing is only supported in Chromium.");

        /// <inheritdoc/>
        public Task<IBrowserContext> NewContextAsync(BrowserContextOptions options)
            => throw NotImplementedHelper.ForMethod(nameof(NewContextAsync));

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
            BrowserContextOptionGuard.ThrowIfNullViewportConflicts(viewportSize, deviceScaleFactor, isMobile);
            BrowserContextOptionGuard.ThrowIfInvalidProxy(proxy);
            ClientCertificateHelper.Verify(clientCertificates);
            FFBrowserContext ctx = await _browser.NewContextAsync().ConfigureAwait(false);
            FirefoxBrowserContext instance = new(ctx, this);
            instance.AttachClientCertificates(clientCertificates);
            instance.StrictSelectors = strictSelectors == true;
            HarRecorder.Start(instance, recordHarPath, recordHarOmitContent, recordHarUrl, recordHarMode, recordHarContent, recordHarUrlRegex);
            VideoRecorder.Start(instance, recordVideoDir, recordVideoSize, viewportSize);
            await ServiceWorkerPolicyHelper.ApplyAsync(instance, serviceWorkers).ConfigureAwait(false);
            Context?.Invoke(this, instance);
            return instance;
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
            if (context is FirefoxBrowserContext owned)
            {
                owned.OwnedByBrowserNewPage = true;
            }

            return page;
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            await _browser.DisposeAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Persistent context created by <c>LaunchPersistentContextAsync</c>.
        /// </summary>
        /// <returns>The default profile context.</returns>
        internal IBrowserContext PersistentContext()
        {
            FFBrowserContext context = _browser.DefaultContext;
            if (context == null)
            {
                throw new PlaywrightNativeException("Browser was not launched as a persistent context.");
            }

            return new FirefoxBrowserContext(context, this);
        }

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
