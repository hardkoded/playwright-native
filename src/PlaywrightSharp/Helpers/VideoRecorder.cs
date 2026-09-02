/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using PlaywrightSharp.Chromium;
using PlaywrightSharp.WebKit;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Records each page in a context to a WebM via Chromium
    /// <c>Page.startScreencast</c> or WebKit <c>Screencast.startScreencast</c>
    /// and ffmpeg.
    /// </summary>
    internal static class VideoRecorder
    {
        private static readonly ConditionalWeakTable<IBrowserContext, Session> Sessions = new();
        private static readonly ConditionalWeakTable<IPage, IVideo> Videos = new();

        /// <summary>
        /// Starts recording when <paramref name="recordVideoDir"/> is set.
        /// </summary>
        /// <param name="context">The context to observe.</param>
        /// <param name="recordVideoDir">Destination directory, or <see langword="null"/>.</param>
        /// <param name="recordVideoSize">Optional frame size.</param>
        /// <param name="viewport">Context viewport used for the official default size.</param>
        internal static void Start(IBrowserContext context, string recordVideoDir, RecordVideoSize recordVideoSize, ViewportSize viewport = null)
        {
            if (context == null || string.IsNullOrEmpty(recordVideoDir))
            {
                return;
            }

            if (Sessions.TryGetValue(context, out Session existing))
            {
                existing.Detach();
                Sessions.Remove(context);
            }

            Directory.CreateDirectory(recordVideoDir);
            Sessions.Add(context, new Session(context, recordVideoDir, VideoSize.Resolve(recordVideoSize, viewport)));
        }

        /// <summary>
        /// Stops every page recording for <paramref name="context"/>.
        /// Safe to call more than once.
        /// </summary>
        /// <param name="context">The context that is closing.</param>
        /// <returns>A task that completes when files have been finalized.</returns>
        internal static async Task FlushAsync(IBrowserContext context)
        {
            if (context == null || !Sessions.TryGetValue(context, out Session session))
            {
                return;
            }

            Sessions.Remove(context);
            await session.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Returns the video for <paramref name="page"/> when the owning context is recording.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <returns>The video, or <see langword="null"/>.</returns>
        internal static IVideo GetVideo(IPage page)
        {
            if (page != null && Videos.TryGetValue(page, out IVideo video))
            {
                return video;
            }

            return null;
        }

        /// <summary>
        /// Last JPEG captured for <paramref name="page"/>, when video is recording.
        /// Used by tracing screenshots so video+trace sees the same pixels.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="jpeg">The last frame, if any.</param>
        /// <returns><see langword="true"/> when a frame is available.</returns>
        internal static bool TryGetLastFrame(IPage page, out byte[] jpeg)
        {
            jpeg = null;
            if (page == null || !Videos.TryGetValue(page, out IVideo video))
            {
                return false;
            }

            if (video is PageVideo pageVideo)
            {
                jpeg = pageVideo.LastJpeg;
            }

            return jpeg != null && jpeg.Length > 0;
        }

        private static void LogError(Exception ex)
        {
            System.Console.Error.WriteLine($"[VideoRecorder] {ex}");
        }

        private sealed class Session
        {
            private readonly IBrowserContext _context;
            private readonly string _directory;
            private readonly RecordVideoSize _size;
            private readonly ConcurrentDictionary<IPage, PageRecording> _recordings = new();
            private bool _detached;

            internal Session(IBrowserContext context, string directory, RecordVideoSize size)
            {
                _context = context;
                _directory = directory;
                _size = size;
                _context.Page += OnPage;
                foreach (IPage page in _context.Pages)
                {
                    Attach(page);
                }
            }

            internal IVideo GetVideo(IPage page)
            {
                return _recordings.TryGetValue(page, out PageRecording recording)
                    ? recording.Video
                    : null;
            }

            internal void Detach()
            {
                if (_detached)
                {
                    return;
                }

                _detached = true;
                _context.Page -= OnPage;
            }

            internal async Task FlushAsync()
            {
                Detach();
                foreach (PageRecording recording in _recordings.Values)
                {
                    await recording.StopAsync().ConfigureAwait(false);
                }

                _recordings.Clear();
            }

            private void OnPage(object sender, IPage page) => Attach(page);

            private void Attach(IPage page)
            {
                if (page == null || _recordings.ContainsKey(page))
                {
                    return;
                }

                string path = Path.Combine(Path.GetFullPath(_directory), Guid.NewGuid().ToString("N") + ".webm");
                PageRecording recording = new(page, path, _size);
                if (!_recordings.TryAdd(page, recording))
                {
                    return;
                }

                Videos.Add(page, recording.Video);

                page.Close += (_, _) =>
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await recording.StopAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            LogError(ex);
                        }
                    });
                };

                recording.Start();
            }
        }

        private sealed class PageVideo : IVideo
        {
            private readonly IBrowser _browser;
            private readonly TaskCompletionSource<bool> _completed =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            private volatile bool _browserClosed;

            internal PageVideo(IPage page, string path)
            {
                Path = path;
                _browser = page?.Context?.Browser;
                if (_browser == null)
                {
                    _browserClosed = true;
                    return;
                }

                if (!_browser.IsConnected)
                {
                    _browserClosed = true;
                    return;
                }

                _browser.Disconnected += (_, _) => _browserClosed = true;
            }

            internal string Path { get; }

            internal byte[] LastJpeg { get; set; }

            public Task<string> GetPathAsync() => Task.FromResult(Path);

            public Task<string> PathAsync() => GetPathAsync();

            public async Task SaveAsAsync(string path)
            {
                if (string.IsNullOrEmpty(path))
                {
                    throw new ArgumentException("A destination path is required.", nameof(path));
                }

                await _completed.Task.ConfigureAwait(false);
                if (_browserClosed || _browser == null || !_browser.IsConnected)
                {
                    throw new PlaywrightSharpException("browser has been closed");
                }

                string directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Copy(Path, path, overwrite: true);
            }

            public async Task DeleteAsync()
            {
                await _completed.Task.ConfigureAwait(false);
                if (File.Exists(Path))
                {
                    File.Delete(Path);
                }
            }

            internal void Complete() => _completed.TrySetResult(true);

            internal void Fail(Exception exception)
            {
                if (exception == null)
                {
                    _completed.TrySetResult(true);
                    return;
                }

                _completed.TrySetException(exception);
            }
        }

        private sealed class PageRecording
        {
            private readonly IPage _page;
            private readonly RecordVideoSize _size;
            private readonly object _gate = new();
            private readonly PageVideo _video;
            private readonly ScreencastVideoWriter _writer;
            private CRSession _crSession;
            private WKSession _wkSession;
            private int _wkGeneration;
            private Task _startTask = Task.CompletedTask;
            private Task _stopTask;

            internal PageRecording(IPage page, string path, RecordVideoSize size)
            {
                _page = page;
                _size = size;
                _video = new PageVideo(page, path);
                _writer = ScreencastVideoWriter.Start(path, size.Width, size.Height);
            }

            internal IVideo Video => _video;

            internal void Start()
            {
                _startTask = StartCoreAsync();
            }

            internal Task StopAsync()
            {
                lock (_gate)
                {
                    if (_stopTask != null)
                    {
                        return _stopTask;
                    }

                    _stopTask = StopCoreAsync();
                    return _stopTask;
                }
            }

            private async Task StartCoreAsync()
            {
                int width = _size.Width;
                int height = _size.Height;
                if (_page is Page instance)
                {
                    _crSession = instance.CrPage.Session;
                    _crSession.MessageReceived += OnCrMessage;
                    try
                    {
                        await _crSession.SendAsync("Page.startScreencast", new
                        {
                            format = "jpeg",
                            quality = 80,
                            maxWidth = width,
                            maxHeight = height,
                            everyNthFrame = 1,
                        })
                        .WithTimeout(TimeSpan.FromSeconds(2), _ => new TimeoutException())
                        .ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                    }
                    catch (Exception ex)
                    {
                        LogError(ex);
                    }

                    return;
                }

                if (_page is WKPage wkPage)
                {
                    _wkSession = wkPage.Session;
                    _wkSession.MessageReceived += OnWkMessage;
                    try
                    {
                        JsonElement? result = await _wkSession.SendAsync("Screencast.startScreencast", new
                        {
                            quality = 80,
                            width,
                            height,
                            toolbarHeight = 0,
                        })
                        .WithTimeout(TimeSpan.FromSeconds(2), _ => new TimeoutException())
                        .ConfigureAwait(false);
                        if (result.HasValue &&
                            result.Value.TryGetProperty("generation", out JsonElement generation) &&
                            generation.TryGetInt32(out int value))
                        {
                            _wkGeneration = value;
                        }
                    }
                    catch (TimeoutException)
                    {
                    }
                    catch (Exception ex)
                    {
                        LogError(ex);
                    }

                    return;
                }

                _video.Complete();
            }

            private async Task StopCoreAsync()
            {
                try
                {
                    await _startTask
                        .WithTimeout(TimeSpan.FromSeconds(2), _ => new TimeoutException())
                        .ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                }
                catch (Exception ex)
                {
                    LogError(ex);
                }

                if (_crSession != null)
                {
                    _crSession.MessageReceived -= OnCrMessage;
                    try
                    {
                        await _crSession.SendAsync("Page.stopScreencast")
                            .WithTimeout(TimeSpan.FromSeconds(2), _ => new TimeoutException())
                            .ConfigureAwait(false);
                    }
                    catch (TargetClosedException)
                    {
                    }
                    catch (TimeoutException)
                    {
                    }
                    catch (PlaywrightSharpException)
                    {
                    }
                }

                if (_wkSession != null)
                {
                    _wkSession.MessageReceived -= OnWkMessage;
                    try
                    {
                        await _wkSession.SendAsync("Screencast.stopScreencast")
                            .WithTimeout(TimeSpan.FromSeconds(2), _ => new TimeoutException())
                            .ConfigureAwait(false);
                    }
                    catch (TargetClosedException)
                    {
                    }
                    catch (TimeoutException)
                    {
                    }
                    catch (PlaywrightSharpException)
                    {
                    }
                }

                try
                {
                    await _writer.StopAsync().ConfigureAwait(false);
                    _video.Complete();
                }
                catch (Exception ex)
                {
                    _video.Fail(ex);
                }
            }

            private void OnCrMessage(string method, JsonElement? parameters)
            {
                if (method != "Page.screencastFrame" || !parameters.HasValue)
                {
                    return;
                }

                JsonElement payload = parameters.Value;
                string data = payload.TryGetProperty("data", out JsonElement dataElement)
                    ? dataElement.GetString()
                    : null;
                int sessionId = payload.TryGetProperty("sessionId", out JsonElement idElement)
                    && idElement.TryGetInt32(out int value)
                    ? value
                    : 0;

                if (!string.IsNullOrEmpty(data))
                {
                    try
                    {
                        byte[] jpeg = Convert.FromBase64String(data);
                        WriteFrame(jpeg);
                    }
                    catch (FormatException)
                    {
                    }
                }

                CRSession session = _crSession;
                if (session == null)
                {
                    return;
                }

                _ = session.SendAsync("Page.screencastFrameAck", new { sessionId });
            }

            private void OnWkMessage(string method, JsonElement? parameters)
            {
                if (method != "Screencast.screencastFrame" || !parameters.HasValue)
                {
                    return;
                }

                JsonElement payload = parameters.Value;
                string data = payload.TryGetProperty("data", out JsonElement dataElement)
                    ? dataElement.GetString()
                    : null;
                if (!string.IsNullOrEmpty(data))
                {
                    try
                    {
                        byte[] jpeg = Convert.FromBase64String(data);
                        WriteFrame(jpeg);
                    }
                    catch (FormatException)
                    {
                    }
                }

                WKSession session = _wkSession;
                if (session == null)
                {
                    return;
                }

                _ = session.SendAsync("Screencast.screencastFrameAck", new { generation = _wkGeneration });
            }

            private void WriteFrame(byte[] jpeg)
            {
                if (jpeg == null || jpeg.Length == 0)
                {
                    return;
                }

                _video.LastJpeg = jpeg;
                _writer.Write(jpeg);
            }
        }
    }
}
