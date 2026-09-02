/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Recodes a PNG as WebP via ffmpeg <c>libwebp</c>. Official WebKit on
    /// Linux is supposed to encode natively; older builds still return PNG.
    /// Quality 100 or omitted is lossless, matching official WK.
    /// </summary>
    internal static class PngToWebp
    {
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
                throw new PlaywrightNativeException("PNG screenshot is empty.");
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
                string qualityArg = lossless
                    ? "-lossless 1"
                    : "-quality " + q.ToString(CultureInfo.InvariantCulture);
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = "-hide_banner -loglevel error -y -i \"" + input + "\" -c:v libwebp " + qualityArg + " \"" + output + "\"",
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using Process process = Process.Start(startInfo);
                if (process == null)
                {
                    throw new PlaywrightNativeException("Failed to start ffmpeg for WebP screenshot.");
                }

                process.WaitForExit();
                if (process.ExitCode != 0 || !File.Exists(output))
                {
                    string error = process.StandardError.ReadToEnd();
                    throw new PlaywrightNativeException(
                        "Failed to encode WebP screenshot." + (string.IsNullOrEmpty(error) ? string.Empty : " " + error.Trim()));
                }

                return File.ReadAllBytes(output);
            }
            finally
            {
                TryDelete(input);
                TryDelete(output);
            }
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
    }
}
