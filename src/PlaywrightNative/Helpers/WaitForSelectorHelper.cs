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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Shared waiter for <c>page.waitForSelector</c>. Polls <c>querySelector</c> until
    /// the element is attached, detached, visible, or hidden. Mirrors a first-match
    /// subset of upstream Frame.waitForSelector.
    /// </summary>
    internal static class WaitForSelectorHelper
    {
        /// <summary>
        /// Waits until <paramref name="selector"/> satisfies <paramref name="state"/>.
        /// </summary>
        /// <param name="querySelectorAsync">One-shot CSS query returning a handle or null.</param>
        /// <param name="selector">CSS selector.</param>
        /// <param name="state">Target state. <see cref="EnumCompat.UndefinedWaitForSelectorState"/> means visible.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <param name="apiName">Name used in the timeout message.</param>
        /// <param name="isDetached">
        /// When set, the wait fails with <c>Frame was detached</c> as soon as the
        /// owning frame is gone.
        /// </param>
        /// <param name="isScopeConnectedAsync">
        /// Optional connected check for element-handle waits. A disconnected host
        /// succeeds hidden/detached waits and fails attached/visible waits.
        /// </param>
        /// <returns>
        /// The matching handle for attached/visible (and hidden-but-attached).
        /// <see langword="null"/> when waiting for detached, or hidden and the node is gone.
        /// </returns>
        internal static async Task<IElementHandle> WaitAsync(
            Func<string, Task<IElementHandle>> querySelectorAsync,
            string selector,
            WaitForSelectorState state,
            float? timeout,
            string apiName = "page.waitForSelector",
            Func<bool> isDetached = null,
            Func<Task<bool>> isScopeConnectedAsync = null)
        {
            if (querySelectorAsync == null)
            {
                throw new ArgumentNullException(nameof(querySelectorAsync));
            }

            if (string.IsNullOrEmpty(selector))
            {
                throw new ArgumentException("Selector must not be empty.", nameof(selector));
            }

            WaitForSelectorState wanted = state == EnumCompat.UndefinedWaitForSelectorState
                ? WaitForSelectorState.Visible
                : state;

            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();
            List<string> logs = new List<string>();

            // Every distinct resolved preview observed while waiting. Darwin WebKit
            // can miss AppendResolvedLog when a node is removed between preview and
            // visibility probes; replaying this list on timeout keeps earlier
            // snapshots (e.g. #mydiv) even after a later node (#another) logs.
            List<(bool Visible, string Preview)> resolvedSnapshots = new List<(bool, string)>();

            while (true)
            {
                if (isDetached != null && isDetached())
                {
                    throw new PlaywrightException(apiName + ": Frame was detached");
                }

                if (isScopeConnectedAsync != null)
                {
                    bool connected = await isScopeConnectedAsync().ConfigureAwait(false);
                    if (!connected)
                    {
                        if (wanted == WaitForSelectorState.Detached || wanted == WaitForSelectorState.Hidden)
                        {
                            return null;
                        }

                        throw new PlaywrightException(
                            ClickAction.NotAttachedMessage +
                            Environment.NewLine +
                            WaitingLog(selector, wanted));
                    }
                }

                IElementHandle handle = null;
                bool attached = false;
                bool visible = false;
                string eagerPreview = null;
                bool destroyedMidProbe = false;
                try
                {
                    handle = await querySelectorAsync(selector).ConfigureAwait(false);
                    attached = handle != null;
                    if (attached)
                    {
                        // Preview first (fast), then visibility. Combined
                        // preview+visibility probes raced Darwin removals: the
                        // evaluate was still in flight when mydiv was removed, so
                        // Remember never ran and timeout logs only showed
                        // "another". Persist the preview before the visibility
                        // round-trip so Hidden/Visible log tests keep mydiv.
                        if (wanted != WaitForSelectorState.Attached
                            && wanted != WaitForSelectorState.Detached)
                        {
                            try
                            {
                                string previewValue = await handle.EvaluateAsync<string>(
                                        RemoteObject.PreviewNodeFunction)
                                    .ConfigureAwait(false);
                                if (!string.IsNullOrEmpty(previewValue))
                                {
                                    eagerPreview = previewValue;
                                    RememberResolvedSnapshot(
                                        resolvedSnapshots,
                                        visible: wanted == WaitForSelectorState.Hidden,
                                        eagerPreview);
                                }
                            }
                            catch (PlaywrightException)
                            {
                            }

                            try
                            {
                                visible = await handle.IsVisibleAsync().ConfigureAwait(false);
                                if (!string.IsNullOrEmpty(eagerPreview))
                                {
                                    RememberResolvedSnapshot(resolvedSnapshots, visible, eagerPreview);
                                }
                            }
                            catch (PlaywrightException ex) when (
                                DestroyedContext.IsDestroyedContext(ex) || IsMissingInjectedScript(ex))
                            {
                                throw;
                            }
                            catch (PlaywrightException)
                            {
                            }
                        }
                        else if (string.IsNullOrEmpty(eagerPreview))
                        {
                            try
                            {
                                string previewValue = await handle.EvaluateAsync<string>(
                                        RemoteObject.PreviewNodeFunction)
                                    .ConfigureAwait(false);
                                if (!string.IsNullOrEmpty(previewValue))
                                {
                                    eagerPreview = previewValue;
                                    RememberResolvedSnapshot(
                                        resolvedSnapshots,
                                        visible: false,
                                        eagerPreview);
                                }
                            }
                            catch (PlaywrightException)
                            {
                            }
                        }
                    }
                }
                catch (PlaywrightException ex) when (IsFrameDetachedError(ex) || (isDetached != null && isDetached()))
                {
                    throw new PlaywrightException(apiName + ": Frame was detached", ex);
                }
                catch (PlaywrightException ex) when (PlaywrightNative.Helpers.DestroyedContext.IsDestroyedContext(ex) || IsMissingInjectedScript(ex))
                {
                    if (isDetached != null && isDetached())
                    {
                        throw new PlaywrightException(apiName + ": Frame was detached", ex);
                    }

                    if (handle != null)
                    {
                        try
                        {
                            await handle.DisposeAsync().ConfigureAwait(false);
                        }
                        catch (PlaywrightException)
                        {
                        }

                        handle = null;
                    }

                    // A mid-probe DestroyedContext is not a reliable "element gone"
                    // signal — the page may already have replaced the node (Hidden
                    // log test remove+add). Re-query next loop instead of succeeding
                    // Hidden/Detached from this stale observation.
                    destroyedMidProbe = true;
                    attached = false;
                    visible = false;
                }

                if (destroyedMidProbe)
                {
                    if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                    {
                        foreach ((bool snapshotVisible, string snapshotPreview) in resolvedSnapshots)
                        {
                            AppendResolvedLog(logs, snapshotVisible, snapshotPreview);
                        }

                        string message = apiName +
                            ": Timeout " +
                            timeoutMs.ToString(CultureInfo.InvariantCulture) +
                            "ms exceeded." +
                            Environment.NewLine +
                            WaitingLog(selector, wanted);
                        if (logs.Count > 0)
                        {
                            message += Environment.NewLine + string.Join(Environment.NewLine, logs);
                        }

                        throw new TimeoutException(message);
                    }

                    await Task.Delay(16).ConfigureAwait(false);
                    continue;
                }

                bool done = wanted switch
                {
                    WaitForSelectorState.Attached => attached,
                    WaitForSelectorState.Detached => !attached,
                    WaitForSelectorState.Hidden => !visible,
                    _ => visible,
                };

                if (!done && !string.IsNullOrEmpty(eagerPreview))
                {
                    RememberResolvedSnapshot(resolvedSnapshots, visible, eagerPreview);
                    AppendResolvedLog(logs, visible, eagerPreview);
                }
                else if (!done && handle != null)
                {
                    string preview = await TryPreviewAsync(handle).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(preview))
                    {
                        RememberResolvedSnapshot(resolvedSnapshots, visible, preview);
                    }

                    await AppendResolvedLogAsync(logs, visible, handle).ConfigureAwait(false);
                }

                if (done)
                {
                    if (wanted == WaitForSelectorState.Detached || (wanted == WaitForSelectorState.Hidden && !attached))
                    {
                        if (handle != null)
                        {
                            await handle.DisposeAsync().ConfigureAwait(false);
                        }

                        return null;
                    }

                    return handle;
                }

                if (handle != null)
                {
                    await handle.DisposeAsync().ConfigureAwait(false);
                }

                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    // Merge snapshots into logs without dropping earlier lines.
                    // Replacing logs entirely lost #mydiv when only the final
                    // "another" node remained in resolvedSnapshots on Darwin.
                    foreach ((bool snapshotVisible, string snapshotPreview) in resolvedSnapshots)
                    {
                        AppendResolvedLog(logs, snapshotVisible, snapshotPreview);
                    }

                    string message = apiName +
                        ": Timeout " +
                        timeoutMs.ToString(CultureInfo.InvariantCulture) +
                        "ms exceeded." +
                        Environment.NewLine +
                        WaitingLog(selector, wanted);
                    if (logs.Count > 0)
                    {
                        message += Environment.NewLine + string.Join(Environment.NewLine, logs);
                    }

                    throw new TimeoutException(message);
                }

                await Task.Delay(16).ConfigureAwait(false);
            }
        }

        private static bool IsFrameDetachedError(Exception ex)
        {
            string message = ex.Message ?? string.Empty;
            return message.Contains("Frame was detached", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMissingInjectedScript(Exception ex)
        {
            string message = ex.Message ?? string.Empty;
            return message.Contains("Missing injected script", StringComparison.OrdinalIgnoreCase)
                || message.Contains("given objectId", StringComparison.OrdinalIgnoreCase);
        }

        private static string WaitingLog(string selector, WaitForSelectorState wanted)
        {
            string escaped = (selector ?? string.Empty)
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("'", "\\'", StringComparison.Ordinal);
            string state = wanted switch
            {
                WaitForSelectorState.Hidden => "hidden",
                WaitForSelectorState.Detached => "detached",
                WaitForSelectorState.Attached => "attached",
                _ => "visible",
            };

            return "waiting for locator('" + escaped + "') to be " + state;
        }

        private static async Task AppendResolvedLogAsync(List<string> logs, bool visible, IElementHandle handle)
        {
            string preview = await TryPreviewAsync(handle).ConfigureAwait(false);
            AppendResolvedLog(logs, visible, string.IsNullOrEmpty(preview) ? "element" : preview);
        }

        private static async Task<string> TryPreviewAsync(IElementHandle handle)
        {
            try
            {
                string value = await handle.EvaluateAsync<string>(RemoteObject.PreviewNodeFunction).ConfigureAwait(false);
                return string.IsNullOrEmpty(value) ? null : value;
            }
            catch (PlaywrightException)
            {
                return null;
            }
        }

        private static void RememberResolvedSnapshot(
            List<(bool Visible, string Preview)> snapshots,
            bool visible,
            string preview)
        {
            if (string.IsNullOrEmpty(preview) || snapshots == null)
            {
                return;
            }

            string line = "locator resolved to " + (visible ? "visible" : "hidden") + " " + preview;
            if (snapshots.Count > 0)
            {
                (bool lastVisible, string lastPreview) = snapshots[snapshots.Count - 1];
                string lastLine = "locator resolved to " + (lastVisible ? "visible" : "hidden") + " " + lastPreview;
                if (string.Equals(lastLine, line, StringComparison.Ordinal))
                {
                    return;
                }
            }

            snapshots.Add((visible, preview));
        }

        private static void AppendResolvedLog(List<string> logs, bool visible, string preview)
        {
            string line = "locator resolved to " + (visible ? "visible" : "hidden") + " " + preview;
            if (logs == null || string.IsNullOrEmpty(preview))
            {
                return;
            }

            for (int i = 0; i < logs.Count; i++)
            {
                if (string.Equals(logs[i], line, StringComparison.Ordinal))
                {
                    return;
                }
            }

            logs.Add(line);
        }
    }
}
