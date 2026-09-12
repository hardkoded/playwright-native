/*
 * Copyright (c) Microsoft Corporation.
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
using System.Runtime.InteropServices;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Resolves an ffmpeg executable for screencast / WebP helpers.
    /// </summary>
    internal static class FfmpegLocator
    {
        private static readonly object Gate = new();
        private static string _resolved;

        /// <summary>
        /// Returns a path or bare command name suitable for <see cref="System.Diagnostics.ProcessStartInfo.FileName"/>.
        /// </summary>
        /// <returns>An ffmpeg path, or <c>ffmpeg</c> / <c>ffmpeg.exe</c> for PATH lookup.</returns>
        internal static string Resolve()
        {
            lock (Gate)
            {
                if (!string.IsNullOrEmpty(_resolved))
                {
                    return _resolved;
                }

                string fromEnv = Environment.GetEnvironmentVariable("PLAYWRIGHT_FFMPEG_PATH");
                if (!string.IsNullOrEmpty(fromEnv) && File.Exists(fromEnv))
                {
                    _resolved = fromEnv;
                    return _resolved;
                }

                string browsersPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
                if (!string.IsNullOrEmpty(browsersPath) && Directory.Exists(browsersPath))
                {
                    string bundled = FindBundled(browsersPath);
                    if (bundled != null)
                    {
                        _resolved = bundled;
                        return _resolved;
                    }
                }

                // BrowserData.DefaultCacheDir() already knows the per-OS convention
                // (~/Library/Caches/ms-playwright on macOS, %LOCALAPPDATA%/ms-playwright
                // on Windows, $XDG_CACHE_HOME or ~/.cache/ms-playwright on Linux) —
                // don't re-derive it here and get it wrong for non-Linux hosts.
                string homeCache = BrowserData.DefaultCacheDir();
                if (Directory.Exists(homeCache))
                {
                    string bundled = FindBundled(homeCache);
                    if (bundled != null)
                    {
                        _resolved = bundled;
                        return _resolved;
                    }
                }

                _resolved = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
                return _resolved;
            }
        }

        private static string FindBundled(string root)
        {
            // Playwright's downloaded ffmpeg build isn't named "ffmpeg" on disk —
            // it's ffmpeg-linux / ffmpeg-mac / ffmpeg-win64.exe (see BrowserData's
            // FfmpegExecutablePaths), so a literal "ffmpeg" search never matches it.
            string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "ffmpeg-win64.exe"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "ffmpeg-mac" : "ffmpeg-linux";
            try
            {
                foreach (string path in Directory.EnumerateFiles(root, exeName, SearchOption.AllDirectories))
                {
                    return path;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return null;
        }
    }
}
