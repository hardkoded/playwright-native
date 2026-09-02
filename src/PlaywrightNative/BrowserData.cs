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
using System.IO;
using System.Runtime.InteropServices;

namespace PlaywrightNative
{
    /// <summary>
    /// Encoding of the Playwright CDN download paths, pinned revisions, and
    /// executable locations. Kept in sync with
    /// <c>packages/playwright-core/browsers.json</c>.
    /// </summary>
    internal static class BrowserData
    {
        internal const string ChromiumRevision = "1219";
        internal const string FirefoxRevision = "1515";
        internal const string WebkitRevision = "2276";

        internal static readonly string[] CdnMirrors =
        [
            "https://cdn.playwright.dev/dbazure/download/playwright",
            "https://playwright.download.prss.microsoft.com/dbazure/download/playwright",
        ];

        private static readonly Dictionary<string, string> WebkitRevisionOverrides = new(StringComparer.Ordinal)
        {
            ["mac14"] = "2251",
            ["mac14-arm64"] = "2251",
        };

        private static readonly Dictionary<string, string> ChromiumDownloadPaths = new(StringComparer.Ordinal)
        {
            ["linux-x64"] = "builds/chromium/{0}/chromium-linux.zip",
            ["linux-arm64"] = "builds/chromium/{0}/chromium-linux-arm64.zip",
            ["mac-x64"] = "builds/chromium/{0}/chromium-mac.zip",
            ["mac-arm64"] = "builds/chromium/{0}/chromium-mac-arm64.zip",
            ["win64"] = "builds/chromium/{0}/chromium-win64.zip",
        };

        private static readonly Dictionary<string, string> FirefoxDownloadPaths = new(StringComparer.Ordinal)
        {
            ["ubuntu22.04-x64"] = "builds/firefox/{0}/firefox-ubuntu-22.04.zip",
            ["ubuntu22.04-arm64"] = "builds/firefox/{0}/firefox-ubuntu-22.04-arm64.zip",
            ["ubuntu24.04-x64"] = "builds/firefox/{0}/firefox-ubuntu-24.04.zip",
            ["ubuntu24.04-arm64"] = "builds/firefox/{0}/firefox-ubuntu-24.04-arm64.zip",
            ["mac-x64"] = "builds/firefox/{0}/firefox-mac.zip",
            ["mac-arm64"] = "builds/firefox/{0}/firefox-mac-arm64.zip",
            ["win64"] = "builds/firefox/{0}/firefox-win64.zip",
        };

        private static readonly Dictionary<string, string> WebkitDownloadPaths = new(StringComparer.Ordinal)
        {
            // WebKit ships per-LTS Linux builds: the 22.04 archive links against libavif.so.13,
            // the 24.04 archive against libavif.so.16. Running the wrong one yields
            // `error while loading shared libraries: libavif.so.X: cannot open shared object file`.
            ["ubuntu22.04-x64"] = "builds/webkit/{0}/webkit-ubuntu-22.04.zip",
            ["ubuntu22.04-arm64"] = "builds/webkit/{0}/webkit-ubuntu-22.04-arm64.zip",
            ["ubuntu24.04-x64"] = "builds/webkit/{0}/webkit-ubuntu-24.04.zip",
            ["ubuntu24.04-arm64"] = "builds/webkit/{0}/webkit-ubuntu-24.04-arm64.zip",
            ["mac14"] = "builds/webkit/{0}/webkit-mac-14.zip",
            ["mac14-arm64"] = "builds/webkit/{0}/webkit-mac-14-arm64.zip",
            ["mac15"] = "builds/webkit/{0}/webkit-mac-15.zip",
            ["mac15-arm64"] = "builds/webkit/{0}/webkit-mac-15-arm64.zip",
            ["mac26"] = "builds/webkit/{0}/webkit-mac-15.zip",
            ["mac26-arm64"] = "builds/webkit/{0}/webkit-mac-15-arm64.zip",
            ["win64"] = "builds/webkit/{0}/webkit-win64.zip",
        };

        private static readonly Dictionary<string, string[]> ChromiumExecutablePaths = new(StringComparer.Ordinal)
        {
            ["linux-x64"] = ["chrome-linux", "chrome"],
            ["linux-arm64"] = ["chrome-linux", "chrome"],
            ["mac-x64"] = ["chrome-mac", "Google Chrome for Testing.app", "Contents", "MacOS", "Google Chrome for Testing"],
            ["mac-arm64"] = ["chrome-mac-arm64", "Google Chrome for Testing.app", "Contents", "MacOS", "Google Chrome for Testing"],
            ["win-x64"] = ["chrome-win", "chrome.exe"],
        };

