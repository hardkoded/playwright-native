/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Chromium;
using PlaywrightNative.Firefox;
using PlaywrightNative.Helpers;
using PlaywrightNative.WebKit;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy network wait / run-and-wait helpers over <see cref="IPage"/> and
    /// <see cref="IBrowserContext"/>.
    /// </summary>
    public static class PageNetworkCompatExtensions
    {
        /// <summary>Wait for the next request matching a URL pattern.</summary>
        public static Task<IRequest> WaitForRequestAsync(this IPage page, string url, float? timeout = default)
            => WaitForRequestAsync(page, url, null, null, timeout);

        /// <summary>Wait for the next request matching a regex.</summary>
        public static Task<IRequest> WaitForRequestAsync(this IPage page, Regex url, float? timeout = default)
            => WaitForRequestAsync(page, null, url, null, timeout);

        /// <summary>Wait for the next request matching a predicate.</summary>
        public static Task<IRequest> WaitForRequestAsync(this IPage page, Func<IRequest, bool> predicate, float? timeout = default)
            => WaitForRequestAsync(page, null, null, predicate, timeout);

        /// <summary>Wait for the next request.</summary>
        public static Task<IRequest> WaitForRequestAsync(
            this IPage page,
            string urlString,
            Regex urlRegex,
            Func<IRequest, bool> predicate,
            float? timeout = default)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            switch (page)
            {
                case Page chromium:
                    return chromium.WaitForRequestAsync(urlString, urlRegex, predicate, timeout);
                case FirefoxPage firefox:
                    return firefox.WaitForRequestAsync(urlString, urlRegex, predicate, timeout);
                case WKPage webkit:
                    return webkit.WaitForRequestAsync(urlString, urlRegex, predicate, timeout);
                default:
                    return page.WaitForRequestAsync(
                        predicate ?? (r => UrlMatcher.Matches(r.Url, urlString, urlRegex, null, null)),
                        new PageWaitForRequestOptions { Timeout = timeout });
            }
        }

        /// <summary>Wait for the next response matching a URL pattern.</summary>
        public static Task<IResponse> WaitForResponseAsync(this IPage page, string url, float? timeout = default)
            => WaitForResponseAsync(page, url, null, null, timeout);

        /// <summary>Wait for the next response matching a regex.</summary>
        public static Task<IResponse> WaitForResponseAsync(this IPage page, Regex url, float? timeout = default)
            => WaitForResponseAsync(page, null, url, null, timeout);

        /// <summary>Wait for the next response matching a predicate.</summary>
        public static Task<IResponse> WaitForResponseAsync(this IPage page, Func<IResponse, bool> predicate, float? timeout = default)
            => WaitForResponseAsync(page, null, null, predicate, timeout);

        /// <summary>Wait for the next response.</summary>
        public static Task<IResponse> WaitForResponseAsync(
            this IPage page,
            string urlString,
            Regex urlRegex,
            Func<IResponse, bool> predicate,
            float? timeout = default)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            switch (page)
            {
                case Page chromium:
                    return chromium.WaitForResponseAsync(urlString, urlRegex, predicate, timeout);
                case FirefoxPage firefox:
                    return firefox.WaitForResponseAsync(urlString, urlRegex, predicate, timeout);
                case WKPage webkit:
                    return webkit.WaitForResponseAsync(urlString, urlRegex, predicate, timeout);
                default:
                    return page.WaitForResponseAsync(
                        predicate ?? (r => UrlMatcher.Matches(r.Url, urlString, urlRegex, null, null)),
                        new PageWaitForResponseOptions { Timeout = timeout });
            }
        }

        /// <summary>Wait for the next finished request matching a URL pattern.</summary>
        public static Task<IRequest> WaitForRequestFinishedAsync(this IPage page, string url, float? timeout = default)
            => WaitForRequestFinishedAsync(page, url, null, null, timeout);

        /// <summary>Wait for the next finished request matching a regex.</summary>
        public static Task<IRequest> WaitForRequestFinishedAsync(this IPage page, Regex url, float? timeout = default)
            => WaitForRequestFinishedAsync(page, null, url, null, timeout);

        /// <summary>Wait for the next finished request matching a predicate.</summary>
        public static Task<IRequest> WaitForRequestFinishedAsync(this IPage page, Func<IRequest, bool> predicate, float? timeout = default)
            => WaitForRequestFinishedAsync(page, null, null, predicate, timeout);

        /// <summary>Wait for the next finished request.</summary>
        public static Task<IRequest> WaitForRequestFinishedAsync(
            this IPage page,
            string urlString,
            Regex urlRegex,
            Func<IRequest, bool> predicate,
            float? timeout = default)
            => page.WaitForEventAsync(
                PageEvent.RequestFinished,
                r => predicate != null ? predicate(r) : UrlMatcher.Matches(r.Url, urlString, urlRegex, null, NavigationUrl.ContextBase(page.Context)),
                timeout);

        /// <summary>Wait for the next failed request matching a URL pattern.</summary>
        public static Task<IRequest> WaitForRequestFailedAsync(this IPage page, string url, float? timeout = default)
            => WaitForRequestFailedAsync(page, url, null, null, timeout);

        /// <summary>Wait for the next failed request matching a regex.</summary>
        public static Task<IRequest> WaitForRequestFailedAsync(this IPage page, Regex url, float? timeout = default)
            => WaitForRequestFailedAsync(page, null, url, null, timeout);

        /// <summary>Wait for the next failed request.</summary>
        public static Task<IRequest> WaitForRequestFailedAsync(
            this IPage page,
            string urlString,
            Regex urlRegex,
            Func<IRequest, bool> predicate,
            float? timeout = default)
            => page.WaitForEventAsync(
                PageEvent.RequestFailed,
                r => predicate != null ? predicate(r) : UrlMatcher.Matches(r.Url, urlString, urlRegex, null, NavigationUrl.ContextBase(page.Context)),
                timeout);

        /// <summary>Run an action and wait for the next request.</summary>
        public static Task<IRequest> RunAndWaitForRequestAsync(this IPage page, Func<Task> action, float? timeout = default)
            => RunAndWaitForRequestAsync(page, action, null, null, null, timeout);

        /// <summary>Run an action and wait for a matching request.</summary>
        public static Task<IRequest> RunAndWaitForRequestAsync(
            this IPage page,
            Func<Task> action,
            string url,
            float? timeout = default)
            => RunAndWaitForRequestAsync(page, action, url, null, null, timeout);

        /// <summary>Run an action and wait for a matching request.</summary>
        public static Task<IRequest> RunAndWaitForRequestAsync(
            this IPage page,
            Func<Task> action,
            string urlString,
            Regex urlRegex,
            Func<IRequest, bool> predicate,
            float? timeout = default)
            => RunAndWaitAsync(action, page.WaitForRequestAsync(urlString, urlRegex, predicate, timeout));

        /// <summary>Run an action and wait for the next finished request.</summary>
        public static Task<IRequest> RunAndWaitForRequestFinishedAsync(this IPage page, Func<Task> action, float? timeout = default)
            => RunAndWaitAsync(action, page.WaitForRequestFinishedAsync(timeout: timeout));

        /// <summary>Run an action and wait for the next failed request.</summary>
        public static Task<IRequest> RunAndWaitForRequestFailedAsync(this IPage page, Func<Task> action, float? timeout = default)
            => RunAndWaitAsync(action, page.WaitForRequestFailedAsync(timeout: timeout));

        /// <summary>Run an action and wait for the next response.</summary>
        public static Task<IResponse> RunAndWaitForResponseAsync(this IPage page, Func<Task> action, float? timeout = default)
            => RunAndWaitForResponseAsync(page, action, null, null, null, timeout);

        /// <summary>Run an action and wait for a matching response.</summary>
        public static Task<IResponse> RunAndWaitForResponseAsync(
            this IPage page,
            Func<Task> action,
            string urlString,
            Regex urlRegex,
            Func<IResponse, bool> predicate,
            float? timeout = default)
            => RunAndWaitAsync(action, page.WaitForResponseAsync(urlString, urlRegex, predicate, timeout));

        /// <summary>Run an action and wait for the next navigation response.</summary>
        public static Task<IResponse> RunAndWaitForNavigationAsync(this IPage page, Func<Task> action, float? timeout = default)
            => RunAndWaitAsync(action, page.WaitForNavigationAsync(new PageWaitForNavigationOptions { Timeout = timeout }));

        /// <summary>Run an action and wait for the next download.</summary>
        public static Task<IDownload> RunAndWaitForDownloadAsync(this IPage page, Func<Task> action, float? timeout = default)
            => RunAndWaitAsync(action, page.WaitForEventAsync(PageEvent.Download, timeout: timeout));

        /// <summary>Run an action and wait for the next file chooser.</summary>
        public static Task<IFileChooser> RunAndWaitForFileChooserAsync(this IPage page, Func<Task> action, float? timeout = default)
            => RunAndWaitAsync(action, page.WaitForEventAsync(PageEvent.FileChooser, timeout: timeout));

        /// <summary>Run an action and wait for the next popup page.</summary>
        public static Task<IPage> RunAndWaitForPopupAsync(this IPage page, Func<Task> action, float? timeout = default)
            => RunAndWaitAsync(action, page.WaitForEventAsync(PageEvent.Popup, timeout: timeout));

        /// <summary>Run an action and wait for the next dialog.</summary>
        public static Task<IDialog> RunAndWaitForDialogAsync(this IPage page, Func<Task> action, float? timeout = default)
            => RunAndWaitAsync(action, page.WaitForEventAsync(PageEvent.Dialog, timeout: timeout));

        /// <summary>Run an action and wait for the next console message.</summary>
        public static Task<IConsoleMessage> RunAndWaitForConsoleMessageAsync(this IPage page, Func<Task> action, float? timeout = default)
            => RunAndWaitAsync(action, page.WaitForEventAsync(PageEvent.Console, timeout: timeout));

        /// <summary>Wait for the next finished request.</summary>
        public static Task<IRequest> WaitForRequestFinishedAsync(this IPage page, float? timeout = default)
            => page.WaitForEventAsync(PageEvent.RequestFinished, timeout: timeout);

        /// <summary>Wait for the next finished request on the context.</summary>
        public static Task<IRequest> WaitForRequestFinishedAsync(
            this IBrowserContext context,
            string urlString,
            Regex urlRegex,
            Func<IRequest, bool> predicate,
            float? timeout = default)
            => context.WaitForEventAsync(
                BrowserContextEvent.RequestFinished,
                r => predicate != null ? predicate(r) : UrlMatcher.Matches(r.Url, urlString, urlRegex, null, ContextBaseUrl(context)),
                timeout);

        /// <summary>Wait for the next finished request on the context.</summary>
        public static Task<IRequest> WaitForRequestFinishedAsync(this IBrowserContext context, string url, float? timeout = default)
            => WaitForRequestFinishedAsync(context, url, null, null, timeout);

        /// <summary>Wait for the next finished request on the context.</summary>
        public static Task<IRequest> WaitForRequestFinishedAsync(this IBrowserContext context, Regex url, float? timeout = default)
            => WaitForRequestFinishedAsync(context, null, url, null, timeout);

        /// <summary>Wait for the next finished request on the context.</summary>
        public static Task<IRequest> WaitForRequestFinishedAsync(this IBrowserContext context, Func<IRequest, bool> predicate, float? timeout = default)
            => WaitForRequestFinishedAsync(context, null, null, predicate, timeout);

        /// <summary>Wait for the next failed request on the context.</summary>
        public static Task<IRequest> WaitForRequestFailedAsync(
            this IBrowserContext context,
            string urlString,
            Regex urlRegex,
            Func<IRequest, bool> predicate,
            float? timeout = default)
            => context.WaitForEventAsync(
                BrowserContextEvent.RequestFailed,
                r => predicate != null ? predicate(r) : UrlMatcher.Matches(r.Url, urlString, urlRegex, null, ContextBaseUrl(context)),
                timeout);

        /// <summary>Wait for the next failed request on the context.</summary>
        public static Task<IRequest> WaitForRequestFailedAsync(this IBrowserContext context, string url, float? timeout = default)
            => WaitForRequestFailedAsync(context, url, null, null, timeout);

        /// <summary>Wait for the next failed request on the context.</summary>
        public static Task<IRequest> WaitForRequestFailedAsync(this IBrowserContext context, Regex url, float? timeout = default)
            => WaitForRequestFailedAsync(context, null, url, null, timeout);

        /// <summary>Wait for the next failed request on the context.</summary>
        public static Task<IRequest> WaitForRequestFailedAsync(this IBrowserContext context, Func<IRequest, bool> predicate, float? timeout = default)
            => WaitForRequestFailedAsync(context, null, null, predicate, timeout);

        /// <summary>Run an action and wait for the next context request.</summary>
        public static Task<IRequest> RunAndWaitForRequestAsync(
            this IBrowserContext context,
            Func<Task> action,
            string urlString,
            Regex urlRegex,
            Func<IRequest, bool> predicate,
            float? timeout = default)
            => RunAndWaitAsync(action, context.WaitForRequestAsync(urlString, urlRegex, predicate, timeout));

        /// <summary>Run an action and wait for the next context request.</summary>
        public static Task<IRequest> RunAndWaitForRequestAsync(this IBrowserContext context, Func<Task> action, float? timeout = default)
            => RunAndWaitForRequestAsync(context, action, null, null, null, timeout);

        /// <summary>Run an action and wait for the next context response.</summary>
        public static Task<IResponse> RunAndWaitForResponseAsync(
            this IBrowserContext context,
            Func<Task> action,
            string urlString,
            Regex urlRegex,
            Func<IResponse, bool> predicate,
            float? timeout = default)
            => RunAndWaitAsync(action, context.WaitForResponseAsync(urlString, urlRegex, predicate, timeout));

        /// <summary>Run an action and wait for the next context response.</summary>
        public static Task<IResponse> RunAndWaitForResponseAsync(this IBrowserContext context, Func<Task> action, float? timeout = default)
            => RunAndWaitForResponseAsync(context, action, null, null, null, timeout);

        /// <summary>Run an action and wait for the next finished context request.</summary>
        public static Task<IRequest> RunAndWaitForRequestFinishedAsync(this IBrowserContext context, Func<Task> action, float? timeout = default)
            => RunAndWaitAsync(action, context.WaitForRequestFinishedAsync(timeout));

        /// <summary>Run an action and wait for the next failed context request.</summary>
        public static Task<IRequest> RunAndWaitForRequestFailedAsync(this IBrowserContext context, Func<Task> action, float? timeout = default)
            => RunAndWaitAsync(action, context.WaitForRequestFailedAsync(timeout));

        /// <summary>Run an action and wait for the next context download.</summary>
        public static Task<IDownload> RunAndWaitForDownloadAsync(this IBrowserContext context, Func<Task> action, float? timeout = default)
            => RunAndWaitAsync(action, context.WaitForEventAsync(BrowserContextEvent.Download, timeout: timeout));

        /// <summary>Run an action and wait for the next context page-close event.</summary>
        public static Task<IPage> RunAndWaitForPageCloseAsync(this IBrowserContext context, Func<Task> action, float? timeout = default)
            => RunAndWaitAsync(action, context.WaitForEventAsync(BrowserContextEvent.PageClose, timeout: timeout));

        /// <summary>Run an action and wait for the next context page-load event.</summary>
        public static Task<IPage> RunAndWaitForPageLoadAsync(this IBrowserContext context, Func<Task> action, float? timeout = default)
            => RunAndWaitAsync(action, context.WaitForEventAsync(BrowserContextEvent.PageLoad, timeout: timeout));

        /// <summary>Run an action and wait for the next context frame navigated event.</summary>
        public static Task<IFrame> RunAndWaitForFrameNavigatedAsync(this IBrowserContext context, Func<Task> action, float? timeout = default)
            => RunAndWaitAsync(action, context.WaitForEventAsync(BrowserContextEvent.FrameNavigated, timeout: timeout));

        /// <summary>Run an action and wait for the next context frame attached event.</summary>
        public static Task<IFrame> RunAndWaitForFrameAttachedAsync(this IBrowserContext context, Func<Task> action, float? timeout = default)
            => RunAndWaitAsync(action, context.WaitForEventAsync(BrowserContextEvent.FrameAttached, timeout: timeout));

        /// <summary>Run an action and wait for the next context frame detached event.</summary>
        public static Task<IFrame> RunAndWaitForFrameDetachedAsync(this IBrowserContext context, Func<Task> action, float? timeout = default)
            => RunAndWaitAsync(action, context.WaitForEventAsync(BrowserContextEvent.FrameDetached, timeout: timeout));

        /// <summary>Run an action and wait for the next service worker.</summary>
        public static Task<IWorker> RunAndWaitForServiceWorkerAsync(this IBrowserContext context, Func<Task> action, float? timeout = default)
            => RunAndWaitAsync(action, context.WaitForEventAsync(BrowserContextEvent.ServiceWorker, timeout: timeout));

        /// <summary>Run an action and wait for the next web error.</summary>
        public static Task<IWebError> RunAndWaitForWebErrorAsync(this IBrowserContext context, Func<Task> action, float? timeout = default)
            => RunAndWaitAsync(action, context.WaitForEventAsync(BrowserContextEvent.WebError, timeout: timeout));

        /// <summary>Legacy route-from-HAR with URL string filter.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task RouteFromHARAsync(
            this IPage page,
            string har,
            string url,
            PageRouteFromHAROptions options = default)
            => page.RouteFromHARAsync(har, new PageRouteFromHAROptions
            {
                Url = url,
                NotFound = options?.NotFound,
                Update = options?.Update,
                UpdateMode = options?.UpdateMode,
                UpdateContent = options?.UpdateContent,
            });

        private static async Task<T> RunAndWaitAsync<T>(Func<Task> action, Task<T> waitTask)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            Task actionTask = action();
            T result = await waitTask.ConfigureAwait(false);
            await actionTask.ConfigureAwait(false);
            return result;
        }

        private static string ContextBaseUrl(IBrowserContext context)
            => context is IHasBaseUrl has ? has.BaseURL : null;
    }
}
