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

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Chromium <c>Page.startScreencast</c> / <c>Page.stopScreencast</c>.
    /// </summary>
    internal sealed partial class CRScreencast : IScreencast
    {
        private readonly Page _page;
        private readonly object _gate = new();
        private Func<ScreencastFrame, Task> _onFrame;
        private ScreencastVideoWriter _video;
        private ScreencastVideoWriter _artifactsVideo;
        private bool _started;

        internal CRScreencast(Page page)
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

            _page.CrPage.Session.MessageReceived += OnMessage;
            try
            {
                await _page.CrPage.Session.SendAsync("Page.startScreencast", new
                {
                    format = "jpeg",
                    quality = quality > 0 ? quality : 90,
                    maxWidth,
                    maxHeight,
                    everyNthFrame = 1,
                }).ConfigureAwait(false);
            }
            catch
            {
                _page.CrPage.Session.MessageReceived -= OnMessage;
                lock (_gate)
                {
                    _started = false;
                    _onFrame = null;
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

            _page.CrPage.Session.MessageReceived -= OnMessage;
            try
            {
                await _page.CrPage.Session.SendAsync("Page.stopScreencast").ConfigureAwait(false);
            }
            catch (TargetClosedException)
            {
                throw;
            }
            catch (PlaywrightNativeException)
            {
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

            if (string.IsNullOrEmpty(data))
            {
                _ = AckAsync(sessionId);
                return;
            }

            byte[] jpeg;
            try
            {
                jpeg = Convert.FromBase64String(data);
            }
            catch (FormatException)
            {
                _ = AckAsync(sessionId);
                return;
            }

            PageViewportSizeResult viewport = _page.ViewportSize;
            int viewportWidth = viewport?.Width ?? 0;
            int viewportHeight = viewport?.Height ?? 0;
            float timestamp = 0;
            if (payload.TryGetProperty("metadata", out JsonElement metadata))
            {
                if (metadata.TryGetProperty("timestamp", out JsonElement ts) && ts.TryGetDouble(out double seconds))
                {
                    timestamp = seconds < 1e12 ? (float)(seconds * 1000) : (float)seconds;
                }

                if (viewportWidth == 0 && metadata.TryGetProperty("deviceWidth", out JsonElement dw) && dw.TryGetInt32(out int w))
                {
                    viewportWidth = w;
                }

                if (viewportHeight == 0 && metadata.TryGetProperty("deviceHeight", out JsonElement dh) && dh.TryGetInt32(out int h))
                {
                    viewportHeight = h;
                }
            }

            ScreencastFrame frame = new()
            {
                Data = jpeg,
                Timestamp = timestamp,
                ViewportWidth = viewportWidth,
                ViewportHeight = viewportHeight,
            };

            _ = DeliverFrameAsync(frame, jpeg, sessionId);
        }

        private async Task DeliverFrameAsync(ScreencastFrame frame, byte[] jpeg, int sessionId)
        {
            Func<ScreencastFrame, Task> onFrame;
            ScreencastVideoWriter video;
            ScreencastVideoWriter artifacts;
            lock (_gate)
            {
                if (!_started)
                {
                    return;
                }

                onFrame = _onFrame;
                video = _video;
                artifacts = _artifactsVideo;
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
                await AckAsync(sessionId).ConfigureAwait(false);
            }
        }

        private async Task AckAsync(int sessionId)
        {
            try
            {
                await _page.CrPage.Session.SendAsync("Page.screencastFrameAck", new { sessionId }).ConfigureAwait(false);
            }
            catch (TargetClosedException)
            {
            }
            catch (PlaywrightNativeException)
            {
            }
        }

        private sealed class StopOnDispose : IAsyncDisposable
        {
            private readonly CRScreencast _owner;

            internal StopOnDispose(CRScreencast owner)
            {
                _owner = owner;
            }

            public ValueTask DisposeAsync() => new ValueTask(_owner.StopAsync());
        }

        private sealed class HideOnDispose : IAsyncDisposable
        {
            private readonly CRScreencast _owner;

            internal HideOnDispose(CRScreencast owner)
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
