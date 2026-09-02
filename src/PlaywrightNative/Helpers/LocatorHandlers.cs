/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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

                    if (!await IsAnyVisibleAsync(entry.Locator).ConfigureAwait(false))
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
                        await WaitHiddenAsync(entry.Locator, timeout).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                registry.Running = false;
            }
        }

        private static async Task<bool> IsAnyVisibleAsync(ILocator locator)
        {
            IReadOnlyList<IElementHandle> handles;
            try
            {
                handles = await locator.ElementHandlesAsync().ConfigureAwait(false);
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
                    if (await handle.IsVisibleAsync().ConfigureAwait(false))
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
            while (await IsAnyVisibleAsync(locator).ConfigureAwait(false))
            {
                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    throw new TimeoutException(
                        "locator handler has finished, waiting for " + locator + " to be hidden");
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
