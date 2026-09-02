/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// Official WebKit <c>Screencast.startScreencast</c> / <c>Screencast.stopScreencast</c>
    /// on the page-proxy session (upstream <c>wkPage.startScreencast</c>).
    /// </summary>
    internal sealed partial class WKScreencast : IScreencast
    {
        private readonly WKPage _page;
        private readonly object _gate = new();
        private Func<ScreencastFrame, Task> _onFrame;
        private ScreencastVideoWriter _video;
        private ScreencastVideoWriter _artifactsVideo;
        private bool _started;
        private bool _ownsProtocol;
        private int _generation;

        internal WKScreencast(WKPage page)
        {
            _page = page ?? throw new ArgumentNullException(nameof(page));
        }

        /// <inheritdoc/>
        public async Task<IAsyncDisposable> StartAsync(Func<ScreencastFrame, Task> onFrame = default, int quality = default, int width = default, int height = default, string path = default)
        {
            ThrowIfClosed();
            lock (_gate)
            {
                if (_started)
                {
                    throw new PlaywrightNativeException("Screencast is already started");
                }

                _started = true;
                _onFrame = onFrame;
            }

            int maxWidth = width > 0 ? width : 800;
            int maxHeight = height > 0 ? height : 800;
            if (!string.IsNullOrEmpty(path))
            {
                _video = ScreencastVideoWriter.Start(path, maxWidth, maxHeight);
            }

            _artifactsVideo = ScreencastArtifacts.TryStart(_page, maxWidth, maxHeight);

            _page.Session.MessageReceived += OnMessage;
            try
            {
                JsonElement? result = await _page.Session.SendAsync("Screencast.startScreencast", new
                {
                    quality = quality > 0 ? quality : 90,
                    width = maxWidth,
                    height = maxHeight,
                    toolbarHeight = 0,
                }).ConfigureAwait(false);
                if (result.HasValue &&
                    result.Value.TryGetProperty("generation", out JsonElement generation) &&
                    generation.TryGetInt32(out int value))
                {
                    _generation = value;
                }

                _ownsProtocol = true;
            }
            catch (PlaywrightNativeException ex) when (ex.Message != null && ex.Message.Contains("Already screencasting", StringComparison.OrdinalIgnoreCase))
            {
                // recordVideo already started the page-proxy screencast. Official
                // multiplexes clients; attach to the existing stream.
                _ownsProtocol = false;
            }
            catch
            {
                _page.Session.MessageReceived -= OnMessage;
                lock (_gate)
                {
                    _started = false;
                    _onFrame = null;
                    _ownsProtocol = false;
                }

                ScreencastVideoWriter video = _video;
                ScreencastVideoWriter artifacts = _artifactsVideo;
                _video = null;
                _artifactsVideo = null;
                if (video != null)
                {
                    await video.StopAsync().ConfigureAwait(false);
                }

                if (artifacts != null)
                {
                    await artifacts.StopAsync().ConfigureAwait(false);
                }

                throw;
            }

            return new StopOnDispose(this);
        }

        /// <inheritdoc/>
        public async Task StopAsync()
        {
            ThrowIfClosed();
            if (!TryMarkStopped(out Func<ScreencastFrame, Task> _, out ScreencastVideoWriter video))
            {
                return;
            }

            _page.Session.MessageReceived -= OnMessage;
            if (_ownsProtocol)
            {
                try
                {
                    await _page.Session.SendAsync("Screencast.stopScreencast").ConfigureAwait(false);
                }
                catch (TargetClosedException)
                {
                    throw;
                }
                catch (PlaywrightNativeException)
                {
                }
            }

            if (video != null)
            {
                await video.StopAsync().ConfigureAwait(false);
            }

            ScreencastVideoWriter artifacts;
            lock (_gate)
            {
                artifacts = _artifactsVideo;
                _artifactsVideo = null;
            }

            if (artifacts != null)
            {
                await artifacts.StopAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ShowOverlayAsync(string html, float? duration = default)
            => ScreencastOverlay.ShowAsync(_page, html, duration);

        /// <inheritdoc/>
        public Task ShowChapterAsync(string title, string description = default, float? duration = default)
            => ScreencastOverlay.ShowChapterAsync(_page, title, description, duration);

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ShowActionsAsync(float? duration = default, AnnotatePosition position = default, int fontSize = default, ScreencastCursor cursor = default)
        {
            ScreencastActions.Show(_page, duration, position, fontSize, cursor);
            return Task.FromResult<IAsyncDisposable>(new HideOnDispose(this));
        }

        /// <inheritdoc/>
        public Task HideActionsAsync() => ScreencastActions.HideAsync(_page);

        /// <inheritdoc/>
        public Task ShowOverlaysAsync() => ScreencastOverlay.SetVisibleAsync(_page, visible: true);

        /// <inheritdoc/>
        public Task HideOverlaysAsync() => ScreencastOverlay.SetVisibleAsync(_page, visible: false);

        private void ThrowIfClosed()
        {
            if (_page.IsClosed)
            {
                throw new TargetClosedException(DriverMessages.BrowserOrContextClosedExceptionMessage);
            }
        }

        private bool TryMarkStopped(out Func<ScreencastFrame, Task> onFrame, out ScreencastVideoWriter video)
        {
            lock (_gate)
            {
                onFrame = _onFrame;
                video = _video;
                if (!_started)
                {
                    return false;
                }

                _started = false;
                _onFrame = null;
                _video = null;
                return true;
            }
        }

        private void OnMessage(string method, JsonElement? parameters)
        {
            if (method != "Screencast.screencastFrame" || !parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;
            string data = payload.TryGetProperty("data", out JsonElement dataElement)
                ? dataElement.GetString()
                : null;
            if (string.IsNullOrEmpty(data))
            {
                return;
            }

            byte[] jpeg;
            try
            {
                jpeg = Convert.FromBase64String(data);
            }
            catch (FormatException)
            {
                return;
            }

            int viewportWidth = 0;
            int viewportHeight = 0;
            if (payload.TryGetProperty("deviceWidth", out JsonElement dw) && dw.TryGetInt32(out int w))
            {
                viewportWidth = w;
            }

            if (payload.TryGetProperty("deviceHeight", out JsonElement dh) && dh.TryGetInt32(out int h))
            {
                viewportHeight = h;
            }

            PageViewportSizeResult viewport = _page.ViewportSize;
            if (viewport != null)
            {
                if (viewportWidth == 0)
                {
                    viewportWidth = viewport.Width;
                }

                if (viewportHeight == 0)
                {
                    viewportHeight = viewport.Height;
                }
            }

            float timestamp = 0;
            if (payload.TryGetProperty("timestamp", out JsonElement ts) && ts.TryGetDouble(out double seconds))
            {
                timestamp = seconds < 1e12 ? (float)(seconds * 1000) : (float)seconds;
            }

            if (timestamp == 0)
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            ScreencastFrame frame = new()
            {
                Data = jpeg,
                Timestamp = timestamp,
                ViewportWidth = viewportWidth,
                ViewportHeight = viewportHeight,
            };

            _ = DeliverFrameAsync(frame, jpeg);
        }

        private async Task DeliverFrameAsync(ScreencastFrame frame, byte[] jpeg)
        {
            Func<ScreencastFrame, Task> onFrame;
            ScreencastVideoWriter video;
            ScreencastVideoWriter artifacts;
            int generation;
            lock (_gate)
            {
                if (!_started)
                {
                    return;
                }

                onFrame = _onFrame;
                video = _video;
                artifacts = _artifactsVideo;
                generation = _generation;
            }

            video?.Write(jpeg);
            artifacts?.Write(jpeg);
            try
            {
                if (onFrame != null)
                {
                    await onFrame(frame).ConfigureAwait(false);
                }
            }
            finally
            {
                if (_ownsProtocol)
                {
                    try
                    {
                        await _page.Session.SendAsync("Screencast.screencastFrameAck", new { generation }).ConfigureAwait(false);
                    }
                    catch (TargetClosedException)
                    {
                    }
                    catch (PlaywrightNativeException)
                    {
                    }
                }
            }
        }

        private sealed class StopOnDispose : IAsyncDisposable
        {
            private readonly WKScreencast _owner;

            internal StopOnDispose(WKScreencast owner)
            {
                _owner = owner;
            }

            public ValueTask DisposeAsync() => new ValueTask(_owner.StopAsync());
        }

        private sealed class HideOnDispose : IAsyncDisposable
        {
            private readonly WKScreencast _owner;

            internal HideOnDispose(WKScreencast owner)
            {
                _owner = owner;
            }

            public ValueTask DisposeAsync() => new ValueTask(_owner.HideActionsAsync());
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task<IAsyncDisposable> IScreencast.ShowActionsAsync(ScreencastShowActionsOptions options)
            => ShowActionsAsync(options?.Duration, options?.Position ?? default, options?.FontSize ?? 0, options?.Cursor ?? default);

        Task IScreencast.ShowChapterAsync(string title, ScreencastShowChapterOptions options) => Task.CompletedTask;

        Task<IAsyncDisposable> IScreencast.ShowOverlayAsync(string html, ScreencastShowOverlayOptions options) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IScreencast.StartAsync(ScreencastStartOptions options)
            => StartAsync(
                options?.OnFrame,
                options?.Quality ?? 0,
                options?.Size?.Width ?? 0,
                options?.Size?.Height ?? 0,
                options?.Path);
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
