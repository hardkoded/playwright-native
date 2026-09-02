/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Chromium;
using PlaywrightNative.Firefox;
using PlaywrightNative.WebKit;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Dispatches legacy PlaywrightNative surface calls to concrete implementations when available.
    /// </summary>
    internal static class PageCompatDispatch
    {
        internal static IFrame FrameByUrl(IPage page, string urlString, Regex urlRegex, Func<string, bool> urlFunc)
            => page switch
            {
                Page chromium => chromium.FrameByUrl(urlString, urlRegex, urlFunc),
                FirefoxPage firefox => firefox.FrameByUrl(urlString, urlRegex, urlFunc),
                WKPage webkit => webkit.FrameByUrl(urlString, urlRegex, urlFunc),
                _ => throw new NotSupportedException("FrameByUrl requires a PlaywrightNative page."),
            };

        internal static Task RemoveAllListenersAsync(IPage page, string type, RemoveAllListenersBehavior behavior)
            => page switch
            {
                Page chromium => chromium.RemoveAllListenersAsync(type, behavior),
                FirefoxPage firefox => firefox.RemoveAllListenersAsync(type, behavior),
                WKPage webkit => webkit.RemoveAllListenersAsync(type, behavior),
                _ => throw new NotSupportedException("RemoveAllListenersAsync requires a PlaywrightNative page."),
            };

        internal static Task EmulateVisionDeficiencyAsync(IPage page, VisionDeficiency type)
            => page switch
            {
                Page chromium => chromium.EmulateVisionDeficiencyAsync(type),
                FirefoxPage firefox => firefox.EmulateVisionDeficiencyAsync(type),
                WKPage webkit => webkit.EmulateVisionDeficiencyAsync(type),
                _ => throw new NotSupportedException("EmulateVisionDeficiencyAsync requires a PlaywrightNative page."),
            };

        internal static Task<IElementHandle> GetByTextAsync(IPage page, string text, bool? exact, float? timeout)
            => page switch
            {
                Page chromium => chromium.GetByTextAsync(text, exact, timeout),
                FirefoxPage firefox => firefox.GetByTextAsync(text, exact, timeout),
                WKPage webkit => webkit.GetByTextAsync(text, exact, timeout),
                _ => page.GetByText(text, new PageGetByTextOptions { Exact = exact })
                    .ElementHandleAsync(new LocatorElementHandleOptions { Timeout = timeout }),
            };

        internal static Task<IElementHandle> GetByTestIdAsync(IPage page, string testId, float? timeout)
            => page switch
            {
                Page chromium => chromium.GetByTestIdAsync(testId, timeout),
                FirefoxPage firefox => firefox.GetByTestIdAsync(testId, timeout),
                WKPage webkit => webkit.GetByTestIdAsync(testId, timeout),
                _ => page.GetByTestId(testId).ElementHandleAsync(new LocatorElementHandleOptions { Timeout = timeout }),
            };

        internal static Task<IElementHandle> GetByPlaceholderAsync(IPage page, string text, bool? exact, float? timeout)
            => page switch
            {
                Page chromium => chromium.GetByPlaceholderAsync(text, exact, timeout),
                FirefoxPage firefox => firefox.GetByPlaceholderAsync(text, exact, timeout),
                WKPage webkit => webkit.GetByPlaceholderAsync(text, exact, timeout),
                _ => page.GetByPlaceholder(text, new PageGetByPlaceholderOptions { Exact = exact })
                    .ElementHandleAsync(new LocatorElementHandleOptions { Timeout = timeout }),
            };

        internal static Task<IElementHandle> GetByAltTextAsync(IPage page, string text, bool? exact, float? timeout)
            => page switch
            {
                Page chromium => chromium.GetByAltTextAsync(text, exact, timeout),
                FirefoxPage firefox => firefox.GetByAltTextAsync(text, exact, timeout),
                WKPage webkit => webkit.GetByAltTextAsync(text, exact, timeout),
                _ => page.GetByAltText(text, new PageGetByAltTextOptions { Exact = exact })
                    .ElementHandleAsync(new LocatorElementHandleOptions { Timeout = timeout }),
            };

        internal static Task<IElementHandle> GetByTitleAsync(IPage page, string text, bool? exact, float? timeout)
            => page switch
            {
                Page chromium => chromium.GetByTitleAsync(text, exact, timeout),
                FirefoxPage firefox => firefox.GetByTitleAsync(text, exact, timeout),
                WKPage webkit => webkit.GetByTitleAsync(text, exact, timeout),
                _ => page.GetByTitle(text, new PageGetByTitleOptions { Exact = exact })
                    .ElementHandleAsync(new LocatorElementHandleOptions { Timeout = timeout }),
            };

        internal static Task<IElementHandle> GetByRoleAsync(
            IPage page,
            string role,
            string name,
            bool? exact,
            float? timeout)
            => page switch
            {
                Page chromium => chromium.GetByRoleAsync(role, name, exact, timeout),
                FirefoxPage firefox => firefox.GetByRoleAsync(role, name, exact, timeout),
                WKPage webkit => webkit.GetByRoleAsync(role, name, exact, timeout),
                _ => page.Locator(RoleSelector.Build(role, name, exact))
                    .ElementHandleAsync(new LocatorElementHandleOptions { Timeout = timeout }),
            };

        internal static Task<IElementHandle> GetByTextAsync(IFrame frame, string text, bool? exact, float? timeout)
            => frame.GetByText(text, new FrameGetByTextOptions { Exact = exact })
                .ElementHandleAsync(new LocatorElementHandleOptions { Timeout = timeout });

        internal static Task<IElementHandle> GetByTestIdAsync(IFrame frame, string testId, float? timeout)
            => frame.GetByTestId(testId).ElementHandleAsync(new LocatorElementHandleOptions { Timeout = timeout });

        internal static Task<IReadOnlyList<string>> PageErrorsAsync(IPage page, PageErrorsFilter filter)
            => page switch
            {
                Page chromium => chromium.PageErrorsAsync(filter),
                FirefoxPage firefox => firefox.PageErrorsAsync(filter),
                WKPage webkit => webkit.PageErrorsAsync(filter),
                _ => throw new NotSupportedException("PageErrorsAsync requires a PlaywrightNative page."),
            };

        internal static Task<IReadOnlyList<IRequest>> RequestsAsync(IPage page)
            => page switch
            {
                Page chromium => chromium.RequestsAsync(),
                FirefoxPage firefox => firefox.RequestsAsync(),
                WKPage webkit => webkit.RequestsAsync(),
                _ => throw new NotSupportedException("RequestsAsync requires a PlaywrightNative page."),
            };
    }
}
