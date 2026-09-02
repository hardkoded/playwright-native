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
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Chromium;
using PlaywrightNative.Firefox;
using PlaywrightNative.Helpers;
using PlaywrightNative.WebKit;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy <see cref="BrowserContextOptions"/> helpers over official <see cref="IBrowser"/>.
    /// </summary>
    public static class BrowserCompatExtensions
    {
        /// <summary>
        /// Creates a context from PlaywrightNative <see cref="BrowserContextOptions"/>.
        /// </summary>
        /// <param name="browser">Browser instance.</param>
        /// <param name="options">PlaywrightNative context options.</param>
        /// <returns>The new browser context.</returns>
        public static Task<IBrowserContext> NewContextAsync(this IBrowser browser, BrowserContextOptions options)
        {
            if (browser == null)
            {
                throw new ArgumentNullException(nameof(browser));
            }

            if (options == null)
            {
                return browser.NewContextAsync();
            }

            switch (browser)
            {
                case ChromiumBrowser chromium:
                    return chromium.NewContextAsync(options);
                case FirefoxBrowser firefox:
                    return firefox.NewContextAsync(options);
                case WKBrowser webkit:
                    return webkit.NewContextAsync(options);
                default:
                    return browser.NewContextAsync(MicrosoftOptionsBridge.ToBrowserNewContextOptions(options));
            }
        }

        /// <summary>
        /// Creates a page in a new context from PlaywrightNative <see cref="BrowserContextOptions"/>.
        /// </summary>
        /// <param name="browser">Browser instance.</param>
        /// <param name="options">PlaywrightNative context options.</param>
        /// <returns>The new page.</returns>
        public static async Task<IPage> NewPageAsync(this IBrowser browser, BrowserContextOptions options)
        {
            if (browser == null)
            {
                throw new ArgumentNullException(nameof(browser));
            }

            if (options == null)
            {
                return await browser.NewPageAsync().ConfigureAwait(false);
            }

            if (browser is not (ChromiumBrowser or FirefoxBrowser or WKBrowser))
            {
                return await browser.NewPageAsync(MicrosoftOptionsBridge.ToBrowserNewPageOptions(options)).ConfigureAwait(false);
            }

            IBrowserContext context = await browser.NewContextAsync(options).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            MarkOwnedNewPage(context, page);
            return page;
        }

        /// <summary>Legacy expanded-parameter <c>browser.newContext</c>.</summary>
        public static Task<IBrowserContext> NewContextAsync(
            this IBrowser browser,
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
            if (browser == null)
            {
                throw new ArgumentNullException(nameof(browser));
            }

            switch (browser)
            {
                case ChromiumBrowser chromium:
                    return chromium.NewContextAsync(
                        acceptDownloads,
                        bypassCSP,
                        colorScheme,
                        deviceScaleFactor,
                        extraHTTPHeaders,
                        geolocation,
                        hasTouch,
                        httpCredentials,
                        ignoreHTTPSErrors,
                        isMobile,
                        javaScriptEnabled,
                        locale,
                        offline,
                        permissions,
                        proxy,
                        recordHarOmitContent,
                        recordHarPath,
                        recordVideoDir,
                        recordVideoSize,
                        screenSize,
                        storageState,
                        storageStatePath,
                        timezoneId,
                        userAgent,
                        viewportSize,
                        recordHarUrl,
                        baseURL,
                        recordHarMode,
                        serviceWorkers,
                        reducedMotion,
                        forcedColors,
                        contrast,
                        recordHarContent,
                        recordHarUrlRegex,
                        strictSelectors,
                        clientCertificates);
                case FirefoxBrowser firefox:
                    return firefox.NewContextAsync(
                        acceptDownloads,
                        bypassCSP,
                        colorScheme,
                        deviceScaleFactor,
                        extraHTTPHeaders,
                        geolocation,
                        hasTouch,
                        httpCredentials,
                        ignoreHTTPSErrors,
                        isMobile,
                        javaScriptEnabled,
                        locale,
                        offline,
                        permissions,
                        proxy,
                        recordHarOmitContent,
                        recordHarPath,
                        recordVideoDir,
                        recordVideoSize,
                        screenSize,
                        storageState,
                        storageStatePath,
                        timezoneId,
                        userAgent,
                        viewportSize,
                        recordHarUrl,
                        baseURL,
                        recordHarMode,
                        serviceWorkers,
                        reducedMotion,
                        forcedColors,
                        contrast,
                        recordHarContent,
                        recordHarUrlRegex,
                        strictSelectors,
                        clientCertificates);
                case WKBrowser webkit:
                    return webkit.NewContextAsync(
                        acceptDownloads,
                        bypassCSP,
                        colorScheme,
                        deviceScaleFactor,
                        extraHTTPHeaders,
                        geolocation,
                        hasTouch,
                        httpCredentials,
                        ignoreHTTPSErrors,
                        isMobile,
                        javaScriptEnabled,
                        locale,
                        offline,
                        permissions,
                        proxy,
                        recordHarOmitContent,
                        recordHarPath,
                        recordVideoDir,
                        recordVideoSize,
                        screenSize,
                        storageState,
                        storageStatePath,
                        timezoneId,
                        userAgent,
                        viewportSize,
                        recordHarUrl,
                        baseURL,
                        recordHarMode,
                        serviceWorkers,
                        reducedMotion,
                        forcedColors,
                        contrast,
                        recordHarContent,
                        recordHarUrlRegex,
                        strictSelectors,
                        clientCertificates);
                default:
                    return browser.NewContextAsync(MicrosoftOptionsBridge.ToBrowserNewContextOptions(new BrowserContextOptions
                    {
                        AcceptDownloads = acceptDownloads,
                        BypassCSP = bypassCSP,
                        ColorScheme = colorScheme,
                        DeviceScaleFactor = deviceScaleFactor,
                        ExtraHTTPHeaders = extraHTTPHeaders == null ? null : new Dictionary<string, string>(extraHTTPHeaders),
                        Geolocation = geolocation,
                        HasTouch = hasTouch,
                        HttpCredentials = httpCredentials,
                        IgnoreHTTPSErrors = ignoreHTTPSErrors,
                        IsMobile = isMobile,
                        JavaScriptEnabled = javaScriptEnabled,
                        Locale = locale,
                        Offline = offline,
                        Permissions = permissions?.ToArray(),
                        Proxy = proxy,
                        RecordHarOmitContent = recordHarOmitContent,
                        RecordHarPath = recordHarPath,
                        RecordVideoDir = recordVideoDir,
                        RecordVideoSize = recordVideoSize,
                        ScreenSize = screenSize,
                        StorageState = storageState,
                        StorageStatePath = storageStatePath,
                        TimezoneId = timezoneId,
                        UserAgent = userAgent,
                        Viewport = viewportSize,
                        RecordHarUrl = recordHarUrl,
                        BaseURL = baseURL,
                        RecordHarMode = recordHarMode,
                        ServiceWorkers = serviceWorkers,
                        ReducedMotion = reducedMotion,
                        ForcedColors = forcedColors,
                        Contrast = contrast,
                        RecordHarContent = recordHarContent,
                        RecordHarUrlRegex = recordHarUrlRegex,
                        StrictSelectors = strictSelectors ?? false,
                        ClientCertificates = clientCertificates,
                    }));
            }
        }

        /// <summary>Legacy expanded-parameter <c>browser.newPage</c>.</summary>
        public static Task<IPage> NewPageAsync(
            this IBrowser browser,
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
            if (browser == null)
            {
                throw new ArgumentNullException(nameof(browser));
            }

            switch (browser)
            {
                case ChromiumBrowser chromium:
                    return chromium.NewPageAsync(
                        acceptDownloads,
                        bypassCSP,
                        colorScheme,
                        deviceScaleFactor,
                        extraHTTPHeaders,
                        geolocation,
                        hasTouch,
                        httpCredentials,
                        ignoreHTTPSErrors,
                        isMobile,
                        javaScriptEnabled,
                        locale,
                        offline,
                        permissions,
                        proxy,
                        recordHarOmitContent,
                        recordHarPath,
                        recordVideoDir,
                        recordVideoSize,
                        screenSize,
                        storageState,
                        storageStatePath,
                        timezoneId,
                        userAgent,
                        viewportSize,
                        recordHarUrl,
                        baseURL,
                        recordHarMode,
                        serviceWorkers,
                        reducedMotion,
                        forcedColors,
                        contrast,
                        recordHarContent,
                        recordHarUrlRegex,
                        strictSelectors,
                        clientCertificates);
                case FirefoxBrowser firefox:
                    return firefox.NewPageAsync(
                        acceptDownloads,
                        bypassCSP,
                        colorScheme,
                        deviceScaleFactor,
                        extraHTTPHeaders,
                        geolocation,
                        hasTouch,
                        httpCredentials,
                        ignoreHTTPSErrors,
                        isMobile,
                        javaScriptEnabled,
                        locale,
                        offline,
                        permissions,
                        proxy,
                        recordHarOmitContent,
                        recordHarPath,
                        recordVideoDir,
                        recordVideoSize,
                        screenSize,
                        storageState,
                        storageStatePath,
                        timezoneId,
                        userAgent,
                        viewportSize,
                        recordHarUrl,
                        baseURL,
                        recordHarMode,
                        serviceWorkers,
                        reducedMotion,
                        forcedColors,
                        contrast,
                        recordHarContent,
                        recordHarUrlRegex,
                        strictSelectors,
                        clientCertificates);
                case WKBrowser webkit:
                    return webkit.NewPageAsync(
                        acceptDownloads,
                        bypassCSP,
                        colorScheme,
                        deviceScaleFactor,
                        extraHTTPHeaders,
                        geolocation,
                        hasTouch,
                        httpCredentials,
                        ignoreHTTPSErrors,
                        isMobile,
                        javaScriptEnabled,
                        locale,
                        offline,
                        permissions,
                        proxy,
                        recordHarOmitContent,
                        recordHarPath,
                        recordVideoDir,
                        recordVideoSize,
                        screenSize,
                        storageState,
                        storageStatePath,
                        timezoneId,
                        userAgent,
                        viewportSize,
                        recordHarUrl,
                        baseURL,
                        recordHarMode,
                        serviceWorkers,
                        reducedMotion,
                        forcedColors,
                        contrast,
                        recordHarContent,
                        recordHarUrlRegex,
                        strictSelectors,
                        clientCertificates);
                default:
                    return browser.NewPageAsync(MicrosoftOptionsBridge.ToBrowserNewPageOptions(new BrowserContextOptions
                    {
                        AcceptDownloads = acceptDownloads,
                        BypassCSP = bypassCSP,
                        ColorScheme = colorScheme,
                        DeviceScaleFactor = deviceScaleFactor,
                        ExtraHTTPHeaders = extraHTTPHeaders == null ? null : new Dictionary<string, string>(extraHTTPHeaders),
                        Geolocation = geolocation,
                        HasTouch = hasTouch,
                        HttpCredentials = httpCredentials,
                        IgnoreHTTPSErrors = ignoreHTTPSErrors,
                        IsMobile = isMobile,
                        JavaScriptEnabled = javaScriptEnabled,
                        Locale = locale,
                        Offline = offline,
                        Permissions = permissions?.ToArray(),
                        Proxy = proxy,
                        RecordHarOmitContent = recordHarOmitContent,
                        RecordHarPath = recordHarPath,
                        RecordVideoDir = recordVideoDir,
                        RecordVideoSize = recordVideoSize,
                        ScreenSize = screenSize,
                        StorageState = storageState,
                        StorageStatePath = storageStatePath,
                        TimezoneId = timezoneId,
                        UserAgent = userAgent,
                        Viewport = viewportSize,
                        RecordHarUrl = recordHarUrl,
                        BaseURL = baseURL,
                        RecordHarMode = recordHarMode,
                        ServiceWorkers = serviceWorkers,
                        ReducedMotion = reducedMotion,
                        ForcedColors = forcedColors,
                        Contrast = contrast,
                        RecordHarContent = recordHarContent,
                        RecordHarUrlRegex = recordHarUrlRegex,
                        StrictSelectors = strictSelectors ?? false,
                        ClientCertificates = clientCertificates,
                    }));
            }
        }

        private static void MarkOwnedNewPage(IBrowserContext context, IPage page)
        {
            switch (page)
            {
                case Page chromiumPage:
                    chromiumPage.OwnedContext = context;
                    break;
                case WKPage webkitPage:
                    webkitPage.OwnedContext = context;
                    break;
            }

            switch (context)
            {
                case ChromiumBrowserContext chromiumContext:
                    chromiumContext.OwnedByBrowserNewPage = true;
                    break;
                case FirefoxBrowserContext firefoxContext:
                    firefoxContext.OwnedByBrowserNewPage = true;
                    break;
                case WKBrowserContext webkitContext:
                    webkitContext.OwnedByBrowserNewPage = true;
                    break;
            }
        }
    }
}
