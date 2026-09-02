/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Queries once and runs an element-handle operation.
    /// </summary>
    internal static class ElementQuery
    {
        /// <summary>
        /// Finds <paramref name="selector"/> and invokes <paramref name="onHandle"/>.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="querySelectorAsync">One-shot CSS query.</param>
        /// <param name="selector">CSS selector.</param>
        /// <param name="onHandle">Operation to run on the matched handle.</param>
        /// <returns>The operation result.</returns>
        internal static async Task<T> QueryAsync<T>(
            Func<string, Task<IElementHandle>> querySelectorAsync,
            string selector,
            Func<IElementHandle, Task<T>> onHandle)
        {
            if (querySelectorAsync == null)
            {
                throw new ArgumentNullException(nameof(querySelectorAsync));
            }

            if (onHandle == null)
            {
                throw new ArgumentNullException(nameof(onHandle));
            }

            IElementHandle handle = await querySelectorAsync(selector).ConfigureAwait(false);
            if (handle == null)
            {
                throw new PlaywrightNativeException($"No node found for selector: {selector}");
            }

            return await onHandle(handle).ConfigureAwait(false);
        }

        /// <summary>
        /// Waits for <paramref name="selector"/> to attach, then invokes <paramref name="onHandle"/>.
        /// Honors <paramref name="timeout"/>.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="querySelectorAsync">One-shot CSS query.</param>
        /// <param name="selector">CSS selector.</param>
        /// <param name="onHandle">Operation to run on the matched handle.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <param name="apiName">Name used in the timeout message.</param>
        /// <param name="scroll">
        /// When <see cref="ActionScroll.None"/>, skip scrolling into view.
        /// Defaults to <see cref="ActionScroll.None"/> so existing callers keep
        /// their current behavior.
        /// </param>
        /// <returns>The operation result.</returns>
        internal static async Task<T> WaitQueryAsync<T>(
            Func<string, Task<IElementHandle>> querySelectorAsync,
            string selector,
            Func<IElementHandle, Task<T>> onHandle,
            float? timeout,
            string apiName = "page.waitForSelector",
            ActionScroll scroll = ActionScroll.None)
        {
            if (onHandle == null)
            {
                throw new ArgumentNullException(nameof(onHandle));
            }

            IElementHandle handle = await WaitForSelectorHelper.WaitAsync(
                querySelectorAsync,
                selector,
                WaitForSelectorState.Attached,
                timeout,
                apiName).ConfigureAwait(false);

            if (handle == null)
            {
                throw new PlaywrightNativeException($"No node found for selector: {selector}");
            }

            if (scroll != ActionScroll.None)
            {
                await handle.EvaluateAsync<bool>(ElementStateScript.ScrollIntoViewIfNeededFunction).ConfigureAwait(false);
            }

            return await onHandle(handle).ConfigureAwait(false);
        }

        /// <summary>
        /// Finds <paramref name="selector"/> and invokes a void element-handle operation.
        /// </summary>
        /// <param name="querySelectorAsync">One-shot CSS query.</param>
        /// <param name="selector">CSS selector.</param>
        /// <param name="onHandle">Operation to run on the matched handle.</param>
        /// <returns>A task that completes when the operation finishes.</returns>
        internal static async Task RunAsync(
            Func<string, Task<IElementHandle>> querySelectorAsync,
            string selector,
            Func<IElementHandle, Task> onHandle)
        {
            if (querySelectorAsync == null)
            {
                throw new ArgumentNullException(nameof(querySelectorAsync));
            }

            if (onHandle == null)
            {
                throw new ArgumentNullException(nameof(onHandle));
            }

            IElementHandle handle = await querySelectorAsync(selector).ConfigureAwait(false);
            if (handle == null)
            {
                throw new PlaywrightNativeException($"No node found for selector: {selector}");
            }

            await onHandle(handle).ConfigureAwait(false);
        }

        /// <summary>
        /// Waits for <paramref name="selector"/> to attach, then invokes <paramref name="onHandle"/>.
        /// Honors <paramref name="timeout"/>.
        /// </summary>
        /// <param name="querySelectorAsync">One-shot CSS query.</param>
        /// <param name="selector">CSS selector.</param>
        /// <param name="onHandle">Operation to run on the matched handle.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <param name="apiName">Name used in the timeout message.</param>
        /// <param name="scroll">
        /// When <see cref="ActionScroll.None"/>, skip scrolling into view.
        /// Defaults to <see cref="ActionScroll.None"/> so existing callers keep
        /// their current behavior.
        /// </param>
        /// <returns>A task that completes when the operation finishes.</returns>
        internal static async Task WaitRunAsync(
            Func<string, Task<IElementHandle>> querySelectorAsync,
            string selector,
            Func<IElementHandle, Task> onHandle,
            float? timeout,
            string apiName = "page.waitForSelector",
            ActionScroll scroll = ActionScroll.None)
        {
            if (onHandle == null)
            {
                throw new ArgumentNullException(nameof(onHandle));
            }

            IElementHandle handle = await WaitForSelectorHelper.WaitAsync(
                querySelectorAsync,
                selector,
                WaitForSelectorState.Attached,
                timeout,
                apiName).ConfigureAwait(false);

            if (handle == null)
            {
                throw new PlaywrightNativeException($"No node found for selector: {selector}");
            }

            if (scroll != ActionScroll.None)
            {
                await handle.EvaluateAsync<bool>(ElementStateScript.ScrollIntoViewIfNeededFunction).ConfigureAwait(false);
            }

            await ScreencastActions.AnnotateIfEnabledAsync(handle, apiName).ConfigureAwait(false);
            string previousApiName = ClickAction.ApiName.Value;
            ClickAction.ApiName.Value = apiName;
            try
            {
                await onHandle(handle).ConfigureAwait(false);
            }
            catch (TimeoutException ex) when (IsPointerApi(apiName) && !HasApiPrefix(ex, apiName))
            {
                throw new TimeoutException(apiName + ": " + ex.Message, ex);
            }
            catch (Exception ex) when (IsFillApi(apiName) && FillAction.IsValidation(ex))
            {
                throw FillAction.Wrap(ex, apiName);
            }
            finally
            {
                ClickAction.ApiName.Value = previousApiName;
            }
        }

        private static bool IsFillApi(string apiName)
            => !string.IsNullOrEmpty(apiName)
                && apiName.EndsWith(".fill", StringComparison.Ordinal);

        private static bool IsPointerApi(string apiName)
            => !string.IsNullOrEmpty(apiName)
                && (apiName.EndsWith(".click", StringComparison.Ordinal)
                    || apiName.EndsWith(".dblclick", StringComparison.Ordinal));

        private static bool HasApiPrefix(Exception ex, string apiName)
            => ex != null
                && !string.IsNullOrEmpty(ex.Message)
                && ex.Message.StartsWith(apiName + ":", StringComparison.Ordinal);
    }
}