        private static readonly Dictionary<string, string[]> FirefoxExecutablePaths = new(StringComparer.Ordinal)
        {
            ["linux-x64"] = ["firefox", "firefox"],
            ["linux-arm64"] = ["firefox", "firefox"],
            ["mac-x64"] = ["firefox", "Nightly.app", "Contents", "MacOS", "firefox"],
            ["mac-arm64"] = ["firefox", "Nightly.app", "Contents", "MacOS", "firefox"],
            ["win-x64"] = ["firefox", "firefox.exe"],
        };

        private static readonly Dictionary<string, string[]> WebkitExecutablePaths = new(StringComparer.Ordinal)
        {
            ["linux-x64"] = ["pw_run.sh"],
            ["linux-arm64"] = ["pw_run.sh"],
            ["mac-x64"] = ["pw_run.sh"],
            ["mac-arm64"] = ["pw_run.sh"],
            ["win-x64"] = ["Playwright.exe"],
        };

        internal static string DefaultRevision(SupportedBrowser browser) => browser switch
        {
            SupportedBrowser.Chromium => ChromiumRevision,
            SupportedBrowser.Firefox => FirefoxRevision,
            SupportedBrowser.Webkit => WebkitRevision,
            _ => throw new ArgumentOutOfRangeException(nameof(browser)),
        };

        internal static Platform CurrentPlatform()
        {
            bool isArm64 = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return isArm64 ? Platform.MacOSArm64 : Platform.MacOS;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Platform.Win64;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return isArm64 ? Platform.LinuxArm64 : Platform.Linux;
            }

            return Platform.Unknown;
        }

        internal static string PlaywrightPlatformKey(SupportedBrowser browser, Platform platform)
        {
            if (browser == SupportedBrowser.Webkit &&
                (platform == Platform.MacOS || platform == Platform.MacOSArm64))
            {
                return MacOSWebkitPlatformKey(platform == Platform.MacOSArm64);
            }

            return SimplePlaywrightKey(browser, platform);
        }

        internal static string ShortPlatformKey(Platform platform) => platform switch
        {
            Platform.MacOS => "mac-x64",
            Platform.MacOSArm64 => "mac-arm64",
            Platform.Linux => "linux-x64",
            Platform.LinuxArm64 => "linux-arm64",
            Platform.Win64 => "win-x64",
            _ => throw new ArgumentOutOfRangeException(nameof(platform)),
        };

        internal static string ResolveRevision(SupportedBrowser browser, string playwrightPlatformKey, string requestedRevision)
        {
            if (requestedRevision != null)
            {
                return requestedRevision;
            }

            if (browser == SupportedBrowser.Webkit &&
                WebkitRevisionOverrides.TryGetValue(playwrightPlatformKey, out string overrideRevision))
            {
                return overrideRevision;
            }

            return DefaultRevision(browser);
        }

        internal static string[] DownloadUrls(SupportedBrowser browser, string playwrightPlatformKey, string revision, string[] hosts)
        {
            Dictionary<string, string> paths = browser switch
            {
                SupportedBrowser.Chromium => ChromiumDownloadPaths,
                SupportedBrowser.Firefox => FirefoxDownloadPaths,
                SupportedBrowser.Webkit => WebkitDownloadPaths,
                _ => throw new ArgumentOutOfRangeException(nameof(browser)),
            };

            if (!paths.TryGetValue(playwrightPlatformKey, out string template))
            {
                throw new PlaywrightNativeException(
                    $"{browser} is not supported on platform '{playwrightPlatformKey}'.");
            }

            string archivePath = string.Format(System.Globalization.CultureInfo.InvariantCulture, template, revision);
            string[] hostList = (hosts != null && hosts.Length > 0) ? hosts : CdnMirrors;

            string[] urls = new string[hostList.Length];
            for (int i = 0; i < hostList.Length; i++)
            {
                urls[i] = $"{hostList[i].TrimEnd('/')}/{archivePath}";
            }

            return urls;
        }

