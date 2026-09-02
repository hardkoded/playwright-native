/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
#pragma warning disable CA1062
using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightSharp.Chromium;
using PlaywrightSharp.Helpers;
using PlaywrightSharp.WebKit;

namespace PlaywrightSharp
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
