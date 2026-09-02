/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Frame-scoped locator-less getBy* queries. Reuses
    /// <see cref="GetBySelectorScript"/> via <see cref="IFrame.EvaluateHandleAsync(string, object)"/>.
    /// </summary>
    internal static class FrameGetBy
    {
        /// <summary>
        /// Runs <paramref name="functionDeclaration"/> in <paramref name="frame"/> and
        /// returns the first matching element, or <see langword="null"/>.
        /// </summary>
        /// <param name="frame">The frame to query.</param>
        /// <param name="functionDeclaration">A JS function returning an Element or null.</param>
        /// <param name="args">Arguments passed to the function.</param>
        /// <returns>The matching element, or <see langword="null"/>.</returns>
        internal static async Task<IElementHandle> QueryAsync(IFrame frame, string functionDeclaration, params object[] args)
        {
            if (frame == null)
            {
                return null;
            }

            string call = "(" + functionDeclaration + ").apply(null, " + JsonSerializer.Serialize(args ?? Array.Empty<object>()) + ")";
            IJSHandle handle = await frame.EvaluateHandleAsync(call).ConfigureAwait(false);
            return handle?.AsElement();
        }

        /// <summary>
        /// Runs <paramref name="functionDeclaration"/> in <paramref name="frame"/> and
        /// returns every matching element.
        /// </summary>
        /// <param name="frame">The frame to query.</param>
        /// <param name="functionDeclaration">A JS function returning an Element array.</param>
        /// <param name="args">Arguments passed to the function.</param>
        /// <returns>Matching elements, in document order.</returns>
        internal static async Task<IReadOnlyList<IElementHandle>> QueryAllAsync(IFrame frame, string functionDeclaration, params object[] args)
        {
            if (frame == null)
            {
                return Array.Empty<IElementHandle>();
            }

            string call = "(" + functionDeclaration + ").apply(null, " + JsonSerializer.Serialize(args ?? Array.Empty<object>()) + ")";
            IJSHandle arrayHandle = await frame.EvaluateHandleAsync(call).ConfigureAwait(false);
            if (arrayHandle == null)
            {
                return Array.Empty<IElementHandle>();
            }

            int count = await arrayHandle.EvaluateAsync<int>("a => a && a.length ? a.length : 0").ConfigureAwait(false);
            List<IElementHandle> list = new List<IElementHandle>(count);
            for (int i = 0; i < count; i++)
            {
                IJSHandle item = await arrayHandle.EvaluateHandleAsync("(a, idx) => a[idx]", i).ConfigureAwait(false);
                IElementHandle element = item?.AsElement();
                if (element != null)
                {
                    list.Add(element);
                }
            }

            return list;
        }

        /// <summary>
        /// Polls <paramref name="functionDeclaration"/> until an element matches.
        /// </summary>
        /// <param name="frame">The frame to query.</param>
        /// <param name="functionDeclaration">A JS function returning an Element or null.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <param name="apiName">Name used in the timeout message.</param>
        /// <param name="args">Arguments passed to the function.</param>
        /// <returns>The first matching element.</returns>
        internal static Task<IElementHandle> WaitAsync(
            IFrame frame,
            string functionDeclaration,
            float? timeout,
            string apiName,
            params object[] args)
            => GetByWaiter.WaitAsync(
                () => QueryAsync(frame, functionDeclaration, args),
                timeout,
                apiName);
    }
}
