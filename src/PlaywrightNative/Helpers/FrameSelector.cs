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
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>internal:control=enter-frame</c>,
    /// <c>internal:control=any-frame</c>, and leftover
    /// <c>internal:control=pierce-frames</c> resolution.
    /// Extra optionals stay at the end.
    /// </summary>
    internal static class FrameSelector
    {
        internal const string AnyFrameToken = "internal:control=any-frame";

        /// <summary>
        /// Whether <paramref name="frame"/> can be queried now without waiting
        /// for an execution context (stalled iframes stay empty).
        /// </summary>
        /// <param name="frame">A frame to inspect.</param>
        /// <returns><see langword="true"/> when the document is already usable.</returns>
        internal static bool HasQueryableContext(IFrame frame)
        {
            if (frame == null || frame.IsDetached)
            {
                return false;
            }

            if (frame is Chromium.ChromiumFrame chromium)
            {
                return chromium.Frame != null && chromium.Frame.ExecutionContext != null;
            }

            if (frame is WebKit.WebKitFrame webkit)
            {
                return webkit.HasQueryableContext();
            }

            return true;
        }

        /// <summary>
        /// Whether <paramref name="selector"/> uses a frame-control engine.
        /// </summary>
        /// <param name="selector">A locator or query selector.</param>
        /// <returns><see langword="true"/> when C# must cross frame documents.</returns>
        internal static bool ContainsControl(string selector)
            => !string.IsNullOrEmpty(selector)
                && (selector.Contains("internal:control=enter-frame", StringComparison.Ordinal)
                    || selector.Contains("internal:control=pierce-frames", StringComparison.Ordinal)
                    || selector.Contains("internal:control=any-frame", StringComparison.Ordinal)
                    || selector.Contains("internal:control = enter-frame", StringComparison.Ordinal)
                    || selector.Contains("internal:control = pierce-frames", StringComparison.Ordinal)
                    || selector.Contains("internal:control = any-frame", StringComparison.Ordinal));

        /// <summary>
        /// Whether <paramref name="selector"/> starts with pierce-frames.
        /// </summary>
        /// <param name="selector">A locator or query selector.</param>
        /// <returns><see langword="true"/> when matches may come from many frames.</returns>
        internal static bool ContainsPierce(string selector)
        {
            if (!ContainsControl(selector))
            {
                return false;
            }

            IReadOnlyList<string> parts = Split(selector);
            return parts.Count > 0 && IsPierce(parts[0]);
        }

        /// <summary>
        /// Whether <paramref name="selector"/> starts with any-frame.
        /// </summary>
        /// <param name="selector">A locator or query selector.</param>
        /// <returns><see langword="true"/> when official <c>frameLocator()</c> search applies.</returns>
        internal static bool ContainsAnyFrame(string selector)
        {
            if (!ContainsControl(selector))
            {
                return false;
            }

            IReadOnlyList<string> parts = Split(selector);
            return parts.Count > 0 && IsAnyFrame(parts[0]);
        }

        /// <summary>
        /// Queries every match, crossing iframe documents as requested.
        /// </summary>
        /// <param name="frame">Starting frame.</param>
        /// <param name="scope">Optional element scope for the first chunk.</param>
        /// <param name="selector">Full selector including control tokens.</param>
        /// <returns>Matching element handles in document / frame order.</returns>
        internal static async Task<IReadOnlyList<IElementHandle>> QueryAllAsync(
            IFrame frame,
            IElementHandle scope,
            string selector)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            IReadOnlyList<string> parts = Split(selector);
            Validate(parts, selector);

            int index = 0;
            if (IsAnyFrame(parts[0]))
            {
                index = 1;
                if (index >= parts.Count)
                {
                    throw new PlaywrightNativeException("Selector cannot be empty after frameLocator()");
                }

                return await QueryAnyFrameAsync(frame, scope, parts, index, selector).ConfigureAwait(false);
            }

            if (IsPierce(parts[0]))
            {
                index = 1;
                if (index >= parts.Count)
                {
                    throw new PlaywrightNativeException("Selector cannot end with entering frame");
                }

                return await QueryPierceAsync(frame, scope, parts, index, selector).ConfigureAwait(false);
            }

            return await QueryEnterChainAsync(frame, scope, parts, index, selector).ConfigureAwait(false);
        }

        /// <summary>
        /// Official <c>$$eval</c> for a frame-control selector.
        /// Extra optionals stay at the end.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="frame">Starting frame.</param>
        /// <param name="scope">Optional element scope.</param>
        /// <param name="selector">Full selector including control tokens.</param>
        /// <param name="expression">Function receiving the element array.</param>
        /// <param name="arg">Optional second argument.</param>
        /// <returns>The function result.</returns>
        internal static async Task<T> EvalOnAllAsync<T>(
            IFrame frame,
            IElementHandle scope,
            string selector,
            string expression,
            object arg = null)
        {
            IReadOnlyList<IElementHandle> matches = await QueryAllAsync(frame, scope, selector).ConfigureAwait(false);
            IFrame owner = frame;
            if (matches.Count > 0)
            {
                IFrame found = await matches[0].OwnerFrameAsync().ConfigureAwait(false);
                if (found != null)
                {
                    owner = found;
                }
            }

            IJSHandle array = await owner.EvaluateHandleAsync("() => []").ConfigureAwait(false);
            try
            {
                for (int i = 0; i < matches.Count; i++)
                {
                    await matches[i].EvaluateAsync("(el, arr) => { arr.push(el); }", array).ConfigureAwait(false);
                }

                return await array.EvaluateAsync<T>(expression, arg).ConfigureAwait(false);
            }
            finally
            {
                await array.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Whether <paramref name="handles"/> come from more than one frame.
        /// </summary>
        /// <param name="handles">Resolved elements.</param>
        /// <returns><see langword="true"/> when pierce matched multiple frames.</returns>
        internal static async Task<bool> FromMultipleFramesAsync(IReadOnlyList<IElementHandle> handles)
        {
            if (handles == null || handles.Count < 2)
            {
                return false;
            }

            IFrame first = await handles[0].OwnerFrameAsync().ConfigureAwait(false);
            for (int i = 1; i < handles.Count; i++)
            {
                IFrame other = await handles[i].OwnerFrameAsync().ConfigureAwait(false);
                if (!ReferenceEquals(first, other))
                {
                    return true;
                }
            }

            return false;
        }

        private static async Task<IReadOnlyList<IElementHandle>> QueryAnyFrameAsync(
            IFrame frame,
            IElementHandle scope,
            IReadOnlyList<string> parts,
            int startIndex,
            string selector)
        {
            List<IElementHandle> results = new List<IElementHandle>();
            if (!HasQueryableContext(frame))
            {
                return results;
            }

            AddRange(results, await QueryEnterChainAsync(frame, scope, parts, startIndex, selector).ConfigureAwait(false));

            IReadOnlyList<IElementHandle> childHosts = await QueryChunkAsync(frame, scope, "iframe, frame").ConfigureAwait(false);
            for (int i = 0; i < childHosts.Count; i++)
            {
                IFrame child = await childHosts[i].ContentFrameAsync().ConfigureAwait(false);
                if (child != null)
                {
                    AddRange(results, await QueryAnyFrameAsync(child, null, parts, startIndex, selector).ConfigureAwait(false));
                }
            }

            return results;
        }

        private static async Task<IReadOnlyList<IElementHandle>> QueryPierceAsync(
            IFrame frame,
            IElementHandle scope,
            IReadOnlyList<string> parts,
            int startIndex,
            string selector)
        {
            List<IElementHandle> results = new List<IElementHandle>();
            AddRange(results, await QueryEnterChainAsync(frame, scope, parts, startIndex, selector).ConfigureAwait(false));

            IReadOnlyList<IElementHandle> childHosts = await QueryChunkAsync(frame, scope, "iframe, frame").ConfigureAwait(false);
            for (int i = 0; i < childHosts.Count; i++)
            {
                IFrame child = await childHosts[i].ContentFrameAsync().ConfigureAwait(false);
                if (child != null)
                {
                    AddRange(results, await QueryPierceAsync(child, null, parts, startIndex, selector).ConfigureAwait(false));
                }
            }

            int firstEnter = IndexOfEnter(parts, startIndex);
            int end = firstEnter < 0 ? parts.Count : firstEnter;
            for (int split = startIndex + 1; split < end; split++)
            {
                string prefix = JoinParts(parts, startIndex, split);
                IReadOnlyList<IElementHandle> prefixMatches = await QueryChunkAsync(frame, scope, prefix).ConfigureAwait(false);
                for (int m = 0; m < prefixMatches.Count; m++)
                {
                    IReadOnlyList<IElementHandle> nested = await QueryChunkAsync(frame, prefixMatches[m], "iframe, frame").ConfigureAwait(false);
                    for (int n = 0; n < nested.Count; n++)
                    {
                        IFrame child = await nested[n].ContentFrameAsync().ConfigureAwait(false);
                        if (child != null)
                        {
                            AddRange(results, await QueryPierceAsync(child, null, parts, split, selector).ConfigureAwait(false));
                        }
                    }
                }
            }

            return results;
        }

        private static async Task<IReadOnlyList<IElementHandle>> QueryEnterChainAsync(
            IFrame frame,
            IElementHandle scope,
            IReadOnlyList<string> parts,
            int index,
            string selector)
        {
            string chunk = TakeChunk(parts, ref index);
            List<IElementHandle> current = new List<IElementHandle>();
            AddRange(current, await QueryChunkAsync(frame, scope, chunk).ConfigureAwait(false));

            while (index < parts.Count)
            {
                if (IsAnyFrame(parts[index]))
                {
                    throw new PlaywrightNativeException("\"any-frame\" is only allowed as the first selector token");
                }

                if (IsPierce(parts[index]))
                {
                    throw new PlaywrightNativeException("\"pierce-frames\" is only allowed as the first selector token");
                }

                if (!IsEnter(parts[index]))
                {
                    throw new PlaywrightNativeException("Selector cannot start with entering frame, select the iframe first");
                }

                index++;
                if (index >= parts.Count)
                {
                    throw new PlaywrightNativeException("Selector cannot end with entering frame, while parsing selector " + selector);
                }

                List<IFrame> entered = new List<IFrame>();
                for (int i = 0; i < current.Count; i++)
                {
                    IFrame content = await EnterFrameAsync(current[i]).ConfigureAwait(false);
                    if (content != null)
                    {
                        entered.Add(content);
                    }
                }

                current.Clear();
                chunk = TakeChunk(parts, ref index);
                for (int e = 0; e < entered.Count; e++)
                {
                    AddRange(current, await QueryChunkAsync(entered[e], null, chunk).ConfigureAwait(false));
                }
            }

            return current;
        }

        private static async Task<IReadOnlyList<IElementHandle>> QueryChunkAsync(
            IFrame frame,
            IElementHandle scope,
            string chunk)
        {
            if (ContainsControl(chunk))
            {
                throw new PlaywrightNativeException("Selector cannot start with entering frame, select the iframe first");
            }

            try
            {
                if (scope == null)
                {
                    return await frame.QuerySelectorAllAsync(chunk).ConfigureAwait(false);
                }

                return await scope.QuerySelectorAllAsync(chunk).ConfigureAwait(false);
            }
            catch (Exception ex) when (
                ex is TimeoutException
                || PlaywrightNativeException.IsDestroyedContext(ex as PlaywrightNativeException)
                || (ex.Message != null && (
                    ex.Message.Contains("Missing injected script", StringComparison.OrdinalIgnoreCase)
                    || ex.Message.Contains("Execution context", StringComparison.OrdinalIgnoreCase))))
            {
                return Array.Empty<IElementHandle>();
            }
        }

        private static async Task<IFrame> EnterFrameAsync(IElementHandle host)
        {
            IFrame content = await host.ContentFrameAsync().ConfigureAwait(false);
            if (content != null)
            {
                return content;
            }

            string nodeName = await host.EvaluateAsync<string>("el => el && el.nodeName ? String(el.nodeName) : ''").ConfigureAwait(false);
            if (string.Equals(nodeName, "IFRAME", StringComparison.OrdinalIgnoreCase)
                || string.Equals(nodeName, "FRAME", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string html = await host.EvaluateAsync<string>("el => el.outerHTML").ConfigureAwait(false);
            throw new PlaywrightNativeException((html ?? string.Empty) + "\n<iframe> was expected");
        }

        private static void Validate(IReadOnlyList<string> parts, string selector)
        {
            if (parts.Count == 0)
            {
                throw new PlaywrightNativeException("Selector cannot be empty");
            }

            if (IsEnter(parts[0]))
            {
                throw new PlaywrightNativeException("Selector cannot start with entering frame, select the iframe first");
            }

            if (IsEnter(parts[parts.Count - 1]))
            {
                throw new PlaywrightNativeException("Selector cannot end with entering frame, while parsing selector " + selector);
            }

            int captureIndex = -1;
            int firstEnter = -1;
            for (int i = 0; i < parts.Count; i++)
            {
                if (IsAnyFrame(parts[i]))
                {
                    if (i != 0)
                    {
                        throw new PlaywrightNativeException("\"any-frame\" is only allowed as the first selector token");
                    }

                    continue;
                }

                if (IsPierce(parts[i]))
                {
                    if (i != 0)
                    {
                        throw new PlaywrightNativeException("\"pierce-frames\" is only allowed as the first selector token");
                    }

                    continue;
                }

                if (IsEnter(parts[i]))
                {
                    if (firstEnter < 0)
                    {
                        firstEnter = i;
                    }

                    continue;
                }

                if (IsCapture(parts[i]) && captureIndex < 0)
                {
                    captureIndex = i;
                }
            }

            if (captureIndex >= 0 && firstEnter >= 0 && captureIndex < firstEnter)
            {
                throw new PlaywrightNativeException("Can not capture the selector before diving into the frame. Only use * after the last frame has been selected");
            }
        }

        private static string TakeChunk(IReadOnlyList<string> parts, ref int index)
        {
            List<string> chunk = new List<string>();
            while (index < parts.Count && !IsEnter(parts[index]) && !IsPierce(parts[index]) && !IsAnyFrame(parts[index]))
            {
                chunk.Add(parts[index]);
                index++;
            }

            if (chunk.Count == 0)
            {
                throw new PlaywrightNativeException("Selector cannot start with entering frame, select the iframe first");
            }

            return string.Join(" >> ", chunk);
        }

        private static bool IsEnter(string part)
        {
            string t = NormalizeControl(part);
            return string.Equals(t, "internal:control=enter-frame", StringComparison.Ordinal);
        }

        private static bool IsPierce(string part)
        {
            string t = NormalizeControl(part);
            return string.Equals(t, "internal:control=pierce-frames", StringComparison.Ordinal);
        }

        private static bool IsAnyFrame(string part)
        {
            string t = NormalizeControl(part);
            return string.Equals(t, AnyFrameToken, StringComparison.Ordinal);
        }

        private static string NormalizeControl(string part)
        {
            string trimmed = (part ?? string.Empty).Trim();
            int equals = trimmed.IndexOf('=');
            if (equals <= 0)
            {
                return trimmed;
            }

            return trimmed.Substring(0, equals).Trim() + "=" + trimmed.Substring(equals + 1).Trim();
        }

        private static bool IsCapture(string part)
        {
            string trimmed = (part ?? string.Empty).Trim();
            return trimmed.Length > 1 && trimmed[0] == '*';
        }

        private static IReadOnlyList<string> Split(string selector)
        {
            List<string> parts = new List<string>();
            if (string.IsNullOrEmpty(selector))
            {
                return parts;
            }

            System.Text.StringBuilder current = new System.Text.StringBuilder();
            char quote = '\0';
            for (int i = 0; i < selector.Length; i++)
            {
                char c = selector[i];
                if (quote != '\0')
                {
                    current.Append(c);
                    if (c == '\\' && i + 1 < selector.Length)
                    {
                        current.Append(selector[++i]);
                        continue;
                    }

                    if (c == quote)
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (c == '"' || c == '\'' || c == '`')
                {
                    quote = c;
                    current.Append(c);
                    continue;
                }

                if (c == '>' && i + 1 < selector.Length && selector[i + 1] == '>')
                {
                    string part = current.ToString().Trim();
                    if (part.Length > 0)
                    {
                        parts.Add(part);
                    }

                    current.Clear();
                    i++;
                    continue;
                }

                current.Append(c);
            }

            string last = current.ToString().Trim();
            if (last.Length > 0)
            {
                parts.Add(last);
            }

            return parts;
        }

        private static int IndexOfEnter(IReadOnlyList<string> parts, int start)
        {
            for (int i = start; i < parts.Count; i++)
            {
                if (IsEnter(parts[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string JoinParts(IReadOnlyList<string> parts, int start, int end)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = start; i < end; i++)
            {
                if (builder.Length > 0)
                {
                    builder.Append(" >> ");
                }

                builder.Append(parts[i]);
            }

            return builder.ToString();
        }

        private static void AddRange(List<IElementHandle> target, IReadOnlyList<IElementHandle> source)
        {
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null)
                {
                    target.Add(source[i]);
                }
            }
        }
    }
}
