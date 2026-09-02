/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Official click auto-wait for navigations scheduled by the pointer
    /// action (<c>waitForSignalsCreatedBy</c> / <c>SignalBarrier</c>).
    /// Waits until those navigations commit, not until <c>load</c>.
    /// </summary>
    internal static class ActionSignals
    {
        private const string SameDocumentToken = "__pwSignalSameDocument";

        /// <summary>
        /// Runs <paramref name="action"/> and, when <paramref name="waitAfter"/>
        /// is <see langword="true"/>, waits for any main-frame navigation the
        /// action scheduled to commit.
        /// </summary>
        /// <param name="hub">Frame-manager barrier list.</param>
        /// <param name="epilogueAsync">
        /// Protocol flush after the action (Chromium <c>Page.enable</c>).
        /// </param>
        /// <param name="waitAfter">
        /// When <see langword="false"/>, return as soon as the action finishes
        /// (dblclick / <c>noWaitAfter</c>).
        /// </param>
        /// <param name="timeout">Click timeout in milliseconds.</param>
        /// <param name="action">The pointer action.</param>
        /// <param name="page">
        /// Optional page used to observe navigation <c>Request</c> events when
        /// the browser does not emit a policy-check signal (WebKit forms).
        /// </param>
        /// <param name="commitSameDocumentUrl">
        /// Commits a same-document navigation when the live URL changed
        /// without a protocol event (WebKit Navigation API intercept).
        /// </param>
        /// <returns>A task that completes when the action and wait finish.</returns>
        internal static async Task RunAsync(
            ActionSignalHubState hub,
            Func<Task> epilogueAsync,
            bool waitAfter,
            float? timeout,
            Func<Task> action,
            IPage page = null,
            Action<string> commitSameDocumentUrl = null)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (!waitAfter || hub == null)
            {
                await action().ConfigureAwait(false);
                return;
            }

            ActionSignalBarrier barrier = new ActionSignalBarrier();
            hub.AddBarrier(barrier);
            bool sawDocumentRequest = false;
            bool sawDownload = false;
            void OnRequest(object sender, IRequest request)
            {
                if (sawDownload || request?.IsNavigationRequest != true)
                {
                    return;
                }

                IFrame frame = null;
                try
                {
                    frame = request.Frame;
                }
                catch (PlaywrightSharpException)
                {
                    return;
                }

                if (frame?.ParentFrame != null)
                {
                    return;
                }

                IPage requestPage = null;
                try
                {
                    requestPage = frame?.Page;
                }
                catch (PlaywrightSharpException)
                {
                    return;
                }

                if (requestPage != null && !ReferenceEquals(requestPage, page))
                {
                    return;
                }

                string url = request.Url ?? string.Empty;
                if (url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                sawDocumentRequest = true;
                hub.ExpectMainFrameNavigation();
            }

            void OnRequestFailed(object sender, IRequest request)
            {
                if (request?.IsNavigationRequest != true)
                {
                    return;
                }

                IFrame failedFrame = null;
                try
                {
                    failedFrame = request.Frame;
                }
                catch (PlaywrightSharpException)
                {
                    return;
                }

                if (failedFrame?.ParentFrame != null)
                {
                    return;
                }

                hub.OnMainFrameNavigated();
            }

            void OnDownload(object sender, IDownload download)
            {
                // Official SignalBarrier: a download resolves the click wait
                // instead of a document commit (library/browsercontext-events).
                sawDownload = true;
                hub.OnMainFrameNavigated();
            }

            if (page != null)
            {
                page.Request += OnRequest;
                page.RequestFailed += OnRequestFailed;
                page.Download += OnDownload;
            }

            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                await StampSameDocumentAsync(page, commitSameDocumentUrl).ConfigureAwait(false);
                Task waitAfterTask = WaitAfterActionAsync(
                    action,
                    epilogueAsync,
                    page,
                    commitSameDocumentUrl,
                    timeout,
                    sw,
                    barrier,
                    () => sawDocumentRequest);
                await WaitForOrTimeoutAsync(waitAfterTask, timeout, sw).ConfigureAwait(false);
            }
            finally
            {
                if (page != null)
                {
                    page.Request -= OnRequest;
                    page.RequestFailed -= OnRequestFailed;
                    page.Download -= OnDownload;
                }

                hub.RemoveBarrier(barrier);
            }
        }

        private static async Task WaitAfterActionAsync(
            Func<Task> action,
            Func<Task> epilogueAsync,
            IPage page,
            Action<string> commitSameDocumentUrl,
            float? timeout,
            Stopwatch sw,
            ActionSignalBarrier barrier,
            Func<bool> sawDocumentRequest)
        {
            await action().ConfigureAwait(false);
            if (epilogueAsync != null)
            {
                try
                {
                    await epilogueAsync().ConfigureAwait(false);
                }
                catch (PlaywrightSharpException)
                {
                }
            }

            // WebKit form navigations often request after the input command
            // returns. Hold the constructor retain until that signal lands.
            await Task.Delay(16).ConfigureAwait(false);
            await TryCommitMissedSameDocumentAsync(
                page,
                commitSameDocumentUrl,
                timeout,
                sw,
                sawDocumentRequest).ConfigureAwait(false);
            await barrier.WaitForAsync(timeout).ConfigureAwait(false);

            // Official waits one extra task so public framenavigated
            // listeners run before click() resolves.
            await Task.Delay(1).ConfigureAwait(false);
        }

        private static async Task WaitForOrTimeoutAsync(Task task, float? timeout, Stopwatch sw)
        {
            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            if (timeoutMs == Timeout.Infinite)
            {
                await task.ConfigureAwait(false);
                return;
            }

            int remaining = timeoutMs - (int)(sw?.ElapsedMilliseconds ?? 0);
            if (remaining <= 0)
            {
                throw ClickTimeout(timeoutMs);
            }

            Task delay = Task.Delay(remaining);
            if (await Task.WhenAny(task, delay).ConfigureAwait(false) != task)
            {
                throw ClickTimeout(timeoutMs);
            }

            await task.ConfigureAwait(false);
        }

        private static async Task StampSameDocumentAsync(IPage page, Action<string> commitSameDocumentUrl)
        {
            if (page == null || commitSameDocumentUrl == null)
            {
                return;
            }

            try
            {
                await page.EvaluateAsync<object>("window." + SameDocumentToken + " = true").ConfigureAwait(false);
            }
            catch (PlaywrightSharpException)
            {
            }
        }

        private static async Task TryCommitMissedSameDocumentAsync(
            IPage page,
            Action<string> commitSameDocumentUrl,
            float? timeout,
            Stopwatch sw,
            Func<bool> sawDocumentRequest)
        {
            if (page == null || commitSameDocumentUrl == null)
            {
                return;
            }

            if (sawDocumentRequest != null && sawDocumentRequest())
            {
                return;
            }

            string tracked = page.Url ?? string.Empty;
            string live;
            try
            {
                Task<string> liveTask = page.EvaluateAsync<string>(
                    "() => window." + SameDocumentToken + " === true ? document.location.href : ''");
                live = await WaitForEvaluateAsync(liveTask, timeout, sw).ConfigureAwait(false);
            }
            catch (PlaywrightSharpException)
            {
                return;
            }

            if (string.IsNullOrEmpty(live) || string.Equals(tracked, live, StringComparison.Ordinal))
            {
                return;
            }

            commitSameDocumentUrl(live);
        }

        private static async Task<string> WaitForEvaluateAsync(Task<string> liveTask, float? timeout, Stopwatch sw)
        {
            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            if (timeoutMs == Timeout.Infinite)
            {
                return await liveTask.ConfigureAwait(false);
            }

            int remaining = timeoutMs - (int)(sw?.ElapsedMilliseconds ?? 0);
            if (remaining <= 0)
            {
                throw ClickTimeout(timeoutMs);
            }

            Task delay = Task.Delay(remaining);
            if (await Task.WhenAny(liveTask, delay).ConfigureAwait(false) != liveTask)
            {
                throw ClickTimeout(timeoutMs);
            }

            return await liveTask.ConfigureAwait(false);
        }

        private static TimeoutException ClickTimeout(int timeoutMs)
        {
            string apiName = ClickAction.ApiName.Value;
            if (string.IsNullOrEmpty(apiName))
            {
                apiName = "page.click";
            }

            return new TimeoutException(
                apiName + ": Timeout " + timeoutMs.ToString(CultureInfo.InvariantCulture) + "ms exceeded.");
        }
    }
}
