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

                string homeCache = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".cache",
                    "ms-playwright");
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
            string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
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
