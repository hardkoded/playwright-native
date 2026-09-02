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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>VideoPlayer</c> / pixel predicates for <c>video.spec.ts</c>.
    /// </summary>
    internal static class OfficialVideo
    {
        internal readonly struct Pixel
        {
            internal Pixel(byte r, byte g, byte b, byte a)
            {
                R = r;
                G = g;
                B = b;
                A = a;
            }

            internal byte R { get; }

            internal byte G { get; }

            internal byte B { get; }

            internal byte A { get; }
        }

        internal sealed class Probe
        {
            internal int Width { get; init; }

            internal int Height { get; init; }

            internal double Duration { get; init; }
        }

        internal static bool IsAlmostRed(Pixel pixel)
            => pixel.R > 185 && pixel.G < 70 && pixel.B < 70 && pixel.A == 255;

        internal static bool IsAlmostBlack(Pixel pixel)
            => pixel.R < 70 && pixel.G < 70 && pixel.B < 70 && pixel.A == 255;

        internal static bool IsAlmostGray(Pixel pixel)
            => pixel.R > 70 && pixel.R < 185
            && pixel.G > 70 && pixel.G < 185
            && pixel.B > 70 && pixel.B < 185
            && pixel.A == 255;

        internal static Probe Read(string videoFile)
        {
            string output = Run(
                "ffprobe",
                "-v error -select_streams v:0 -show_entries stream=width,height -show_entries format=duration -of csv=p=0 " + Quote(videoFile));
            string[] lines = output.Replace(',', ' ').Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 3)
            {
                throw new InvalidOperationException("ffprobe failed for " + videoFile + ": " + output);
            }

            return new Probe
            {
                Width = int.Parse(lines[0], CultureInfo.InvariantCulture),
                Height = int.Parse(lines[1], CultureInfo.InvariantCulture),
                Duration = double.Parse(lines[2], CultureInfo.InvariantCulture),
            };
        }

        internal static void ExpectRedFrames(string videoFile, int width, int height)
        {
            Probe probe = Read(videoFile);
            if (probe.Duration <= 0)
            {
                throw new InvalidOperationException("Expected video duration > 0");
            }

            if (probe.Width != width || probe.Height != height)
            {
                throw new InvalidOperationException(
                    "Expected " + width + "x" + height + " got " + probe.Width + "x" + probe.Height);
            }

            ExpectAll(LastFrame(videoFile), IsAlmostRed);
            ExpectAll(LastFrame(videoFile, width - 20, 10, 1, 1), IsAlmostRed);
        }

        internal static Image<Rgba32> LastFrame(string videoFile, int x = 0, int y = 0, int width = 0, int height = 0)
        {
            string temp = Path.Combine(Path.GetTempPath(), "pw-video-last-" + Guid.NewGuid().ToString("N") + ".png");
            Run(
                "ffmpeg",
                "-y -sseof -0.04 -i " + Quote(videoFile) + " -frames:v 1 " + Quote(temp));
            if (!File.Exists(temp) || new FileInfo(temp).Length == 0)
            {
                Run(
                    "ffmpeg",
                    "-y -i " + Quote(videoFile) + " -update 1 -frames:v 1 " + Quote(temp));
            }

            Image<Rgba32> image = Image.Load<Rgba32>(temp);
            try
            {
                File.Delete(temp);
            }
            catch (IOException)
            {
            }
            if (width <= 0 || height <= 0)
            {
                return image;
            }

            Image<Rgba32> crop = new(width, height);
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    int sx = Math.Clamp(x + col, 0, image.Width - 1);
                    int sy = Math.Clamp(y + row, 0, image.Height - 1);
                    crop[col, row] = image[sx, sy];
                }
            }

            image.Dispose();
            return crop;
        }

        internal static bool FindFrame(string videoFile, Func<Image<Rgba32>, bool> predicate)
        {
            string temp = Path.Combine(Path.GetTempPath(), "pw-video-frames-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                Run("ffmpeg", "-y -i " + Quote(videoFile) + " -vsync 0 " + Quote(Path.Combine(temp, "f-%03d.png")));
                foreach (string file in Directory.GetFiles(temp, "*.png"))
                {
                    using Image<Rgba32> image = Image.Load<Rgba32>(file);
                    if (predicate(image))
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                try
                {
                    Directory.Delete(temp, recursive: true);
                }
                catch (IOException)
                {
                }
            }
        }

        internal static bool EveryPixel(Image<Rgba32> image, Func<Pixel, bool> predicate)
        {
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 c = image[x, y];
                    if (!predicate(new Pixel(c.R, c.G, c.B, c.A)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        internal static void ExpectAll(Image<Rgba32> image, Func<Pixel, bool> predicate)
        {
            if (EveryPixel(image, predicate))
            {
                return;
            }

            throw new InvalidOperationException("Expected all pixels to satisfy " + predicate.Method.Name);
        }

        private static string Quote(string path) => "\"" + path.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

        private static string Run(string fileName, string arguments)
        {
            using Process process = Start(fileName, arguments, stdout: true);
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0 && string.IsNullOrEmpty(stdout))
            {
                throw new InvalidOperationException(fileName + " failed: " + stderr);
            }

            return stdout;
        }

        private static byte[] RunBytes(string fileName, string arguments)
        {
            using Process process = Start(fileName, arguments, stdout: true);
            using MemoryStream buffer = new MemoryStream();
            process.StandardOutput.BaseStream.CopyTo(buffer);
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0 && buffer.Length == 0)
            {
                throw new InvalidOperationException(fileName + " failed: " + stderr);
            }

            return buffer.ToArray();
        }

        private static Process Start(string fileName, string arguments, bool stdout)
        {
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = stdout,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            if (!process.Start())
            {
                process.Dispose();
                throw new InvalidOperationException("Failed to start " + fileName);
            }

            return process;
        }
    }
}
