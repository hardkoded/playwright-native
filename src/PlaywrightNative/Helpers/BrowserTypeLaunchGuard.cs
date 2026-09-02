/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>browserType.launch</c> / <c>launchPersistentContext</c>
    /// option checks and launch-error wrapping from
    /// <c>library/browsertype-launch.spec.ts</c>.
    /// </summary>
    internal static class BrowserTypeLaunchGuard
    {
        /// <summary>
        /// Official <c>kNoXServerRunningError</c> from
        /// <c>packages/playwright-core/src/server/browserType.ts</c>.
        /// </summary>
        internal const string NoXServerRunningError =
            "Looks like you launched a headed browser without having a XServer running.\n" +
            "Set either 'headless: true' or use 'xvfb-run ' before running Playwright.\n\n<3 Playwright Team";

        /// <summary>
        /// Rejects <c>userDataDir</c>, <c>port</c>, profile args, and page URLs
        /// on <c>browserType.launch</c>.
        /// </summary>
        /// <param name="options">Launch options. <see langword="null"/> is ignored.</param>
        internal static void ThrowIfLaunchForbidden(BrowserTypeLaunchOptions options)
        {
            if (options == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(options.UserDataDir))
            {
                throw new PlaywrightNativeException(
                    "userDataDir option is not supported in `browserType.launch`. Use `browserType.launchPersistentContext` instead");
            }

            if (options.Port.HasValue)
            {
                throw new PlaywrightNativeException("Cannot specify a port without launching as a server.");
            }

            ThrowIfHeadedWithoutXServer(options);

            if (options.Args == null)
            {
                return;
            }

            foreach (string arg in options.Args)
            {
                if (string.IsNullOrEmpty(arg))
                {
                    continue;
                }

                if (arg.StartsWith("--user-data-dir", StringComparison.Ordinal)
                    || arg.StartsWith("--profile", StringComparison.Ordinal))
                {
                    throw new PlaywrightNativeException(
                        "Pass userDataDir parameter to 'browserType.launchPersistentContext");
                }

                if (!arg.StartsWith('-'))
                {
                    throw new PlaywrightNativeException("Arguments can not specify page to be opened");
                }
            }
        }

        /// <summary>
        /// Rejects <c>port</c> on <c>browserType.launchPersistentContext</c>.
        /// </summary>
        /// <param name="options">Launch options. <see langword="null"/> is ignored.</param>
        internal static void ThrowIfPersistentForbidden(BrowserTypeLaunchOptions options)
        {
            if (options == null)
            {
                return;
            }

            if (options.Port.HasValue)
            {
                throw new PlaywrightNativeException("Cannot specify a port without launching as a server.");
            }

            ThrowIfHeadedWithoutXServer(options);
        }

        /// <summary>
        /// Official headed Linux launch without <c>DISPLAY</c> uses
        /// <see cref="NoXServerRunningError"/>.
        /// </summary>
        /// <param name="options">Launch options. <see langword="null"/> is ignored.</param>
        internal static void ThrowIfHeadedWithoutXServer(BrowserTypeLaunchOptions options)
        {
            if (options == null || options.Headless || !OperatingSystem.IsLinux())
            {
                return;
            }

            if (string.IsNullOrEmpty(ResolveDisplay(options)))
            {
                throw new PlaywrightNativeException(NoXServerRunningError);
            }
        }

        /// <summary>
        /// Official launch errors start with the API name and include
        /// <c>Browser logs:</c> so callers can match
        /// <c>browserType.launch</c> plus spawn / log text.
        /// </summary>
        /// <param name="api">Official API name, for example <c>browserType.launch</c>.</param>
        /// <param name="ex">The launch failure.</param>
        /// <returns>A wrapped <see cref="PlaywrightNativeException"/>.</returns>
        internal static PlaywrightNativeException WrapLaunch(string api, Exception ex)
        {
            string inner = RewriteStartupLog(ex?.Message ?? string.Empty);
            if (inner.StartsWith(api, StringComparison.Ordinal))
            {
                return ex as PlaywrightNativeException ?? new PlaywrightNativeException(inner, ex);
            }

            return new PlaywrightNativeException(api + ": " + inner + "\nBrowser logs:\n\n" + inner, ex);
        }

        /// <summary>
        /// Official <c>browserType.connectOverCDP</c> errors start with the
        /// API name and include <c>Browser logs:</c> plus the close reason.
        /// </summary>
        /// <param name="ex">The connect failure.</param>
        /// <param name="browserLogs">WebSocket close reason, or <see langword="null"/>.</param>
        /// <returns>A wrapped <see cref="PlaywrightNativeException"/>.</returns>
        internal static PlaywrightNativeException WrapConnectOverCdp(Exception ex, string browserLogs = null)
        {
            const string api = "browserType.connectOverCDP";
            string inner = ex?.Message ?? string.Empty;
            if (inner.StartsWith(api, StringComparison.Ordinal))
            {
                return ex as PlaywrightNativeException ?? new PlaywrightNativeException(inner, ex);
            }

            string logs = string.IsNullOrEmpty(browserLogs) ? inner : browserLogs;
            return new PlaywrightNativeException(api + ": " + inner + "\nBrowser logs:\n\n" + logs + "\n", ex);
        }

        /// <summary>
        /// Official Chromium / WebKit <c>doRewriteStartupLog</c> plus Chromium
        /// <c>profileInUseError</c> from <c>chromium.ts</c>.
        /// </summary>
        /// <param name="logs">Raw launch or protocol logs.</param>
        /// <returns>The rewritten log text.</returns>
        internal static string RewriteStartupLog(string logs)
        {
            if (string.IsNullOrEmpty(logs))
            {
                return logs;
            }

            if (logs.Contains("Missing X server", StringComparison.Ordinal)
                || logs.Contains("Failed to open display", StringComparison.Ordinal)
                || logs.Contains("cannot open display", StringComparison.OrdinalIgnoreCase))
            {
                return "\n" + NoXServerRunningError;
            }

            return RewriteProfileInUse(logs);
        }

        private static string RewriteProfileInUse(string logs)
        {
            const string profileInUse =
                "This usually means that the profile is already in use by another instance of Chromium.";
            if (logs.Contains(profileInUse, StringComparison.Ordinal))
            {
                return logs;
            }

            string marker = null;
            if (logs.Contains("Failed to create a ProcessSingleton for your profile directory.", StringComparison.Ordinal))
            {
                marker = "Failed to create a ProcessSingleton for your profile directory.";
            }
            else if (logs.Contains("Opening in existing browser session.", StringComparison.Ordinal))
            {
                marker = "Opening in existing browser session.";
            }
            else if (logs.Contains("SingletonLock", StringComparison.Ordinal)
                && logs.Contains("Failed to create", StringComparison.Ordinal))
            {
                marker = "Failed to create a ProcessSingleton for your profile directory.";
            }

            if (marker == null)
            {
                return logs;
            }

            return logs + "\n" + marker + " " + profileInUse;
        }

        private static string ResolveDisplay(BrowserTypeLaunchOptions options)
        {
            if (options.Env != null && options.Env.TryGetValue("DISPLAY", out string display))
            {
                return display;
            }

            return Environment.GetEnvironmentVariable("DISPLAY");
        }
    }
}
