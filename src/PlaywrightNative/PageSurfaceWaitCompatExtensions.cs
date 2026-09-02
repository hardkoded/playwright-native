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
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy wait/navigation helpers shadowed by official options-bag APIs.
    /// </summary>
    public static class PageSurfaceWaitCompatExtensions
    {
        /// <summary>Legacy wait-for-navigation with URL regex.</summary>
        [OverloadResolutionPriority(1)]
        public static Task<IResponse> WaitForNavigationAsync(this IPage page, Regex urlRegex, PageWaitForNavigationOptions options = default)
            => page.WaitForNavigationAsync(new PageWaitForNavigationOptions
            {
                UrlRegex = urlRegex,
                Timeout = options?.Timeout,
                WaitUntil = options?.WaitUntil,
            });

        /// <summary>Legacy wait-for-navigation with URL predicate.</summary>
        [OverloadResolutionPriority(1)]
        public static Task<IResponse> WaitForNavigationAsync(this IPage page, Func<string, bool> urlFunc, PageWaitForNavigationOptions options = default)
            => page.WaitForNavigationAsync(new PageWaitForNavigationOptions
            {
                UrlFunc = urlFunc,
                Timeout = options?.Timeout,
                WaitUntil = options?.WaitUntil,
            });

        /// <summary>Legacy wait-for-function with string polling interval as third argument.</summary>
        [OverloadResolutionPriority(1)]
        public static Task<IJSHandle> WaitForFunctionAsync(
            this IPage page,
            string expression,
            object arg,
            string polling,
            PageWaitForFunctionOptions options = default)
            => page.WaitForFunctionAsync(expression, arg, options?.Timeout, polling);

        /// <summary>Legacy wait-for-websocket with URL string.</summary>
        [OverloadResolutionPriority(1)]
        public static Task<IWebSocket> WaitForWebSocketAsync(this IPage page, string url, PageWaitForWebSocketOptions options = default)
            => page.WaitForWebSocketAsync(new PageWaitForWebSocketOptions
            {
                Predicate = webSocket => webSocket.Url.Contains(url, StringComparison.Ordinal),
                Timeout = options?.Timeout,
            });

        /// <summary>Legacy wait-for-websocket with URL regex.</summary>
        [OverloadResolutionPriority(1)]
        public static Task<IWebSocket> WaitForWebSocketAsync(this IPage page, Regex urlRegex, PageWaitForWebSocketOptions options = default)
            => page.WaitForWebSocketAsync(new PageWaitForWebSocketOptions
            {
                Predicate = webSocket => urlRegex.IsMatch(webSocket.Url),
                Timeout = options?.Timeout,
            });

        /// <summary>Legacy wait-for-file-chooser with predicate.</summary>
        [OverloadResolutionPriority(1)]
        public static Task<IFileChooser> WaitForFileChooserAsync(
            this IPage page,
            Func<IFileChooser, bool> predicate,
            PageWaitForFileChooserOptions options = default)
            => page.WaitForFileChooserAsync(new PageWaitForFileChooserOptions
            {
                Predicate = predicate,
                Timeout = options?.Timeout,
            });

        /// <summary>Legacy run-and-wait-for-file-chooser with action.</summary>
        [OverloadResolutionPriority(1)]
        public static Task<IFileChooser> WaitForFileChooserAsync(
            this IPage page,
            Func<Task> action,
            PageWaitForFileChooserOptions options = default)
            => page.RunAndWaitForFileChooserAsync(action, new PageRunAndWaitForFileChooserOptions
            {
                Timeout = options?.Timeout,
            });

        /// <summary>Legacy run-and-wait-for-download with action.</summary>
        [OverloadResolutionPriority(1)]
        public static Task<IDownload> WaitForDownloadAsync(
            this IPage page,
            Func<Task> action,
            PageWaitForDownloadOptions options = default)
            => page.RunAndWaitForDownloadAsync(action, new PageRunAndWaitForDownloadOptions
            {
                Timeout = options?.Timeout,
            });

        /// <summary>Legacy wait-for-download with predicate (no action).</summary>
        [OverloadResolutionPriority(1)]
        public static Task<IDownload> WaitForDownloadAsync(
            this IPage page,
            Func<IDownload, bool> predicate,
            PageWaitForDownloadOptions options = default)
            => page.WaitForEventAsync(
                PageEvent.Download,
                download => predicate((IDownload)download),
                options?.Timeout);

        /// <summary>Legacy page aria snapshot scoped to a ref selector.</summary>
        [OverloadResolutionPriority(1)]
        public static Task<string> AriaSnapshotAsync(
            this IPage page,
            string refSelector,
            PageAriaSnapshotOptions options = default)
            => page.Locator(refSelector).AriaSnapshotAsync(new LocatorAriaSnapshotOptions
            {
                Timeout = options?.Timeout,
            });

        /// <summary>Legacy async get-by-role returning an element handle.</summary>
        [OverloadResolutionPriority(1)]
        public static Task<IElementHandle> GetByRoleAsync(this IPage page, AriaRole role, float? timeout = null)
            => page.GetByRoleAsync(role.ToString(), timeout: timeout);

        /// <summary>Legacy set-checked with expanded parameters.</summary>
        [OverloadResolutionPriority(1)]
        public static Task SetCheckedAsync(
            this IPage page,
            string selector,
            bool checkedState,
            Position position = default,
            bool? force = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => page.SetCheckedAsync(selector, checkedState, new PageSetCheckedOptions
            {
                Position = position,
                Force = force,
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Trial = trial,
                Scroll = ActionScrollBridge.ToScrollOption(scroll),
                Strict = strict,
            });

        /// <summary>Legacy dispatch-event with strict flag.</summary>
        [OverloadResolutionPriority(1)]
        public static Task DispatchEventAsync(
            this IPage page,
            string selector,
            string type,
            object eventInit = default,
            bool? strict = default,
            float? timeout = default)
            => page.DispatchEventAsync(selector, type, eventInit, new PageDispatchEventOptions
            {
                Strict = strict,
                Timeout = timeout,
            });
    }
}
