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
#pragma warning disable CA1062
using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Chromium;
using PlaywrightNative.Helpers;
using PlaywrightNative.WebKit;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy screencast helpers.
    /// </summary>
    public static class ScreencastCompatExtensions
    {
        /// <summary>Legacy show-chapter with description and duration.</summary>
        public static Task ShowChapterAsync(
            this IScreencast screencast,
            string title,
            string description = default,
            float? duration = default)
        {
            switch (screencast)
            {
                case CRScreencast chromium:
                    return chromium.ShowChapterAsync(title, description, duration);
                case WKScreencast webkit:
                    return webkit.ShowChapterAsync(title, description, duration);
                case EmptyScreencast empty:
                    return empty.ShowChapterAsync(title, description, duration);
                default:
                    return screencast.ShowChapterAsync(title, new ScreencastShowChapterOptions
                    {
                        Description = description,
                        Duration = duration,
                    });
            }
        }

        /// <summary>Legacy show-overlay with duration.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task<IAsyncDisposable> ShowOverlayAsync(
            this IScreencast screencast,
            string html,
            float? duration = default)
        {
            switch (screencast)
            {
                case CRScreencast chromium:
                    return chromium.ShowOverlayAsync(html, duration);
                case WKScreencast webkit:
                    return webkit.ShowOverlayAsync(html, duration);
                case EmptyScreencast empty:
                    return empty.ShowOverlayAsync(html, duration);
                default:
                    return screencast.ShowOverlayAsync(html, new ScreencastShowOverlayOptions { Duration = duration });
            }
        }
    }
}
