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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Page-scoped overlay handlers for <see cref="IPage.AddLocatorHandlerAsync(ILocator, Func{ILocator, Task}, int?, bool?)"/>.
    /// </summary>
    internal static class LocatorHandlers
    {
        private static readonly ConditionalWeakTable<IPage, Registry> _registries = new ConditionalWeakTable<IPage, Registry>();

        /// <summary>
        /// Registers a handler on <paramref name="page"/>.
        /// </summary>
        /// <param name="page">The page that owns the handler.</param>
        /// <param name="locator">Locator that triggers the handler when visible.</param>
        /// <param name="handler">Callback that should dismiss the overlay.</param>
        /// <param name="times">Optional maximum invocations.</param>
        /// <param name="noWaitAfter">When <see langword="true"/>, do not wait for the overlay to hide.</param>
        internal static void Add(IPage page, ILocator locator, Func<ILocator, Task> handler, int? times, bool? noWaitAfter)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (locator == null)
            {
                throw new ArgumentNullException(nameof(locator));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (times.HasValue)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(times.Value);
            }

            Registry registry = _registries.GetOrCreateValue(page);
            registry.EverAdded = true;
            registry.Entries.Add(new Entry
            {
                Locator = locator,
                Handler = handler,
                Remaining = times,
                NoWaitAfter = noWaitAfter == true,
            });
        }

        /// <summary>
        /// Removes handlers registered with <paramref name="locator"/>.
        /// </summary>
        /// <param name="page">The page that owns the handler.</param>
        /// <param name="locator">The same locator instance or an equal selector string.</param>
        internal static void Remove(IPage page, ILocator locator)
        {
            if (page == null || locator == null)
            {
                return;
            }

            if (!_registries.TryGetValue(page, out Registry registry))
            {
                return;
            }

            string selector = locator.ToString();
            registry.Entries.RemoveAll(entry =>
                ReferenceEquals(entry.Locator, locator)
                || (entry.Locator != null
                    && string.Equals(entry.Locator.ToString(), selector, StringComparison.Ordinal)));
        }

        /// <summary>
        /// Returns whether <paramref name="page"/> has any registered handlers.
        /// </summary>
        /// <param name="page">The page to inspect.</param>
        /// <returns><see langword="true"/> when at least one handler is registered.</returns>
        internal static bool Has(IPage page)
        {
            if (page == null || !_registries.TryGetValue(page, out Registry registry))
            {
                return false;
            }

            return registry.Entries.Count > 0;
        }

        /// <summary>
        /// Returns whether <paramref name="page"/> ever registered a handler.
        /// Pointer prepare still hovers after <c>times</c> is exhausted.
        /// </summary>
        /// <param name="page">The page to inspect.</param>
        /// <returns><see langword="true"/> after the first <see cref="Add"/>.</returns>
        internal static bool ShouldHover(IPage page)
        {
            if (page == null || !_registries.TryGetValue(page, out Registry registry))
            {
                return false;
            }

            return registry.EverAdded;
        }

        /// <summary>
        /// Runs visible overlay handlers before an action.
        /// </summary>
        /// <param name="page">The page performing the action.</param>
        /// <returns>A task that completes when handlers have run.</returns>
        internal static Task RunAsync(IPage page)
            => RunAsync(page, timeout: null);

        /// <summary>
        /// Runs visible overlay handlers before an action, using the remaining
        /// budget from <paramref name="timeoutMs"/> and <paramref name="sw"/>.
        /// </summary>
        /// <param name="page">The page performing the action.</param>
        /// <param name="timeoutMs">Resolved timeout in milliseconds, or <see cref="Timeout.Infinite"/>.</param>
        /// <param name="sw">Stopwatch started when the action/expect began.</param>
        /// <returns>A task that completes when handlers have run.</returns>
        internal static Task RunAsync(IPage page, int timeoutMs, Stopwatch sw)
        {
            if (sw == null)
            {
                throw new ArgumentNullException(nameof(sw));
            }

            float? remaining = timeoutMs == Timeout.Infinite
                ? (float?)0
                : Math.Max(1, timeoutMs - (int)sw.ElapsedMilliseconds);
            return RunAsync(page, remaining);
        }

        /// <summary>
        /// Runs visible overlay handlers before an action, using
        /// <paramref name="timeout"/> when waiting for the overlay to hide.
        /// </summary>
        /// <param name="page">The page performing the action.</param>
        /// <param name="timeout">
        /// Remaining action timeout in milliseconds. <see langword="null"/> uses
        /// the default. <c>0</c> waits forever.
        /// </param>
        /// <returns>A task that completes when handlers have run.</returns>
        internal static async Task RunAsync(IPage page, float? timeout)
        {
            if (page == null || !_registries.TryGetValue(page, out Registry registry))
            {
                return;
            }

            if (registry.Running)
            {
                return;
            }

            registry.Running = true;
            try
            {
                int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
                Stopwatch sw = Stopwatch.StartNew();
                List<Entry> snapshot = new List<Entry>(registry.Entries);
                foreach (Entry entry in snapshot)
                {
                    if (!registry.Entries.Contains(entry))
                    {
                        continue;
                    }

                    if (entry.Remaining.HasValue && entry.Remaining.Value <= 0)
                    {
                        registry.Entries.Remove(entry);
                        continue;
                    }

                    int queryMs = RemainingQueryMs(timeoutMs, sw);

                    // Unbounded ElementHandles during navigation can hang the
                    // whole expect/action loop; treat a timed-out probe as not
                    // visible so we skip the handler instead of blocking forever.
                    if (!await IsAnyVisibleAsync(entry.Locator, queryMs, assumeVisibleOnTimeout: false)
                        .ConfigureAwait(false))
                    {
                        continue;
                    }

                    await entry.Handler(entry.Locator).ConfigureAwait(false);

                    if (entry.Remaining.HasValue)
                    {
                        entry.Remaining = entry.Remaining.Value - 1;
                        if (entry.Remaining.Value <= 0)
                        {
                            registry.Entries.Remove(entry);
                        }
                    }

                    if (!entry.NoWaitAfter)
                    {
                        float? remaining = timeoutMs == Timeout.Infinite
                            ? (float?)0
                            : Math.Max(1, timeoutMs - (int)sw.ElapsedMilliseconds);
                        await WaitHiddenAsync(entry.Locator, remaining).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                registry.Running = false;
            }
        }

        private static int RemainingQueryMs(int timeoutMs, Stopwatch sw)
        {
            if (timeoutMs == Timeout.Infinite)
            {
                return 5_000;
            }

            return Math.Max(50, Math.Min(5_000, timeoutMs - (int)sw.ElapsedMilliseconds));
        }

        private static async Task<bool> IsAnyVisibleAsync(
            ILocator locator,
            int queryTimeoutMs,
            bool assumeVisibleOnTimeout)
        {
            IReadOnlyList<IElementHandle> handles;
            try
            {
                handles = await locator.ElementHandlesAsync()
                    .WaitAsync(TimeSpan.FromMilliseconds(Math.Max(50, queryTimeoutMs)))
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return assumeVisibleOnTimeout;
            }
            catch (PlaywrightNativeException ex) when (ClosedTarget.IsClosed(ex))
            {
                throw;
            }
            catch (PlaywrightNativeException)
            {
                return false;
            }

            foreach (IElementHandle handle in handles)
            {
                try
                {
                    bool visible = await handle.IsVisibleAsync()
                        .WaitAsync(TimeSpan.FromMilliseconds(Math.Max(50, Math.Min(2_000, queryTimeoutMs))))
                        .ConfigureAwait(false);
                    if (visible)
                    {
                        return true;
                    }
                }
                catch (TimeoutException)
                {
                    if (assumeVisibleOnTimeout)
                    {
                        return true;
                    }
                }
                catch (PlaywrightNativeException ex) when (ClosedTarget.IsClosed(ex))
                {
                    throw;
                }
                catch (PlaywrightNativeException)
                {
                }
            }

            return false;
        }

        private static async Task WaitHiddenAsync(ILocator locator, float? timeout)
        {
            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();
            while (true)
            {
                // Check the wall clock before each probe so a hung visibility
                // query cannot prevent the handler hide timeout from firing.
                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    throw new TimeoutException(
                        "locator handler has finished, waiting for " + locator + " to be hidden");
                }

                int queryMs = RemainingQueryMs(timeoutMs, sw);
                bool visible = await IsAnyVisibleAsync(
                    locator,
                    queryMs,
                    assumeVisibleOnTimeout: true).ConfigureAwait(false);
                if (!visible)
                {
                    return;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        private sealed class Registry
        {
            internal List<Entry> Entries { get; } = new List<Entry>();

            internal bool Running { get; set; }

            internal bool EverAdded { get; set; }
        }

        private sealed class Entry
        {
            internal ILocator Locator { get; set; }

            internal Func<ILocator, Task> Handler { get; set; }

            internal int? Remaining { get; set; }

            internal bool NoWaitAfter { get; set; }
        }
    }
}
