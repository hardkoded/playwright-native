/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
#pragma warning disable CA1062
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy <c>browserContext.waitFor*</c> sugar over
    /// <see cref="IBrowserContext.WaitForEventAsync{T}"/>.
    /// </summary>
    public static class BrowserContextWaitCompatExtensions
    {
        /// <summary>Wait for a browser-context event.</summary>
        /// <typeparam name="T">The event payload type.</typeparam>
        public static Task<T> WaitForEventAsync<T>(
            this IBrowserContext context,
            PlaywrightEvent<T> contextEvent,
            Func<T, bool> predicate = null,
            float? timeout = null)
            => ContextWaitForEventHelper.WaitAsync(context, contextEvent, predicate, timeout);

        /// <summary>Wait for the next service worker.</summary>
        public static Task<IWorker> WaitForServiceWorkerAsync(this IBrowserContext context, float? timeout = default)
            => ContextWaitForEventHelper.WaitAsync(context, BrowserContextEvent.ServiceWorker, null, timeout);

        /// <summary>Wait for the next request matching a URL pattern.</summary>
        public static Task<IRequest> WaitForRequestAsync(this IBrowserContext context, string url, float? timeout = default)
            => WaitForRequestAsync(context, url, null, null, timeout);

        /// <summary>Wait for the next request matching a regex.</summary>
        public static Task<IRequest> WaitForRequestAsync(this IBrowserContext context, Regex url, float? timeout = default)
            => WaitForRequestAsync(context, null, url, null, timeout);

        /// <summary>Wait for the next response matching a URL pattern.</summary>
        public static Task<IResponse> WaitForResponseAsync(this IBrowserContext context, string url, float? timeout = default)
            => WaitForResponseAsync(context, url, null, null, timeout);

        /// <summary>Wait for the next response matching a regex.</summary>
        public static Task<IResponse> WaitForResponseAsync(this IBrowserContext context, Regex url, float? timeout = default)
            => WaitForResponseAsync(context, null, url, null, timeout);

        /// <summary>Wait for the next request.</summary>
        public static Task<IRequest> WaitForRequestAsync(this IBrowserContext context, Func<IRequest, bool> predicate, float? timeout = default)
            => context.WaitForEventAsync(BrowserContextEvent.Request, predicate, timeout);

        /// <summary>Wait for the next request.</summary>
        public static Task<IRequest> WaitForRequestAsync(this IBrowserContext context, string urlString, Regex urlRegex, Func<IRequest, bool> predicate, float? timeout = default)
            => context.WaitForEventAsync(
                BrowserContextEvent.Request,
                r => predicate != null ? predicate(r) : UrlMatcher.Matches(r.Url, urlString, urlRegex, null, null),
                timeout);

        /// <summary>Wait for the next response.</summary>
        public static Task<IResponse> WaitForResponseAsync(this IBrowserContext context, Func<IResponse, bool> predicate, float? timeout = default)
            => context.WaitForEventAsync(BrowserContextEvent.Response, predicate, timeout);

        /// <summary>Wait for the next response.</summary>
        public static Task<IResponse> WaitForResponseAsync(this IBrowserContext context, string urlString, Regex urlRegex, Func<IResponse, bool> predicate, float? timeout = default)
            => context.WaitForEventAsync(
                BrowserContextEvent.Response,
                r => predicate != null ? predicate(r) : UrlMatcher.Matches(r.Url, urlString, urlRegex, null, null),
                timeout);

        /// <summary>Wait for the next dialog.</summary>
        public static Task<IDialog> WaitForDialogAsync(this IBrowserContext context, float? timeout = default)
            => context.WaitForEventAsync(BrowserContextEvent.Dialog, timeout: timeout);

        /// <summary>Wait for the next dialog-closed event.</summary>
        public static Task<IDialog> WaitForDialogClosedAsync(this IBrowserContext context, float? timeout = default)
            => context.WaitForEventAsync(BrowserContextEvent.DialogClosed, timeout: timeout);

        /// <summary>Wait for the next download.</summary>
        public static Task<IDownload> WaitForDownloadAsync(this IBrowserContext context, float? timeout = default)
            => context.WaitForEventAsync(BrowserContextEvent.Download, timeout: timeout);

        /// <summary>Wait for the next page event.</summary>
        public static Task<IPage> WaitForPageAsync(this IBrowserContext context, float? timeout = default)
            => context.WaitForEventAsync(BrowserContextEvent.Page, timeout: timeout);

        /// <summary>Wait for the next page-close event.</summary>
        public static Task<IPage> WaitForPageCloseAsync(this IBrowserContext context, float? timeout = default)
            => context.WaitForEventAsync(BrowserContextEvent.PageClose, timeout: timeout);

        /// <summary>Wait for the next page-load event.</summary>
        public static Task<IPage> WaitForPageLoadAsync(this IBrowserContext context, float? timeout = default)
            => context.WaitForEventAsync(BrowserContextEvent.PageLoad, timeout: timeout);

        /// <summary>Wait for the next frame navigated event.</summary>
        public static Task<IFrame> WaitForFrameNavigatedAsync(this IBrowserContext context, float? timeout = default)
            => context.WaitForEventAsync(BrowserContextEvent.FrameNavigated, timeout: timeout);

        /// <summary>Wait for the next frame attached event.</summary>
        public static Task<IFrame> WaitForFrameAttachedAsync(this IBrowserContext context, float? timeout = default)
            => context.WaitForEventAsync(BrowserContextEvent.FrameAttached, timeout: timeout);

        /// <summary>Wait for the next frame detached event.</summary>
        public static Task<IFrame> WaitForFrameDetachedAsync(this IBrowserContext context, float? timeout = default)
            => context.WaitForEventAsync(BrowserContextEvent.FrameDetached, timeout: timeout);

        /// <summary>Wait for the next request-failed event.</summary>
        public static Task<IRequest> WaitForRequestFailedAsync(this IBrowserContext context, float? timeout = default)
            => context.WaitForEventAsync(BrowserContextEvent.RequestFailed, timeout: timeout);

        /// <summary>Wait for the next request-finished event.</summary>
        public static Task<IRequest> WaitForRequestFinishedAsync(this IBrowserContext context, float? timeout = default)
            => context.WaitForEventAsync(BrowserContextEvent.RequestFinished, timeout: timeout);

        /// <summary>Wait for the next web error.</summary>
        public static Task<IWebError> WaitForWebErrorAsync(this IBrowserContext context, float? timeout = default)
            => context.WaitForEventAsync(BrowserContextEvent.WebError, timeout: timeout);

        /// <summary>Wait for the next context close event.</summary>
        public static Task<IBrowserContext> WaitForCloseAsync(this IBrowserContext context, float? timeout = default)
            => context.WaitForEventAsync(BrowserContextEvent.Close, timeout: timeout);
    }
}
