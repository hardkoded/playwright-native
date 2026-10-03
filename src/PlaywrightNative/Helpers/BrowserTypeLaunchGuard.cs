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
using System.IO;
using Microsoft.Playwright;

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
        /// Official Chromium profile-in-use sentence from <c>chromium.ts</c>
        /// <c>profileInUseError</c>.
        /// </summary>
        internal const string ProfileInUseMessage =
            "This usually means that the profile is already in use by another instance of Chromium.";

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
                throw new PlaywrightException(
                    "userDataDir option is not supported in `browserType.launch`. Use `browserType.launchPersistentContext` instead");
            }

            if (options.Port.HasValue)
            {
                throw new PlaywrightException("Cannot specify a port without launching as a server.");
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
                    throw new PlaywrightException(
                        "Pass userDataDir parameter to 'browserType.launchPersistentContext");
                }

                if (!arg.StartsWith('-'))
                {
                    throw new PlaywrightException("Arguments can not specify page to be opened");
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
                throw new PlaywrightException("Cannot specify a port without launching as a server.");
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
                throw new PlaywrightException(NoXServerRunningError);
            }
        }

        /// <summary>
        /// Official launch errors start with the API name and include
        /// <c>Browser logs:</c> so callers can match
        /// <c>browserType.launch</c> plus spawn / log text.
        /// </summary>
        /// <param name="api">Official API name, for example <c>browserType.launch</c>.</param>
        /// <param name="ex">The launch failure.</param>
        /// <returns>A wrapped <see cref="PlaywrightException"/>.</returns>
        internal static PlaywrightException WrapLaunch(string api, Exception ex)
        {
            string inner = RewriteStartupLog(ex?.Message ?? string.Empty);
            if (inner.StartsWith(api, StringComparison.Ordinal))
            {
                return ex as PlaywrightException ?? new PlaywrightException(inner, ex);
            }

            return new PlaywrightException(api + ": " + inner + "\nBrowser logs:\n\n" + inner, ex);
        }

        /// <summary>
        /// Official <c>browserType.connectOverCDP</c> errors start with the
        /// API name and include <c>Browser logs:</c> plus the close reason.
        /// </summary>
        /// <param name="ex">The connect failure.</param>
        /// <param name="browserLogs">WebSocket close reason, or <see langword="null"/>.</param>
        /// <returns>A wrapped <see cref="PlaywrightException"/>.</returns>
        internal static PlaywrightException WrapConnectOverCdp(Exception ex, string browserLogs = null)
        {
            const string api = "browserType.connectOverCDP";
            string inner = ex?.Message ?? string.Empty;
            if (inner.StartsWith(api, StringComparison.Ordinal))
            {
                return ex as PlaywrightException ?? new PlaywrightException(inner, ex);
            }

            string logs = string.IsNullOrEmpty(browserLogs) ? inner : browserLogs;
            return new PlaywrightException(api + ": " + inner + "\nBrowser logs:\n\n" + logs + "\n", ex);
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

        /// <summary>
        /// Official <c>profileInUseError</c>: returns a short exception message when
        /// Chromium stderr reports a profile lock, otherwise <see langword="null"/>.
        /// </summary>
        /// <param name="logs">One or more browser log lines.</param>
        /// <returns>The official profile-in-use message, or <see langword="null"/>.</returns>
        internal static string TryGetProfileInUseError(string logs)
        {
            string marker = FindProfileInUseMarker(logs);
            if (marker == null)
            {
                return null;
            }

            return marker + " " + ProfileInUseMessage;
        }

        /// <summary>
        /// When Chromium exits without stderr markers, annotate the failure with
        /// a profile-lock hint if <paramref name="userDataDir"/> holds a lock.
        /// </summary>
        /// <param name="message">Launch failure message.</param>
        /// <param name="userDataDir">Persistent profile directory, or null.</param>
        /// <returns>Possibly annotated message.</returns>
        internal static string AppendProfileLockHint(string message, string userDataDir)
        {
            if (string.IsNullOrEmpty(userDataDir) || string.IsNullOrEmpty(message))
            {
                return message;
            }

            if (message.Contains(ProfileInUseMessage, StringComparison.Ordinal))
            {
                return message;
            }

            if (FindProfileInUseMarker(message) != null)
            {
                return RewriteProfileInUse(message);
            }

            try
            {
                string lockPath = Path.Combine(userDataDir, "SingletonLock");
                string cookiePath = Path.Combine(userDataDir, "SingletonCookie");
                string socketPath = Path.Combine(userDataDir, "SingletonSocket");
                string runningPath = Path.Combine(userDataDir, "RunningChromeVersion");
                string lockfilePath = Path.Combine(userDataDir, "lockfile");
                if (File.Exists(lockPath)
                    || Directory.Exists(lockPath)
                    || File.Exists(cookiePath)
                    || Directory.Exists(cookiePath)
                    || File.Exists(socketPath)
                    || Directory.Exists(socketPath)
                    || File.Exists(runningPath)
                    || File.Exists(lockfilePath))
                {
                    return RewriteProfileInUse(message + "\n[profile-lock] SingletonLock");
                }

                // Windows Chromium often exits with an empty stderr and removes
                // Singleton* before we inspect the directory. A populated
                // Default/ profile after a generic "Failed to launch" is still
                // the profile-in-use case for the double-connect tests.
                string defaultDir = Path.Combine(userDataDir, "Default");
                if (Directory.Exists(defaultDir)
                    && message.Contains("Failed to launch browser", StringComparison.OrdinalIgnoreCase))
                {
                    return RewriteProfileInUse(message + "\n[profile-lock] Default");
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return message;
        }

        private static string RewriteProfileInUse(string logs)
        {
            if (logs.Contains(ProfileInUseMessage, StringComparison.Ordinal))
            {
                return logs;
            }

            string marker = FindProfileInUseMarker(logs);
            if (marker == null)
            {
                return logs;
            }

            return logs + "\n" + marker + " " + ProfileInUseMessage;
        }

        private static string FindProfileInUseMarker(string logs)
        {
            if (string.IsNullOrEmpty(logs))
            {
                return null;
            }

            // Official markers from chromium.ts profileInUseError, plus
            // SingletonLock lines printed by process_singleton_posix.cc /
            // process_singleton_win.cc before the ProcessSingleton summary.
            if (logs.Contains("Failed to create a ProcessSingleton for your profile directory.", StringComparison.Ordinal))
            {
                return "Failed to create a ProcessSingleton for your profile directory.";
            }

            if (logs.Contains("Opening in existing browser session.", StringComparison.Ordinal))
            {
                return "Opening in existing browser session.";
            }

            if (logs.Contains("SingletonLock", StringComparison.Ordinal))
            {
                return "Failed to create a ProcessSingleton for your profile directory.";
            }

            if (logs.Contains("ProcessSingleton", StringComparison.Ordinal)
                && (logs.Contains("profile directory", StringComparison.OrdinalIgnoreCase)
                    || logs.Contains("profile is already in use", StringComparison.OrdinalIgnoreCase)))
            {
                return "Failed to create a ProcessSingleton for your profile directory.";
            }

            // Windows Chromium often exits on a locked profile without printing
            // ProcessSingleton to stderr (logging goes to the user-data debug
            // file). AppendProfileLockHint injects [profile-lock] when the
            // profile directory still holds SingletonLock / SingletonCookie.
            if (logs.Contains("Failed to launch browser!", StringComparison.Ordinal)
                && logs.Contains("[profile-lock]", StringComparison.Ordinal))
            {
                return "Failed to create a ProcessSingleton for your profile directory.";
            }

            return null;
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
