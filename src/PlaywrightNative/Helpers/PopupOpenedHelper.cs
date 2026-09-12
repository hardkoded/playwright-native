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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Raises <see cref="IPage.Popup"/> after the new page is initialized or
    /// closed. Immediately-closed popups still emit; evaluate-able popups are
    /// ready when the event fires.
    /// </summary>
    internal static class PopupOpenedHelper
    {
        private static readonly ConcurrentDictionary<IPage, byte> Suppressed = new();

        /// <summary>
        /// Emits <paramref name="popup"/> on <paramref name="emit"/> after
        /// <paramref name="initialized"/> completes or the page closes.
        /// </summary>
        /// <param name="popup">The new page.</param>
        /// <param name="initialized">Page initialization task, or <see langword="null"/>.</param>
        /// <param name="emit">Raises the opener's <see cref="IPage.Popup"/> event.</param>
        internal static void EmitWhenReady(IPage popup, Task initialized, Action<IPage> emit)
            => EmitWhenReady(popup, initialized, emit, delayMs: 0);

        /// <summary>
        /// Emits <paramref name="popup"/> after an optional delay, then init or close.
        /// Delayed emits are skipped when <see cref="Suppress"/> was called.
        /// </summary>
        /// <param name="popup">The new page.</param>
        /// <param name="initialized">Page initialization task, or <see langword="null"/>.</param>
        /// <param name="emit">Raises the opener's <see cref="IPage.Popup"/> event.</param>
        /// <param name="delayMs">
        /// Milliseconds to wait before emitting. Used for inferred
        /// <c>about:blank</c> intermediates so a successor can promote.
        /// </param>
        internal static void EmitWhenReady(IPage popup, Task initialized, Action<IPage> emit, int delayMs)
        {
            if (popup == null)
            {
                throw new ArgumentNullException(nameof(popup));
            }

            if (emit == null)
            {
                throw new ArgumentNullException(nameof(emit));
            }

            _ = EmitAsync(popup, initialized, emit, delayMs);
        }

        /// <summary>
        /// Cancels a delayed emit for an intermediate noopener page.
        /// </summary>
        /// <param name="page">The intermediate page.</param>
        internal static void Suppress(IPage page)
        {
            if (page != null)
            {
                Suppressed.TryAdd(page, 0);
            }
        }

        /// <summary>
        /// Returns whether <paramref name="url"/> is empty or <c>about:blank</c>
        /// (including <c>about:blank#blocked</c>).
        /// </summary>
        /// <param name="url">The target URL.</param>
        /// <returns><see langword="true"/> when the URL is a blank document.</returns>
        internal static bool IsBlankUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return true;
            }

            return url.StartsWith("about:blank", StringComparison.Ordinal);
        }

        /// <summary>
        /// Official Chromium/WebKit initial empty document (<c>:</c> or
        /// <c>""</c>). <c>about:blank</c> is already a real navigation.
        /// </summary>
        /// <param name="url">The frame URL.</param>
        /// <returns><see langword="true"/> when the document is the initial empty page.</returns>
        internal static bool IsInitialEmptyDocumentUrl(string url)
            => string.IsNullOrEmpty(url) || string.Equals(url, ":", StringComparison.Ordinal);

        /// <summary>
        /// Public <see cref="IPage.Url"/> / <see cref="IFrame.Url"/> value.
        /// Internal frame state may be <c>""</c> for initial-empty detection;
        /// callers see official <c>about:blank</c>.
        /// </summary>
        /// <param name="url">Internal frame URL.</param>
        /// <returns>The URL exposed on the public API.</returns>
        internal static string PublicDocumentUrl(string url)
            => IsInitialEmptyDocumentUrl(url) ? "about:blank" : url;

        /// <summary>
        /// Returns the only sibling of <paramref name="popup"/>, or
        /// <see langword="null"/> when the set is empty or ambiguous.
        /// Used when CDP/WebKit omit <c>openerId</c> for <c>noopener</c> popups.
        /// </summary>
        /// <typeparam name="T">The page type.</typeparam>
        /// <param name="pages">Pages in the same context, including <paramref name="popup"/>.</param>
        /// <param name="popup">The newly attached page.</param>
        /// <returns>The inferred opener, or <see langword="null"/>.</returns>
        internal static T InferSoleSibling<T>(IEnumerable<T> pages, T popup)
            where T : class
        {
            if (pages == null)
            {
                return null;
            }

            T only = null;
            foreach (T page in pages)
            {
                if (ReferenceEquals(page, popup))
                {
                    continue;
                }

                if (only != null)
                {
                    return null;
                }

                only = page;
            }

            return only;
        }

        /// <summary>
        /// Returns the unique sibling that is waiting for <c>popup</c>, or
        /// <see cref="InferSoleSibling{T}"/> when no listener is unique.
        /// Used when a noopener successor arrives after a blank intermediate.
        /// </summary>
        /// <typeparam name="T">The page type.</typeparam>
        /// <param name="pages">Pages in the same context, including <paramref name="popup"/>.</param>
        /// <param name="popup">The newly attached page.</param>
        /// <param name="hasPopupListeners">Whether a sibling is waiting for <c>popup</c>.</param>
        /// <returns>The inferred opener, or <see langword="null"/>.</returns>
        internal static T InferListenerOrSoleSibling<T>(IEnumerable<T> pages, T popup, Func<T, bool> hasPopupListeners)
            where T : class
        {
            if (pages == null)
            {
                return InferSoleSibling(pages, popup);
            }

            T listener = null;
            int listeners = 0;
            foreach (T page in pages)
            {
                if (ReferenceEquals(page, popup) || hasPopupListeners == null || !hasPopupListeners(page))
                {
                    continue;
                }

                listener = page;
                listeners++;
            }

            return listeners == 1 ? listener : InferSoleSibling(pages, popup);
        }

        private static async Task EmitAsync(IPage popup, Task initialized, Action<IPage> emit, int delayMs)
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs).ConfigureAwait(false);
            }

            if (Suppressed.ContainsKey(popup))
            {
                return;
            }

            TaskCompletionSource<bool> closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<IPage> onClose = (_, _) => closed.TrySetResult(true);
            popup.Close += onClose;
            try
            {
                if (popup.IsClosed)
                {
                    closed.TrySetResult(true);
                }

                if (initialized != null)
                {
                    Task ready = initialized.ContinueWith(
                        static _ => 0,
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                    await Task.WhenAny(ready, closed.Task, Task.Delay(3_000)).ConfigureAwait(false);
                }
            }
            finally
            {
                popup.Close -= onClose;
            }

            if (Suppressed.ContainsKey(popup))
            {
                return;
            }

            emit(popup);
        }
    }
}
