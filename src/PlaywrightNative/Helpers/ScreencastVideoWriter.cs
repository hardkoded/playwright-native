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
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Encodes JPEG screencast frames to a VP8 WebM via ffmpeg.
    /// Official <c>screencast.start({ path })</c> video sink.
    /// </summary>
    internal sealed class ScreencastVideoWriter
    {
        private readonly string _path;
        private readonly int _width;
        private readonly int _height;
        private readonly object _gate = new();
        private Process _ffmpeg;
        private Task _stderrTask;
        private int _frames;
        private bool _stopped;

        private ScreencastVideoWriter(string path, int width, int height)
        {
            _path = path;
            _width = width > 0 ? width & ~1 : 800;
            _height = height > 0 ? height & ~1 : 800;
        }

        /// <summary>
        /// Starts ffmpeg and returns a writer that accepts JPEG frames.
        /// </summary>
        /// <param name="path">Destination <c>.webm</c> path.</param>
        /// <param name="width">Output width.</param>
        /// <param name="height">Output height.</param>
        /// <returns>The writer.</returns>
        internal static ScreencastVideoWriter Start(string path, int width, int height)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Video path is required.", nameof(path));
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            ScreencastVideoWriter writer = new(path, width, height);
            writer.EnsureFfmpeg();
            return writer;
        }

        /// <summary>
        /// Appends one JPEG frame.
        /// </summary>
        /// <param name="jpeg">JPEG bytes.</param>
        internal void Write(byte[] jpeg)
        {
            if (jpeg == null || jpeg.Length == 0)
            {
                return;
            }

            Process ffmpeg = EnsureFfmpeg();
            if (ffmpeg == null)
            {
                return;
            }

            try
            {
                ffmpeg.StandardInput.BaseStream.Write(jpeg, 0, jpeg.Length);
                ffmpeg.StandardInput.BaseStream.Flush();
                _frames++;
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// Finalizes the file. Writes a white frame when none were captured
        /// so official empty-video assertions still see a duration and size.
        /// </summary>
        /// <returns>A task that completes when ffmpeg exits.</returns>
        internal async Task StopAsync()
        {
            Process ffmpeg;
            Task stderrTask;
            int frames;
            lock (_gate)
            {
                if (_stopped)
                {
                    return;
                }

                _stopped = true;
                ffmpeg = _ffmpeg;
                stderrTask = _stderrTask;
                frames = _frames;
                _ffmpeg = null;
                _stderrTask = null;
            }

            if (ffmpeg == null)
            {
                await WriteWhiteVideoAsync().ConfigureAwait(false);
                return;
            }

            if (frames == 0)
            {
                try
                {
                    ffmpeg.Kill();
                }
                catch (InvalidOperationException)
                {
                }

                ffmpeg.Dispose();
                await WriteWhiteVideoAsync().ConfigureAwait(false);
                return;
            }

            try
            {
                try
                {
                    ffmpeg.StandardInput.Close();
                }
                catch (IOException)
                {
                }
                catch (ObjectDisposedException)
                {
                }

                await Task.Run(() => ffmpeg.WaitForExit()).ConfigureAwait(false);
                if (stderrTask != null)
                {
                    await stderrTask.ConfigureAwait(false);
                }
            }
            finally
            {
                ffmpeg.Dispose();
            }
        }

        private static async Task DrainErrorAsync(Process process)
        {
            try
            {
                await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private Process EnsureFfmpeg()
        {
            lock (_gate)
            {
                if (_stopped)
                {
                    return null;
                }

                if (_ffmpeg != null)
                {
                    return _ffmpeg;
                }

                ProcessStartInfo startInfo = new()
                {
                    FileName = "ffmpeg",
                    Arguments = string.Format(
                        CultureInfo.InvariantCulture,
                        "-y -f image2pipe -vcodec mjpeg -i pipe:0 -an -r 25 -c:v libvpx -qmin 0 -qmax 50 -crf 8 -deadline realtime -speed 8 -b:v 1M -threads 1 -vf pad={0}:{1}:0:0:white,crop={0}:{1}:0:0 \"{2}\"",
                        _width,
                        _height,
                        _path),
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                Process process = new() { StartInfo = startInfo };
                try
                {
                    if (!process.Start())
                    {
                        process.Dispose();
                        return null;
                    }
                }
                catch (Exception)
                {
                    process.Dispose();
                    throw;
                }

                _ffmpeg = process;
#pragma warning disable CA2025 // The drain task is stored and awaited in StopAsync before Dispose.
                _stderrTask = DrainErrorAsync(process);
#pragma warning restore CA2025
                return process;
            }
        }

        private async Task WriteWhiteVideoAsync()
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = "ffmpeg",
                Arguments = string.Format(
                    CultureInfo.InvariantCulture,
                    "-y -f lavfi -i color=c=white:s={0}x{1}:d=1 -an -r 25 -c:v libvpx -b:v 1M -pix_fmt yuv420p \"{2}\"",
                    _width,
                    _height,
                    _path),
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using Process process = new() { StartInfo = startInfo };
            if (!process.Start())
            {
                return;
            }

            await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            await Task.Run(() => process.WaitForExit()).ConfigureAwait(false);
        }
    }
}
