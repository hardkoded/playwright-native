/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Copyright (c) 2020 Meir Blachman
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PlaywrightNative.Helpers;
using PlaywrightNative.Transport;
using PlaywrightNative.Transport.Protocol;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Handles launching Chromium browsers with correct default arguments,
    /// establishing a WebSocket connection to the DevTools endpoint, and
    /// creating a <see cref="CRBrowser"/> instance.
    /// </summary>
    internal static class ChromiumBrowserType
    {
        private static readonly string[] DefaultArgs =
        [
            "--disable-background-networking",
            "--enable-features=NetworkService,NetworkServiceInProcess",
            "--disable-background-timer-throttling",
            "--disable-backgrounding-occluded-windows",
            "--disable-back-forward-cache",
            "--disable-breakpad",
            "--disable-component-extensions-with-background-pages",
            "--disable-component-update",
            "--disable-default-apps",
            "--disable-dev-shm-usage",
            "--disable-extensions",
            "--disable-hang-monitor",
            "--disable-ipc-flooding-protection",
            "--disable-popup-blocking",
            "--disable-prompt-on-repost",
            "--disable-renderer-backgrounding",
            "--disable-search-engine-choice-screen",
            "--disable-sync",
            "--enable-unsafe-swiftshader",
            "--force-color-profile=srgb",
            "--metrics-recording-only",
            "--no-first-run",
            "--password-store=basic",
            "--use-mock-keychain",
            "--no-service-autorun",
            "--export-tagged-pdf",
            "--enable-automation",

            // Official Playwright Chromium does not enforce Local Network Access on
            // localhost↔127.0.0.1 iframe navigations. Chrome 148+ does, so disable
            // the checks to keep automation aligned with official bundled Chromium.
            "--disable-features=ThirdPartyStoragePartitioning,LocalNetworkAccessChecks",

            // Locale handshake proxy must see localhost WebSocket upgrades.
            // Chromium otherwise bypasses loopback (Chrome < 151 ignores locale on WS).
            "--proxy-bypass-list=<-loopback>",
        ];

        private static readonly string[] HeadlessArgs =
        [
            "--headless",
            "--hide-scrollbars",
            "--mute-audio",

            // Headless Linux has no mouse, so Blink reports hover:none / pointer:none.
            // Official Playwright desktop pages match (hover: hover) and (pointer: fine).
            "--blink-settings=primaryHoverType=2,availableHoverTypes=2,primaryPointerType=4,availablePointerTypes=4",
        ];

        /// <summary>
        /// Builds the list of command-line arguments for launching Chromium.
        /// </summary>
        /// <param name="headless">Whether to launch in headless mode.</param>
        /// <param name="chromiumSandbox">Whether to enable the Chromium sandbox.</param>
        /// <param name="additionalArgs">Optional additional arguments to append.</param>
        /// <param name="ignoreDefaultArgs">When <see langword="true"/>, skip built-in default arguments.</param>
        /// <param name="devtools">When <see langword="true"/>, add <c>--auto-open-devtools-for-tabs</c>.</param>
        /// <param name="ignoreDefaultArgsList">Default switches to omit by exact match. Ignored when <paramref name="ignoreDefaultArgs"/> is <see langword="true"/>.</param>
        /// <returns>The complete list of arguments.</returns>
        internal static List<string> GetDefaultArgs(bool headless = true, bool chromiumSandbox = false, string[] additionalArgs = null, bool ignoreDefaultArgs = false, bool devtools = false, IEnumerable<string> ignoreDefaultArgsList = null)
        {
            List<string> args = ignoreDefaultArgs ? new List<string>() : new List<string>(DefaultArgs);

            if (!ignoreDefaultArgs)
            {
                if (headless)
                {
                    args.AddRange(HeadlessArgs);
                }

                if (!chromiumSandbox)
                {
                    args.Add("--no-sandbox");
                }
            }

            if (additionalArgs != null)
            {
                foreach (string extra in additionalArgs)
                {
                    if (extra != null && extra.StartsWith("--remote-debugging-pipe", StringComparison.Ordinal))
                    {
                        // Official _innerDefaultArgs: Playwright owns the
                        // debugging transport (pipe or websocket).
                        throw new PlaywrightNativeException(
                            "Playwright manages remote debugging connection itself.");
                    }
                }

                args.AddRange(additionalArgs);
                if (!ignoreDefaultArgs)
                {
                    bool loadsExtensions = false;
                    foreach (string extra in additionalArgs)
                    {
                        if (extra != null
                            && (extra.StartsWith("--load-extension", StringComparison.Ordinal)
                                || extra.StartsWith("--disable-extensions-except", StringComparison.Ordinal)))
                        {
                            loadsExtensions = true;
                            break;
                        }
                    }

                    if (loadsExtensions)
                    {
                        // Official tests pass --load-extension / --disable-extensions-except
                        // alongside chromiumSwitches. --disable-extensions must not win.
                        args.RemoveAll(arg => string.Equals(arg, "--disable-extensions", StringComparison.Ordinal));
                    }
                }
            }

            if (devtools)
            {
                args.Add("--auto-open-devtools-for-tabs");
            }

            if (!ignoreDefaultArgs && ignoreDefaultArgsList != null)
            {
                HashSet<string> omitted = new(ignoreDefaultArgsList, StringComparer.Ordinal);
                args.RemoveAll(arg => omitted.Contains(arg));
            }

            return args;
        }

        /// <summary>
        /// Launches a Chromium browser process and connects to it via WebSocket.
        /// </summary>
        /// <param name="executablePath">Path to the Chromium executable.</param>
        /// <param name="headless">Whether to launch in headless mode. Defaults to <c>true</c>.</param>
        /// <param name="args">Optional additional command-line arguments.</param>
        /// <param name="proxy">Optional proxy configuration.</param>
        /// <param name="chromiumSandbox">Whether to enable the Chromium sandbox. Defaults to <c>false</c>.</param>
        /// <param name="timeout">Launch timeout in milliseconds. Defaults to 30000.</param>
        /// <param name="ignoreDefaultArgs">When <see langword="true"/>, skip built-in default arguments.</param>
        /// <param name="environment">Optional extra environment variables for the browser process.</param>
        /// <param name="loggerFactory">Optional logger factory for diagnostic output.</param>
        /// <param name="devtools">When <see langword="true"/>, open DevTools for each tab.</param>
        /// <param name="userDataDir">Optional persistent user data directory.</param>
        /// <param name="persistent">When <see langword="true"/>, attach a default context.</param>
        /// <param name="deleteUserDataDirOnClose">When <see langword="true"/>, delete <paramref name="userDataDir"/> on exit.</param>
        /// <param name="handleSIGINT">When <see langword="true"/>, close the browser on Ctrl-C.</param>
        /// <param name="handleSIGTERM">When <see langword="true"/>, close the browser on SIGTERM.</param>
        /// <param name="handleSIGHUP">When <see langword="true"/>, close the browser on SIGHUP.</param>
        /// <param name="ignoreDefaultArgsList">Default switches to omit by exact match. Ignored when <paramref name="ignoreDefaultArgs"/> is <see langword="true"/>.</param>
        /// <returns>A connected <see cref="CRBrowser"/> instance.</returns>
        internal static async Task<CRBrowser> LaunchAsync(
            string executablePath,
            bool headless = true,
            string[] args = null,
            Proxy proxy = null,
            bool chromiumSandbox = false,
            int timeout = 30_000,
            bool ignoreDefaultArgs = false,
            IReadOnlyDictionary<string, string> environment = null,
            ILoggerFactory loggerFactory = null,
            bool devtools = false,
            string userDataDir = null,
            bool persistent = false,
            bool deleteUserDataDirOnClose = false,
            bool handleSIGINT = true,
            bool handleSIGTERM = true,
            bool handleSIGHUP = true,
            IEnumerable<string> ignoreDefaultArgsList = null)
        {
            List<string> launchArgs = GetDefaultArgs(headless, chromiumSandbox, args, ignoreDefaultArgs, devtools, ignoreDefaultArgsList);

            // Remote debugging over WebSocket. Honor a caller-supplied port
            // (official connectOverCDP / oopif reconnect).
            bool hasDebugPort = false;
            foreach (string arg in launchArgs)
            {
                if (arg.StartsWith("--remote-debugging-port=", StringComparison.Ordinal))
                {
                    hasDebugPort = true;
                    break;
                }
            }

            if (!hasDebugPort)
            {
                launchArgs.Add("--remote-debugging-port=0");
            }

            // Proxy configuration.
            string proxyServer = ProxySettings.FormatServer(proxy, includeCredentials: false);
            if (!string.IsNullOrEmpty(proxyServer))
            {
                launchArgs.Add("--proxy-server=" + proxyServer);

                // Official always prefixes <-loopback> so localhost / link-local
                // still go through a launch proxy when the caller also sets bypass.
                string bypass = ProxySettings.FormatBypassList(proxy);
                launchArgs.RemoveAll(arg => arg.StartsWith("--proxy-bypass-list=", StringComparison.Ordinal));
                if (!string.IsNullOrEmpty(bypass))
                {
                    launchArgs.Add("--proxy-bypass-list=" + bypass);
                }
            }

            // Create a temporary user data directory unless the caller provided one via args
            // or LaunchPersistentContextAsync.
            string tempUserDataDir = null;
            bool hasUserDataDir = !string.IsNullOrEmpty(userDataDir);

            if (args != null)
            {
                foreach (string arg in args)
                {
                    if (arg.StartsWith("--user-data-dir", StringComparison.Ordinal))
                    {
                        hasUserDataDir = true;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(userDataDir))
            {
                Directory.CreateDirectory(userDataDir);
                launchArgs.Add($"--user-data-dir={userDataDir}");
                if (deleteUserDataDirOnClose)
                {
                    tempUserDataDir = userDataDir;
                }
            }
            else if (!hasUserDataDir)
            {
                tempUserDataDir = Path.Combine(Path.GetTempPath(), "playwright_chromium_" + Path.GetRandomFileName());
                Directory.CreateDirectory(tempUserDataDir);
                launchArgs.Add($"--user-data-dir={tempUserDataDir}");
            }

            // Chromium needs an initial URL to finish startup over websocket
            // (--no-startup-window leaks processes and times out leftover
            // oopif). Official launch() still exposes no pages: close the
            // leftover about:blank after connect.
            launchArgs.Add("about:blank");

            WebSocketTransport transport = null;
            CRConnection connection = null;
            BrowserProcessManager processManager = new(
                executablePath,
                launchArgs,
                transportMode: TransportMode.WebSocket,
                tempUserDataDir: tempUserDataDir,
                timeout: timeout,
                loggerFactory: loggerFactory,
                environment: environment,
                handleSIGINT: handleSIGINT,
                handleSIGTERM: handleSIGTERM,
                handleSIGHUP: handleSIGHUP);

            try
            {
                await processManager.StartAsync().ConfigureAwait(false);

                string endpoint = processManager.Endpoint;

                transport = await WebSocketTransport.ConnectAsync(endpoint, timeout: timeout).ConfigureAwait(false);
                connection = new CRConnection(transport, loggerFactory);

                CRBrowser browser = await CRBrowser.ConnectAsync(connection, transport, processManager, loggerFactory, persistent).ConfigureAwait(false);

                // Ownership of processManager, connection, and transport has been
                // transferred to the CRBrowser instance. Null out locals so the
                // finally block does not dispose them.
                processManager = null;
                connection = null;
                transport = null;

                return browser;
            }
            finally
            {
                connection?.Dispose();

                if (transport != null)
                {
                    await transport.CloseAsync().ConfigureAwait(false);
                    transport.Dispose();
                }

                if (processManager != null)
                {
                    try
                    {
                        await processManager.KillAsync().ConfigureAwait(false);
                    }
                    finally
                    {
                        processManager.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Connects to an already-running Chromium over CDP.
        /// </summary>
        /// <param name="endpointURL">HTTP DevTools URL or WebSocket debugger URL.</param>
        /// <param name="timeout">Connection timeout in milliseconds.</param>
        /// <param name="headers">Optional HTTP headers for the version request and WebSocket handshake.</param>
        /// <param name="noDefaults">Official <c>connectOverCDP({ noDefaults })</c>.</param>
        /// <returns>A connected <see cref="CRBrowser"/>.</returns>
        internal static async Task<CRBrowser> ConnectOverCDPAsync(string endpointURL, int timeout = 30_000, IEnumerable<KeyValuePair<string, string>> headers = null, bool noDefaults = false)
        {
            List<KeyValuePair<string, string>> headerList = WithDefaultUserAgent(headers);
            string webSocketUrl = await ResolveWebSocketUrlAsync(endpointURL, timeout, headerList).ConfigureAwait(false);
            WebSocketTransport transport = null;
            CRConnection connection = null;
            try
            {
                transport = await WebSocketTransport.ConnectAsync(webSocketUrl, headerList, timeout).ConfigureAwait(false);
                connection = new CRConnection(transport, loggerFactory: null);
                Task<CRBrowser> connect = CRBrowser.ConnectAsync(
                    connection,
                    transport,
                    processManager: null,
                    loggerFactory: null,
                    noDefaults: noDefaults);
                int timeoutMs = timeout > 0 ? timeout : 30_000;
                Task delay = Task.Delay(timeoutMs);
                Task finished = await Task.WhenAny(connect, delay).ConfigureAwait(false);
                if (finished != connect)
                {
                    throw new TimeoutException("Timeout " + timeoutMs + "ms exceeded.");
                }

                CRBrowser browser = await connect.ConfigureAwait(false);
                transport = null;
                connection = null;
                return browser;
            }
            catch (Exception ex)
            {
                throw BrowserTypeLaunchGuard.WrapConnectOverCdp(ex, connection?.CloseReason);
            }
            finally
            {
                connection?.Dispose();
                if (transport != null)
                {
                    await transport.CloseAsync().ConfigureAwait(false);
                    transport.Dispose();
                }
            }
        }

        /// <summary>
        /// Sends a <c>Browser.close</c> command over the transport using the special
        /// <see cref="CRConnection.KBrowserCloseMessageId"/> so the connection ignores
        /// the response. Used as the graceful-close callback for the browser process.
        /// </summary>
        /// <param name="transport">The connection transport to send the close message on.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        internal static Task AttemptToGracefullyCloseBrowserAsync(IConnectionTransport transport)
        {
            var request = new ProtocolRequest
            {
                Id = CRConnection.KBrowserCloseMessageId,
                Method = "Browser.close",
            };

            return transport.SendAsync(request);
        }

        private static async Task<string> ResolveWebSocketUrlAsync(string endpointURL, int timeout, IEnumerable<KeyValuePair<string, string>> headers)
        {
            if (endpointURL.StartsWith("ws://", StringComparison.OrdinalIgnoreCase)
                || endpointURL.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            {
                return endpointURL;
            }

            string httpURL = ToJsonVersionUrl(endpointURL);
            using HttpClient client = CreateDiscoveryClient(timeout, headers);
            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(new Uri(httpURL)).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new PlaywrightNativeException(ex.Message, ex);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new PlaywrightNativeException(
                        "Unexpected status " + ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " when connecting to " + httpURL + ".\n"
                        + "This does not look like a DevTools server, try connecting via ws://.");
                }

                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                using JsonDocument document = JsonDocument.Parse(string.IsNullOrEmpty(json) ? "{}" : json);
                if (!document.RootElement.TryGetProperty("webSocketDebuggerUrl", out JsonElement wsEl)
                    || wsEl.ValueKind != JsonValueKind.String
                    || string.IsNullOrEmpty(wsEl.GetString()))
                {
                    throw new PlaywrightNativeException("Invalid URL");
                }

                return wsEl.GetString();
            }
        }

        private static string ToJsonVersionUrl(string endpointURL)
        {
            if (!Uri.TryCreate(endpointURL, UriKind.Absolute, out Uri uri))
            {
                throw new PlaywrightNativeException("Invalid URL");
            }

            string path = uri.AbsolutePath;
            if (!path.EndsWith('/'))
            {
                path += "/";
            }

            path += "json/version/";
            UriBuilder builder = new UriBuilder(uri.Scheme, uri.Host, uri.Port)
            {
                Path = path,
                Query = uri.Query.StartsWith('?') ? uri.Query.Substring(1) : uri.Query,
            };
            return builder.Uri.ToString();
        }

        private static HttpClient CreateDiscoveryClient(int timeout, IEnumerable<KeyValuePair<string, string>> headers)
        {
            HttpClientHandler handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                UseProxy = false,
                CheckCertificateRevocationList = true,
            };
            string proxy = Environment.GetEnvironmentVariable("HTTP_PROXY")
                ?? Environment.GetEnvironmentVariable("http_proxy");
            if (!string.IsNullOrEmpty(proxy)
                && Uri.TryCreate(proxy, UriKind.Absolute, out Uri proxyUri))
            {
                handler.Proxy = new WebProxy(proxyUri, BypassOnLocal: false);
                handler.UseProxy = true;
            }

            HttpClient client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromMilliseconds(timeout > 0 ? timeout : 30_000),
            };
            ApplyConnectHeaders(client, headers);
            return client;
        }

        private static List<KeyValuePair<string, string>> WithDefaultUserAgent(IEnumerable<KeyValuePair<string, string>> headers)
        {
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            bool hasUserAgent = false;
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers)
                {
                    list.Add(header);
                    if (string.Equals(header.Key, "User-Agent", StringComparison.OrdinalIgnoreCase))
                    {
                        hasUserAgent = true;
                    }
                }
            }

            if (!hasUserAgent)
            {
                list.Add(new KeyValuePair<string, string>("User-Agent", PlaywrightUserAgent.GetUserAgent()));
            }

            return list;
        }

        private static void ApplyConnectHeaders(HttpClient client, IEnumerable<KeyValuePair<string, string>> headers)
        {
            if (headers == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
    }
}
