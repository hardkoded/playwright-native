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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Shared waiter for <c>page.waitForNavigation</c> / <c>frame.waitForNavigation</c>.
    /// Subscribes to the next matching frame navigation (unlike <c>waitForURL</c>,
    /// the current URL never resolves the wait) and then waits for the requested
    /// load state. <see cref="WaitUntilState.Commit"/> resolves as soon as the
    /// navigation commits. Failed navigations, SSL errors, and a detached frame
    /// reject the wait with the official log lines.
    /// </summary>
    internal static class WaitForNavigationHelper
    {
        /// <summary>
        /// Waits for the next navigation that matches the frame and URL filters.
        /// </summary>
        /// <param name="page">Page that owns the frame events.</param>
        /// <param name="waitForLoadStateAsync">Waits for a load state after the navigation commits.</param>
        /// <param name="urlString">Glob pattern or exact URL. All-null matchers match any navigation.</param>
        /// <param name="urlRegex">Regular expression matcher.</param>
        /// <param name="urlFunc">Predicate matcher.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <param name="waitUntil">Load state to wait for after the navigation commits.</param>
        /// <param name="isTargetFrame">
        /// When omitted, only the main frame is considered. Pass a predicate to wait
        /// for a specific frame (used by <c>frame.waitForNavigation</c>).
        /// </param>
        /// <param name="apiName">Name used in the timeout message.</param>
        /// <returns>
        /// The document response for the navigation, or <see langword="null"/> when
        /// there is no document response (same-document navigations).
        /// </returns>
        internal static async Task<IResponse> WaitAsync(
            IPage page,
            Func<LoadState, float?, Task> waitForLoadStateAsync,
            string urlString,
            Regex urlRegex,
            Func<string, bool> urlFunc,
            float? timeout,
            WaitUntilState waitUntil,
            Func<IFrame, bool> isTargetFrame = null,
            string apiName = "page.waitForNavigation")
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (waitForLoadStateAsync == null)
            {
                throw new ArgumentNullException(nameof(waitForLoadStateAsync));
            }

            Func<IFrame, bool> targetFrame = isTargetFrame ?? (frame => frame?.ParentFrame == null);
            List<string> navigatedUrls = new List<string>();
            IResponse captured = null;
            TaskCompletionSource<bool> navigatedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<Exception> failureTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnResponse(object sender, IResponse response)
            {
                if (response?.Request == null || !response.Request.IsNavigationRequest)
                {
                    return;
                }

                if (!targetFrame(response.Request.Frame))
                {
                    return;
                }

                if (!MatchesUrl(response.Url, urlString, urlRegex, urlFunc))
                {
                    return;
                }

                captured = response;
            }

            void OnNavigated(object sender, IFrame frame)
            {
                if (!targetFrame(frame))
                {
                    return;
                }

                navigatedUrls.Add(frame.Url ?? string.Empty);
                if (MatchesUrl(frame.Url, urlString, urlRegex, urlFunc))
                {
                    navigatedTcs.TrySetResult(true);
                }
            }

            void OnDetached(object sender, IFrame frame)
            {
                if (!targetFrame(frame))
                {
                    return;
                }

                failureTcs.TrySetException(
                    new PlaywrightNativeException(
                        WaitingLine(urlString, urlRegex, waitUntil) + Environment.NewLine + "frame was detached"));
            }

            void OnRequestFailed(object sender, IRequest request)
            {
                if (request == null || !request.IsNavigationRequest)
                {
                    return;
                }

                IFrame frame;
                try
                {
                    frame = request.Frame;
                }
                catch (PlaywrightNativeException)
                {
                    return;
                }

                if (!targetFrame(frame))
                {
                    return;
                }

                if (!MatchesUrl(request.Url, urlString, urlRegex, urlFunc))
                {
                    return;
                }

                string failure = string.IsNullOrEmpty(request.Failure)
                    ? "Navigation failed"
                    : request.Failure;
                if (IsRedirectAbort(failure))
                {
                    return;
                }

                failureTcs.TrySetException(new PlaywrightNativeException(failure));
            }

            page.Response += OnResponse;
            page.FrameNavigated += OnNavigated;
            page.FrameDetached += OnDetached;
            page.RequestFailed += OnRequestFailed;
            try
            {
                int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
                Stopwatch sw = Stopwatch.StartNew();
                await WaitForMatchOrFailureAsync(
                    navigatedTcs.Task,
                    failureTcs.Task,
                    timeoutMs,
                    apiName,
                    urlString,
                    urlRegex,
                    waitUntil,
                    navigatedUrls).ConfigureAwait(false);

                if (waitUntil != WaitUntilState.Commit)
                {
                    try
                    {
                        Task loadTask = waitForLoadStateAsync(ToLoadState(waitUntil), RemainingTimeout(timeoutMs, sw));
                        await WaitForMatchOrFailureAsync(
                            loadTask,
                            failureTcs.Task,
                            RemainingTimeoutMs(timeoutMs, sw),
                            apiName,
                            urlString,
                            urlRegex,
                            waitUntil,
                            navigatedUrls).ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                        throw BuildTimeout(apiName, timeoutMs, urlString, urlRegex, waitUntil, navigatedUrls);
                    }
                    catch (PlaywrightNativeException ex) when (ex.Message.Contains("frame was detached", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new PlaywrightNativeException(
                            WaitingLine(urlString, urlRegex, waitUntil) + Environment.NewLine + "frame was detached",
                            ex);
                    }
                }

                return captured;
            }
            finally
            {
                page.Response -= OnResponse;
                page.FrameNavigated -= OnNavigated;
                page.FrameDetached -= OnDetached;
                page.RequestFailed -= OnRequestFailed;
            }
        }

        private static async Task WaitForMatchOrFailureAsync(
            Task matchTask,
            Task<Exception> failureTask,
            int timeoutMs,
            string apiName,
            string urlString,
            Regex urlRegex,
            WaitUntilState waitUntil,
            List<string> navigatedUrls)
        {
            Task timeoutTask = timeoutMs == Timeout.Infinite
                ? new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously).Task
                : Task.Delay(timeoutMs);

            Task completed = await Task.WhenAny(matchTask, failureTask, timeoutTask).ConfigureAwait(false);
            if (completed == failureTask)
            {
                throw await failureTask.ConfigureAwait(false);
            }

            if (completed == matchTask)
            {
                await matchTask.ConfigureAwait(false);
                return;
            }

            throw BuildTimeout(apiName, timeoutMs, urlString, urlRegex, waitUntil, navigatedUrls);
        }

        private static TimeoutException BuildTimeout(
            string apiName,
            int timeoutMs,
            string urlString,
            Regex urlRegex,
            WaitUntilState waitUntil,
            List<string> navigatedUrls)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(apiName);
            builder.Append(": Timeout ");
            builder.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
            builder.Append("ms exceeded.");
            builder.Append(Environment.NewLine);
            builder.Append(WaitingLine(urlString, urlRegex, waitUntil));
            foreach (string url in navigatedUrls)
            {
                builder.Append(Environment.NewLine);
                builder.Append("navigated to \"");
                builder.Append(url);
                builder.Append('"');
            }

            return new TimeoutException(builder.ToString());
        }

        private static string WaitingLine(string urlString, Regex urlRegex, WaitUntilState waitUntil)
        {
            string until = NavigationTimeout.WaitUntilName(waitUntil);
            if (urlString != null)
            {
                return "waiting for navigation to \"" + urlString + "\" until \"" + until + "\"";
            }

            if (urlRegex != null)
            {
                return "waiting for navigation to \"" + urlRegex + "\" until \"" + until + "\"";
            }

            return "waiting for navigation until \"" + until + "\"";
        }

        private static bool IsRedirectAbort(string failure)
        {
            if (string.IsNullOrEmpty(failure))
            {
                return false;
            }

            return failure.Contains("interrupted", StringComparison.OrdinalIgnoreCase)
                || failure.Contains("ERR_ABORTED", StringComparison.OrdinalIgnoreCase)
                || failure.Contains("NS_BINDING_ABORTED", StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesUrl(string url, string urlString, Regex urlRegex, Func<string, bool> urlFunc)
        {
            if (urlString == null && urlRegex == null && urlFunc == null)
            {
                return true;
            }

            return UrlMatcher.Matches(url, urlString, urlRegex, urlFunc);
        }

        private static LoadState ToLoadState(WaitUntilState waitUntil)
        {
            return waitUntil switch
            {
                WaitUntilState.DOMContentLoaded => LoadState.DOMContentLoaded,
                WaitUntilState.NetworkIdle => LoadState.NetworkIdle,
                _ => LoadState.Load,
            };
        }

        private static float? RemainingTimeout(int timeoutMs, Stopwatch sw)
        {
            int left = RemainingTimeoutMs(timeoutMs, sw);
            if (left == Timeout.Infinite)
            {
                return 0;
            }

            return left < 1 ? 1 : left;
        }

        private static int RemainingTimeoutMs(int timeoutMs, Stopwatch sw)
        {
            if (timeoutMs == Timeout.Infinite)
            {
                return Timeout.Infinite;
            }

            int left = timeoutMs - (int)sw.ElapsedMilliseconds;
            return left < 1 ? 1 : left;
        }
    }
}
