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
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Polls an existing element handle until it reaches a requested
    /// <see cref="ElementState"/>.
    /// </summary>
    internal static class WaitForElementStateHelper
    {
        /// <summary>
        /// Waits until <paramref name="handle"/> satisfies <paramref name="state"/>.
        /// </summary>
        /// <param name="handle">The element to observe.</param>
        /// <param name="state">Target state. <see cref="EnumCompat.UndefinedElementState"/> means visible.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <returns>A task that completes when the state is reached.</returns>
        internal static async Task WaitAsync(IElementHandle handle, ElementState state, float? timeout)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            ElementState wanted = state == EnumCompat.UndefinedElementState ? ElementState.Visible : state;
            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();
            StableProbe probe = new StableProbe();

            while (true)
            {
                bool done = false;
                try
                {
                    bool attached = await IsAttachedAsync(handle).ConfigureAwait(false);
                    if (!attached)
                    {
                        if (wanted == ElementState.Hidden)
                        {
                            return;
                        }

                        throw new PlaywrightNativeException(ClickAction.NotAttachedMessage);
                    }

                    done = wanted switch
                    {
                        ElementState.Hidden => await handle.IsHiddenAsync().ConfigureAwait(false),
                        ElementState.Enabled => await IsAriaEnabledAsync(handle).ConfigureAwait(false),
                        ElementState.Disabled => !await IsAriaEnabledAsync(handle).ConfigureAwait(false),
                        ElementState.Editable => await handle.IsEditableAsync().ConfigureAwait(false),
                        ElementState.Stable => await IsStableAsync(handle, probe).ConfigureAwait(false),
                        _ => await handle.IsVisibleAsync().ConfigureAwait(false),
                    };
                }
                catch (PlaywrightNativeException ex) when (!IsNotAttached(ex))
                {
                    if (wanted == ElementState.Hidden)
                    {
                        return;
                    }
                }

                if (done)
                {
                    return;
                }

                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    throw new TimeoutException($"element.waitForElementState({wanted}): Timeout {timeoutMs}ms exceeded.");
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Waits until <paramref name="handle"/> is visible unless <paramref name="force"/>
        /// is <see langword="true"/>.
        /// </summary>
        /// <param name="handle">The element to observe.</param>
        /// <param name="force">When <see langword="true"/>, skip the visibility wait.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <returns>A task that completes when the wait is done or skipped.</returns>
        internal static Task WaitVisibleUnlessForcedAsync(IElementHandle handle, bool? force, float? timeout)
        {
            if (force == true)
            {
                return Task.CompletedTask;
            }

            return WaitAsync(handle, ElementState.Visible, timeout);
        }

        private static bool IsNotAttached(PlaywrightNativeException ex)
            => ex != null && !string.IsNullOrEmpty(ex.Message)
                && ex.Message.Contains(ClickAction.NotAttachedMessage, StringComparison.Ordinal);

        private static async Task<bool> IsAttachedAsync(IElementHandle handle)
        {
            try
            {
                return await handle.EvaluateAsync<bool>("el => !!(el && el.isConnected)").ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                return false;
            }
        }

        private static Task<bool> IsAriaEnabledAsync(IElementHandle handle)
            => handle.EvaluateAsync<bool>(
                @"el => {
                    const tag = el && el.nodeName;
                    if (tag !== 'BUTTON' && tag !== 'INPUT' && tag !== 'SELECT' && tag !== 'TEXTAREA' && tag !== 'OPTION' && tag !== 'LABEL' && !(el && el.getAttribute && el.getAttribute('role'))) {
                        let n = el;
                        while (n) {
                            if (n.nodeName === 'BUTTON' || n.nodeName === 'A') {
                                el = n;
                                break;
                            }
                            n = n.parentElement;
                        }
                    }
                    const isEnabled = " + ElementStateScript.IsEnabledFunction + @";
                    return isEnabled(el);
                }");

        private static async Task<bool> IsStableAsync(IElementHandle handle, StableProbe probe)
        {
            if (!await handle.IsVisibleAsync().ConfigureAwait(false))
            {
                probe.LastBox = null;
                return false;
            }

            ElementHandleBoundingBoxResult box = await handle.BoundingBoxAsync().ConfigureAwait(false);
            if (box == null)
            {
                probe.LastBox = null;
                return false;
            }

            if (probe.LastBox == null)
            {
                probe.LastBox = box;
                return false;
            }

            bool same = probe.LastBox.X == box.X
                && probe.LastBox.Y == box.Y
                && probe.LastBox.Width == box.Width
                && probe.LastBox.Height == box.Height;
            probe.LastBox = box;
            return same;
        }

        private sealed class StableProbe
        {
            internal ElementHandleBoundingBoxResult LastBox { get; set; }
        }
    }
}
