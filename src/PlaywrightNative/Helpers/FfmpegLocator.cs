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
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

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
        /// (CI installs one via apt/brew/choco). Candidates are probed for an actual
        /// <c>libwebp</c> encoder — Homebrew sometimes leaves an older bottle ahead of
        /// the freshly installed one, and PATH can contain a wrapper that still
        /// resolves to the screencast build.
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

                foreach (string candidate in WebpCandidates())
                {
                    if (SupportsLibWebp(candidate))
                    {
                        _resolvedWebp = candidate;
                        return _resolvedWebp;
                    }
                }

                // Last resort: bare command name (Process PATH lookup). Callers will
                // surface a clear encode error if this also lacks libwebp.
                _resolvedWebp = BareCommandName();
                return _resolvedWebp;
            }
        }

        /// <summary>
        /// Whether <paramref name="ffmpegPath"/> lists a <c>libwebp</c> encoder.
        /// </summary>
        /// <param name="ffmpegPath">ffmpeg executable path or bare command name.</param>
        /// <returns><see langword="true"/> when WebP encoding is available.</returns>
        internal static bool SupportsLibWebp(string ffmpegPath)
        {
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                return false;
            }

            // Bare command names are always worth trying; absolute paths must exist.
            if (ffmpegPath.Contains(Path.DirectorySeparatorChar)
                || (Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar
                    && ffmpegPath.Contains(Path.AltDirectorySeparatorChar)))
            {
                if (!File.Exists(ffmpegPath))
                {
                    return false;
                }

                if (IsBundledName(ffmpegPath))
                {
                    return false;
                }
            }

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-hide_banner -encoders",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using Process process = Process.Start(startInfo);
                if (process == null)
                {
                    return false;
                }

                StringBuilder output = new StringBuilder();
                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                    {
                        output.AppendLine(e.Data);
                    }
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                    {
                        output.AppendLine(e.Data);
                    }
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (!process.WaitForExit(5_000))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    return false;
                }

                process.WaitForExit();
                string text = output.ToString();

                // Encoder listing lines look like: " V....D libwebp  libwebp WebP image"
                return text.Contains("libwebp", StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return false;
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

        private static System.Collections.Generic.IEnumerable<string> WebpCandidates()
        {
            // Prefer well-known package-manager locations before a PATH walk so a
            // stale/bundled `ffmpeg` earlier on PATH cannot win.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                yield return "/opt/homebrew/bin/ffmpeg";
                yield return "/usr/local/bin/ffmpeg";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                yield return "/usr/bin/ffmpeg";
                yield return "/usr/local/bin/ffmpeg";
            }

            string onPath = FindOnPath(BareCommandName());
            if (onPath != null)
            {
                yield return onPath;
            }

            // Every PATH hit, in order — FindOnPath only returns the first.
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                string fileName = BareCommandName();
                foreach (string directory in pathEnv.Split(Path.PathSeparator))
                {
                    if (string.IsNullOrWhiteSpace(directory))
                    {
                        continue;
                    }

                    string candidate;
                    try
                    {
                        candidate = Path.Combine(directory.Trim(), fileName);
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }

                    if (File.Exists(candidate))
                    {
                        yield return candidate;
                    }
                }
            }

            string fromEnv = Environment.GetEnvironmentVariable("PLAYWRIGHT_FFMPEG_PATH");
            if (!string.IsNullOrEmpty(fromEnv) && File.Exists(fromEnv) && !IsBundledName(fromEnv))
            {
                yield return fromEnv;
            }
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
