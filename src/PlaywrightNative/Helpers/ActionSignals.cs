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
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
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
        /// <param name="expectNavigation">
        /// When <see langword="true"/>, WebKit uses a longer empty poll for
        /// late form GETs (submit / link clicks).
        /// </param>
        /// <returns>A task that completes when the action and wait finish.</returns>
        internal static async Task RunAsync(
            ActionSignalHubState hub,
            Func<Task> epilogueAsync,
            bool waitAfter,
            float? timeout,
            Func<Task> action,
            IPage page = null,
            Action<string> commitSameDocumentUrl = null,
            bool expectNavigation = false)
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
                catch (PlaywrightException)
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
                catch (PlaywrightException)
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
                hub.ExpectMainFrameNavigation(fromDocumentRequest: true);
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
                catch (PlaywrightException)
                {
                    return;
                }

                if (failedFrame?.ParentFrame != null)
                {
                    return;
                }

                // Redirect / superseded aborts ("cancelled", "interrupted") must
                // drop only the failed document retain. A terminal failure
                // (bad TLS certificate) never commits, and leaving the
                // willCheck policy retains armed hangs click() for the full
                // timeout (clicking on links which do not commit navigation).
                if (IsSupersededNavigationFailure(request.Failure))
                {
                    hub.OnDocumentNavigationAborted();
                }
                else
                {
                    hub.OnTerminalDocumentNavigationFailed();
                }
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
                    () => sawDocumentRequest,
                    expectNavigation);
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
            Func<bool> sawDocumentRequest,
            bool expectNavigation)
        {
            await action().ConfigureAwait(false);

            // Snapshot URL before WebKit's async form/link navigation lands so
            // expectNavigation can wait for a real commit, not just a barrier
            // release from a cancelled willCheck (ShouldWorkWithGotoFollowingClick).
            string urlAfterAction = page?.Url ?? string.Empty;

            // WebKit processes form submits asynchronously after Input.dispatch*
            // returns. A rAF pair lets willCheck / Network land before Page.enable.
            // Navigable targets (expectNavigation) get a longer empty ceiling;
            // ordinary buttons stay short so Darwin multi-click / scroll=none survive.
            bool chromiumPage = string.Equals(page?.GetType().Name, "Page", StringComparison.Ordinal);
            if (!chromiumPage && page != null)
            {
                try
                {
                    await page.EvaluateAsync<object>(
                        "() => new Promise(f => requestAnimationFrame(() => requestAnimationFrame(f)))")
                        .ConfigureAwait(false);
                }
                catch (PlaywrightException)
                {
                }
            }

            if (epilogueAsync != null)
            {
                try
                {
                    await epilogueAsync().ConfigureAwait(false);
                }
                catch (PlaywrightException)
                {
                }
            }

            // Chromium: 16×16ms default; form/link expectNavigation gets the longer
            // WebKit-style ceiling so late document requests under Windows suite load
            // still arm the barrier before DropOrphanedPolicyNavigations.
            // WebKit buttons: 8×16ms ≈ 128ms. WebKit submit/link/form: 64×16ms ≈ 1s.
            int pollLimit = chromiumPage
                ? (expectNavigation ? 64 : 16)
                : (expectNavigation ? 64 : 8);
            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            bool sawNavigationSignal = false;
            for (int i = 0; i < pollLimit; i++)
            {
                if (sawDocumentRequest != null && sawDocumentRequest())
                {
                    sawNavigationSignal = true;
                    break;
                }

                if (barrier != null && barrier.HasPendingNavigations)
                {
                    sawNavigationSignal = true;

                    // Navigable WebKit clicks must not early-break on willCheck alone:
                    // didCheck cancel releases that retain before the form GET is
                    // retained, and WaitForAsync would then return too early.
                    if (!expectNavigation)
                    {
                        break;
                    }
                }

                // Darwin clicks spend most of a 2s timeout in hit-testing.
                // A long empty poll after pressAsync then fails the click
                // even when the pointer action already succeeded.
                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds + 16 >= timeoutMs)
                {
                    break;
                }

                await Task.Delay(16).ConfigureAwait(false);
            }

            await TryCommitMissedSameDocumentAsync(
                page,
                commitSameDocumentUrl,
                timeout,
                sw,
                sawDocumentRequest).ConfigureAwait(false);

            // Speculative WebKit willCheck retains without a document request
            // must not block non-navigating clicks (scroll=none 2s budgets).
            // Drop once before waiting, then keep dropping while WaitForAsync
            // runs — a late willCheck after the first drop re-arms the barrier
            // (ShouldClickInViewportElementWhenScrollIsNone under Darwin load).
            if (!expectNavigation
                && barrier != null
                && (sawDocumentRequest == null || !sawDocumentRequest()))
            {
                barrier.DropOrphanedPolicyNavigations();
                await WaitBarrierDroppingOrphansAsync(barrier, timeout, sawDocumentRequest)
                    .ConfigureAwait(false);
            }
            else
            {
                await barrier.WaitForAsync(timeout).ConfigureAwait(false);
            }

            // Navigable WebKit clicks: wait for a non-blank main-frame navigation
            // to settle (FrameNavigated + load). Returning on a provisional URL /
            // barrier idle alone races a following goto
            // (ShouldWorkWithGotoFollowingClick under suite load).
            if (!chromiumPage
                && page != null
                && (expectNavigation || sawNavigationSignal))
            {
                await WaitForWebKitNavigationSettleAsync(
                    page,
                    urlAfterAction,
                    expectNavigation,
                    sawDocumentRequest,
                    barrier,
                    timeout,
                    sw).ConfigureAwait(false);
            }

            // Official waits one extra task so public framenavigated
            // listeners run before click() resolves.
            await Task.Delay(1).ConfigureAwait(false);
        }

        private static async Task WaitForWebKitNavigationSettleAsync(
            IPage page,
            string urlAfterAction,
            bool expectNavigation,
            Func<bool> sawDocumentRequest,
            ActionSignalBarrier barrier,
            float? timeout,
            Stopwatch sw)
        {
            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            int settleMs = expectNavigation ? 2000 : 640;
            if (timeoutMs != Timeout.Infinite)
            {
                int remaining = timeoutMs - (int)sw.ElapsedMilliseconds;
                if (remaining <= 0)
                {
                    return;
                }

                settleMs = Math.Min(settleMs, remaining);
            }

            Stopwatch settleWatch = Stopwatch.StartNew();
            while (settleWatch.ElapsedMilliseconds < settleMs)
            {
                if (IsCommittedNavigationUrlChange(urlAfterAction, page.Url ?? string.Empty))
                {
                    if (barrier != null && barrier.HasPendingNavigations)
                    {
                        await barrier.WaitUntilIdleAsync(timeout).ConfigureAwait(false);
                    }

                    // Give the document commit a beat after the public URL flips
                    // so a following goto is not interrupted by the form GET
                    // (ShouldWorkWithGotoFollowingClick under suite load).
                    await Task.Delay(50).ConfigureAwait(false);
                    return;
                }

                if (sawDocumentRequest != null && sawDocumentRequest())
                {
                    await barrier.WaitUntilIdleAsync(timeout).ConfigureAwait(false);
                    if (IsCommittedNavigationUrlChange(urlAfterAction, page.Url ?? string.Empty))
                    {
                        await Task.Delay(50).ConfigureAwait(false);
                        return;
                    }
                }
                else if (barrier != null && barrier.HasPendingNavigations)
                {
                    await barrier.WaitUntilIdleAsync(timeout).ConfigureAwait(false);
                    continue;
                }

                await Task.Delay(16).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Waits for the signal barrier while repeatedly dropping orphaned
        /// WebKit <c>willCheck</c> policy retains when no document request
        /// has been observed. A single pre-wait drop is not enough: late
        /// policy checks reopen <see cref="ActionSignalBarrier.WaitForAsync"/>.
        /// </summary>
        private static async Task WaitBarrierDroppingOrphansAsync(
            ActionSignalBarrier barrier,
            float? timeout,
            Func<bool> sawDocumentRequest)
        {
            Task wait = barrier.WaitForAsync(timeout);
            while (!wait.IsCompleted)
            {
                if (sawDocumentRequest == null || !sawDocumentRequest())
                {
                    barrier.DropOrphanedPolicyNavigations();
                }

                await Task.WhenAny(wait, Task.Delay(16)).ConfigureAwait(false);
            }

            await wait.ConfigureAwait(false);
        }

        /// <summary>
        /// Returns whether <paramref name="currentUrl"/> reflects a real
        /// cross-document navigation away from <paramref name="urlAfterAction"/>.
        /// Ignores blank churn (<c>""</c> ↔ <c>about:blank</c>).
        /// </summary>
        private static bool IsCommittedNavigationUrlChange(string urlAfterAction, string currentUrl)
        {
            if (string.IsNullOrEmpty(currentUrl) || PopupOpenedHelper.IsBlankUrl(currentUrl))
            {
                return false;
            }

            string baseline = urlAfterAction ?? string.Empty;
            if (PopupOpenedHelper.IsBlankUrl(baseline))
            {
                return true;
            }

            return !string.Equals(currentUrl, baseline, StringComparison.Ordinal);
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
            catch (PlaywrightException)
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
            catch (PlaywrightException)
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

        private static bool IsSupersededNavigationFailure(string reason)
            => !string.IsNullOrEmpty(reason)
                && (reason.Contains("interrupted", StringComparison.OrdinalIgnoreCase)
                    || reason.Contains("cancelled", StringComparison.OrdinalIgnoreCase)
                    || reason.Contains("canceled", StringComparison.OrdinalIgnoreCase))
                && reason.IndexOf("certificate", StringComparison.OrdinalIgnoreCase) < 0
                && reason.IndexOf("SSL", StringComparison.OrdinalIgnoreCase) < 0
                && reason.IndexOf("TLS", StringComparison.OrdinalIgnoreCase) < 0;

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
