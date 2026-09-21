// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
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

        /// <summary>
        /// Parent-document check for <c>loading=lazy</c> without callFunctionOn
        /// on the iframe objectId (Darwin wedges that path for unloaded lazy frames).
        /// </summary>
        private const string IsLazyIframeRefFunction = @"(ref) => {
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
  const el = visit(document.documentElement);
  if (el) {
    return (el.getAttribute('loading') || '').toLowerCase() === 'lazy';
  }
  const frames = document.querySelectorAll('iframe, frame');
  if (!frames.length) return false;
  for (let i = 0; i < frames.length; i++) {
    if ((frames[i].getAttribute('loading') || '').toLowerCase() !== 'lazy') return false;
  }
  return true;
}";

        /// <summary>
        /// Parent-document capture-ready check by aria-ref (no iframe objectId).
        /// </summary>
        private const string IframeCaptureReadyByRefFunction = @"(ref) => {
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
  const el = visit(document.documentElement);
  if (!el) return true;
  try {
    const loading = (el.getAttribute('loading') || '').toLowerCase();
    if (loading === 'lazy') return false;
    return true;
  } catch (e) {
    return true;
  }
}";

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
                // Cap readyState wait so SnapshotForAI(timeout: 3000) cannot burn
                // the full NUnit budget when lazy iframes keep readyState busy.
                // interactive/complete still auto-waits navigations (reload).
                float waitMs = timeout ?? 3_000f;
                if (waitMs > 1_500f)
                {
                    waitMs = 1_500f;
                }

                await page.WaitForFunctionAsync(
                    "() => document.readyState === 'complete' || document.readyState === 'interactive'",
                    timeout: waitMs).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
            }
            catch (PlaywrightException)
            {
            }

            IElementHandle root = await page.Locator("body, frameset").First.ElementHandleAsync(timeout).ConfigureAwait(false);
            Stopwatch deadlineClock = Stopwatch.StartNew();
            int budgetMs = TimeoutSettings.TimeoutMs(timeout);
            if (budgetMs > 3_000)
            {
                budgetMs = 3_000;
            }

            await EnsurePrefixesAsync(page, deadlineClock, budgetMs).ConfigureAwait(false);
            IFrame frame = page.MainFrame;
            string prefix = await RaceOrDefaultAsync(
                () => PrefixForAsync(page, frame),
                deadlineClock,
                budgetMs,
                fallback: string.Empty).ConfigureAwait(false);
            string yaml = await RaceOrDefaultAsync(
                () => AriaSnapshotOfficialAi.CaptureYamlAsync(root, depth, boxes, prefix),
                deadlineClock,
                budgetMs,
                fallback: string.Empty).ConfigureAwait(false);
            if (string.IsNullOrEmpty(yaml))
            {
                return string.Empty;
            }

            return await StitchAsync(page, frame, yaml, depth, boxes, deadlineClock, budgetMs).ConfigureAwait(false);
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
                float waitMs = timeout ?? 3_000f;
                if (waitMs > 1_500f)
                {
                    waitMs = 1_500f;
                }

                await page.WaitForFunctionAsync(
                    "() => document.readyState === 'complete' || document.readyState === 'interactive'",
                    timeout: waitMs).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
            }
            catch (PlaywrightException)
            {
            }

            IElementHandle root = await page.Locator("body, frameset").First.ElementHandleAsync(timeout).ConfigureAwait(false);
            Stopwatch deadlineClock = Stopwatch.StartNew();
            int budgetMs = TimeoutSettings.TimeoutMs(timeout);
            if (budgetMs > 3_000)
            {
                budgetMs = 3_000;
            }

            await EnsurePrefixesAsync(page, deadlineClock, budgetMs).ConfigureAwait(false);
            IFrame frame = page.MainFrame;
            string prefix = await RaceOrDefaultAsync(
                () => PrefixForAsync(page, frame),
                deadlineClock,
                budgetMs,
                fallback: string.Empty).ConfigureAwait(false);
            string json = await RaceOrDefaultAsync(
                () => AriaSnapshotOfficialAi.CaptureJsonAsync(root, depth, boxes, prefix),
                deadlineClock,
                budgetMs,
                fallback: "[]").ConfigureAwait(false);
            return await StitchJsonAsync(page, frame, json, depth, boxes, deadlineClock, budgetMs).ConfigureAwait(false);
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
                throw new PlaywrightException("Cannot take an aria snapshot of a detached element.");
            }

            await EnsurePrefixesAsync(page, Stopwatch.StartNew(), 2000).ConfigureAwait(false);
            string prefix = await PrefixForAsync(page, owner).ConfigureAwait(false);
            string yaml = await AriaSnapshotOfficialAi.CaptureYamlAsync(root, depth, boxes, prefix).ConfigureAwait(false);
            return await StitchAsync(page, owner, yaml, depth, boxes, Stopwatch.StartNew(), 2000).ConfigureAwait(false);
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
                throw new PlaywrightException("Cannot take an aria snapshot of a detached element.");
            }

            await EnsurePrefixesAsync(page, Stopwatch.StartNew(), 2000).ConfigureAwait(false);
            string prefix = await PrefixForAsync(page, owner).ConfigureAwait(false);
            string json = await AriaSnapshotOfficialAi.CaptureJsonAsync(root, depth, boxes, prefix).ConfigureAwait(false);
            return await StitchJsonAsync(page, owner, json, depth, boxes, Stopwatch.StartNew(), 2000).ConfigureAwait(false);
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
            Stopwatch deadlineClock,
            int budgetMs,
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

                if (RemainingMs(deadlineClock, budgetMs) <= 0)
                {
                    result.Append(line);
                    continue;
                }

                string ariaRef = match.Groups[2].Value;
                string childYaml = await RaceOrDefaultAsync(
                    () => CaptureChildYamlAsync(page, frame, ariaRef, depth, boxes, deadlineClock, budgetMs, lineDepth + 1),
                    deadlineClock,
                    budgetMs,
                    fallback: null).ConfigureAwait(false);

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

        private static async Task<string> CaptureChildYamlAsync(
            IPage page,
            IFrame frame,
            string ariaRef,
            int? depth,
            bool boxes,
            Stopwatch deadlineClock,
            int budgetMs,
            int startDepth)
        {
            // Never resolve ContentFrame / evaluate on lazy iframe objectIds —
            // Darwin WebKit wedges the target for the full command timeout
            // (ReturnEmptySnapshotWhenIframeIsNotLoaded → NUnit 30s).
            if (await IsLazyIframeRefAsync(frame, ariaRef).ConfigureAwait(false))
            {
                return null;
            }

            // Name/src match first — does not need _ariaRef on the element and
            // avoids IsCaptureReady false-negatives when refs live in another world
            // (ShouldStitchAllFrameSnapshots on Windows).
            IFrame child = await ChildFrameForAriaRefAsync(frame, ariaRef).ConfigureAwait(false);
            if (child == null || child.IsDetached)
            {
                if (!await IsCaptureReadyIframeRefAsync(frame, ariaRef).ConfigureAwait(false))
                {
                    return null;
                }

                IElementHandle iframeEl = await RaceOrDefaultAsync(
                    () => FindInFrameAsync(frame, ariaRef),
                    deadlineClock,
                    Math.Min(300, RemainingMs(deadlineClock, budgetMs)),
                    fallback: null).ConfigureAwait(false);

                child = await ContentFrameOrNullAsync(iframeEl).ConfigureAwait(false);
                if (child == null || child.IsDetached)
                {
                    child = await RaceOrDefaultAsync(
                        () => ChildFrameByElementIdentityAsync(frame, iframeEl),
                        deadlineClock,
                        Math.Min(250, RemainingMs(deadlineClock, budgetMs)),
                        fallback: null).ConfigureAwait(false);
                }
            }

            if (child == null || child.IsDetached)
            {
                return null;
            }

            IElementHandle childRoot = await RaceOrDefaultAsync(
                () => child.QuerySelectorAsync("body, frameset"),
                deadlineClock,
                Math.Min(400, RemainingMs(deadlineClock, budgetMs)),
                fallback: null).ConfigureAwait(false);
            if (childRoot == null)
            {
                return null;
            }

            string prefix = await PrefixForAsync(page, child).ConfigureAwait(false);
            string childYaml = await AriaSnapshotOfficialAi
                .CaptureYamlAsync(childRoot, depth, boxes, prefix, startDepth)
                .ConfigureAwait(false);
            return await StitchAsync(page, child, childYaml, depth, boxes, deadlineClock, budgetMs, startDepth)
                .ConfigureAwait(false);
        }

        private static async Task<string> StitchJsonAsync(
            IPage page,
            IFrame frame,
            string json,
            int? depth,
            bool boxes,
            Stopwatch deadlineClock,
            int budgetMs,
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

            await WalkJsonAsync(page, frame, root, depth, boxes, deadlineClock, budgetMs, startDepth).ConfigureAwait(false);
            return root.ToJsonString();
        }

        private static async Task WalkJsonAsync(
            IPage page,
            IFrame frame,
            JsonNode node,
            int? depth,
            bool boxes,
            Stopwatch deadlineClock,
            int budgetMs,
            int nodeDepth)
        {
            if (node is JsonArray array)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    await WalkJsonAsync(page, frame, array[i], depth, boxes, deadlineClock, budgetMs, nodeDepth).ConfigureAwait(false);
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
                (IFrame childFrame, JsonArray childNodes) = await RaceOrDefaultAsync(
                    () => CaptureFrameJsonAsync(page, frame, ariaRef, depth, boxes, nodeDepth + 1),
                    deadlineClock,
                    budgetMs,
                    fallback: (null, null)).ConfigureAwait(false);
                if (childNodes != null && childFrame != null)
                {
                    obj["children"] = childNodes;
                    await WalkJsonAsync(page, childFrame, childNodes, depth, boxes, deadlineClock, budgetMs, nodeDepth + 1)
                        .ConfigureAwait(false);
                    return;
                }
            }

            if (obj["children"] is JsonArray children)
            {
                await WalkJsonAsync(page, frame, children, depth, boxes, deadlineClock, budgetMs, nodeDepth + 1)
                    .ConfigureAwait(false);
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
            if (await IsLazyIframeRefAsync(frame, ariaRef).ConfigureAwait(false))
            {
                return (null, null);
            }

            IFrame child = await ChildFrameForAriaRefAsync(frame, ariaRef).ConfigureAwait(false);
            if (child == null || child.IsDetached)
            {
                if (!await IsCaptureReadyIframeRefAsync(frame, ariaRef).ConfigureAwait(false))
                {
                    return (null, null);
                }

                IElementHandle iframeEl = await FindInFrameAsync(frame, ariaRef).ConfigureAwait(false);
                child = await ContentFrameOrNullAsync(iframeEl).ConfigureAwait(false);
                if (child == null || child.IsDetached)
                {
                    child = await ChildFrameByElementIdentityAsync(frame, iframeEl).ConfigureAwait(false);
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
            catch (PlaywrightException)
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

        private static async Task EnsurePrefixesAsync(IPage page, Stopwatch deadlineClock, int budgetMs)
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
                if (RemainingMs(deadlineClock, budgetMs) <= 0)
                {
                    return;
                }

                IFrame frame = queue.Dequeue();
                if (frame == null || frame.IsDetached || !seen.Add(frame))
                {
                    continue;
                }

                try
                {
                    await RaceOrDefaultAsync(
                        () => PrefixForAsync(page, frame),
                        deadlineClock,
                        budgetMs,
                        fallback: string.Empty).ConfigureAwait(false);
                }
                catch (PlaywrightException)
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
                    hosts = await RaceOrDefaultAsync(
                        () => frame.QuerySelectorAllAsync("iframe:not([loading=lazy]), frame"),
                        deadlineClock,
                        budgetMs,
                        fallback: (IReadOnlyList<IElementHandle>)Array.Empty<IElementHandle>()).ConfigureAwait(false);
                }
                catch (PlaywrightException)
                {
                    continue;
                }
                catch (TimeoutException)
                {
                    continue;
                }

                if (hosts == null)
                {
                    continue;
                }

                for (int i = 0; i < hosts.Count; i++)
                {
                    if (RemainingMs(deadlineClock, budgetMs) <= 0)
                    {
                        return;
                    }

                    IFrame child = await RaceOrDefaultAsync(
                        () => ContentFrameOrNullAsync(hosts[i]),
                        deadlineClock,
                        budgetMs,
                        fallback: null).ConfigureAwait(false);

                    if (child != null && !child.IsDetached)
                    {
                        queue.Enqueue(child);
                    }
                }
            }
        }

        private static async Task<IFrame> ChildFrameForAriaRefAsync(IFrame frame, string ariaRef)
        {
            if (frame == null || frame.IsDetached || string.IsNullOrEmpty(ariaRef))
            {
                return null;
            }

            // Match by name/src first — ChildFrames is creation order, not DOM
            // querySelectorAll order (srcdoc re-add swaps indices).
            string[] info;
            try
            {
                info = await frame.EvaluateAsync<string[]>(
                    @"(ref) => {
  const want = String(ref || '');
  const frames = document.querySelectorAll('iframe, frame');
  for (let i = 0; i < frames.length; i++) {
    const aria = frames[i]._ariaRef;
    if (aria && aria.ref === want) {
      const el = frames[i];
      let src = '';
      try { src = el.getAttribute('src') || el.src || ''; } catch (e) {}
      return [String(el.name || ''), String(src), String(i)];
    }
  }
  if (frames.length === 1) {
    const el = frames[0];
    let src = '';
    try { src = el.getAttribute('src') || el.src || ''; } catch (e) {}
    return [String(el.name || ''), String(src), '0'];
  }
  return null;
}",
                    ariaRef).ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                return null;
            }
            catch (TimeoutException)
            {
                return null;
            }

            if (info == null || info.Length < 3)
            {
                return null;
            }

            string wantName = info[0] ?? string.Empty;
            string wantSrc = info[1] ?? string.Empty;

            // Nested framesets parent some frames under an inner frameset, so
            // direct ChildFrames is not enough. Search descendants, not creation index.
            List<IFrame> descendants = new List<IFrame>();
            CollectFrames(frame, descendants);
            if (!string.IsNullOrEmpty(wantName))
            {
                for (int i = 0; i < descendants.Count; i++)
                {
                    IFrame child = descendants[i];
                    if (child == null
                        || ReferenceEquals(child, frame)
                        || child.IsDetached)
                    {
                        continue;
                    }

                    if (string.Equals(child.Name, wantName, StringComparison.Ordinal))
                    {
                        return child;
                    }
                }
            }

            if (!string.IsNullOrEmpty(wantSrc)
                && !wantSrc.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 0; i < descendants.Count; i++)
                {
                    IFrame child = descendants[i];
                    if (child == null || ReferenceEquals(child, frame) || child.IsDetached)
                    {
                        continue;
                    }

                    string childUrl = child.Url ?? string.Empty;
                    if (childUrl.Contains(wantSrc, StringComparison.Ordinal)
                        || wantSrc.Contains(childUrl, StringComparison.Ordinal)
                        || string.Equals(childUrl, wantSrc, StringComparison.Ordinal))
                    {
                        return child;
                    }
                }
            }

            if (wantSrc.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                IFrame dataChild = null;
                int dataCount = 0;
                for (int i = 0; i < descendants.Count; i++)
                {
                    IFrame child = descendants[i];
                    if (child == null || ReferenceEquals(child, frame) || child.IsDetached)
                    {
                        continue;
                    }

                    if ((child.Url ?? string.Empty).StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        dataCount++;
                        dataChild = child;
                    }
                }

                if (dataCount == 1)
                {
                    return dataChild;
                }
            }

            int childCount = 0;
            IFrame only = null;
            for (int i = 0; i < descendants.Count; i++)
            {
                IFrame child = descendants[i];
                if (child == null || ReferenceEquals(child, frame) || child.IsDetached)
                {
                    continue;
                }

                childCount++;
                only = child;
            }

            if (childCount == 1)
            {
                return only;
            }

            // Do not index-match when multiple children — creation order ≠ DOM order.
            return null;
        }

        /// <summary>
        /// Resolves the child frame whose <c>FrameElement</c> is the same DOM node
        /// as <paramref name="iframeEl"/> (srcdoc-safe, order-independent).
        /// </summary>
        private static async Task<IFrame> ChildFrameByElementIdentityAsync(
            IFrame parent,
            IElementHandle iframeEl)
        {
            if (parent == null || parent.IsDetached || iframeEl == null)
            {
                return null;
            }

            IReadOnlyList<IFrame> children = parent.ChildFrames;
            if (children == null || children.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < children.Count; i++)
            {
                IFrame child = children[i];
                if (child == null || child.IsDetached)
                {
                    continue;
                }

                try
                {
                    IElementHandle host = await FrameElementHelper.ResolveAsync(child).ConfigureAwait(false);
                    if (host == null)
                    {
                        continue;
                    }

                    bool same = await iframeEl
                        .EvaluateAsync<bool>("(a, b) => a === b", host)
                        .ConfigureAwait(false);
                    if (same)
                    {
                        return child;
                    }
                }
                catch (PlaywrightException)
                {
                }
                catch (TimeoutException)
                {
                }
            }

            return null;
        }

        private static async Task<IFrame> ContentFrameOrNullAsync(IElementHandle iframeEl)
        {
            if (iframeEl == null)
            {
                return null;
            }

            try
            {
                // Never Evaluate / callFunctionOn the iframe objectId. Darwin
                // WebKit never replies for unloaded lazy iframes, and a stuck
                // command wedges the target until the NUnit 30s kill even when
                // RaceOrDefaultAsync returns a fallback (the in-flight evaluate
                // is not cancelled). Callers must fail-closed via parent-document
                // lazy checks before this path; only attempt ContentFrame.
                // Long enough for a loaded frameset frame on a slow runner.
                // Unloaded lazy iframes never reach this path.
                return await RaceOrDefaultAsync(
                    () => iframeEl.ContentFrameAsync(),
                    Stopwatch.StartNew(),
                    1200,
                    fallback: null).ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                return null;
            }
            catch (TimeoutException)
            {
                return null;
            }
        }

        /// <summary>
        /// Parent-document check: every <c>iframe</c>/<c>frame</c> is
        /// <c>loading=lazy</c> (safe — no iframe objectId evaluate).
        /// </summary>
        private static async Task<bool> FrameHasOnlyLazyIframesAsync(IFrame frame)
        {
            if (frame == null || frame.IsDetached)
            {
                return false;
            }

            try
            {
                return await frame.EvaluateAsync<bool>(
                    @"() => {
  const frames = document.querySelectorAll('iframe, frame');
  if (!frames.length) return false;
  for (let i = 0; i < frames.length; i++) {
    if ((frames[i].getAttribute('loading') || '').toLowerCase() !== 'lazy') return false;
  }
  return true;
}").ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                // Fail open for named/loaded frames — a failed probe must not
                // skip stitching (ShouldStitchAllFrameSnapshots).
                return false;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        private static async Task<bool> IsLazyIframeRefAsync(IFrame frame, string ariaRef)
        {
            if (frame == null || frame.IsDetached || string.IsNullOrEmpty(ariaRef))
            {
                return false;
            }

            try
            {
                return await frame.EvaluateAsync<bool>(IsLazyIframeRefFunction, ariaRef)
                    .ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                // Fail closed — prefer an empty iframe stitch over a wedged session.
                return true;
            }
            catch (TimeoutException)
            {
                return true;
            }
        }

        /// <summary>
        /// Parent-document check: iframe is safe for <c>ContentFrame</c> /
        /// <c>DOM.describeNode</c>. Unloaded lazy frames return false without
        /// touching the iframe objectId.
        /// </summary>
        private static async Task<bool> IsCaptureReadyIframeRefAsync(IFrame frame, string ariaRef)
        {
            if (frame == null || frame.IsDetached || string.IsNullOrEmpty(ariaRef))
            {
                return false;
            }

            try
            {
                return await frame.EvaluateAsync<bool>(IframeCaptureReadyByRefFunction, ariaRef)
                    .ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                return false;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        private static int RemainingMs(Stopwatch clock, int budgetMs)
        {
            if (budgetMs == Timeout.Infinite)
            {
                return int.MaxValue;
            }

            long left = budgetMs - clock.ElapsedMilliseconds;
            return left <= 0 ? 0 : (int)Math.Min(int.MaxValue, left);
        }

        private static async Task<T> RaceOrDefaultAsync<T>(
            Func<Task<T>> operation,
            Stopwatch deadlineClock,
            int budgetMs,
            T fallback)
        {
            int left = RemainingMs(deadlineClock, budgetMs);
            if (left <= 0)
            {
                return fallback;
            }

            Task<T> work = operation();
            Task finished = await Task.WhenAny(work, Task.Delay(left)).ConfigureAwait(false);
            if (finished != work)
            {
                // Leave the in-flight protocol call; callers treat timeout as empty iframe.
                return fallback;
            }

            try
            {
                return await work.ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                return fallback;
            }
            catch (TimeoutException)
            {
                return fallback;
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
            catch (PlaywrightException)
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
            catch (PlaywrightException)
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
            catch (PlaywrightException)
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
