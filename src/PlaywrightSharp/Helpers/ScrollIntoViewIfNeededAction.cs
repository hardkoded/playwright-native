/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Official <c>ElementHandle.scrollIntoViewIfNeeded</c>: retry until stable,
    /// then protocol-scroll. Mirrors <c>dom.ts</c> <c>_waitAndScrollIntoViewIfNeeded</c>
    /// with <c>waitForVisible: false</c>.
    /// </summary>
    internal static class ScrollIntoViewIfNeededAction
    {
        internal const string ResultDone = "done";
        internal const string ResultNotVisible = "notvisible";
        internal const string ResultNotConnected = "notconnected";

        private const string ResultNotStable = "notstable";
        private const string ResultOk = "ok";

        /// <summary>
        /// Official <c>_checkElementIsStable</c> with <c>stableRafCount === 1</c>:
        /// two consecutive animation frames must report the same box.
        /// </summary>
        private const string IsStableFunction = @"async el => {
    if (!el || !el.isConnected) {
        return 'notconnected';
    }
    const view = (el.ownerDocument && el.ownerDocument.defaultView) || window;
    function boxOf() {
        const r = el.getBoundingClientRect();
        return [r.top, r.left, r.width, r.height];
    }
    function raf() {
        return new Promise(resolve => view.requestAnimationFrame(resolve));
    }
    await raf();
    const first = boxOf();
    await raf();
    const second = boxOf();
    return first[0] === second[0] && first[1] === second[1] && first[2] === second[2] && first[3] === second[3]
        ? 'ok'
        : 'notstable';
}";

        /// <summary>
        /// Retries a protocol scroll until the element is attached and stable,
        /// or <paramref name="timeout"/> elapses.
        /// </summary>
        /// <param name="handle">The element to scroll.</param>
        /// <param name="scrollAsync">
        /// Protocol <c>DOM.scrollIntoViewIfNeeded</c>. Returns
        /// <see cref="ResultDone"/>, <see cref="ResultNotVisible"/>, or
        /// <see cref="ResultNotConnected"/>.
        /// </param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <returns>A task that completes when the scroll has been issued.</returns>
        internal static async Task RunAsync(
            IElementHandle handle,
            Func<Task<string>> scrollAsync,
            float? timeout)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (scrollAsync == null)
            {
                throw new ArgumentNullException(nameof(scrollAsync));
            }

            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();
            StringBuilder log = new StringBuilder();
            log.Append("Call log:\n");
            int retry = 0;
            int[] waitTime = { 0, 20, 100, 100, 500 };

            while (true)
            {
                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    throw new TimeoutException(
                        "Timeout " + timeoutMs.ToString(CultureInfo.InvariantCulture) + "ms exceeded.\n" + log);
                }

                if (retry > 0)
                {
                    log.Append("  - retrying scroll into view action\n");
                    int wait = waitTime[Math.Min(retry - 1, waitTime.Length - 1)];
                    if (wait > 0)
                    {
                        int remaining = timeoutMs == Timeout.Infinite
                            ? wait
                            : timeoutMs - (int)sw.ElapsedMilliseconds;
                        if (remaining <= 0)
                        {
                            throw new TimeoutException(
                                "Timeout " + timeoutMs.ToString(CultureInfo.InvariantCulture) + "ms exceeded.\n" + log);
                        }

                        await Task.Delay(Math.Min(wait, remaining)).ConfigureAwait(false);
                    }
                }
                else
                {
                    log.Append("  - attempting scroll into view action\n");
                }

                if (!await IsConnectedAsync(handle).ConfigureAwait(false))
                {
                    throw new PlaywrightSharpException(ClickAction.NotAttachedMessage);
                }

                log.Append("  - waiting for element to be stable\n");
                string stable = await CheckStableAsync(handle).ConfigureAwait(false);
                if (stable == ResultNotConnected)
                {
                    throw new PlaywrightSharpException(ClickAction.NotAttachedMessage);
                }

                if (stable != ResultOk)
                {
                    log.Append("  - element is not stable\n");
                    retry++;
                    continue;
                }

                string result = await scrollAsync().ConfigureAwait(false);
                if (result == ResultNotConnected)
                {
                    throw new PlaywrightSharpException(ClickAction.NotAttachedMessage);
                }

                if (result == ResultNotVisible)
                {
                    log.Append("  - element is not visible\n");
                    retry++;
                    continue;
                }

                return;
            }
        }

        /// <summary>
        /// Maps Chromium/WebKit <c>DOM.scrollIntoViewIfNeeded</c> errors to
        /// <see cref="ResultNotVisible"/> or <see cref="ResultNotConnected"/>.
        /// </summary>
        /// <param name="message">The protocol exception message.</param>
        /// <returns>A result token, or <see langword="null"/> when the error is fatal.</returns>
        internal static string MapProtocolError(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return null;
            }

            if (message.Contains("Node does not have a layout object", StringComparison.Ordinal))
            {
                return ResultNotVisible;
            }

            if (message.Contains("Node is detached from document", StringComparison.Ordinal))
            {
                return ResultNotConnected;
            }

            return null;
        }

        private static async Task<string> CheckStableAsync(IElementHandle handle)
        {
            try
            {
                string result = await handle.EvaluateAsync<string>(IsStableFunction).ConfigureAwait(false);
                if (result == ResultNotConnected || result == ResultNotStable || result == ResultOk)
                {
                    return result;
                }
            }
            catch (PlaywrightSharpException)
            {
                if (!await IsConnectedAsync(handle).ConfigureAwait(false))
                {
                    return ResultNotConnected;
                }
            }

            return ResultNotStable;
        }

        private static async Task<bool> IsConnectedAsync(IElementHandle handle)
        {
            try
            {
                return await handle.EvaluateAsync<bool>(ClickAction.IsConnectedFunction).ConfigureAwait(false);
            }
            catch (PlaywrightSharpException)
            {
                return false;
            }
        }
    }
}
