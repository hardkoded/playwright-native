// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Entry point for direct-CDP / direct-Juggler / direct-WIP browser automation.
    /// Matches the official microsoft/playwright-dotnet shape:
    /// <c>using var playwright = await Playwright.CreateAsync();</c>
    /// then <c>await playwright.Chromium.LaunchAsync()</c>.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1724", Justification = "Playwright is the entrypoint for all languages.")]
    public static class Playwright
    {
        /// <summary>Default timeout.</summary>
        public const int DefaultTimeout = 30_000;

        /// <summary>
        /// Chromium browser type. Prefer <see cref="CreateAsync"/> then
        /// <c>playwright.Chromium</c> for the official API shape. Use
        /// <see cref="IBrowserType.LaunchAsync"/> to start a Chromium browser.
        /// </summary>
        public static IBrowserType Chromium => BrowserTypeInfo.Chromium;

        /// <summary>
        /// Firefox browser type. Prefer <see cref="CreateAsync"/> then
        /// <c>playwright.Firefox</c> for the official API shape. Use
        /// <see cref="IBrowserType.LaunchAsync"/> to start a Firefox browser.
        /// </summary>
        public static IBrowserType Firefox => BrowserTypeInfo.Firefox;

        /// <summary>
        /// WebKit browser type. Prefer <see cref="CreateAsync"/> then
        /// <c>playwright.Webkit</c> for the official API shape. Use
        /// <see cref="IBrowserType.LaunchAsync"/> to start a WebKit browser.
        /// </summary>
        public static IBrowserType Webkit => BrowserTypeInfo.Webkit;

        /// <summary>
        /// Official Playwright device descriptors for
        /// <see cref="IBrowser.NewContextAsync(BrowserContextOptions)"/>.
        /// </summary>
        public static IReadOnlyDictionary<string, BrowserContextOptions> Devices { get; } = PlaywrightDevices.Load();

        /// <summary>
        /// Official <c>playwright.errors</c>.
        /// </summary>
        public static PlaywrightErrors Errors { get; } = new PlaywrightErrors();

        /// <summary>
        /// Creates standalone HTTP clients that do not need a browser.
        /// </summary>
        public static IAPIRequest APIRequest { get; } = Helpers.APIRequest.Instance;

        /// <summary>
        /// Custom selector engines. Official <c>playwright.selectors</c>.
        /// Register with <see cref="ISelectors.RegisterAsync"/> before using
        /// <c>name=body</c> selectors.
        /// </summary>
        public static ISelectors Selectors { get; } = Helpers.Selectors.Instance;

        /// <summary>
        /// Sets the attribute <see cref="IPage.GetByTestIdAsync"/> and
        /// <see cref="IFrame.GetByTestIdAsync"/> match. Defaults to <c>data-testid</c>.
        /// </summary>
        /// <param name="attributeName">
        /// HTML attribute name, for example <c>data-testid</c>. Official
        /// Playwright accepts a comma-separated list, e.g. <c>data-pw,data-ti</c>.
        /// </param>
        public static void SetTestIdAttribute(string attributeName)
            => GetBySelectorScript.SetTestIdAttributeName(attributeName);

        /// <summary>
        /// Creates a Playwright instance. Official entry point — same shape as
        /// <c>Microsoft.Playwright.Playwright.CreateAsync()</c>, without a Node.js driver.
        /// </summary>
        /// <returns>An <see cref="IPlaywright"/> with Chromium, Firefox, and WebKit browser types.</returns>
        public static Task<IPlaywright> CreateAsync()
            => Task.FromResult<IPlaywright>(new PlaywrightImpl());

        /// <summary>
        /// Launches a Chromium browser. Prefer
        /// <c>await (await Playwright.CreateAsync()).Chromium.LaunchAsync(options)</c>
        /// or <c>await Playwright.Chromium.LaunchAsync(options)</c>.
        /// When <paramref name="options"/> is <c>null</c> or
        /// <c>options.ExecutablePath</c> is <c>null</c>, Chromium is downloaded via
        /// a default <see cref="BrowserFetcher"/> if not already cached.
        /// </summary>
        /// <param name="options">Optional launch options.</param>
        /// <returns>A direct-CDP-backed browser instance.</returns>
        public static async Task<IBrowser> LaunchChromiumAsync(BrowserTypeLaunchOptions options = null)
        {
            options ??= new BrowserTypeLaunchOptions();
            BrowserTypeLaunchGuard.ThrowIfLaunchForbidden(options);
            try
            {
                string executablePath = await ResolveExecutablePathAsync(SupportedBrowser.Chromium, options).ConfigureAwait(false);

                PlaywrightNative.Chromium.CRBrowser crBrowser = await PlaywrightNative.Chromium.ChromiumBrowserType
                    .LaunchAsync(executablePath, options.Headless, args: ToArgArray(options.Args), proxy: options.Proxy, chromiumSandbox: options.ChromiumSandbox, timeout: ResolveTimeout(options), ignoreDefaultArgs: options.IgnoreDefaultArgs, environment: options.Env, loggerFactory: options.LoggerFactory, devtools: options.Devtools, handleSIGINT: HandleSIGINT(options), handleSIGTERM: HandleSIGTERM(options), handleSIGHUP: HandleSIGHUP(options), ignoreDefaultArgsList: options.IgnoreDefaultArgsList)
                    .ConfigureAwait(false);

                PlaywrightNative.Chromium.ChromiumBrowser chromium = new PlaywrightNative.Chromium.ChromiumBrowser(crBrowser, ResolveDownloadsPath(options), options.Logger)
                {
                    LaunchProxy = options.Proxy,
                };
                ((PlaywrightNative.Helpers.IHasTracesDir)chromium).TracesDir = options.TracesDir;
                ((PlaywrightNative.Helpers.IHasArtifactsDir)chromium).ArtifactsDir = options.ArtifactsDir;
                return chromium;
            }
            catch (Exception ex)
            {
                throw BrowserTypeLaunchGuard.WrapLaunch("browserType.launch", ex);
            }
        }

        /// <summary>
        /// Launches a Firefox browser. Prefer
        /// <c>await (await Playwright.CreateAsync()).Firefox.LaunchAsync(options)</c>
        /// or <c>await Playwright.Firefox.LaunchAsync(options)</c>.
        /// When <paramref name="options"/> is <c>null</c> or
        /// <c>options.ExecutablePath</c> is <c>null</c>, Firefox is downloaded via
        /// a default <see cref="BrowserFetcher"/> if not already cached.
        /// </summary>
        /// <param name="options">Optional launch options.</param>
        /// <returns>A direct-Juggler-backed browser instance.</returns>
        public static async Task<IBrowser> LaunchFirefoxAsync(BrowserTypeLaunchOptions options = null)
        {
            options ??= new BrowserTypeLaunchOptions();
            BrowserTypeLaunchGuard.ThrowIfLaunchForbidden(options);
            try
            {
                string executablePath = await ResolveExecutablePathAsync(SupportedBrowser.Firefox, options).ConfigureAwait(false);

                PlaywrightNative.Firefox.FFBrowser ffBrowser = await PlaywrightNative.Firefox.FirefoxBrowserType
                    .LaunchAsync(executablePath, options.Headless, args: ToArgArray(options.Args), timeout: ResolveTimeout(options), environment: options.Env, loggerFactory: options.LoggerFactory, handleSIGINT: HandleSIGINT(options), handleSIGTERM: HandleSIGTERM(options), handleSIGHUP: HandleSIGHUP(options), firefoxUserPrefs: options.FirefoxUserPrefs)
                    .ConfigureAwait(false);

                return new PlaywrightNative.Firefox.FirefoxBrowser(ffBrowser);
            }
            catch (Exception ex)
            {
                throw BrowserTypeLaunchGuard.WrapLaunch("browserType.launch", ex);
            }
        }

        /// <summary>
        /// Launches a WebKit browser. Prefer
        /// <c>await (await Playwright.CreateAsync()).Webkit.LaunchAsync(options)</c>
        /// or <c>await Playwright.Webkit.LaunchAsync(options)</c>.
        /// When <paramref name="options"/> is <c>null</c> or
        /// <c>options.ExecutablePath</c> is <c>null</c>, WebKit is downloaded via
        /// a default <see cref="BrowserFetcher"/> if not already cached.
        /// </summary>
        /// <param name="options">Optional launch options.</param>
        /// <returns>A direct-WIP-backed browser instance.</returns>
        public static async Task<IBrowser> LaunchWebkitAsync(BrowserTypeLaunchOptions options = null)
        {
            options ??= new BrowserTypeLaunchOptions();
            BrowserTypeLaunchGuard.ThrowIfLaunchForbidden(options);
            try
            {
                string executablePath = await ResolveExecutablePathAsync(SupportedBrowser.Webkit, options).ConfigureAwait(false);

                WebKit.WKBrowser wkBrowser = await WebKit.WebkitBrowserType
                    .LaunchAsync(executablePath, options.Headless, args: ToArgArray(options.Args), proxy: options.Proxy, timeout: ResolveTimeout(options), environment: options.Env, loggerFactory: options.LoggerFactory, handleSIGINT: HandleSIGINT(options), handleSIGTERM: HandleSIGTERM(options), handleSIGHUP: HandleSIGHUP(options))
                    .ConfigureAwait(false);

                wkBrowser.LaunchDownloadsPath = ResolveDownloadsPath(options);
                wkBrowser.LaunchProxy = options.Proxy;
                wkBrowser.Logger = options.Logger;
                ((PlaywrightNative.Helpers.IHasTracesDir)wkBrowser).TracesDir = options.TracesDir;
                ((PlaywrightNative.Helpers.IHasArtifactsDir)wkBrowser).ArtifactsDir = options.ArtifactsDir;
                return wkBrowser;
            }
            catch (Exception ex)
            {
                throw BrowserTypeLaunchGuard.WrapLaunch("browserType.launch", ex);
            }
        }

        /// <summary>
        /// Launches Chromium with a persistent user data directory.
        /// </summary>
        /// <param name="userDataDir">User data directory, or empty for a temporary directory.</param>
        /// <param name="options">Optional launch options.</param>
        /// <returns>The persistent context.</returns>
        internal static async Task<IBrowserContext> LaunchChromiumPersistentContextAsync(string userDataDir, BrowserTypeLaunchOptions options = null)
        {
            options ??= new BrowserTypeLaunchOptions();
            BrowserTypeLaunchGuard.ThrowIfPersistentForbidden(options);
            ThrowIfPageArgument(options.Args);
            ClientCertificatesProxy certsProxy = null;
            try
            {
                string executablePath = await ResolveExecutablePathAsync(SupportedBrowser.Chromium, options).ConfigureAwait(false);
                bool ownsUserDataDir = string.IsNullOrEmpty(userDataDir);
                string resolvedDir = ResolveUserDataDir(userDataDir);
                Proxy launchProxy = StartPersistentClientCertificates(options, out certsProxy);

                PlaywrightNative.Chromium.CRBrowser crBrowser = await PlaywrightNative.Chromium.ChromiumBrowserType
                    .LaunchAsync(
                        executablePath,
                        options.Headless,
                        args: PersistentChromiumArgs(options),
                        proxy: launchProxy,
                        chromiumSandbox: options.ChromiumSandbox,
                        timeout: ResolveTimeout(options),
                        ignoreDefaultArgs: options.IgnoreDefaultArgs,
                        environment: options.Env,
                        loggerFactory: options.LoggerFactory,
                        devtools: options.Devtools,
                        userDataDir: resolvedDir,
                        persistent: true,
                        deleteUserDataDirOnClose: ownsUserDataDir,
                        handleSIGINT: HandleSIGINT(options),
                        handleSIGTERM: HandleSIGTERM(options),
                        handleSIGHUP: HandleSIGHUP(options),
                        ignoreDefaultArgsList: options.IgnoreDefaultArgsList)
                    .ConfigureAwait(false);

                PlaywrightNative.Chromium.ChromiumBrowser instance = new(crBrowser, ResolveDownloadsPath(options))
                {
                    LaunchProxy = options.Proxy,
                };
                ((PlaywrightNative.Helpers.IHasTracesDir)instance).TracesDir = options.TracesDir;
                ((PlaywrightNative.Helpers.IHasArtifactsDir)instance).ArtifactsDir = options.ArtifactsDir;
                IBrowserContext context = instance.PersistentContext();
                if (context is PlaywrightNative.Chromium.ChromiumBrowserContext chromiumCerts)
                {
                    chromiumCerts.AttachClientCertificatesProxy(certsProxy, options.Proxy);
                    certsProxy = null;
                }

                await ApplyPersistentEmulationAsync(context, options).ConfigureAwait(false);
                return context;
            }
            catch (Exception ex)
            {
                certsProxy?.Dispose();
                throw BrowserTypeLaunchGuard.WrapLaunch("browserType.launchPersistentContext", ex);
            }
        }

        /// <summary>
        /// Launches Firefox with a persistent profile directory.
        /// </summary>
        /// <param name="userDataDir">Profile directory, or empty for a temporary directory.</param>
        /// <param name="options">Optional launch options.</param>
        /// <returns>The persistent context.</returns>
        internal static async Task<IBrowserContext> LaunchFirefoxPersistentContextAsync(string userDataDir, BrowserTypeLaunchOptions options = null)
        {
            options ??= new BrowserTypeLaunchOptions();
            BrowserTypeLaunchGuard.ThrowIfPersistentForbidden(options);
            ThrowIfPageArgument(options.Args);
            string executablePath = await ResolveExecutablePathAsync(SupportedBrowser.Firefox, options).ConfigureAwait(false);
            bool ownsUserDataDir = string.IsNullOrEmpty(userDataDir);
            string resolvedDir = ResolveUserDataDir(userDataDir);

            PlaywrightNative.Firefox.FFBrowser ffBrowser = await PlaywrightNative.Firefox.FirefoxBrowserType
                .LaunchAsync(
                    executablePath,
                    options.Headless,
                    args: ToArgArray(options.Args),
                    timeout: ResolveTimeout(options),
                    environment: options.Env,
                    loggerFactory: options.LoggerFactory,
                    handleSIGINT: HandleSIGINT(options),
                    handleSIGTERM: HandleSIGTERM(options),
                    handleSIGHUP: HandleSIGHUP(options),
                    userDataDir: resolvedDir,
                    persistent: true,
                    deleteUserDataDirOnClose: ownsUserDataDir,
                    firefoxUserPrefs: options.FirefoxUserPrefs)
                .ConfigureAwait(false);

            PlaywrightNative.Firefox.FirefoxBrowser instance = new(ffBrowser);
            IBrowserContext context = instance.PersistentContext();
            await ApplyPersistentEmulationAsync(context, options).ConfigureAwait(false);
            return context;
        }

        /// <summary>
        /// Launches WebKit with a persistent user data directory.
        /// </summary>
        /// <param name="userDataDir">User data directory, or empty for a temporary directory.</param>
        /// <param name="options">Optional launch options.</param>
        /// <returns>The persistent context.</returns>
        internal static async Task<IBrowserContext> LaunchWebkitPersistentContextAsync(string userDataDir, BrowserTypeLaunchOptions options = null)
        {
            options ??= new BrowserTypeLaunchOptions();
            BrowserTypeLaunchGuard.ThrowIfPersistentForbidden(options);
            ThrowIfPageArgument(options.Args);
            ClientCertificatesProxy webkitCertsProxy = null;
            try
            {
                string executablePath = await ResolveExecutablePathAsync(SupportedBrowser.Webkit, options).ConfigureAwait(false);
                bool ownsUserDataDir = string.IsNullOrEmpty(userDataDir);
                string resolvedDir = ResolveUserDataDir(userDataDir);
                Proxy webkitLaunchProxy = StartPersistentClientCertificates(options, out webkitCertsProxy);

                WebKit.WKBrowser wkBrowser = await WebKit.WebkitBrowserType
                    .LaunchAsync(
                        executablePath,
                        options.Headless,
                        args: ToArgArray(options.Args),
                        proxy: webkitLaunchProxy,
                        timeout: ResolveTimeout(options),
                        environment: options.Env,
                        loggerFactory: options.LoggerFactory,
                        userDataDir: resolvedDir,
                        persistent: true,
                        deleteUserDataDirOnClose: ownsUserDataDir,
                        handleSIGINT: HandleSIGINT(options),
                        handleSIGTERM: HandleSIGTERM(options),
                        handleSIGHUP: HandleSIGHUP(options))
                    .ConfigureAwait(false);

                wkBrowser.LaunchDownloadsPath = ResolveDownloadsPath(options);
                wkBrowser.LaunchProxy = options.Proxy;
                ((PlaywrightNative.Helpers.IHasTracesDir)wkBrowser).TracesDir = options.TracesDir;
                ((PlaywrightNative.Helpers.IHasArtifactsDir)wkBrowser).ArtifactsDir = options.ArtifactsDir;
                IBrowserContext context = wkBrowser.PersistentContext();
                if (context is WebKit.WKBrowserContext webkitCerts)
                {
                    webkitCerts.AttachClientCertificatesProxy(webkitCertsProxy, options.Proxy);
                    webkitCertsProxy = null;
                }

                await ApplyPersistentEmulationAsync(context, options).ConfigureAwait(false);
                return context;
            }
            catch
            {
                webkitCertsProxy?.Dispose();
                throw;
            }
        }

        private static Proxy StartPersistentClientCertificates(
            BrowserTypeLaunchOptions options,
            out ClientCertificatesProxy proxy)
        {
            proxy = null;
            if (options is not BrowserTypeLaunchPersistentContextOptions persistent
                || !ClientCertificateHelper.HasAny(persistent.ClientCertificates))
            {
                return options?.Proxy;
            }

            proxy = ClientCertificatesProxy.Create(
                persistent.ClientCertificates,
                persistent.IgnoreHTTPSErrors == true,
                options.Proxy);
            return proxy.BrowserProxy;
        }

        private static async Task ApplyPersistentEmulationAsync(IBrowserContext context, BrowserTypeLaunchOptions options)
        {
            if (options is not BrowserTypeLaunchPersistentContextOptions persistent)
            {
                return;
            }

            if (context is PlaywrightNative.Chromium.ChromiumBrowserContext chromiumBase)
            {
                chromiumBase.BaseURL = persistent.BaseURL;
                chromiumBase.StrictSelectors = persistent.StrictSelectors;
                chromiumBase.AttachClientCertificates(persistent.ClientCertificates);
            }

            if (context is WebKit.WKBrowserContext webkitBase)
            {
                webkitBase.BaseURL = persistent.BaseURL;
                webkitBase.StrictSelectors = persistent.StrictSelectors;
                webkitBase.AttachClientCertificates(persistent.ClientCertificates);
            }

            await ServiceWorkerPolicyHelper.ApplyAsync(context, persistent.ServiceWorkers).ConfigureAwait(false);
            HarRecorder.Start(
                context,
                persistent.RecordHarPath,
                persistent.RecordHarOmitContent,
                persistent.RecordHarUrl,
                persistent.RecordHarMode,
                persistent.RecordHarContent,
                persistent.RecordHarUrlRegex);
            await StorageStateHelper.ApplyAsync(context, persistent.StorageState, persistent.StorageStatePath).ConfigureAwait(false);

            if (context is WebKit.WKBrowserContext webkitShims)
            {
                await webkitShims.ApplyWebKitPageShimsAsync().ConfigureAwait(false);
                await webkitShims.ApplyPersistentStorageShimsAsync().ConfigureAwait(false);
            }

            if (persistent.ViewportSize == null
                && string.IsNullOrEmpty(persistent.Locale)
                && string.IsNullOrEmpty(persistent.TimezoneId)
                && string.IsNullOrEmpty(persistent.UserAgent)
                && persistent.Offline != true
                && persistent.ColorScheme == ColorScheme.Null
                && persistent.ReducedMotion == ReducedMotion.Null
                && persistent.ForcedColors == ForcedColors.Null
                && persistent.HasTouch != true
                && (persistent.ExtraHTTPHeaders == null || persistent.ExtraHTTPHeaders.Count == 0)
                && persistent.Geolocation == null
                && (persistent.Permissions == null || persistent.Permissions.Length == 0)
                && persistent.BypassCSP != true
                && persistent.IgnoreHTTPSErrors != true
                && persistent.JavaScriptEnabled != false
                && !persistent.DeviceScaleFactor.HasValue
                && persistent.IsMobile != true
                && persistent.ScreenSize == null
                && persistent.AcceptDownloads != true
                && persistent.HttpCredentials == null
                && persistent.Contrast == Contrast.Null
                && !ClientCertificateHelper.HasAny(persistent.ClientCertificates))
            {
                await ApplyPersistentDownloadBehaviorAsync(context).ConfigureAwait(false);
                if (context is PlaywrightNative.Chromium.ChromiumBrowserContext chromiumWorkers)
                {
                    await chromiumWorkers.AdoptExistingServiceWorkersAsync().ConfigureAwait(false);
                }

                VideoRecorder.Start(context, persistent.RecordVideoDir, persistent.RecordVideoSize, persistent.ViewportSize);
                return;
            }

            if (context is PlaywrightNative.Chromium.ChromiumBrowserContext chromium)
            {
                chromium.ConfigureEmulation(
                    persistent.ViewportSize,
                    userAgent: persistent.UserAgent,
                    extraHeaders: persistent.ExtraHTTPHeaders,
                    locale: persistent.Locale,
                    timezoneId: persistent.TimezoneId,
                    offline: persistent.Offline,
                    colorScheme: persistent.ColorScheme,
                    reducedMotion: persistent.ReducedMotion,
                    forcedColors: persistent.ForcedColors,
                    hasTouch: persistent.HasTouch,
                    geolocation: persistent.Geolocation,
                    permissions: persistent.Permissions,
                    bypassCSP: persistent.BypassCSP,
                    ignoreHTTPSErrors: persistent.IgnoreHTTPSErrors,
                    javaScriptEnabled: persistent.JavaScriptEnabled,
                    deviceScaleFactor: persistent.DeviceScaleFactor,
                    isMobile: persistent.IsMobile,
                    screenSize: persistent.ScreenSize,
                    acceptDownloads: persistent.AcceptDownloads,
                    httpCredentials: persistent.HttpCredentials,
                    contrast: persistent.Contrast);
                await chromium.ApplyDownloadBehaviorAsync().ConfigureAwait(false);
                await ApplyPersistentChromeToExistingPagesAsync(chromium).ConfigureAwait(false);
                await chromium.AdoptExistingServiceWorkersAsync().ConfigureAwait(false);
                VideoRecorder.Start(context, persistent.RecordVideoDir, persistent.RecordVideoSize, persistent.ViewportSize);
                return;
            }

            if (context is WebKit.WKBrowserContext webkit)
            {
                webkit.ConfigureEmulation(
                    persistent.ViewportSize,
                    userAgent: persistent.UserAgent,
                    extraHeaders: persistent.ExtraHTTPHeaders,
                    locale: persistent.Locale,
                    timezoneId: persistent.TimezoneId,
                    offline: persistent.Offline,
                    colorScheme: persistent.ColorScheme,
                    reducedMotion: persistent.ReducedMotion,
                    forcedColors: persistent.ForcedColors,
                    hasTouch: persistent.HasTouch,
                    geolocation: persistent.Geolocation,
                    permissions: persistent.Permissions,
                    bypassCSP: persistent.BypassCSP,
                    ignoreHTTPSErrors: persistent.IgnoreHTTPSErrors,
                    javaScriptEnabled: persistent.JavaScriptEnabled,
                    deviceScaleFactor: persistent.DeviceScaleFactor,
                    isMobile: persistent.IsMobile,
                    screenSize: persistent.ScreenSize,
                    acceptDownloads: persistent.AcceptDownloads,
                    httpCredentials: persistent.HttpCredentials,
                    contrast: persistent.Contrast);
                await webkit.ApplyDownloadBehaviorAsync().ConfigureAwait(false);
                await webkit.ApplyLanguagesAsync().ConfigureAwait(false);
                await ApplyPersistentChromeToExistingPagesAsync(webkit).ConfigureAwait(false);
            }

            VideoRecorder.Start(context, persistent.RecordVideoDir, persistent.RecordVideoSize, persistent.ViewportSize);
        }

        private static async Task ApplyPersistentChromeToExistingPagesAsync(IBrowserContext context)
        {
            if (context is PlaywrightNative.Chromium.ChromiumBrowserContext chromium)
            {
                foreach (IPage page in chromium.Pages)
                {
                    await chromium.ApplyChromeToPageAsync(page).ConfigureAwait(false);
                }

                return;
            }

            if (context is WebKit.WKBrowserContext webkit)
            {
                foreach (IPage page in webkit.Pages)
                {
                    await webkit.ApplyChromeToPageAsync(page).ConfigureAwait(false);
                }
            }
        }

        private static Task ApplyPersistentDownloadBehaviorAsync(IBrowserContext context)
        {
            if (context is PlaywrightNative.Chromium.ChromiumBrowserContext chromium)
            {
                return chromium.ApplyDownloadBehaviorAsync();
            }

            if (context is WebKit.WKBrowserContext webkit)
            {
                return webkit.ApplyDownloadBehaviorAsync();
            }

            return Task.CompletedTask;
        }

        private static string ResolveUserDataDir(string userDataDir)
        {
            if (string.IsNullOrEmpty(userDataDir))
            {
                string temp = Path.Combine(Path.GetTempPath(), "playwright_persistent_" + Path.GetRandomFileName());
                Directory.CreateDirectory(temp);
                return temp;
            }

            string resolved = Path.GetFullPath(userDataDir);
            Directory.CreateDirectory(resolved);
            return resolved;
        }

        private static void ThrowIfPageArgument(IEnumerable<string> args)
        {
            if (args == null)
            {
                return;
            }

            foreach (string arg in args)
            {
                if (!string.IsNullOrEmpty(arg) && !arg.StartsWith('-'))
                {
                    throw new PlaywrightNativeException("Arguments can not specify page to be opened");
                }
            }
        }

        private static bool HandleSIGINT(BrowserTypeLaunchOptions options)
            => options.HandleSIGINT != false;

        private static bool HandleSIGTERM(BrowserTypeLaunchOptions options)
            => options.HandleSIGTERM != false;

        private static bool HandleSIGHUP(BrowserTypeLaunchOptions options)
            => options.HandleSIGHUP != false;

        private static int ResolveTimeout(BrowserTypeLaunchOptions options)
            => options.Timeout ?? DefaultTimeout;

        private static string ResolveDownloadsPath(BrowserTypeLaunchOptions options)
        {
            if (!string.IsNullOrEmpty(options.DownloadsPath))
            {
                Directory.CreateDirectory(options.DownloadsPath);
                return Path.GetFullPath(options.DownloadsPath);
            }

            if (string.IsNullOrEmpty(options.ArtifactsDir))
            {
                return null;
            }

            Directory.CreateDirectory(options.ArtifactsDir);
            return options.ArtifactsDir;
        }

        private static string[] ToArgArray(IEnumerable<string> args)
        {
            if (args == null)
            {
                return null;
            }

            if (args is string[] array)
            {
                return array;
            }

            return args.ToArray();
        }

        private static string[] PersistentChromiumArgs(BrowserTypeLaunchOptions options)
        {
            List<string> args = new();
            if (options?.Args != null)
            {
                args.AddRange(options.Args);
            }

            string userAgent = (options as BrowserTypeLaunchPersistentContextOptions)?.UserAgent;
            if (!string.IsNullOrEmpty(userAgent))
            {
                bool hasUserAgent = false;
                foreach (string arg in args)
                {
                    if (arg != null && arg.StartsWith("--user-agent", StringComparison.Ordinal))
                    {
                        hasUserAgent = true;
                        break;
                    }
                }

                if (!hasUserAgent)
                {
                    args.Add("--user-agent=" + userAgent);
                }
            }

            return args.Count == 0 ? null : args.ToArray();
        }

        private static async Task<string> ResolveExecutablePathAsync(SupportedBrowser browser, BrowserTypeLaunchOptions options)
        {
            if (!string.IsNullOrEmpty(options.ExecutablePath))
            {
                return options.ExecutablePath;
            }

            if (options.Channel != BrowserChannel.Undefined)
            {
                if (browser != SupportedBrowser.Chromium)
                {
                    throw new PlaywrightNativeException("Browser channel is only supported when launching Chromium.");
                }

                return BrowserChannelResolver.Resolve(options.Channel);
            }

            BrowserFetcher fetcher = new(
                new BrowserFetcherOptions { Browser = browser },
                options.LoggerFactory);
            InstalledBrowser installed = await fetcher.DownloadAsync().ConfigureAwait(false);
            return installed.GetExecutablePath();
        }
    }
}
