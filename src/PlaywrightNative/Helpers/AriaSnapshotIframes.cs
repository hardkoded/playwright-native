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

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Embeds iframe accessibility trees into an AI-mode aria snapshot.
    /// Official <c>locator.ariaSnapshot({ mode: 'ai' })</c> includes
    /// snapshots of iframes inside the target.
    /// </summary>
    internal static class AriaSnapshotIframes
    {
        private const string WalkerScript = @"(() => {
  const skip = new Set(['SCRIPT', 'STYLE', 'NOSCRIPT', 'LINK', 'META']);
  const heading = /^H([1-6])$/;
  function roleOf(el) {
    const explicitRole = el.getAttribute('role');
    if (explicitRole) {
      return explicitRole;
    }
    const tag = el.tagName;
    if (tag === 'BUTTON') {
      return 'button';
    }
    if (tag === 'A' && el.hasAttribute('href')) {
      return 'link';
    }
    if (heading.test(tag)) {
      return 'heading';
    }
    if (tag === 'UL' || tag === 'OL') {
      return 'list';
    }
    if (tag === 'LI') {
      return 'listitem';
    }
    if (tag === 'TEXTAREA') {
      return 'textbox';
    }
    if (tag === 'SELECT') {
      return 'combobox';
    }
    if (tag === 'IMG') {
      return 'img';
    }
    if (tag === 'IFRAME' || tag === 'FRAME') {
      return 'iframe';
    }
    if (tag === 'NAV') {
      return 'navigation';
    }
    if (tag === 'MAIN') {
      return 'main';
    }
    if (tag === 'HEADER') {
      return 'banner';
    }
    if (tag === 'FOOTER') {
      return 'contentinfo';
    }
    if (tag === 'ARTICLE') {
      return 'article';
    }
    if (tag === 'INPUT') {
      const type = String(el.type || 'text').toLowerCase();
      if (type === 'hidden') {
        return null;
      }
      if (type === 'checkbox') {
        return 'checkbox';
      }
      if (type === 'radio') {
        return 'radio';
      }
      if (type === 'button' || type === 'submit' || type === 'reset') {
        return 'button';
      }
      return 'textbox';
    }
    return 'generic';
  }
  function nameOf(el, role) {
    const labelled = el.getAttribute('aria-label');
    if (labelled) {
      return labelled;
    }
    if (role === 'img') {
      return el.getAttribute('alt') || '';
    }
    if (role === 'textbox') {
      return el.getAttribute('placeholder') || '';
    }
    if (role === 'button' || role === 'link' || role === 'heading' || role === 'listitem') {
      return String(el.innerText || '').trim().replace(/\s+/g, ' ');
    }
    return '';
  }
  function walk(el) {
    if (skip.has(el.tagName)) {
      return null;
    }
    const role = roleOf(el);
    if (!role) {
      return null;
    }
    const match = heading.exec(el.tagName);
    const node = {
      role: role,
      name: nameOf(el, role),
      level: match ? Number(match[1]) : 0,
      children: []
    };
    if (role === 'iframe') {
      return node;
    }
    for (const child of el.children) {
      const nested = walk(child);
      if (nested) {
        node.children.push(nested);
      }
    }
    return node;
  }
  const root = document.body || document.documentElement;
  const out = [];
  if (!root) {
    return out;
  }
  for (const child of root.children) {
    const node = walk(child);
    if (node) {
      out.push(node);
    }
  }
  return JSON.stringify(out);
})()";

        /// <summary>
        /// Fills iframe nodes under <paramref name="root"/> with their
        /// content-document snapshots.
        /// </summary>
        /// <param name="root">The snapshot root element.</param>
        /// <param name="snapshot">The AX tree to mutate.</param>
        /// <returns>A task that completes when iframe trees are attached.</returns>
        internal static async Task AttachAsync(IElementHandle root, AccessibilitySnapshotResult snapshot)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            IFrame ownContent = await root.ContentFrameAsync().ConfigureAwait(false);
            if (ownContent != null)
            {
                snapshot.Role = "iframe";
                snapshot.Children = await SnapshotFrameChildrenAsync(ownContent).ConfigureAwait(false);
                return;
            }

            Queue<IReadOnlyList<AccessibilitySnapshotResult>> pending = new Queue<IReadOnlyList<AccessibilitySnapshotResult>>();
            IReadOnlyList<IElementHandle> frames = await root
                .QuerySelectorAllAsync("iframe, frame")
                .ConfigureAwait(false);
            foreach (IElementHandle frameEl in frames)
            {
                IFrame content = await frameEl.ContentFrameAsync().ConfigureAwait(false);
                if (content == null)
                {
                    continue;
                }

                pending.Enqueue(await SnapshotFrameChildrenAsync(content).ConfigureAwait(false));
            }

            if (pending.Count == 0)
            {
                return;
            }

            FillIframeNodes(snapshot, pending);
            while (pending.Count > 0)
            {
                AppendChild(
                    snapshot,
                    new AccessibilitySnapshotResult
                    {
                        Role = "iframe",
                        Children = pending.Dequeue(),
                    });
            }
        }

        private static async Task<IReadOnlyList<AccessibilitySnapshotResult>> SnapshotFrameChildrenAsync(IFrame frame)
        {
            await WaitFrameReadyAsync(frame).ConfigureAwait(false);

            List<AccessibilitySnapshotResult> children;
            try
            {
                string json = await frame.EvaluateAsync<string>(WalkerScript).ConfigureAwait(false);
                children = new List<AccessibilitySnapshotResult>(ParseJson(json));
            }
            catch (PlaywrightNativeException)
            {
                children = new List<AccessibilitySnapshotResult>();
            }

            Queue<IReadOnlyList<AccessibilitySnapshotResult>> pending = new Queue<IReadOnlyList<AccessibilitySnapshotResult>>();
            IReadOnlyList<IElementHandle> nested = await frame
                .QuerySelectorAllAsync("iframe, frame")
                .ConfigureAwait(false);
            foreach (IElementHandle frameEl in nested)
            {
                IFrame content = await frameEl.ContentFrameAsync().ConfigureAwait(false);
                if (content == null)
                {
                    continue;
                }

                pending.Enqueue(await SnapshotFrameChildrenAsync(content).ConfigureAwait(false));
            }

            foreach (AccessibilitySnapshotResult child in children)
            {
                FillIframeNodes(child, pending);
            }

            while (pending.Count > 0)
            {
                children.Add(new AccessibilitySnapshotResult
                {
                    Role = "iframe",
                    Children = pending.Dequeue(),
                });
            }

            return children;
        }

        private static void FillIframeNodes(
            AccessibilitySnapshotResult node,
            Queue<IReadOnlyList<AccessibilitySnapshotResult>> pending)
        {
            if (node == null || pending.Count == 0)
            {
                return;
            }

            if (IsIframeRole(node.Role))
            {
                node.Role = "iframe";
                node.Children = pending.Dequeue();
                return;
            }

            if (node.Children == null)
            {
                return;
            }

            foreach (AccessibilitySnapshotResult child in node.Children)
            {
                FillIframeNodes(child, pending);
                if (pending.Count == 0)
                {
                    return;
                }
            }
        }

        private static void AppendChild(AccessibilitySnapshotResult node, AccessibilitySnapshotResult child)
        {
            List<AccessibilitySnapshotResult> children = new List<AccessibilitySnapshotResult>();
            if (node.Children != null)
            {
                children.AddRange(node.Children);
            }

            children.Add(child);
            node.Children = children;
        }

        private static bool IsIframeRole(string role)
            => string.Equals(role, "iframe", StringComparison.OrdinalIgnoreCase);

        private static async Task WaitFrameReadyAsync(IFrame frame)
        {
            for (int i = 0; i < 40; i++)
            {
                try
                {
                    bool ready = await frame.EvaluateAsync<bool>(
                            "!!document.body && document.readyState !== 'loading'")
                        .ConfigureAwait(false);
                    if (ready)
                    {
                        return;
                    }
                }
                catch (PlaywrightNativeException)
                {
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        private static IReadOnlyList<AccessibilitySnapshotResult> ParseJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return Array.Empty<AccessibilitySnapshotResult>();
            }

            using JsonDocument document = JsonDocument.Parse(json);
            return ParseNodes(document.RootElement);
        }

        private static IReadOnlyList<AccessibilitySnapshotResult> ParseNodes(JsonElement? raw)
        {
            if (raw == null)
            {
                return Array.Empty<AccessibilitySnapshotResult>();
            }

            JsonElement value = raw.Value;
            if (value.ValueKind == JsonValueKind.Array)
            {
                List<AccessibilitySnapshotResult> list = new List<AccessibilitySnapshotResult>();
                foreach (JsonElement item in value.EnumerateArray())
                {
                    AccessibilitySnapshotResult node = ParseNode(item);
                    if (node != null)
                    {
                        list.Add(node);
                    }
                }

                return list;
            }

            AccessibilitySnapshotResult single = ParseNode(value);
            return single == null
                ? Array.Empty<AccessibilitySnapshotResult>()
                : new[] { single };
        }

        private static AccessibilitySnapshotResult ParseNode(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            AccessibilitySnapshotResult node = new AccessibilitySnapshotResult();
            if (value.TryGetProperty("role", out JsonElement roleEl) && roleEl.ValueKind == JsonValueKind.String)
            {
                node.Role = roleEl.GetString();
            }

            if (value.TryGetProperty("name", out JsonElement nameEl) && nameEl.ValueKind == JsonValueKind.String)
            {
                node.Name = nameEl.GetString();
            }

            if (value.TryGetProperty("level", out JsonElement levelEl)
                && levelEl.ValueKind == JsonValueKind.Number
                && levelEl.TryGetInt32(out int level))
            {
                node.Level = level;
            }

            if (value.TryGetProperty("children", out JsonElement childrenEl))
            {
                node.Children = ParseNodes(childrenEl);
            }

            return node;
        }
    }
}
