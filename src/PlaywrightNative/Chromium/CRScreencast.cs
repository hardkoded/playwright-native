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
        private Task _deliverChain = Task.CompletedTask;
        private Func<ScreencastFrame, Task> _onFrame;
        private ScreencastVideoWriter _video;
        private ScreencastVideoWriter _artifactsVideo;
        private bool _started;
        private int _maxWidth;
        private int _maxHeight;

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
                    throw new PlaywrightException("Screencast is already started");
                }

                _started = true;
                _onFrame = onFrame;
            }

            // Chromium screencast sizes are even (upstream screencast.ts).
            int maxWidth = width > 0 ? width : 800;
            int maxHeight = height > 0 ? height : 800;
            maxWidth &= ~1;
            maxHeight &= ~1;
            _maxWidth = maxWidth;
            _maxHeight = maxHeight;

            // Re-apply device metrics before the first frame so early about:blank
            // captures are not letterboxed to the wrong aspect ratio.
            PageViewportSizeResult viewport = _page.ViewportSize;
            if (viewport != null && viewport.Width > 0 && viewport.Height > 0)
            {
                await _page.SetViewportSizeAsync(viewport.Width, viewport.Height).ConfigureAwait(false);
            }

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
            catch (PlaywrightException)
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
        public Task<IAsyncDisposable> ShowActionsAsync(float? duration = default, AnnotatePosition position = EnumCompat.UndefinedAnnotatePosition, int fontSize = default, ScreencastCursor cursor = EnumCompat.UndefinedScreencastCursor)
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

        private static bool JpegMatchesSize(byte[] buffer, int expectedWidth, int expectedHeight)
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
                if ((marker >= 0xC0 && marker <= 0xC3)
                    || (marker >= 0xC5 && marker <= 0xC7)
                    || (marker >= 0xC9 && marker <= 0xCB)
                    || (marker >= 0xCD && marker <= 0xCF))
                {
                    int height = (buffer[i + 5] << 8) | buffer[i + 6];
                    int width = (buffer[i + 7] << 8) | buffer[i + 8];
                    return width == expectedWidth && height == expectedHeight;
                }

                if (segmentLength < 2)
                {
                    break;
                }

                i += 2 + segmentLength;
            }

            return false;
        }

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

            // Drop frames captured before the emulated viewport settled (wrong
            // aspect → wrong JPEG size). Still ack so Chrome keeps streaming.
            if (_maxWidth > 0 && _maxHeight > 0 && viewportWidth > 0 && viewportHeight > 0)
            {
                double scale = Math.Min(1.0, Math.Min((double)_maxWidth / viewportWidth, (double)_maxHeight / viewportHeight));
                int expectedWidth = (int)Math.Floor(viewportWidth * scale) & ~1;
                int expectedHeight = (int)Math.Floor(viewportHeight * scale) & ~1;
                if (expectedWidth > 0 && expectedHeight > 0
                    && !JpegMatchesSize(jpeg, expectedWidth, expectedHeight))
                {
                    _ = AckAsync(sessionId);
                    return;
                }
            }

            _ = DeliverFrameAsync(frame, jpeg, sessionId);
        }

        private Task DeliverFrameAsync(ScreencastFrame frame, byte[] jpeg, int sessionId)
        {
            // Serialize delivery + ack so an async OnFrame callback applies
            // backpressure (Chrome waits for screencastFrameAck). Fire-and-forget
            // CDP handlers must not overlap.
            Task previous;
            TaskCompletionSource<bool> done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
            {
                previous = _deliverChain;
                _deliverChain = done.Task;
            }

            return DeliverFrameCoreAsync(previous, done, frame, jpeg, sessionId);
        }

        private async Task DeliverFrameCoreAsync(
            Task previous,
            TaskCompletionSource<bool> done,
            ScreencastFrame frame,
            byte[] jpeg,
            int sessionId)
        {
            try
            {
                await previous.ConfigureAwait(false);

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
            finally
            {
                done.TrySetResult(true);
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
            catch (PlaywrightException)
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
            => ShowActionsAsync(
                options?.Duration,
                options?.Position ?? EnumCompat.UndefinedAnnotatePosition,
                options?.FontSize ?? 0,
                options?.Cursor ?? EnumCompat.UndefinedScreencastCursor);

        Task IScreencast.ShowChapterAsync(string title, ScreencastShowChapterOptions options)
            => ShowChapterAsync(title, options?.Description, options?.Duration);

        Task<IAsyncDisposable> IScreencast.ShowOverlayAsync(string html, ScreencastShowOverlayOptions options)
            => ShowOverlayAsync(html, options?.Duration);

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