        internal static string ExecutablePath(SupportedBrowser browser, Platform platform, string installationDir)
        {
            string shortKey = ShortPlatformKey(platform);

            Dictionary<string, string[]> paths = browser switch
            {
                SupportedBrowser.Chromium => ChromiumExecutablePaths,
                SupportedBrowser.Firefox => FirefoxExecutablePaths,
                SupportedBrowser.Webkit => WebkitExecutablePaths,
                _ => throw new ArgumentOutOfRangeException(nameof(browser)),
            };

            if (!paths.TryGetValue(shortKey, out string[] segments))
            {
                throw new PlaywrightNativeException(
                    $"{browser} executable path is not defined for platform '{shortKey}'.");
            }

            return Path.Combine(installationDir, Path.Combine(segments));
        }

        internal static string DefaultCacheDir()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library",
                    "Caches",
                    "ms-playwright");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ms-playwright");
            }

            string xdgCache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            string cacheBase = !string.IsNullOrEmpty(xdgCache)
                ? xdgCache
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");

            return Path.Combine(cacheBase, "ms-playwright");
        }

        internal static string InstallationDir(string cacheDir, SupportedBrowser browser, string buildId)
        {
            string name = browser switch
            {
                SupportedBrowser.Chromium => "chromium",
                SupportedBrowser.Firefox => "firefox",
                SupportedBrowser.Webkit => "webkit",
                _ => throw new ArgumentOutOfRangeException(nameof(browser)),
            };

            return Path.Combine(cacheDir, $"{name}-{buildId}");
        }

        private static string SimplePlaywrightKey(SupportedBrowser browser, Platform platform) => platform switch
        {
            Platform.MacOS => "mac-x64",
            Platform.MacOSArm64 => "mac-arm64",
            Platform.Linux => browser == SupportedBrowser.Chromium ? "linux-x64" : $"{UbuntuVersionTag()}-x64",
            Platform.LinuxArm64 => browser == SupportedBrowser.Chromium ? "linux-arm64" : $"{UbuntuVersionTag()}-arm64",
            Platform.Win64 => "win64",
            _ => throw new ArgumentOutOfRangeException(nameof(platform)),
        };

        /// <summary>
        /// Reads <c>/etc/os-release</c> to choose the right Ubuntu-versioned WebKit/Firefox
        /// archive. Playwright ships separate Linux builds per LTS — the 22.04 archive links
        /// against libavif.so.13 / libicu70, while 24.04 links against libavif.so.16 / libicu74.
        /// Defaults to <c>ubuntu24.04</c> when the host isn't Ubuntu (or the file is unreadable)
        /// since that's what GitHub's ubuntu-latest runner ships today.
        /// </summary>
        private static string UbuntuVersionTag()
        {
            try
            {
                if (File.Exists("/etc/os-release"))
                {
                    foreach (string raw in File.ReadAllLines("/etc/os-release"))
                    {
                        if (!raw.StartsWith("VERSION_ID=", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        string value = raw.Substring("VERSION_ID=".Length).Trim('"');
                        if (value.StartsWith("22.", StringComparison.Ordinal))
                        {
                            return "ubuntu22.04";
                        }

                        // 24.04 today; future LTS bumps fall through to this case rather
                        // than silently picking a stale build.
                        return "ubuntu24.04";
                    }
                }
            }
            catch (IOException)
            {
                // Treat /etc/os-release errors as "unknown Linux" and let the caller fall
                // back to ubuntu24.04 — better than throwing during platform-key resolution.
            }

            return "ubuntu24.04";
        }

        private static string MacOSWebkitPlatformKey(bool arm64)
        {
            // .NET 5+ reports the macOS product version here (e.g. 14, 15, 26), NOT the
            // Darwin kernel version (23, 24, 25). The old Darwin-based mapping shipped a
            // mac-15 binary to macOS-14 hosts and crashed on dyld symbol lookup.
            int macOsMajor = Environment.OSVersion.Version.Major;

            // Apple jumped from macOS 15 (2024) to macOS 26 (2025); anything not in
            // {14, 15} falls through to the latest available "mac26" build. This is
            // also the safe path for non-mac hosts (e.g. unit tests on Linux that pass
            // MacOSArm64 artificially) — better to return a plausible label than throw.
            string macOsLabel = macOsMajor switch
            {
                14 => "mac14",
                15 => "mac15",
                _ => "mac26",
            };

            return arm64 ? $"{macOsLabel}-arm64" : macOsLabel;
        }
    }
}
