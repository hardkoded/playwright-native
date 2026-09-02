/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
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

            while (true)
            {
                if (isDetached != null && isDetached())
                {
                    throw new PlaywrightSharpException(apiName + ": Frame was detached");
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

                        throw new PlaywrightSharpException(
                            ClickAction.NotAttachedMessage +
                            Environment.NewLine +
                            WaitingLog(selector, wanted));
                    }
                }

                IElementHandle handle = null;
                bool attached = false;
                bool visible = false;
                try
                {
                    handle = await querySelectorAsync(selector).ConfigureAwait(false);
                    attached = handle != null;
                    visible = attached && await handle.IsVisibleAsync().ConfigureAwait(false);
                }
                catch (PlaywrightSharpException ex) when (IsFrameDetachedError(ex) || (isDetached != null && isDetached()))
                {
                    throw new PlaywrightSharpException(apiName + ": Frame was detached", ex);
                }
                catch (PlaywrightSharpException ex) when (PlaywrightSharpException.IsDestroyedContext(ex) || IsMissingInjectedScript(ex))
                {
                    if (isDetached != null && isDetached())
                    {
                        throw new PlaywrightSharpException(apiName + ": Frame was detached", ex);
                    }

                    if (handle != null)
                    {
                        try
                        {
                            await handle.DisposeAsync().ConfigureAwait(false);
                        }
                        catch (PlaywrightSharpException)
                        {
                        }

                        handle = null;
                    }

                    attached = false;
                    visible = false;
                }

                bool done = wanted switch
                {
                    WaitForSelectorState.Attached => attached,
                    WaitForSelectorState.Detached => !attached,
                    WaitForSelectorState.Hidden => !visible,
                    _ => visible,
                };

                if (!done && handle != null)
                {
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
            string preview = "element";
            try
            {
                string value = await handle.EvaluateAsync<string>(RemoteObject.PreviewNodeFunction).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(value))
                {
                    preview = value;
                }
            }
            catch (PlaywrightSharpException)
            {
            }

            string line = "locator resolved to " + (visible ? "visible" : "hidden") + " " + preview;
            if (logs.Count == 0 || !string.Equals(logs[logs.Count - 1], line, StringComparison.Ordinal))
            {
                logs.Add(line);
            }
        }
    }
}
