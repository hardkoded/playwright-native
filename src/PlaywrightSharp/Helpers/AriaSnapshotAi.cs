// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Official AI-mode <c>ariaSnapshot</c>: frame prefixes, iframe stitch,
    /// and <c>aria-ref</c> lookup.
    /// </summary>
    internal static class AriaSnapshotAi
    {
        internal const string FindRefFunction = @"(ref) => {
  const want = String(ref || '');
  const visit = (el) => {
    if (!el || el.nodeType !== 1) return null;
    if (el._ariaRef && el._ariaRef.ref === want) return el;
    const kids = el.children || [];
    for (let i = 0; i < kids.length; i++) {
      const hit = visit(kids[i]);
      if (hit) return hit;
    }
    if (el.shadowRoot) {
      const sk = el.shadowRoot.children || [];
      for (let i = 0; i < sk.length; i++) {
        const hit = visit(sk[i]);
        if (hit) return hit;
      }
    }
    return null;
  };
  return visit(document.documentElement);
}";

        internal const string ReadPrefixFunction = @"() => {
  if (window.__pwAriaFramePrefix === undefined) return null;
  return String(window.__pwAriaFramePrefix);
}";

        internal const string WritePrefixFunction = @"(p) => { window.__pwAriaFramePrefix = String(p); return true; }";

        private static readonly ConditionalWeakTable<IPage, State> PageState = new ConditionalWeakTable<IPage, State>();

        /// <summary>
        /// Official page-level AI snapshot (waits for body, stitches frames).
        /// </summary>
        /// <param name="page">Page to snapshot.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <param name="depth">Maximum descendant level, or <see langword="null"/>.</param>
        /// <param name="boxes">When <see langword="true"/>, append boxes.</param>
        /// <returns>Stitched AI YAML.</returns>
        internal static async Task<string> CapturePageAsync(IPage page, float? timeout, int? depth, bool boxes)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            try
            {
                await page.WaitForFunctionAsync(
                    "() => document.readyState === 'complete'",
                    timeout: timeout).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
            }
            catch (PlaywrightSharpException)
            {
            }

            IElementHandle root = await page.Locator("body, frameset").First.ElementHandleAsync(timeout).ConfigureAwait(false);
            await EnsurePrefixesAsync(page).ConfigureAwait(false);
            IFrame frame = page.MainFrame;
            string prefix = await PrefixForAsync(page, frame).ConfigureAwait(false);
            string yaml = await AriaSnapshotOfficialAi.CaptureYamlAsync(root, depth, boxes, prefix).ConfigureAwait(false);
            return await StitchAsync(page, frame, yaml, depth, boxes, timeout).ConfigureAwait(false);
        }

        /// <summary>
        /// Official page-level AI JSON snapshot (waits for body, stitches frames).
        /// </summary>
        /// <param name="page">Page to snapshot.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <param name="depth">Maximum descendant level, or <see langword="null"/>.</param>
        /// <param name="boxes">When <see langword="true"/>, include boxes.</param>
        /// <returns>Stitched AI JSON.</returns>
        internal static async Task<string> CapturePageJsonAsync(IPage page, float? timeout, int? depth, bool boxes)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            try
            {
                await page.WaitForFunctionAsync(
                    "() => document.readyState === 'complete'",
                    timeout: timeout).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
            }
            catch (PlaywrightSharpException)
            {
            }

            IElementHandle root = await page.Locator("body, frameset").First.ElementHandleAsync(timeout).ConfigureAwait(false);
            await EnsurePrefixesAsync(page).ConfigureAwait(false);
            IFrame frame = page.MainFrame;
            string prefix = await PrefixForAsync(page, frame).ConfigureAwait(false);
            string json = await AriaSnapshotOfficialAi.CaptureJsonAsync(root, depth, boxes, prefix).ConfigureAwait(false);
            return await StitchJsonAsync(page, frame, json, depth, boxes, timeout).ConfigureAwait(false);
        }

        /// <summary>
        /// Official element-level AI snapshot.
        /// </summary>
        /// <param name="root">Snapshot root.</param>
        /// <param name="depth">Maximum descendant level, or <see langword="null"/>.</param>
        /// <param name="boxes">When <see langword="true"/>, append boxes.</param>
        /// <returns>Stitched AI YAML.</returns>
        internal static async Task<string> CaptureElementAsync(IElementHandle root, int? depth, bool boxes)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            IFrame owner = await root.OwnerFrameAsync().ConfigureAwait(false);
            IPage page = owner?.Page;
            if (page == null)
            {
                throw new PlaywrightSharpException("Cannot take an aria snapshot of a detached element.");
            }

            await EnsurePrefixesAsync(page).ConfigureAwait(false);
            string prefix = await PrefixForAsync(page, owner).ConfigureAwait(false);
            string yaml = await AriaSnapshotOfficialAi.CaptureYamlAsync(root, depth, boxes, prefix).ConfigureAwait(false);
            return await StitchAsync(page, owner, yaml, depth, boxes, 2000).ConfigureAwait(false);
        }

        /// <summary>
        /// Official element-level AI JSON snapshot.
        /// </summary>
        /// <param name="root">Snapshot root.</param>
        /// <param name="depth">Maximum descendant level, or <see langword="null"/>.</param>
        /// <param name="boxes">When <see langword="true"/>, include boxes.</param>
        /// <returns>Stitched AI JSON.</returns>
        internal static async Task<string> CaptureElementJsonAsync(IElementHandle root, int? depth, bool boxes)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            IFrame owner = await root.OwnerFrameAsync().ConfigureAwait(false);
            IPage page = owner?.Page;
            if (page == null)
            {
                throw new PlaywrightSharpException("Cannot take an aria snapshot of a detached element.");
            }

            await EnsurePrefixesAsync(page).ConfigureAwait(false);
            string prefix = await PrefixForAsync(page, owner).ConfigureAwait(false);
            string json = await AriaSnapshotOfficialAi.CaptureJsonAsync(root, depth, boxes, prefix).ConfigureAwait(false);
            return await StitchJsonAsync(page, owner, json, depth, boxes, 2000).ConfigureAwait(false);
        }

        /// <summary>
        /// Finds the element that last received <paramref name="ariaRef"/>,
        /// searching <paramref name="start"/> and descendant frames.
        /// </summary>
        /// <param name="start">Frame to start from.</param>
        /// <param name="ariaRef">Full ref, such as <c>e2</c> or <c>f1e2</c>.</param>
        /// <returns>The matching element, or <see langword="null"/>.</returns>
        internal static Task<IElementHandle> FindAsync(IFrame start, string ariaRef)
            => FindAsync(start, ariaRef, descendants: true);

        /// <summary>
        /// Finds the element that last received <paramref name="ariaRef"/>.
        /// </summary>
        /// <param name="start">Frame to start from.</param>
        /// <param name="ariaRef">Full ref, such as <c>e2</c> or <c>f1e2</c>.</param>
        /// <param name="descendants">
        /// When <see langword="true"/>, search <paramref name="start"/> and
        /// descendant frames. When <see langword="false"/>, search only
        /// <paramref name="start"/>.
        /// </param>
        /// <returns>The matching element, or <see langword="null"/>.</returns>
        internal static async Task<IElementHandle> FindAsync(IFrame start, string ariaRef, bool descendants)
        {
            if (start == null || string.IsNullOrEmpty(ariaRef))
            {
                return null;
            }

            if (!descendants)
            {
                return await FindInFrameAsync(start, ariaRef).ConfigureAwait(false);
            }

            List<IFrame> frames = new List<IFrame>();
            CollectFrames(start, frames);
            for (int i = 0; i < frames.Count; i++)
            {
                IElementHandle hit = await FindInFrameAsync(frames[i], ariaRef).ConfigureAwait(false);
                if (hit != null)
                {
                    return hit;
                }
            }

            return null;
        }

        /// <summary>
        /// Whether <paramref name="selector"/> is an <c>aria-ref=</c> engine.
        /// </summary>
        /// <param name="selector">Locator selector.</param>
        /// <param name="ariaRef">Parsed ref body when this is an aria-ref selector.</param>
        /// <returns><see langword="true"/> when the selector is <c>aria-ref=…</c>.</returns>
        internal static bool TryParse(string selector, out string ariaRef)
        {
            ariaRef = null;
            if (string.IsNullOrEmpty(selector))
            {
                return false;
            }

            const string prefix = "aria-ref=";
            if (!selector.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            ariaRef = selector.Substring(prefix.Length);
            return true;
        }

        private static async Task<string> StitchAsync(
            IPage page,
            IFrame frame,
            string yaml,
            int? depth,
            bool boxes,
            float? timeout,
            int depthOffset = 0)
        {
            if (string.IsNullOrEmpty(yaml))
            {
                return yaml ?? string.Empty;
            }

            Regex iframeLine = new Regex(@"^(\s*)- iframe\b.*\[ref=([^\]]+)\]", RegexOptions.CultureInvariant);
            string[] lines = yaml.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (i > 0)
                {
                    result.Append('\n');
                }

                Match match = iframeLine.Match(line);
                if (!match.Success)
                {
                    result.Append(line);
                    continue;
                }

                int lineDepth = depthOffset + (match.Groups[1].Value.Length / 2);
                if (depth != null && lineDepth >= depth.Value)
                {
                    result.Append(line);
                    continue;
                }

                string ariaRef = match.Groups[2].Value;
                IElementHandle iframeEl = await FindInFrameAsync(frame, ariaRef).ConfigureAwait(false);
                IFrame child = null;
                if (iframeEl != null)
                {
                    try
                    {
                        child = await iframeEl.ContentFrameAsync().ConfigureAwait(false);
                    }
                    catch (PlaywrightSharpException)
                    {
                        child = null;
                    }
                }

                if (child == null || child.IsDetached)
                {
                    result.Append(line);
                    continue;
                }

                int startDepth = lineDepth + 1;
                string childYaml;
                try
                {
                    IElementHandle childRoot = await child.QuerySelectorAsync("body, frameset").ConfigureAwait(false);
                    if (childRoot == null)
                    {
                        result.Append(line);
                        continue;
                    }

                    string prefix = await PrefixForAsync(page, child).ConfigureAwait(false);
                    childYaml = await AriaSnapshotOfficialAi
                        .CaptureYamlAsync(childRoot, depth, boxes, prefix, startDepth)
                        .ConfigureAwait(false);
                    childYaml = await StitchAsync(page, child, childYaml, depth, boxes, timeout, startDepth).ConfigureAwait(false);
                }
                catch (PlaywrightSharpException)
                {
                    result.Append(line);
                    continue;
                }
                catch (TimeoutException)
                {
                    result.Append(line);
                    continue;
                }

                if (string.IsNullOrEmpty(childYaml))
                {
                    result.Append(line);
                    continue;
                }

                string indent = match.Groups[1].Value + "  ";
                bool hasColon = line.TrimEnd().EndsWith(':');
                result.Append(hasColon ? line : line + ":");
                string[] childLines = childYaml.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
                for (int c = 0; c < childLines.Length; c++)
                {
                    if (string.IsNullOrEmpty(childLines[c]))
                    {
                        continue;
                    }

                    result.Append('\n');
                    result.Append(indent);
                    result.Append(childLines[c]);
                }
            }

            return result.ToString();
        }

        private static async Task<string> StitchJsonAsync(
            IPage page,
            IFrame frame,
            string json,
            int? depth,
            bool boxes,
            float? timeout,
            int startDepth = 0)
        {
            if (string.IsNullOrEmpty(json))
            {
                return "[]";
            }

            JsonNode root;
            try
            {
                root = JsonNode.Parse(json);
            }
            catch (System.Text.Json.JsonException)
            {
                return json;
            }

            if (root == null)
            {
                return "[]";
            }

            await WalkJsonAsync(page, frame, root, depth, boxes, timeout, startDepth).ConfigureAwait(false);
            return root.ToJsonString();
        }

        private static async Task WalkJsonAsync(
            IPage page,
            IFrame frame,
            JsonNode node,
            int? depth,
            bool boxes,
            float? timeout,
            int nodeDepth)
        {
            if (node is JsonArray array)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    await WalkJsonAsync(page, frame, array[i], depth, boxes, timeout, nodeDepth).ConfigureAwait(false);
                }

                return;
            }

            if (node is not JsonObject obj)
            {
                return;
            }

            string role = obj["role"]?.GetValue<string>();
            string ariaRef = obj["ref"]?.GetValue<string>();
            if (string.Equals(role, "iframe", StringComparison.Ordinal) && !string.IsNullOrEmpty(ariaRef)
                && (depth == null || nodeDepth < depth.Value))
            {
                (IFrame childFrame, JsonArray childNodes) = await CaptureFrameJsonAsync(
                    page, frame, ariaRef, depth, boxes, nodeDepth + 1).ConfigureAwait(false);
                if (childNodes != null && childFrame != null)
                {
                    obj["children"] = childNodes;
                    await WalkJsonAsync(page, childFrame, childNodes, depth, boxes, timeout, nodeDepth + 1).ConfigureAwait(false);
                    return;
                }
            }

            if (obj["children"] is JsonArray children)
            {
                await WalkJsonAsync(page, frame, children, depth, boxes, timeout, nodeDepth + 1).ConfigureAwait(false);
            }
        }

        private static async Task<(IFrame Frame, JsonArray Nodes)> CaptureFrameJsonAsync(
            IPage page,
            IFrame frame,
            string ariaRef,
            int? depth,
            bool boxes,
            int startDepth)
        {
            IElementHandle iframeEl = await FindInFrameAsync(frame, ariaRef).ConfigureAwait(false);
            IFrame child = null;
            if (iframeEl != null)
            {
                try
                {
                    child = await iframeEl.ContentFrameAsync().ConfigureAwait(false);
                }
                catch (PlaywrightSharpException)
                {
                    child = null;
                }
            }

            if (child == null || child.IsDetached)
            {
                return (null, null);
            }

            try
            {
                IElementHandle childRoot = await child.QuerySelectorAsync("body, frameset").ConfigureAwait(false);
                if (childRoot == null)
                {
                    return (null, null);
                }

                string prefix = await PrefixForAsync(page, child).ConfigureAwait(false);
                string childJson = await AriaSnapshotOfficialAi
                    .CaptureJsonAsync(childRoot, depth, boxes, prefix, startDepth)
                    .ConfigureAwait(false);
                JsonNode parsed = JsonNode.Parse(childJson ?? "[]");
                return (child, parsed as JsonArray);
            }
            catch (PlaywrightSharpException)
            {
                return (null, null);
            }
            catch (TimeoutException)
            {
                return (null, null);
            }
            catch (System.Text.Json.JsonException)
            {
                return (null, null);
            }
        }

        private static async Task EnsurePrefixesAsync(IPage page)
        {
            Queue<IFrame> queue = new Queue<IFrame>();
            IFrame main = page.MainFrame;
            if (main == null)
            {
                return;
            }

            queue.Enqueue(main);
            HashSet<IFrame> seen = new HashSet<IFrame>();
            while (queue.Count > 0)
            {
                IFrame frame = queue.Dequeue();
                if (frame == null || frame.IsDetached || !seen.Add(frame))
                {
                    continue;
                }

                try
                {
                    await PrefixForAsync(page, frame).ConfigureAwait(false);
                }
                catch (PlaywrightSharpException)
                {
                    continue;
                }
                catch (TimeoutException)
                {
                    continue;
                }

                IReadOnlyList<IElementHandle> hosts;
                try
                {
                    hosts = await frame.QuerySelectorAllAsync("iframe, frame").ConfigureAwait(false);
                }
                catch (PlaywrightSharpException)
                {
                    continue;
                }
                catch (TimeoutException)
                {
                    continue;
                }

                for (int i = 0; i < hosts.Count; i++)
                {
                    IFrame child;
                    try
                    {
                        child = await hosts[i].ContentFrameAsync().ConfigureAwait(false);
                    }
                    catch (PlaywrightSharpException)
                    {
                        continue;
                    }

                    if (child != null && !child.IsDetached)
                    {
                        queue.Enqueue(child);
                    }
                }
            }
        }

        private static async Task<string> PrefixForAsync(IPage page, IFrame frame)
        {
            State state = GetState(page);
            try
            {
                string existing = await frame.EvaluateAsync<string>(ReadPrefixFunction).ConfigureAwait(false);
                if (existing != null)
                {
                    return existing;
                }
            }
            catch (PlaywrightSharpException)
            {
            }
            catch (TimeoutException)
            {
                if (frame.ParentFrame != null)
                {
                    return string.Empty;
                }
            }

            string prefix;
            if (frame.ParentFrame == null && !state.UsedEmptyMainPrefix)
            {
                prefix = string.Empty;
                state.UsedEmptyMainPrefix = true;
            }
            else
            {
                state.NextFrameId++;
                prefix = "f" + state.NextFrameId.ToString(CultureInfo.InvariantCulture);
            }

            try
            {
                await frame.EvaluateAsync<object>(WritePrefixFunction, prefix).ConfigureAwait(false);
            }
            catch (PlaywrightSharpException)
            {
            }

            return prefix;
        }

        private static async Task<IElementHandle> FindInFrameAsync(IFrame frame, string ariaRef)
        {
            if (frame == null || frame.IsDetached || string.IsNullOrEmpty(ariaRef))
            {
                return null;
            }

            try
            {
                IJSHandle handle = await frame.EvaluateHandleAsync(FindRefFunction, ariaRef).ConfigureAwait(false);
                return handle?.AsElement();
            }
            catch (PlaywrightSharpException)
            {
                return null;
            }
        }

        private static void CollectFrames(IFrame start, List<IFrame> into)
        {
            if (start == null || start.IsDetached)
            {
                return;
            }

            into.Add(start);
            IReadOnlyCollection<IFrame> children = start.ChildFrames;
            if (children == null)
            {
                return;
            }

            foreach (IFrame child in children)
            {
                CollectFrames(child, into);
            }
        }

        private static State GetState(IPage page)
        {
            if (!PageState.TryGetValue(page, out State state))
            {
                state = new State();
                PageState.Add(page, state);
            }

            return state;
        }

        private sealed class State
        {
            internal int NextFrameId { get; set; }

            internal bool UsedEmptyMainPrefix { get; set; }
        }
    }
}
