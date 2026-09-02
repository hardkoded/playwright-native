/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Shared poll loop for <c>page.waitForFunction</c>. Evaluates the predicate until it
    /// returns a truthy value, using rAF by default or a millisecond interval when
    /// <c>pollingInterval</c> is set. Execution-context destruction mid-poll is retried
    /// until the timeout, matching upstream waitForFunction.
    /// </summary>
    internal static class WaitForFunctionHelper
    {
        private static readonly AsyncLocal<float?> _ambientTimeout = new AsyncLocal<float?>();
        private static readonly AsyncLocal<object> _pendingArg = new AsyncLocal<object>();
        private static int _predicateNonce;

        /// <summary>
        /// Stores the page default timeout so implementations that omit <c>timeout</c>
        /// (WebKit <c>WKPage</c>) still honor <see cref="IPage.SetDefaultTimeout"/>.
        /// </summary>
        /// <param name="timeout">The page default, or <see langword="null"/> to clear.</param>
        internal static void SetAmbientTimeout(float? timeout) => _ambientTimeout.Value = timeout;

        /// <summary>
        /// Stashes an argument for the next predicate wrap when the instance does
        /// not forward <c>arg</c>.
        /// </summary>
        /// <param name="arg">The waitForFunction argument.</param>
        internal static void SetPendingArg(object arg) => _pendingArg.Value = arg;

        /// <summary>
        /// Clears <see cref="SetPendingArg"/>.
        /// </summary>
        internal static void ClearPendingArg() => _pendingArg.Value = null;

        /// <summary>
        /// Rejects Node-style string polling values other than <c>raf</c>.
        /// </summary>
        /// <param name="polling">The polling option (<c>raf</c> or an invalid strategy).</param>
        internal static void ValidatePollingOption(string polling)
        {
            if (polling == null || string.Equals(polling, "raf", StringComparison.Ordinal))
            {
                return;
            }

            throw new PlaywrightNativeException("Unknown polling option: " + polling);
        }

        /// <summary>
        /// Rejects a non-positive millisecond polling interval.
        /// </summary>
        /// <param name="pollingInterval">The interval, or <see langword="null"/> for rAF.</param>
        internal static void ValidatePollingInterval(float? pollingInterval)
        {
            if (pollingInterval.HasValue && pollingInterval.Value <= 0)
            {
                throw new PlaywrightNativeException("Cannot poll with non-positive interval");
            }
        }

        /// <summary>
        /// Builds a page-side async IIFE that evaluates <paramref name="expression"/>
        /// (function or raw expression) and boxes a truthy result as <c>{ v }</c>.
        /// </summary>
        /// <param name="expression">The Playwright evaluate expression or function.</param>
        /// <returns>A JavaScript expression that yields the boxed predicate result.</returns>
        internal static string BuildPredicateExpression(string expression)
            => BuildPredicateExpression(expression, null);

        /// <summary>
        /// Builds a page-side async IIFE that evaluates <paramref name="expression"/>
        /// with an optional JSON argument.
        /// </summary>
        /// <param name="expression">The Playwright evaluate expression or function.</param>
        /// <param name="arg">JSON-serializable argument, or <see langword="null"/>.</param>
        /// <returns>A JavaScript expression that yields the boxed predicate result.</returns>
        internal static string BuildPredicateExpression(string expression, object arg)
        {
            if (string.IsNullOrEmpty(expression))
            {
                throw new ArgumentNullException(nameof(expression));
            }

            if (arg != null && !EvaluateWithArg.IsHandle(arg) && expression.IsJavascriptFunction())
            {
                expression = EvaluateWithArg.Wrap(expression, arg);
            }

            int nonce = Interlocked.Increment(ref _predicateNonce);
            string key = "__pwWff" + nonce.ToString();

            // Keep the wrapper synchronous. WebKit Runtime.evaluate (returnByValue:
            // false) does not honor awaitPromise, so an async IIFE becomes a Promise
            // handle whose jsonValue is empty. Sync predicates resolve to { v } /
            // null immediately; thenables are returned as-is for implementations that await.
            string compute = expression.IsJavascriptFunction()
                ? "raw = (" + expression + ")()"
                : "raw = (() => (" + expression + "))()";
            return "(() => { const key = '" + key + "'; if (window[key]) return window[key]; let raw; " +
                compute +
                "; const box = (value) => { const boxed = value ? { v: value } : null; if (boxed) window[key] = boxed; return boxed; }; if (raw && typeof raw.then === 'function') return Promise.resolve(raw).then(box); return box(raw); })()";
        }

        /// <summary>
        /// Builds a function that applies <paramref name="expression"/> to a handle argument.
        /// </summary>
        /// <param name="expression">The Playwright evaluate expression or function.</param>
        /// <returns>A JavaScript function of <c>(arg)</c> that yields the boxed predicate result.</returns>
        internal static string BuildPredicateFunction(string expression)
        {
            if (string.IsNullOrEmpty(expression))
            {
                throw new ArgumentNullException(nameof(expression));
            }

            if (expression.IsJavascriptFunction())
            {
                return "async (arg) => { const fn = " + expression + "; const value = await Promise.resolve(fn(arg)); return value ? { v: value } : null; }";
            }

            return "async () => { const value = await Promise.resolve((() => (" + expression + "))()); return value ? { v: value } : null; }";
        }

        /// <summary>
        /// Builds an element-side async function that evaluates <paramref name="expression"/>
        /// with the matching element as the first argument and the user argument as the second.
        /// Falsy results become <c>null</c> (no <c>objectId</c>) so handle evaluate returns null.
        /// </summary>
        /// <param name="expression">The Playwright evaluate expression or function.</param>
        /// <returns>A JavaScript function of <c>(el, arg)</c> that yields the boxed predicate result.</returns>
        internal static string BuildLocatorPredicateExpression(string expression)
        {
            if (string.IsNullOrEmpty(expression))
            {
                throw new ArgumentNullException(nameof(expression));
            }

            if (expression.IsJavascriptFunction())
            {
                return "async (el, arg) => { const fn = " + expression + "; const value = await Promise.resolve(fn(el, arg)); return value ? { v: value } : null; }";
            }

            return "async (el) => { const value = await Promise.resolve((() => (" + expression + "))()); return value ? { v: value } : null; }";
        }

        /// <summary>
        /// Polls <paramref name="evaluateAsync"/> until it returns a non-null handle.
        /// The evaluator should return <see langword="null"/> for a falsy predicate.
        /// Unlike the expression overload, this does not wrap <paramref name="evaluateAsync"/>.
        /// </summary>
        /// <typeparam name="THandle">The handle type returned to the caller.</typeparam>
        /// <param name="evaluateAsync">Evaluates the predicate; null means keep polling.</param>
        /// <param name="pollingInterval">Millisecond poll interval, or <see langword="null"/> for rAF.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <param name="rafAsync">Optional rAF wait; defaults to a 16ms delay.</param>
        /// <param name="apiName">Name used in the timeout message.</param>
        /// <param name="isDetached">When set, a true result fails the wait as a detached frame.</param>
        /// <param name="signal">Official <c>signal</c>. Aborting throws <see cref="AbortError"/>.</param>
        /// <returns>The first non-null handle.</returns>
        internal static Task<THandle> WaitAsync<THandle>(
            Func<Task<THandle>> evaluateAsync,
            float? pollingInterval,
            float? timeout,
            Func<Task> rafAsync = null,
            string apiName = "page.waitForFunction",
            Func<bool> isDetached = null,
            AbortSignal signal = default)
            where THandle : class
        {
            if (evaluateAsync == null)
            {
                throw new ArgumentNullException(nameof(evaluateAsync));
            }

            return PollAsync(evaluateAsync, pollingInterval, timeout, rafAsync, apiName, isDetached, signal);
        }

        /// <summary>
        /// Polls <paramref name="evaluateAsync"/> until it returns a non-null handle.
        /// The evaluator should return <see langword="null"/> for a falsy predicate.
        /// </summary>
        /// <typeparam name="THandle">The handle type returned to the caller.</typeparam>
        /// <param name="evaluateAsync">Evaluates the wrapped predicate; null means keep polling.</param>
        /// <param name="expression">The original waitForFunction expression.</param>
        /// <param name="pollingInterval">Millisecond poll interval, or <see langword="null"/> for rAF.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <param name="rafAsync">Optional rAF wait; defaults to a 16ms delay.</param>
        /// <param name="apiName">Name used in the timeout message.</param>
        /// <param name="isDetached">When set, a true result fails the wait as a detached frame.</param>
        /// <param name="arg">Optional argument passed to a function expression.</param>
        /// <returns>The first non-null handle.</returns>
        internal static async Task<THandle> WaitAsync<THandle>(
            Func<string, Task<THandle>> evaluateAsync,
            string expression,
            float? pollingInterval,
            float? timeout,
            Func<Task> rafAsync = null,
            string apiName = "page.waitForFunction",
            Func<bool> isDetached = null,
            object arg = null)
            where THandle : class
        {
            if (evaluateAsync == null)
            {
                throw new ArgumentNullException(nameof(evaluateAsync));
            }

            object effectiveArg = arg ?? _pendingArg.Value;
            _pendingArg.Value = null;
            string wrapped = BuildPredicateExpression(expression, effectiveArg);
            return await PollAsync(
                () => evaluateAsync(wrapped),
                pollingInterval,
                timeout,
                rafAsync,
                apiName,
                isDetached,
                signal: null).ConfigureAwait(false);
        }

        private static async Task<THandle> PollAsync<THandle>(
            Func<Task<THandle>> evaluateAsync,
            float? pollingInterval,
            float? timeout,
            Func<Task> rafAsync,
            string apiName,
            Func<bool> isDetached,
            AbortSignal signal)
            where THandle : class
        {
            ValidatePollingInterval(pollingInterval);
            _ = rafAsync;
            float? resolvedTimeout = timeout ?? _ambientTimeout.Value;
            int timeoutMs = TimeoutSettings.TimeoutMs(resolvedTimeout);
            Stopwatch sw = Stopwatch.StartNew();
            Task timeoutTask = timeoutMs == Timeout.Infinite
                ? new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously).Task
                : Task.Delay(timeoutMs);

            while (true)
            {
                if (signal != null && signal.Aborted)
                {
                    throw AbortError.InFlight(apiName, signal);
                }

                ThrowIfTimedOut(sw, timeoutMs, timeoutTask, apiName);
                if (isDetached != null && isDetached())
                {
                    throw new PlaywrightNativeException(apiName + ": Frame was detached");
                }

                try
                {
                    Task<THandle> evalTask = evaluateAsync();
                    await WhenAnyOrTimeoutAsync(evalTask, timeoutTask, sw, timeoutMs, apiName).ConfigureAwait(false);
                    THandle handle = await evalTask.ConfigureAwait(false);
                    if (handle is IJSHandle jsHandle)
                    {
                        Task<IJSHandle> unboxTask = UnboxPredicateResultAsync(jsHandle);
                        await WhenAnyOrTimeoutAsync(unboxTask, timeoutTask, sw, timeoutMs, apiName).ConfigureAwait(false);
                        IJSHandle unboxed = await unboxTask.ConfigureAwait(false);
                        if (unboxed != null)
                        {
                            return (THandle)(object)unboxed;
                        }
                    }
                    else if (handle != null)
                    {
                        return handle;
                    }
                }
                catch (TimeoutException)
                {
                    throw;
                }
                catch (Exception ex) when (IsDetachedError(ex))
                {
                    throw new PlaywrightNativeException(apiName + ": Frame was detached", ex);
                }
                catch (Exception ex) when (IsRetriableContextError(ex))
                {
                    if (isDetached != null && isDetached())
                    {
                        throw new PlaywrightNativeException(apiName + ": Frame was detached", ex);
                    }
                }

                ThrowIfTimedOut(sw, timeoutMs, timeoutTask, apiName);

                Task delay = pollingInterval.HasValue && pollingInterval.Value > 0
                    ? Task.Delay((int)pollingInterval.Value)
                    : Task.Delay(16);
                if (signal != null)
                {
                    await Task.WhenAny(delay, signal.WhenAbortedAsync(), timeoutTask).ConfigureAwait(false);
                }
                else
                {
                    await WhenAnyOrTimeoutAsync(delay, timeoutTask, sw, timeoutMs, apiName).ConfigureAwait(false);
                }
            }
        }

        private static void ThrowIfTimedOut(Stopwatch sw, int timeoutMs, Task timeoutTask, string apiName)
        {
            if (timeoutMs == Timeout.Infinite)
            {
                return;
            }

            if (timeoutTask.IsCompleted || sw.ElapsedMilliseconds >= timeoutMs)
            {
                throw new TimeoutException($"{apiName}: Timeout {timeoutMs}ms exceeded.");
            }
        }

        private static async Task WhenAnyOrTimeoutAsync(
            Task operation,
            Task timeoutTask,
            Stopwatch sw,
            int timeoutMs,
            string apiName)
        {
            if (timeoutMs == Timeout.Infinite)
            {
                await operation.ConfigureAwait(false);
                return;
            }

            Task finished = await Task.WhenAny(operation, timeoutTask).ConfigureAwait(false);
            if (finished == timeoutTask || sw.ElapsedMilliseconds >= timeoutMs)
            {
                await Task.WhenAny(operation, Task.Delay(250)).ConfigureAwait(false);
                throw new TimeoutException($"{apiName}: Timeout {timeoutMs}ms exceeded.");
            }
        }

        private static async Task<IJSHandle> UnboxPredicateResultAsync(IJSHandle handle)
        {
            if (handle == null)
            {
                return null;
            }

            if (handle is ImmediateJSHandle)
            {
                object value = await handle.JsonValueAsync<object>().ConfigureAwait(false);
                if (IsFalsyJson(value))
                {
                    return null;
                }

                return handle;
            }

            try
            {
                // callFunctionOn honors awaitPromise, so a leftover Promise from
                // WebKit EvaluateHandle unwraps here before { v } is extracted.
                IJSHandle extracted = await handle.EvaluateHandleAsync(
                    "async x => { const r = await x; if (r == null) return r; return (typeof r === 'object' && Object.prototype.hasOwnProperty.call(r, 'v')) ? r.v : r; }")
                    .ConfigureAwait(false);
                await handle.DisposeAsync().ConfigureAwait(false);
                if (extracted == null)
                {
                    return null;
                }

                if (extracted is ImmediateJSHandle primitive)
                {
                    object value = await primitive.JsonValueAsync<object>().ConfigureAwait(false);
                    return IsFalsyJson(value) ? null : primitive;
                }

                return extracted;
            }
            catch (PlaywrightNativeException ex) when (IsRetriableContextError(ex))
            {
                throw;
            }
            catch (PlaywrightNativeException)
            {
                try
                {
                    await handle.DisposeAsync().ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }

                return null;
            }
        }

        private static bool IsFalsyJson(object value)
        {
            if (value == null)
            {
                return true;
            }

            if (value is bool flag)
            {
                return !flag;
            }

            if (value is JsonElement element)
            {
                return element.ValueKind == JsonValueKind.Null
                    || element.ValueKind == JsonValueKind.Undefined
                    || element.ValueKind == JsonValueKind.False;
            }

            return false;
        }

        private static bool IsDetachedError(Exception ex)
        {
            string message = ex.Message ?? string.Empty;
            return message.Contains("Frame was detached", StringComparison.OrdinalIgnoreCase)
                || message.Contains("frame was detached", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRetriableContextError(Exception ex)
        {
            string message = ex.Message ?? string.Empty;
            return message.Contains("context", StringComparison.OrdinalIgnoreCase)
                || message.Contains("destroyed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Target closed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Session closed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("page has been closed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Missing injected script", StringComparison.OrdinalIgnoreCase)
                || message.Contains("objectId", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Execution context", StringComparison.OrdinalIgnoreCase)
                || message.Contains("navigat", StringComparison.OrdinalIgnoreCase)
                || message.Contains("disconnected", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Cannot find", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Inspected", StringComparison.OrdinalIgnoreCase)
                || message.Contains("not attached", StringComparison.OrdinalIgnoreCase);
        }
    }
}
