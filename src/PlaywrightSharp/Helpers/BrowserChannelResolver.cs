/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.IO;

namespace PlaywrightSharp.Helpers
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
        /// <exception cref="PlaywrightSharpException">
        /// The channel is unset or no matching binary is installed.
        /// </exception>
        internal static string Resolve(BrowserChannel channel)
        {
            if (channel == BrowserChannel.Undefined)
            {
                throw new PlaywrightSharpException("Browser channel is not set.");
            }

            foreach (string path in CandidatePaths(channel))
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            throw new PlaywrightSharpException($"Failed to find browser for channel '{ToName(channel)}'.");
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
                    return new[]
                    {
                        "/usr/bin/microsoft-edge",
                        "/usr/bin/microsoft-edge-stable",
                        "/opt/microsoft/msedge/msedge",
                        @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                        @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
                        "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
                    };
                case BrowserChannel.MsedgeBeta:
                    return new[]
                    {
                        "/usr/bin/microsoft-edge-beta",
                        "/opt/microsoft/msedge-beta/msedge",
                    };
                case BrowserChannel.MsedgeDev:
                    return new[]
                    {
                        "/usr/bin/microsoft-edge-dev",
                        "/opt/microsoft/msedge-dev/msedge",
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
