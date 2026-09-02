/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/screencast.spec.ts</c> parity. Do not edit leftover
    /// <c>PageScreencast*.cs</c> classes.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryScreencastParityTests : PageTestEx
    {
        private const string TargetClosed = "Target page, context or browser has been closed";

        private static string EmptyPage => TestConstants.EmptyPage;

        [PlaywrightTest("screencast.spec.ts", "screencast.start delivers frames via onFrame callback")]
        [Test]
        [Timeout(60_000)]
        public async Task ScreencastStartDeliversFramesViaOnFrameCallback()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 1000, Height = 400 } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<byte[]> frames = new();
            await page.Screencast.StartAsync(new()
            {
                OnFrame = frame =>
                {
                    frames.Add(frame.Data);
                    return Task.CompletedTask;
                },
                Size = new ScreencastSize { Width = 500, Height = 400 }
            }).ConfigureAwait(false);
            await GoEmptyAsync(page).ConfigureAwait(false);
            await page.EvaluateAsync("() => document.body.style.backgroundColor = 'red'").ConfigureAwait(false);
            await EnsureSomeFramesAsync(page).ConfigureAwait(false);
            await page.Screencast.StopAsync().ConfigureAwait(false);

            Assert.That(frames.Count, Is.GreaterThan(0));
            foreach (byte[] frame in frames)
            {
                Assert.That(frame[0], Is.EqualTo(0xFF));
                Assert.That(frame[1], Is.EqualTo(0xD8));
                JpegSize size = JpegDimensions(frame);
                Assert.That(size.Width, Is.EqualTo(500));
                Assert.That(size.Height, Is.EqualTo(200));
            }
        }

        [PlaywrightTest("screencast.spec.ts", "applies backpressure while async onFrame callback is pending")]
        [Test]
        [Timeout(120_000)]
        public async Task AppliesBackpressureWhileAsyncOnFrameCallbackIsPending()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 500, Height = 400 } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> firstFrame = new(TaskCreationOptions.RunContinuationsAsynchronously);
            int frameCount = 0;
            long lastFrameTimestamp = 0;
            await page.Screencast.StartAsync(async _ =>
            {
                Interlocked.Increment(ref frameCount);
                Interlocked.Exchange(ref lastFrameTimestamp, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                firstFrame.TrySetResult(true);
                await release.Task.ConfigureAwait(false);
            }).ConfigureAwait(false);
            await GoEmptyAsync(page).ConfigureAwait(false);
            await page.EvaluateAsync(
                "() => { const animate = () => { document.body.style.backgroundColor = document.body.style.backgroundColor === 'red' ? 'blue' : 'red'; requestAnimationFrame(animate); }; requestAnimationFrame(animate); }").ConfigureAwait(false);
            await firstFrame.Task.ConfigureAwait(false);
            await PollAsync(
                () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - Interlocked.Read(ref lastFrameTimestamp) > 1000,
                30_000).ConfigureAwait(false);

            int framesWhileBlocked = Volatile.Read(ref frameCount);
            await EnsureSomeFramesAsync(page).ConfigureAwait(false);
            Assert.That(Volatile.Read(ref frameCount), Is.EqualTo(framesWhileBlocked));

            release.TrySetResult(true);
            await PollAsync(
                async () =>
                {
                    await page.EvaluateAsync(
                        "() => { document.body.style.backgroundColor = document.body.style.backgroundColor === 'red' ? 'blue' : 'red'; }").ConfigureAwait(false);
                    await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                    return Volatile.Read(ref frameCount) > framesWhileBlocked;
                },
                30_000).ConfigureAwait(false);

            await page.Screencast.StopAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("screencast.spec.ts", "onFrame receives viewport size")]
        [Test]
        [Timeout(60_000)]
        public async Task OnFrameReceivesViewportSize()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 1000, Height = 400 } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<(float Timestamp, int ViewportWidth, int ViewportHeight)> frames = new();
            await page.Screencast.StartAsync(new()
            {
                OnFrame = frame =>
                {
                    frames.Add((frame.Timestamp, frame.ViewportWidth, frame.ViewportHeight));
                    return Task.CompletedTask;
                },
                Size = new ScreencastSize { Width = 500, Height = 400 }
            }).ConfigureAwait(false);
            await GoEmptyAsync(page).ConfigureAwait(false);
            await EnsureSomeFramesAsync(page).ConfigureAwait(false);
            await page.Screencast.StopAsync().ConfigureAwait(false);

            Assert.That(frames.Count, Is.GreaterThan(0));
            foreach ((float timestamp, int viewportWidth, int viewportHeight) in frames)
            {
                Assert.That(viewportWidth, Is.EqualTo(1000));
                Assert.That(viewportHeight, Is.EqualTo(400));
                Assert.That(timestamp, Is.GreaterThanOrEqualTo(0));
            }
        }

        [PlaywrightTest("screencast.spec.ts", "start throws if screencast is already started")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task StartThrowsIfScreencastIsAlreadyStarted()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 500, Height = 400 } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.Screencast.StartAsync(_ => Task.CompletedTask).ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Screencast.StartAsync(_ => Task.CompletedTask));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Screencast is already started"));

            await page.Screencast.StopAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("screencast.spec.ts", "start allows restart with different options after stop")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task StartAllowsRestartWithDifferentOptionsAfterStop()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 500, Height = 400 } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.Screencast.StartAsync(_ => Task.CompletedTask, width: 500, height: 400).ConfigureAwait(false);
            await page.Screencast.StopAsync().ConfigureAwait(false);
            await page.Screencast.StartAsync(_ => Task.CompletedTask, width: 320, height: 240).ConfigureAwait(false);
            await page.Screencast.StopAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("screencast.spec.ts", "start returns a disposable that stops screencast")]
        [Test]
        [Timeout(60_000)]
        public async Task StartReturnsADisposableThatStopsScreencast()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 500, Height = 400 } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<byte[]> frames = new();
            await page.Screencast.StartAsync(new()
            {
                OnFrame = frame =>
                {
                    frames.Add(frame.Data);
                    return Task.CompletedTask;
                },
                Size = new ScreencastSize { Width = 500, Height = 400 }
            }).ConfigureAwait(false);
            await GoEmptyAsync(page).ConfigureAwait(false);
            await page.EvaluateAsync("() => document.body.style.backgroundColor = 'red'").ConfigureAwait(false);
            await EnsureSomeFramesAsync(page).ConfigureAwait(false);
            await page.Screencast.StopAsync().ConfigureAwait(false);

            int frameCountAfterDispose = frames.Count;
            Assert.That(frameCountAfterDispose, Is.GreaterThan(0));

            await page.EvaluateAsync("() => document.body.style.backgroundColor = 'blue'").ConfigureAwait(false);
            await EnsureSomeFramesAsync(page).ConfigureAwait(false);
            Assert.That(frames.Count, Is.EqualTo(frameCountAfterDispose));
        }

        [PlaywrightTest("screencast.spec.ts", "start/stop twice without path creates two files in artifactsDir")]
        [Test]
        [Timeout(60_000)]
        public async Task StartStopTwiceWithoutPathCreatesTwoFilesInArtifactsDir()
        {
            string artifactsDir = OutputPath("artifacts");
            string video1 = OutputPath("video1.webm");
            string video2 = OutputPath("video2.webm");
            Directory.CreateDirectory(artifactsDir);
            try
            {
                ViewportSize size = new() { Width = 800, Height = 800 };
                await using IBrowser browser = await BrowserLauncher.LaunchAsync(
                    new BrowserTypeLaunchOptions { ArtifactsDir = artifactsDir }).ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = size }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);

                await page.Screencast.StartAsync(new() { Path = video1, Size = new ScreencastSize { Width = size.Width, Height = size.Height } }).ConfigureAwait(false);
                await page.EvaluateAsync("() => document.body.style.backgroundColor = 'red'").ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await page.Screencast.StopAsync().ConfigureAwait(false);

                await page.Screencast.StartAsync(new() { Path = video2, Size = new ScreencastSize { Width = size.Width, Height = size.Height } }).ConfigureAwait(false);
                await page.EvaluateAsync("() => document.body.style.backgroundColor = 'blue'").ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await page.Screencast.StopAsync().ConfigureAwait(false);

                string[] videoFiles = Directory.GetFiles(artifactsDir, "*.webm");
                Assert.That(videoFiles, Has.Length.EqualTo(2));
            }
            finally
            {
                TryDeleteDir(artifactsDir);
            }
        }

        [PlaywrightTest("screencast.spec.ts", "start should work when recordVideo is set")]
        [Test]
        [Timeout(60_000)]
        public async Task StartShouldWorkWhenRecordVideoIsSet()
        {
            string autoDir = OutputPath("auto");
            string manualDir = OutputPath("manual");
            Directory.CreateDirectory(autoDir);
            Directory.CreateDirectory(manualDir);
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = autoDir }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);

                await page.Screencast.StartAsync(new() { Path = Path.Combine(manualDir, "video.webm") }).ConfigureAwait(false);
                await page.EvaluateAsync("() => document.body.style.backgroundColor = 'blue'").ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await page.Screencast.StopAsync().ConfigureAwait(false);
                string[] videoFiles1 = Directory.GetFiles(manualDir, "*.webm");
                Assert.That(videoFiles1, Has.Length.EqualTo(1));

                await context.CloseAsync().ConfigureAwait(false);
                string[] videoFiles2 = Directory.GetFiles(autoDir, "*.webm");
                Assert.That(videoFiles2, Has.Length.EqualTo(1));
            }
            finally
            {
                TryDeleteDir(autoDir);
                TryDeleteDir(manualDir);
            }
        }

        [PlaywrightTest("screencast.spec.ts", "start should fail when another recording is in progress")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task StartShouldFailWhenAnotherRecordingIsInProgress()
        {
            string video1 = OutputPath("video.webm");
            string video2 = OutputPath("video2.webm");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.Screencast.StartAsync(new() { Path = video1 }).ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Screencast.StartAsync(new() { Path = video2 }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Screencast is already started"));
            await page.Screencast.StopAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("screencast.spec.ts", "stop should not fail when no recording is in progress")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task StopShouldNotFailWhenNoRecordingIsInProgress()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.Screencast.StopAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("screencast.spec.ts", "start should finish when page is closed")]
        [Test]
        [Timeout(60_000)]
        public async Task StartShouldFinishWhenPageIsClosed()
        {
            string videoPath = OutputPath("video.webm");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.Screencast.StartAsync(new() { Path = videoPath, Size = new ScreencastSize { Width = 800, Height = 800 } }).ConfigureAwait(false);
            await page.EvaluateAsync("() => document.body.style.backgroundColor = 'red'").ConfigureAwait(false);
            await EnsureSomeFramesAsync(page).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
            TargetClosedException error = Assert.CatchAsync<TargetClosedException>(
                () => page.Screencast.StopAsync());
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain(TargetClosed));
        }

        [PlaywrightTest("screencast.spec.ts", "empty video")]
        [Test]
        [Timeout(60_000)]
        public async Task EmptyVideo()
        {
            ViewportSize size = new() { Width = 800, Height = 800 };
            string videoPath = OutputPath("empty-video.webm");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = size }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.Screencast.StartAsync(new() { Path = videoPath, Size = new ScreencastSize { Width = size.Width, Height = size.Height } }).ConfigureAwait(false);
            await page.Screencast.StopAsync().ConfigureAwait(false);
            ExpectFrames(videoPath, size, IsAlmostWhite);
        }

        [PlaywrightTest("screencast.spec.ts", "start dispose stops recording")]
        [Test]
        [Timeout(60_000)]
        public async Task StartDisposeStopsRecording()
        {
            ViewportSize size = new() { Width = 800, Height = 800 };
            string videoPath = OutputPath("dispose-video.webm");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = size }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IAsyncDisposable disposable = await page.Screencast.StartAsync(new() { Path = videoPath, Size = new ScreencastSize { Width = size.Width, Height = size.Height } }).ConfigureAwait(false);
            await page.EvaluateAsync("() => document.body.style.backgroundColor = 'red'").ConfigureAwait(false);
            await EnsureSomeFramesAsync(page).ConfigureAwait(false);
            await disposable.DisposeAsync().ConfigureAwait(false);
            ExpectRedFrames(videoPath, size);
        }

        private static async Task GoEmptyAsync(IPage page)
        {
            if (TestServerSetup.Server != null)
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                return;
            }

            await page.SetContentAsync("<html><body></body></html>").ConfigureAwait(false);
        }

        private static async Task EnsureSomeFramesAsync(IPage page)
        {
            for (int i = 0; i < 100; i++)
            {
                await page.EvaluateAsync(
                    "() => new Promise(f => requestAnimationFrame(() => requestAnimationFrame(f)))").ConfigureAwait(false);
            }

            await page.ScreenshotAsync().ConfigureAwait(false);
        }

        private static async Task PollAsync(Func<bool> predicate, int timeoutMs)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (predicate())
                {
                    return;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.Fail("Timed out waiting for condition.");
        }

        private static async Task PollAsync(Func<Task<bool>> predicate, int timeoutMs)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (await predicate().ConfigureAwait(false))
                {
                    return;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.Fail("Timed out waiting for condition.");
        }

        private static string OutputPath(string name)
            => Path.Combine(Path.GetTempPath(), "pwsharp-screencast-" + Guid.NewGuid().ToString("N") + "-" + name);

        private static void TryDeleteDir(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void ExpectRedFrames(string videoFile, ViewportSize size)
            => ExpectFrames(videoFile, size, IsAlmostRed);

        private static void ExpectFrames(string videoFile, ViewportSize size, Func<Pixel, bool> pixelPredicate)
        {
            VideoInfo info = ProbeVideo(videoFile);
            Assert.That(info.DurationMs, Is.GreaterThan(0));
            Assert.That(info.Width, Is.EqualTo(size.Width));
            Assert.That(info.Height, Is.EqualTo(size.Height));

            byte[] last = ReadLastFrameRgba(videoFile, info.Width, info.Height);
            ExpectAll(last, info.Width, info.Height, 10, 10, pixelPredicate);
            ExpectAll(last, info.Width, info.Height, size.Width - 20, 10, pixelPredicate);
        }

        private static void ExpectAll(byte[] pixels, int width, int height, int x, int y, Func<Pixel, bool> pixelPredicate)
        {
            for (int row = 0; row < 10; row++)
            {
                for (int col = 0; col < 10; col++)
                {
                    int px = x + col;
                    int py = y + row;
                    if (px < 0 || py < 0 || px >= width || py >= height)
                    {
                        continue;
                    }

                    int index = ((py * width) + px) * 4;
                    Pixel pixel = new()
                    {
                        R = pixels[index],
                        G = pixels[index + 1],
                        B = pixels[index + 2],
                        Alpha = pixels[index + 3],
                    };
                    if (!pixelPredicate(pixel))
                    {
                        string rgba = string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}, {1}, {2}, {3}",
                            pixel.R,
                            pixel.G,
                            pixel.B,
                            pixel.Alpha);
                        Assert.Fail("Expected all pixels to satisfy predicate, found bad pixel (" + rgba + ")");
                    }
                }
            }
        }

        private static bool IsAlmostWhite(Pixel pixel)
            => pixel.R > 185 && pixel.G > 185 && pixel.B > 185 && pixel.Alpha == 255;

        private static bool IsAlmostRed(Pixel pixel)
            => pixel.R > 185 && pixel.G < 70 && pixel.B < 70 && pixel.Alpha == 255;

        private static VideoInfo ProbeVideo(string videoFile)
        {
            Assert.That(File.Exists(videoFile), Is.True, "video file missing: " + videoFile);
            Assert.That(new FileInfo(videoFile).Length, Is.GreaterThan(0), "video file empty: " + videoFile);
            ProcessStartInfo startInfo = new()
            {
                FileName = "ffprobe",
                Arguments = "-v error -select_streams v:0 -show_entries stream=width,height -show_entries format=duration -of default=nw=1:nk=1 " + Quote(videoFile),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using Process process = new() { StartInfo = startInfo };
            Assert.That(process.Start(), Is.True);
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.That(lines.Length, Is.GreaterThanOrEqualTo(3), "ffprobe output: " + output);
            return new VideoInfo
            {
                Width = int.Parse(lines[0], CultureInfo.InvariantCulture),
                Height = int.Parse(lines[1], CultureInfo.InvariantCulture),
                DurationMs = (int)(double.Parse(lines[2], CultureInfo.InvariantCulture) * 1000),
            };
        }

        private static byte[] ReadLastFrameRgba(string videoFile, int width, int height)
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = "ffmpeg",
                Arguments = "-sseof -0.1 -i " + Quote(videoFile) + " -frames:v 1 -f rawvideo -pix_fmt rgba pipe:1",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using Process process = new() { StartInfo = startInfo };
            Assert.That(process.Start(), Is.True);
            using MemoryStream buffer = new();
            process.StandardOutput.BaseStream.CopyTo(buffer);
            process.WaitForExit();
            byte[] pixels = buffer.ToArray();
            int expected = width * height * 4;
            Assert.That(pixels.Length, Is.EqualTo(expected), "raw frame size");
            return pixels;
        }

        private static string Quote(string path)
            => "\"" + path + "\"";

        private static JpegSize JpegDimensions(byte[] buffer)
        {
            int i = 2;
            while (i < buffer.Length - 8)
            {
                if (buffer[i] != 0xFF)
                {
                    break;
                }

                byte marker = buffer[i + 1];
                int segmentLength = (buffer[i + 2] << 8) | buffer[i + 3];
                if ((marker >= 0xC0 && marker <= 0xC3) ||
                    (marker >= 0xC5 && marker <= 0xC7) ||
                    (marker >= 0xC9 && marker <= 0xCB) ||
                    (marker >= 0xCD && marker <= 0xCF))
                {
                    int height = (buffer[i + 5] << 8) | buffer[i + 6];
                    int width = (buffer[i + 7] << 8) | buffer[i + 8];
                    return new JpegSize { Width = width, Height = height };
                }

                i += 2 + segmentLength;
            }

            throw new InvalidOperationException("Could not parse JPEG dimensions");
        }

        private struct JpegSize
        {
            internal int Width;
            internal int Height;
        }

        private struct VideoInfo
        {
            internal int Width;
            internal int Height;
            internal int DurationMs;
        }

        private struct Pixel
        {
            internal int R;
            internal int G;
            internal int B;
            internal int Alpha;
        }
    }
}
