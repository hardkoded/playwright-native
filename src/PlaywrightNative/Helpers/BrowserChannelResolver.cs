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

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Resolves a system browser binary for <see cref="BrowserTypeLaunchOptions.Channel"/>.
    /// </summary>
    internal static class BrowserChannelResolver
    {
        /// <summary>
        /// Returns the first existing executable for <paramref name="channel"/>.
        /// </summary>
        /// <param name="channel">A Chromium or Edge channel.</param>
        /// <returns>An absolute path to the browser binary.</returns>
        /// <exception cref="PlaywrightNativeException">
        /// The channel is unset or no matching binary is installed.
        /// </exception>
        internal static string Resolve(BrowserChannel channel)
        {
            if (channel == BrowserChannel.Undefined)
            {
                throw new PlaywrightNativeException("Browser channel is not set.");
            }

            string fallback = null;
            foreach (string path in CandidatePaths(channel))
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                // Prefer binaries whose path contains the channel name (e.g.
                // /opt/microsoft/msedge/msedge over /usr/bin/microsoft-edge).
                string name = ToName(channel);
                if (!string.IsNullOrEmpty(name)
                    && path.Contains(name, StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }

                fallback ??= path;
            }

            if (fallback != null)
            {
                return fallback;
            }

            throw new PlaywrightNativeException($"Failed to find browser for channel '{ToName(channel)}'.");
        }

        /// <summary>
        /// Known install locations for <paramref name="channel"/>.
        /// </summary>
        /// <param name="channel">A Chromium or Edge channel.</param>
        /// <returns>Candidate executable paths.</returns>
        internal static IReadOnlyList<string> CandidatePaths(BrowserChannel channel)
        {
            switch (channel)
            {
                case BrowserChannel.Chrome:
                    return new[]
                    {
                        "/opt/google/chrome/chrome",
                        "/usr/bin/google-chrome",
                        "/usr/bin/google-chrome-stable",
                        "/usr/local/bin/google-chrome",
                        "/usr/local/bin/chrome",
                        @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                        @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                        "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
                    };
                case BrowserChannel.ChromeBeta:
                    return new[]
                    {
                        "/opt/google/chrome-beta/chrome",
                        "/usr/bin/google-chrome-beta",
                    };
                case BrowserChannel.ChromeDev:
                    return new[]
                    {
                        "/opt/google/chrome-unstable/chrome",
                        "/usr/bin/google-chrome-unstable",
                    };
                case BrowserChannel.ChromeCanary:
                    return new[]
                    {
                        "/Applications/Google Chrome Canary.app/Contents/MacOS/Google Chrome Canary",
                    };
                case BrowserChannel.Msedge:
                    // Prefer paths that contain "msedge" (upstream registry +
                    // LaunchChannelTests). /usr/bin/microsoft-edge* exists on
                    // some distros but fails the msedge path assertion.
                    return new[]
                    {
                        "/opt/microsoft/msedge/msedge",
                        "/usr/bin/microsoft-edge",
                        "/usr/bin/microsoft-edge-stable",
                        @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                        @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
                        "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
                    };
                case BrowserChannel.MsedgeBeta:
                    return new[]
                    {
                        "/opt/microsoft/msedge-beta/msedge",
                        "/usr/bin/microsoft-edge-beta",
                    };
                case BrowserChannel.MsedgeDev:
                    return new[]
                    {
                        "/opt/microsoft/msedge-dev/msedge",
                        "/usr/bin/microsoft-edge-dev",
                    };
                case BrowserChannel.MsedgeCanary:
                    return new[]
                    {
                        "/Applications/Microsoft Edge Canary.app/Contents/MacOS/Microsoft Edge Canary",
                    };
                default:
                    return System.Array.Empty<string>();
            }
        }

        private static string ToName(BrowserChannel channel)
        {
            switch (channel)
            {
                case BrowserChannel.Chrome:
                    return "chrome";
                case BrowserChannel.ChromeBeta:
                    return "chrome-beta";
                case BrowserChannel.ChromeDev:
                    return "chrome-dev";
                case BrowserChannel.ChromeCanary:
                    return "chrome-canary";
                case BrowserChannel.Msedge:
                    return "msedge";
                case BrowserChannel.MsedgeBeta:
                    return "msedge-beta";
                case BrowserChannel.MsedgeDev:
                    return "msedge-dev";
                case BrowserChannel.MsedgeCanary:
                    return "msedge-canary";
                default:
                    return channel.ToString();
            }
        }
    }
}
