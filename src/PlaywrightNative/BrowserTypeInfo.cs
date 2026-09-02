/*
 * Copyright (c) 2020 Dario Kondratiuk
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
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Built-in <see cref="IBrowserType"/> values for Chromium, Firefox, and WebKit.
    /// </summary>
    public sealed partial class BrowserTypeInfo : IBrowserType
    {
        /// <summary>
        /// Chromium / Chrome.
        /// </summary>
        public static readonly IBrowserType Chromium = new BrowserTypeInfo("chromium");

        /// <summary>
        /// Firefox.
        /// </summary>
        public static readonly IBrowserType Firefox = new BrowserTypeInfo("firefox");

        /// <summary>
        /// WebKit.
        /// </summary>
        public static readonly IBrowserType Webkit = new BrowserTypeInfo("webkit");

        private readonly string _name;

        private BrowserTypeInfo(string name)
        {
            _name = name;
        }

        /// <inheritdoc/>
        public string Name => _name;

        /// <inheritdoc/>
        public string ExecutablePath
        {
            get
            {
                SupportedBrowser browser = _name switch
                {
                    "firefox" => SupportedBrowser.Firefox,
                    "webkit" => SupportedBrowser.Webkit,
                    _ => SupportedBrowser.Chromium,
                };

                BrowserFetcher fetcher = new(browser);
                foreach (InstalledBrowser installed in fetcher.GetInstalledBrowsers())
                {
                    if (installed.Browser != browser)
                    {
                        continue;
                    }

                    string installedPath = installed.GetExecutablePath();
                    if (File.Exists(installedPath))
                    {
                        return installedPath;
                    }
                }

                string playwrightKey = BrowserData.PlaywrightPlatformKey(browser, fetcher.Platform);
                string buildId = BrowserData.ResolveRevision(browser, playwrightKey, requestedRevision: null);
                string computed = fetcher.GetExecutablePath(buildId);
                if (File.Exists(computed))
                {
                    return computed;
                }

                if (browser == SupportedBrowser.Chromium)
                {
                    foreach (string candidate in BrowserChannelResolver.CandidatePaths(BrowserChannel.Chrome))
                    {
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }
                    }
                }

                return computed;
            }
        }

        /// <inheritdoc/>
        public Task<IBrowser> LaunchAsync(BrowserTypeLaunchOptions options = default)
        {
            return _name switch
            {
                "firefox" => Playwright.LaunchFirefoxAsync(options),
                "webkit" => Playwright.LaunchWebkitAsync(options),
                _ => Playwright.LaunchChromiumAsync(options),
            };
        }

        /// <inheritdoc/>
        public async Task<IBrowser> ConnectOverCDPAsync(string endpointURL, float? timeout = default, IEnumerable<KeyValuePair<string, string>> headers = default, string artifactsDir = default, bool? noDefaults = default)
        {
            if (!string.Equals(_name, "chromium", StringComparison.Ordinal))
            {
                throw new PlaywrightNativeException("Connecting over CDP is only supported in Chromium and WebKit.");
            }

            if (string.IsNullOrEmpty(endpointURL))
            {
                throw new ArgumentException("endpointURL is required.", nameof(endpointURL));
            }

            int timeoutMs = timeout.HasValue && timeout.Value > 0 ? (int)timeout.Value : Playwright.DefaultTimeout;
            string resolvedArtifacts = artifactsDir;
            if (string.IsNullOrEmpty(resolvedArtifacts))
            {
                resolvedArtifacts = Path.Combine(Path.GetTempPath(), "playwright-artifacts-" + Guid.NewGuid().ToString("N"));
            }

            Directory.CreateDirectory(resolvedArtifacts);

            try
            {
                PlaywrightNative.Chromium.CRBrowser crBrowser = await PlaywrightNative.Chromium.ChromiumBrowserType
                    .ConnectOverCDPAsync(endpointURL, timeoutMs, headers, noDefaults == true)
                    .ConfigureAwait(false);
                PlaywrightNative.Chromium.ChromiumBrowser instance = new(crBrowser, resolvedArtifacts);
                ((IHasTracesDir)instance).TracesDir = resolvedArtifacts;
                if (noDefaults != true)
                {
                    foreach (IBrowserContext context in instance.Contexts)
                    {
                        if (context is PlaywrightNative.Chromium.ChromiumBrowserContext defaultContext)
                        {
                            await defaultContext.ApplyDownloadBehaviorAsync().ConfigureAwait(false);
                        }
                    }
                }

                return instance;
            }
            catch (Exception ex)
            {
                throw BrowserTypeLaunchGuard.WrapConnectOverCdp(ex);
            }
        }

        /// <inheritdoc/>
        public Task<IBrowserContext> LaunchPersistentContextAsync(string userDataDir, BrowserTypeLaunchOptions options = default)
        {
            return _name switch
            {
                "firefox" => Playwright.LaunchFirefoxPersistentContextAsync(userDataDir, options),
                "webkit" => Playwright.LaunchWebkitPersistentContextAsync(userDataDir, options),
                _ => Playwright.LaunchChromiumPersistentContextAsync(userDataDir, options),
            };
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task<IBrowser> IBrowserType.ConnectAsync(string endpoint, BrowserTypeConnectOptions options) => Task.FromResult<IBrowser>(default!);

        Task<IBrowser> IBrowserType.ConnectOverCDPAsync(string endpointURL, BrowserTypeConnectOverCDPOptions options) => Task.FromResult<IBrowser>(default!);

        Task<IBrowser> IBrowserType.LaunchAsync(Microsoft.Playwright.BrowserTypeLaunchOptions options)
            => LaunchAsync(MicrosoftOptionsBridge.ToLaunchOptions(options));

        Task<IBrowserContext> IBrowserType.LaunchPersistentContextAsync(string userDataDir, Microsoft.Playwright.BrowserTypeLaunchPersistentContextOptions options)
            => LaunchPersistentContextAsync(userDataDir, MicrosoftOptionsBridge.ToPersistentLaunchOptions(options));
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
