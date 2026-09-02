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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Shared waiter for <c>page.waitForURL</c>. If the current URL already matches,
    /// waits only for the requested load state. Otherwise polls until the URL matches,
    /// then waits for the load state. Mirrors upstream Frame.waitForURL.
    /// </summary>
    internal static class WaitForUrlHelper
    {
        /// <summary>
        /// Waits until the page URL matches and the requested load state is reached.
        /// </summary>
        /// <param name="getUrl">Returns the current main-frame URL.</param>
        /// <param name="waitForLoadStateAsync">Waits for a load state on the page.</param>
        /// <param name="urlString">Glob pattern or exact URL.</param>
        /// <param name="urlRegex">Regular expression matcher.</param>
        /// <param name="urlFunc">Predicate matcher.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <param name="waitUntil">Load state to wait for after the URL matches.</param>
        /// <param name="apiName">Name used in the timeout message.</param>
        /// <param name="baseUrl">Optional context <c>baseURL</c> for relative globs.</param>
        /// <returns>A task that completes when the URL and load state are reached.</returns>
        internal static async Task WaitAsync(
            Func<string> getUrl,
            Func<LoadState, float?, Task> waitForLoadStateAsync,
            string urlString,
            Regex urlRegex,
            Func<string, bool> urlFunc,
            float? timeout,
            WaitUntilState waitUntil,
            string apiName = "page.waitForURL",
            string baseUrl = null)
        {
            if (getUrl == null)
            {
                throw new ArgumentNullException(nameof(getUrl));
            }

            if (waitForLoadStateAsync == null)
            {
                throw new ArgumentNullException(nameof(waitForLoadStateAsync));
            }

            LoadState loadState = ToLoadState(waitUntil);
            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();

            if (UrlMatcher.Matches(getUrl(), urlString, urlRegex, urlFunc, baseUrl))
            {
                await WaitForLoadIfNeededAsync(waitForLoadStateAsync, loadState, waitUntil, RemainingTimeout(timeoutMs, sw)).ConfigureAwait(false);
                return;
            }

            while (true)
            {
                if (UrlMatcher.Matches(getUrl(), urlString, urlRegex, urlFunc, baseUrl))
                {
                    await WaitForLoadIfNeededAsync(waitForLoadStateAsync, loadState, waitUntil, RemainingTimeout(timeoutMs, sw)).ConfigureAwait(false);
                    return;
                }

                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    throw new TimeoutException($"{apiName}: Timeout {timeoutMs}ms exceeded.");
                }

                await Task.Delay(20).ConfigureAwait(false);
            }
        }

        private static Task WaitForLoadIfNeededAsync(
            Func<LoadState, float?, Task> waitForLoadStateAsync,
            LoadState loadState,
            WaitUntilState waitUntil,
            float? timeout)
        {
            // Official waitForURL({ waitUntil: 'commit' }) resolves at navigation
            // commit and does not wait for load (hanging subresources stay pending).
            if (waitUntil == WaitUntilState.Commit)
            {
                return Task.CompletedTask;
            }

            return waitForLoadStateAsync(loadState, timeout);
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
            if (timeoutMs == Timeout.Infinite)
            {
                return 0;
            }

            int left = timeoutMs - (int)sw.ElapsedMilliseconds;
            return left < 1 ? 1 : left;
        }
    }
}
