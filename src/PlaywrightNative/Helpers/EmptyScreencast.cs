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

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// <see cref="IScreencast"/> for browsers without <c>Page.startScreencast</c>.
    /// </summary>
    internal sealed partial class EmptyScreencast : IScreencast
    {
        private readonly string _browserName;
        private readonly IPage _page;

        internal EmptyScreencast(string browserName, IPage page)
        {
            _browserName = string.IsNullOrEmpty(browserName) ? "this browser" : browserName;
            _page = page ?? throw new ArgumentNullException(nameof(page));
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> StartAsync(Func<ScreencastFrame, Task> onFrame = default, int quality = default, int width = default, int height = default, string path = default)
        {
            _ = onFrame;
            _ = quality;
            _ = width;
            _ = height;
            _ = path;
            throw new PlaywrightNativeException("Screencast is not supported on " + _browserName + ".");
        }

        /// <inheritdoc/>
        public Task StopAsync() => Task.CompletedTask;

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

        private sealed class HideOnDispose : IAsyncDisposable
        {
            private readonly EmptyScreencast _owner;

            internal HideOnDispose(EmptyScreencast owner)
            {
                _owner = owner;
            }

            public ValueTask DisposeAsync() => new ValueTask(_owner.HideActionsAsync());
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task<IAsyncDisposable> IScreencast.ShowActionsAsync(ScreencastShowActionsOptions options)
            => ShowActionsAsync(options?.Duration, options?.Position ?? default, options?.FontSize ?? 0, options?.Cursor ?? default);

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
