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
        private static string _resolvedWebp;

        /// <summary>
        /// Returns a path or bare command name suitable for <see cref="System.Diagnostics.ProcessStartInfo.FileName"/>.
        /// Prefers Playwright's bundled screencast build when present.
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

                _resolved = BareCommandName();
                return _resolved;
            }
        }

        /// <summary>
        /// Returns an ffmpeg that can encode WebP via <c>libwebp</c>.
        /// Playwright's bundled build is screencast-only (<c>--disable-everything</c>,
        /// no libwebp) and rejects <c>-lossless</c>; prefer a system ffmpeg on PATH
        /// (CI installs one via apt/brew/choco).
        /// </summary>
        /// <returns>An ffmpeg path, or <c>ffmpeg</c> / <c>ffmpeg.exe</c> for PATH lookup.</returns>
        internal static string ResolveForWebp()
        {
            lock (Gate)
            {
                if (!string.IsNullOrEmpty(_resolvedWebp))
                {
                    return _resolvedWebp;
                }

                string onPath = FindOnPath(BareCommandName());
                if (onPath != null)
                {
                    _resolvedWebp = onPath;
                    return _resolvedWebp;
                }

                string fromEnv = Environment.GetEnvironmentVariable("PLAYWRIGHT_FFMPEG_PATH");
                if (!string.IsNullOrEmpty(fromEnv) && File.Exists(fromEnv) && !IsBundledName(fromEnv))
                {
                    _resolvedWebp = fromEnv;
                    return _resolvedWebp;
                }

                _resolvedWebp = BareCommandName();
                return _resolvedWebp;
            }
        }

        private static string BareCommandName()
            => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";

        private static bool IsBundledName(string path)
        {
            string name = Path.GetFileName(path);
            return string.Equals(name, "ffmpeg-linux", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "ffmpeg-mac", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "ffmpeg-win64.exe", StringComparison.OrdinalIgnoreCase);
        }

        private static string FindOnPath(string fileName)
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv))
            {
                return null;
            }

            char[] separators = { Path.PathSeparator };
            foreach (string directory in pathEnv.Split(separators, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string candidate = Path.Combine(directory.Trim(), fileName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (ArgumentException)
                {
                }
            }

            return null;
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
