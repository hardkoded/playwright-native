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
        // 16x16 white JPEG; ffmpeg pad/crop expands to the recording size.
        private static readonly byte[] WhiteJpegFrame =
        {
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x02, 0x00, 0x00, 0x01,
            0x00, 0x01, 0x00, 0x00, 0xFF, 0xFE, 0x00, 0x10, 0x4C, 0x61, 0x76, 0x63, 0x36, 0x30, 0x2E, 0x33,
            0x31, 0x2E, 0x31, 0x30, 0x32, 0x00, 0xFF, 0xDB, 0x00, 0x43, 0x00, 0x08, 0x04, 0x04, 0x04, 0x04,
            0x04, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06,
            0x06, 0x06, 0x06, 0x06, 0x07, 0x07, 0x07, 0x08, 0x08, 0x08, 0x07, 0x07, 0x07, 0x06, 0x06, 0x07,
            0x07, 0x08, 0x08, 0x08, 0x08, 0x09, 0x09, 0x09, 0x08, 0x08, 0x08, 0x08, 0x09, 0x09, 0x0A, 0x0A,
            0x0A, 0x0C, 0x0C, 0x0B, 0x0B, 0x0E, 0x0E, 0x0E, 0x11, 0x11, 0x14, 0xFF, 0xC4, 0x00, 0x4B, 0x00,
            0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x07, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x10, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x11, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xC0, 0x00, 0x11, 0x08, 0x00, 0x10, 0x00,
            0x10, 0x03, 0x01, 0x22, 0x00, 0x02, 0x11, 0x00, 0x03, 0x11, 0x00, 0xFF, 0xDA, 0x00, 0x0C, 0x03,
            0x01, 0x00, 0x02, 0x11, 0x03, 0x11, 0x00, 0x3F, 0x00, 0xBF, 0x80, 0x0F, 0xFF, 0xD9,
        };

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

            // Do not start ffmpeg here. Attach must always register IVideo;
            // a missing/broken ffmpeg must not leave page.Video null.
            return new ScreencastVideoWriter(path, width, height);
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

                // Bound the wait: a misbehaving ffmpeg build that hangs instead of
                // exiting once stalled an entire CI shard for the rest of its budget.
                if (!await Task.Run(() => ffmpeg.WaitForExit(15_000)).ConfigureAwait(false))
                {
                    try
                    {
                        ffmpeg.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }

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
                    FileName = FfmpegLocator.Resolve(),
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
                    return null;
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
            // Bundled screencast ffmpeg often lacks lavfi. Prefer the same
            // image2pipe path as live frames (pad/crop to size) so empty
            // recordings still leave a .webm (ShouldCloseFfmpegEvenIfThereWereNoFrames).
            if (await WriteWhiteVideoViaImagePipeAsync().ConfigureAwait(false))
            {
                return;
            }

            // Optional system-ffmpeg lavfi fallback when image2pipe is unavailable.
            string[] candidates =
            {
                FfmpegLocator.ResolveForWebp(),
                FfmpegLocator.Resolve(),
            };

            foreach (string ffmpeg in candidates)
            {
                if (string.IsNullOrEmpty(ffmpeg))
                {
                    continue;
                }

                ProcessStartInfo startInfo = new()
                {
                    FileName = ffmpeg,
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

                try
                {
                    using Process process = new() { StartInfo = startInfo };
                    if (!process.Start())
                    {
                        continue;
                    }

                    await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                    if (!await Task.Run(() => process.WaitForExit(15_000)).ConfigureAwait(false))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (InvalidOperationException)
                        {
                        }

                        continue;
                    }

                    if (process.ExitCode == 0 && File.Exists(_path) && new FileInfo(_path).Length > 0)
                    {
                        return;
                    }
                }
                catch (InvalidOperationException)
                {
                    // Try the next ffmpeg candidate.
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // ffmpeg missing or not executable on this candidate.
                }
                catch (IOException)
                {
                    // Try the next ffmpeg candidate.
                }
            }
        }

        private async Task<bool> WriteWhiteVideoViaImagePipeAsync()
        {
            string ffmpegPath = FfmpegLocator.Resolve();
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                return false;
            }

            ProcessStartInfo startInfo = new()
            {
                FileName = ffmpegPath,
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

            try
            {
                Process process = new() { StartInfo = startInfo };
                try
                {
                    if (!process.Start())
                    {
                        return false;
                    }

#pragma warning disable CA2025 // Drain completes before Dispose below.
                    Task drain = DrainErrorAsync(process);
#pragma warning restore CA2025
                    try
                    {
                        // ~1s at 25fps. Pad/crop expands the tiny white JPEG to size.
                        for (int i = 0; i < 25; i++)
                        {
                            await process.StandardInput.BaseStream.WriteAsync(WhiteJpegFrame).ConfigureAwait(false);
                        }

                        await process.StandardInput.BaseStream.FlushAsync().ConfigureAwait(false);
                        process.StandardInput.Close();
                    }
                    catch (IOException)
                    {
                    }
                    catch (ObjectDisposedException)
                    {
                    }

                    if (!await Task.Run(() => process.WaitForExit(15_000)).ConfigureAwait(false))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    }

                    await drain.ConfigureAwait(false);
                    return process.ExitCode == 0 && File.Exists(_path) && new FileInfo(_path).Length > 0;
                }
                finally
                {
                    process.Dispose();
                }
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }
    }
}
