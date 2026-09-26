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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Recodes a PNG as WebP. Prefers ffmpeg <c>libwebp</c> when available;
    /// falls back to Google's <c>cwebp</c> (Homebrew <c>webp</c> / apt
    /// <c>webp</c>) because recent Homebrew ffmpeg bottles ship without
    /// <c>--enable-libwebp</c>. Quality 100 or omitted is lossless.
    /// </summary>
    internal static class PngToWebp
    {
        private static readonly object Gate = new();
        private static string _resolvedCwebp;

        /// <summary>
        /// Converts PNG bytes to WebP.
        /// </summary>
        /// <param name="png">PNG bytes.</param>
        /// <param name="quality">0–100. Values of 100 or <see langword="null"/> are lossless.</param>
        /// <returns>WebP bytes.</returns>
        internal static byte[] Convert(byte[] png, int? quality)
        {
            if (png == null || png.Length == 0)
            {
                throw new PlaywrightException("PNG screenshot is empty.");
            }

            bool lossless = !quality.HasValue || quality.Value >= 100;
            int q = quality ?? 100;
            if (q < 0)
            {
                q = 0;
            }
            else if (q > 100)
            {
                q = 100;
            }

            string input = Path.Combine(Path.GetTempPath(), "pw-webp-" + Path.GetRandomFileName() + ".png");
            string output = Path.Combine(Path.GetTempPath(), "pw-webp-" + Path.GetRandomFileName() + ".webp");
            File.WriteAllBytes(input, png);
            try
            {
                string ffmpeg = FfmpegLocator.ResolveForWebp();
                if (FfmpegLocator.SupportsLibWebp(ffmpeg))
                {
                    string ffmpegArgs = lossless
                        ? "-hide_banner -loglevel error -y -i \"" + input + "\" -c:v libwebp -lossless 1 \"" + output + "\""
                        : "-hide_banner -loglevel error -y -i \"" + input + "\" -c:v libwebp -quality "
                            + q.ToString(CultureInfo.InvariantCulture) + " \"" + output + "\"";
                    return RunEncoder(ffmpeg, ffmpegArgs, output, "ffmpeg");
                }

                string cwebp = ResolveCwebp();
                if (!string.IsNullOrEmpty(cwebp))
                {
                    string cwebpArgs = lossless
                        ? "-quiet -lossless \"" + input + "\" -o \"" + output + "\""
                        : "-quiet -q " + q.ToString(CultureInfo.InvariantCulture)
                            + " \"" + input + "\" -o \"" + output + "\"";
                    return RunEncoder(cwebp, cwebpArgs, output, "cwebp");
                }

                throw new PlaywrightException(
                    "Failed to encode WebP screenshot. Install an ffmpeg build with libwebp "
                    + "or the webp package (cwebp).");
            }
            finally
            {
                TryDelete(input);
                TryDelete(output);
            }
        }

        private static byte[] RunEncoder(string fileName, string arguments, string output, string tool)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using Process process = Process.Start(startInfo);
            if (process == null)
            {
                throw new PlaywrightException("Failed to start " + tool + " for WebP screenshot.");
            }

            StringBuilder errorBuilder = new();
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };
            process.BeginErrorReadLine();
            if (!process.WaitForExit(15_000))
            {
                TryKill(process);
                throw new PlaywrightException("Timed out waiting for " + tool + " to encode WebP screenshot.");
            }

            process.WaitForExit();
            if (process.ExitCode != 0 || !File.Exists(output))
            {
                string error = errorBuilder.ToString();
                throw new PlaywrightException(
                    "Failed to encode WebP screenshot." + (string.IsNullOrEmpty(error) ? string.Empty : " " + error.Trim()));
            }

            return File.ReadAllBytes(output);
        }

        private static string ResolveCwebp()
        {
            lock (Gate)
            {
                if (_resolvedCwebp != null)
                {
                    return _resolvedCwebp.Length == 0 ? null : _resolvedCwebp;
                }

                foreach (string candidate in CwebpCandidates())
                {
                    if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                    {
                        _resolvedCwebp = candidate;
                        return _resolvedCwebp;
                    }
                }

                string onPath = FindOnPath(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cwebp.exe" : "cwebp");
                _resolvedCwebp = onPath ?? string.Empty;
                return onPath;
            }
        }

        private static System.Collections.Generic.IEnumerable<string> CwebpCandidates()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                yield return "/opt/homebrew/bin/cwebp";
                yield return "/usr/local/bin/cwebp";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                yield return "/usr/bin/cwebp";
                yield return "/usr/local/bin/cwebp";
            }
        }

        private static string FindOnPath(string fileName)
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv))
            {
                return null;
            }

            foreach (string directory in pathEnv.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

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

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                process.Kill();
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
