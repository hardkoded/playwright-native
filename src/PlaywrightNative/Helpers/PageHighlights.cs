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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official highlight registry: multiple locator overlays, hide by
    /// locator, and re-apply after navigation.
    /// Extra optionals stay at the end.
    /// </summary>
    internal static class PageHighlights
    {
        private static readonly ConditionalWeakTable<IPage, Registry> _pages = new ConditionalWeakTable<IPage, Registry>();

        /// <summary>
        /// Remembers <paramref name="locator"/> so its overlay is restored
        /// after a later navigation.
        /// </summary>
        /// <param name="page">The page that owns the overlay.</param>
        /// <param name="locator">The highlighted locator.</param>
        /// <param name="style">Optional extra inline CSS.</param>
        /// <param name="id">Stable overlay id for this locator.</param>
        internal static void Remember(IPage page, ILocator locator, string style, string id)
        {
            if (page == null || locator == null || string.IsNullOrEmpty(id))
            {
                return;
            }

            Registry registry = _pages.GetOrCreateValue(page);
            lock (registry.Sync)
            {
                RemoveId(registry, id);
                registry.Entries.Add(new Entry
                {
                    Locator = locator,
                    Style = style,
                    Id = id,
                });

                if (!registry.Hooked)
                {
                    registry.Hooked = true;
                    page.Load += (_, _) => _ = ReapplyAsync(page);
                }
            }
        }

        /// <summary>
        /// Forgets one locator overlay after <c>locator.hideHighlight()</c>.
        /// </summary>
        /// <param name="page">The page that owns the overlay.</param>
        /// <param name="id">Stable overlay id for this locator.</param>
        internal static void Forget(IPage page, string id)
        {
            if (page == null || string.IsNullOrEmpty(id) || !_pages.TryGetValue(page, out Registry registry))
            {
                return;
            }

            lock (registry.Sync)
            {
                RemoveId(registry, id);
            }
        }

        /// <summary>
        /// Forgets every overlay after <c>page.hideHighlight()</c>.
        /// </summary>
        /// <param name="page">The page that owns the overlays.</param>
        internal static void Clear(IPage page)
        {
            if (page == null || !_pages.TryGetValue(page, out Registry registry))
            {
                return;
            }

            lock (registry.Sync)
            {
                registry.Entries.Clear();
            }
        }

        private static async Task ReapplyAsync(IPage page)
        {
            if (page == null || !_pages.TryGetValue(page, out Registry registry))
            {
                return;
            }

            List<Entry> copy;
            lock (registry.Sync)
            {
                copy = new List<Entry>(registry.Entries);
            }

            for (int i = 0; i < copy.Count; i++)
            {
                try
                {
                    if (copy[i].Locator is Locator sharp)
                    {
                        await sharp.HighlightInternalAsync(timeout: default, copy[i].Style).ConfigureAwait(false);
                    }
                    else
                    {
                        await copy[i].Locator.HighlightAsync(style: copy[i].Style).ConfigureAwait(false);
                    }
                }
                catch (PlaywrightNativeException)
                {
                }
            }
        }

        private static void RemoveId(Registry registry, string id)
        {
            for (int i = registry.Entries.Count - 1; i >= 0; i--)
            {
                if (string.Equals(registry.Entries[i].Id, id, StringComparison.Ordinal))
                {
                    registry.Entries.RemoveAt(i);
                }
            }
        }

        private sealed class Registry
        {
            internal object Sync { get; } = new object();

            internal List<Entry> Entries { get; } = new List<Entry>();

            internal bool Hooked { get; set; }
        }

        private sealed class Entry
        {
            internal ILocator Locator { get; set; }

            internal string Style { get; set; }

            internal string Id { get; set; }
        }
    }
}
