/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy <c>page.waitFor*</c> sugar over <see cref="IPage.WaitForEventAsync{T}"/>.
    /// </summary>
    public static class PageWaitCompatExtensions
    {
        /// <summary>Wait for a page event.</summary>
        /// <typeparam name="T">The event payload type.</typeparam>
        public static Task<T> WaitForEventAsync<T>(
            this IPage page,
            PlaywrightEvent<T> pageEvent,
            Func<T, bool> predicate = null,
            float? timeout = null)
            => PageWaitForEventHelper.WaitAsync(page, pageEvent, predicate, timeout);

        /// <summary>Wait for the next load event.</summary>
        public static Task<IPage> WaitForLoadAsync(this IPage page, float? timeout = default)
            => page.WaitForEventAsync(PageEvent.Load, timeout: timeout);

        /// <summary>Wait for the next DOMContentLoaded event.</summary>
        public static Task<IPage> WaitForDOMContentLoadedAsync(this IPage page, float? timeout = default)
            => page.WaitForEventAsync(PageEvent.DOMContentLoaded, timeout: timeout);

        /// <summary>Wait for the next page error.</summary>
        public static async Task<string> WaitForPageErrorAsync(this IPage page, float? timeout = default)
        {
            PageErrorEventArgs args = await page.WaitForEventAsync(PageEvent.PageError, timeout: timeout).ConfigureAwait(false);
            return args.Message;
        }

        /// <summary>Wait for the next crash event.</summary>
        public static Task<IPage> WaitForCrashAsync(this IPage page, float? timeout = default)
            => page.WaitForEventAsync(PageEvent.Crash, timeout: timeout);

        /// <summary>Wait for the next dialog event.</summary>
        public static Task<IDialog> WaitForDialogAsync(this IPage page, float? timeout = default)
            => page.WaitForEventAsync(PageEvent.Dialog, timeout: timeout);

        /// <summary>Wait for the next dialog-closed event.</summary>
        public static Task<IDialog> WaitForDialogClosedAsync(this IPage page, float? timeout = default)
            => page.WaitForEventAsync(PageEvent.DialogClosed, timeout: timeout);

        /// <summary>Wait for the next frame navigated event.</summary>
        public static Task<IFrame> WaitForFrameNavigatedAsync(this IPage page, Func<IFrame, bool> predicate = default, float? timeout = default)
            => page.WaitForEventAsync(PageEvent.FrameNavigated, predicate, timeout);

        /// <summary>Wait for the next frame attached event.</summary>
        public static Task<IFrame> WaitForFrameAttachedAsync(this IPage page, float? timeout = default)
            => page.WaitForEventAsync(PageEvent.FrameAttached, timeout: timeout);

        /// <summary>Wait for the next frame detached event.</summary>
        public static Task<IFrame> WaitForFrameDetachedAsync(this IPage page, float? timeout = default)
            => page.WaitForEventAsync(PageEvent.FrameDetached, timeout: timeout);

        /// <summary>Wait for the next request-failed event.</summary>
        public static Task<IRequest> WaitForRequestFailedAsync(this IPage page, Func<IRequest, bool> predicate = default, float? timeout = default)
            => page.WaitForEventAsync(PageEvent.RequestFailed, predicate, timeout);

        /// <summary>Wait for the next page close event.</summary>
        public static Task<IPage> WaitForCloseAsync(this IPage page, float? timeout = default)
            => page.WaitForEventAsync(PageEvent.Close, timeout: timeout);
    }
}
