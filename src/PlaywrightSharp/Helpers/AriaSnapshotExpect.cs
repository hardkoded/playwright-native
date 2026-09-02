// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Official <c>toMatchAriaSnapshot</c> parse / poll / fail-message helper.
    /// </summary>
    internal static class AriaSnapshotExpect
    {
        private const string CollectDomStateFunction = @"(root) => {
  const scope = root || document.documentElement;
  const links = [];
  const attrs = [];
  const rows = [];
  const values = [];
  const pressed = [];
  const visit = (el) => {
    if (!el || el.nodeType !== 1) return;
    const role = (el.getAttribute('role') || '').toLowerCase();
    if ((el.tagName === 'A' || role === 'link') && el.hasAttribute('href')) {
      links.push(el.getAttribute('href') || '');
    }
    if (el.tagName === 'TR' || role === 'row') {
      rows.push(el.getAttribute('aria-selected') || 'false');
    }
    const invalid = el.getAttribute('aria-invalid');
    const selected = el.getAttribute('aria-selected');
    if ((invalid != null && invalid !== '') || selected != null) {
      attrs.push({
        name: String(el.getAttribute('aria-label') || el.innerText || el.textContent || '').replace(/[\s\u200b\u00ad]+/g, ' ').trim(),
        invalid: invalid == null ? '' : String(invalid),
        selected: selected == null ? '' : String(selected)
      });
    }
            if (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA' || role === 'textbox' || role === 'searchbox' || role === 'progressbar') {
      const rawValue = (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA')
        ? String(el.value ?? '')
        : String(el.innerText || el.textContent || '');
      values.push({
        name: String(el.getAttribute('aria-label') || el.getAttribute('placeholder') || '').replace(/[\s\u200b\u00ad]+/g, ' ').trim(),
        invalid: invalid == null ? '' : String(invalid),
        value: rawValue.replace(/[\s\u200b\u00ad]+/g, ' ').trim(),
        placeholder: String(el.getAttribute('placeholder') || '')
      });
    }
    if (el.tagName === 'BUTTON' || role === 'button') {
      pressed.push(el.getAttribute('aria-pressed') || '');
    }
    for (let i = 0; i < el.children.length; i++) visit(el.children[i]);
  };
  visit(scope);
  return JSON.stringify({ links: links, attrs: attrs, rows: rows, values: values, pressed: pressed });
}";

        private const string CollectBlockTextFunction = @"(root) => {
  const scope = root || document.body || document.documentElement;
  const items = [];
  const norm = (s) => String(s || '').replace(/[\s\u200b\u00ad]+/g, ' ').trim();
  const walk = (el) => {
    if (!el || el.nodeType !== 1) return;
    const tag = (el.tagName || '').toUpperCase();
    if (tag === 'SCRIPT' || tag === 'STYLE' || tag === 'HEAD' || tag === 'NOSCRIPT' || tag === 'META') return;
    let direct = '';
    for (let i = 0; i < el.childNodes.length; i++) {
      const n = el.childNodes[i];
      if (n.nodeType === 3) direct += n.nodeValue || '';
    }
    direct = norm(direct);
    const role = (el.getAttribute('role') || '').toLowerCase();
    if (direct && (tag === 'DIV' || tag === 'SPAN') && !role) {
      items.push({ kind: 'text', text: direct });
    }
    if (tag === 'P' || role === 'paragraph') {
      items.push({ kind: 'paragraph', text: norm(el.innerText || el.textContent || direct) });
      return;
    }
    if (tag === 'H1' || tag === 'H2' || tag === 'H3' || tag === 'H4' || tag === 'H5' || tag === 'H6' || role === 'heading') {
      items.push({ kind: 'mark', text: norm(el.innerText || el.textContent) });
      return;
    }
    if (tag === 'UL' || tag === 'OL' || role === 'list') {
      items.push({ kind: 'mark', text: norm(el.innerText || el.textContent) });
      return;
    }
    if (role === 'progressbar') {
      items.push({
        kind: 'progressbar',
        text: norm(el.getAttribute('aria-label') || ''),
        value: norm(el.innerText || el.textContent)
      });
      return;
    }
    if (tag === 'TABLE' || role === 'table') {
      const rows = [];
      const rowEls = el.rows || el.querySelectorAll('tr, [role=row]');
      for (let i = 0; i < rowEls.length; i++) {
        const row = rowEls[i];
        const cells = [];
        const cellEls = row.cells || row.querySelectorAll('td, th, [role=cell], [role=gridcell]');
        for (let j = 0; j < cellEls.length; j++) cells.push(norm(cellEls[j].innerText || cellEls[j].textContent));
        rows.push({
          selected: String(row.getAttribute('aria-selected') || '') === 'true',
          name: norm(row.innerText || row.textContent),
          cells: cells
        });
      }
      items.push({ kind: 'table', rows: rows });
      return;
    }
    for (let i = 0; i < el.children.length; i++) walk(el.children[i]);
  };
  walk(scope);
  return JSON.stringify(items);
}";

        private const string CollectDomAriaTreeFunction = @"(root) => {
  const scope = root || document.body || document.documentElement;
  const norm = (s) => String(s || '').replace(/[\s\u200b\u00ad]+/g, ' ').trim();
  const implicitRole = (el) => {
    const explicit = (el.getAttribute('role') || '').toLowerCase();
    if (explicit && explicit !== 'presentation' && explicit !== 'none') return explicit;
    const tag = (el.tagName || '').toUpperCase();
    if (/^H[1-6]$/.test(tag)) return 'heading';
    if (tag === 'BUTTON') return 'button';
    if (tag === 'A' && el.hasAttribute('href')) return 'link';
    if (tag === 'TEXTAREA') return 'textbox';
    if (tag === 'SELECT') return 'combobox';
    if (tag === 'INPUT') {
      const type = (el.getAttribute('type') || 'text').toLowerCase();
      if (type === 'checkbox') return 'checkbox';
      if (type === 'radio') return 'radio';
      if (type === 'search') return 'searchbox';
      if (type === 'button' || type === 'submit' || type === 'reset') return 'button';
      if (type === 'hidden') return '';
      return 'textbox';
    }
    if (tag === 'UL' || tag === 'OL') return 'list';
    if (tag === 'LI') return 'listitem';
    if (tag === 'TABLE') return 'table';
    if (tag === 'THEAD' || tag === 'TBODY' || tag === 'TFOOT') return 'rowgroup';
    if (tag === 'TR') return 'row';
    if (tag === 'TD') return 'cell';
    if (tag === 'TH') return 'columnheader';
    if (tag === 'P') return 'paragraph';
    if (tag === 'HEADER') return 'banner';
    if (tag === 'DETAILS') return 'group';
    if (tag === 'SUMMARY') return 'button';
    if (tag === 'PROGRESS') return 'progressbar';
    return '';
  };
  const visit = (el) => {
    if (!el || el.nodeType !== 1) return null;
    const tag = (el.tagName || '').toUpperCase();
    if (tag === 'SCRIPT' || tag === 'STYLE' || tag === 'HEAD' || tag === 'NOSCRIPT' || tag === 'META' || tag === 'LINK') return null;
    const role = implicitRole(el);
    const children = [];
    if (!role) {
      let direct = '';
      for (let i = 0; i < el.childNodes.length; i++) {
        const n = el.childNodes[i];
        if (n.nodeType === 3) direct += n.nodeValue || '';
        else if (n.nodeType === 1) {
          const child = visit(n);
          if (child) {
            if (child.role === 'fragment') children.push.apply(children, child.children || []);
            else children.push(child);
          }
        }
      }
      direct = norm(direct);
      if (direct) children.unshift({ role: 'text', name: direct, children: [] });
      if (children.length === 1) return children[0];
      return { role: 'fragment', name: '', children: children };
    }
    const node = { role: role, name: '', children: [] };
    if (role === 'heading') node.level = parseInt((el.tagName || 'H1').substring(1), 10) || 0;
    if (role === 'link' && el.hasAttribute('href')) node.url = el.getAttribute('href') || '';
    const label = el.getAttribute('aria-label');
    if (label) node.name = norm(label);
    else if (role === 'textbox' || role === 'searchbox') {
      node.name = norm(el.getAttribute('placeholder') || label || '');
      const ph = el.getAttribute('placeholder');
      if (ph && ph !== node.name) node.placeholder = ph;
    }
    const selected = el.getAttribute('aria-selected');
    if (selected === 'true') node.selected = true;
    const pressed = el.getAttribute('aria-pressed');
    if (pressed === 'mixed') node.pressed = 'mixed';
    else if (pressed === 'true') node.pressed = 'true';
    const invalid = el.getAttribute('aria-invalid');
    if (invalid && invalid !== 'false') node.invalid = invalid;
    if (el.disabled || el.getAttribute('aria-disabled') === 'true') node.disabled = true;
    if (role === 'checkbox' || role === 'radio' || role === 'switch') {
      if (el.indeterminate || el.getAttribute('aria-checked') === 'mixed') node.checked = 'mixed';
      else if (el.checked || el.getAttribute('aria-checked') === 'true') node.checked = 'true';
    }
    if (el.open || el.getAttribute('aria-expanded') === 'true') node.expanded = true;
    if (role === 'textbox' || role === 'searchbox') node.value = String(el.value || '');
    if (role === 'progressbar') node.value = norm(el.innerText || el.textContent);
    if (role === 'button' || role === 'link' || role === 'heading' || role === 'paragraph' || role === 'listitem' || role === 'group' || role === 'cell' || role === 'columnheader' || role === 'row') {
      if (!node.name) node.name = norm(el.innerText || el.textContent);
    }
    if (role === 'table' || role === 'rowgroup' || role === 'list' || role === 'row' || role === 'banner' || role === 'group' || role === 'listitem' || role === 'progressbar') {
      for (let i = 0; i < el.children.length; i++) {
        const child = visit(el.children[i]);
        if (!child) continue;
        if (child.role === 'fragment') node.children.push.apply(node.children, child.children || []);
        else node.children.push(child);
      }
    }
    if ((role === 'textbox' || role === 'searchbox') && node.value && node.value !== node.name) {
      node.children = [{ role: 'text', name: node.value, children: [] }];
    }
    if (role === 'progressbar' && node.value && node.name && node.value !== node.name) {
      node.children = [{ role: 'text', name: node.value, children: [] }];
    }
    return node;
  };
  const result = visit(scope);
  if (!result) return JSON.stringify({ role: 'WebArea', name: '', children: [] });
  if (result.role === 'fragment') return JSON.stringify({ role: 'WebArea', name: '', children: result.children || [] });
  return JSON.stringify({ role: 'WebArea', name: '', children: [result] });
}";

        private static readonly JsonSerializerOptions DomStateJson = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        internal static async Task MatchAsync(
            IPage page,
            ILocator locator,
            IElementHandle root,
            string expected,
            bool? exact,
            float? timeout,
            bool negate,
            AbortSignal signal = default)
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            int timeoutMs = TimeoutSettings.ExpectTimeoutMs(timeout);
            string receiver = locator == null ? "page" : "locator";
            string header = negate
                ? "expect(" + receiver + ").not.toMatchAriaSnapshot(expected) failed"
                : "expect(" + receiver + ").toMatchAriaSnapshot(expected) failed";
            string expectLog = negate ? "not toMatchAriaSnapshot" : "toMatchAriaSnapshot";
            ExpectAbort.ThrowIfAlreadyAborted(
                signal,
                header,
                locator == null ? string.Empty : "Locator: " + locator + "\n");

            if (!AriaSnapshotTemplateParser.IsYamlSequence(expected)
                && expected.IndexOf('\n') < 0)
            {
                await MatchContainsAsync(page, root, expected, exact, timeout, negate, header, expectLog, locator, signal)
                    .ConfigureAwait(false);
                return;
            }

            AriaSnapshotTemplate template;
            try
            {
                template = AriaSnapshotTemplateParser.Parse(expected);
            }
            catch (AriaSnapshotParseException ex)
            {
                throw ParseFailed(header, expected, ex.Message, timeoutMs);
            }

            if (exact == true
                && (string.IsNullOrEmpty(template.ContainerMode)
                    || string.Equals(template.ContainerMode, "contain", StringComparison.Ordinal)))
            {
                template.ContainerMode = "deep-equal";
            }

            string unshifted = AriaSnapshotTemplateParser.Unshift(expected);
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            string lastReceived = string.Empty;
            while (true)
            {
                if (ExpectAbort.TryMidAbort(signal, out string abortReason))
                {
                    throw ExpectException.Fail(
                        header + "\n\n  - operation was aborted: " + abortReason + "\n",
                        actual: lastReceived,
                        expected: unshifted,
                        name: "toMatchAriaSnapshot",
                        pass: false,
                        timeoutMs,
                        ariaSnapshot: lastReceived);
                }

                if (page != null)
                {
                    await LocatorHandlers.RunAsync(page, timeout).ConfigureAwait(false);
                }

                IElementHandle handle = await ResolveRootAsync(page, locator, root).ConfigureAwait(false);
                AccessibilitySnapshotResult snapshot = handle == null && locator != null
                    ? null
                    : IsWebKitPage(page)
                        ? await CaptureDomAriaAsync(page, handle).ConfigureAwait(false)
                        : await CaptureAsync(page, handle).ConfigureAwait(false);
                if (snapshot != null)
                {
                    await EnrichAsync(page, handle, snapshot).ConfigureAwait(false);
                }

                bool ok = snapshot != null && AriaSnapshotMatcher.Matches(snapshot, template);
                if (!ok && handle != null)
                {
                    try
                    {
                        string officialYaml = await AriaSnapshotOfficial.CaptureYamlAsync(handle, depth: null, boxes: false).ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(officialYaml)
                            && (OfficialYamlMatches(officialYaml, unshifted)
                                || OfficialTreeMatches(officialYaml, template)))
                        {
                            ok = true;
                        }
                    }
                    catch (Exception ex) when (ex is PlaywrightSharpException || ex is TimeoutException || ex is AriaSnapshotParseException)
                    {
                    }
                }

                lastReceived = AriaSnapshotMatcher.FormatReceived(snapshot);
                if (negate ? !ok : ok)
                {
                    return;
                }

                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    throw Mismatch(
                        header,
                        locator == null ? null : locator.ToString(),
                        unshifted,
                        lastReceived,
                        timeoutMs,
                        expectLog);
                }

                await ExpectAbort.DelayOrAbortAsync(signal).ConfigureAwait(false);
            }
        }

        private static bool OfficialTreeMatches(string officialYaml, AriaSnapshotTemplate template)
        {
            AriaSnapshotTemplate received = AriaSnapshotTemplateParser.Parse(officialYaml);
            return AriaSnapshotMatcher.Matches(SnapshotFromTemplate(received), template);
        }

        private static AccessibilitySnapshotResult SnapshotFromTemplate(AriaSnapshotTemplate node)
        {
            if (node == null)
            {
                return null;
            }

            AccessibilitySnapshotResult result = new AccessibilitySnapshotResult
            {
                Role = string.Equals(node.Kind, "text", StringComparison.Ordinal) ? "text" : node.Role,
                Name = string.Equals(node.Kind, "text", StringComparison.Ordinal)
                    ? (node.Text ?? string.Empty)
                    : (node.Name ?? string.Empty),
                Value = node.Text ?? string.Empty,
                Url = node.Url ?? string.Empty,
                Placeholder = node.Placeholder ?? string.Empty,
                Invalid = node.Invalid ?? string.Empty,
                Disabled = node.Disabled == true,
                Expanded = node.Expanded == true,
                Selected = node.Selected == true,
                Level = node.Level ?? 0,
            };

            if (string.Equals(node.Checked, "mixed", StringComparison.OrdinalIgnoreCase))
            {
                result.Checked = MixedState.Mixed;
            }
            else if (string.Equals(node.Checked, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.Checked, "on", StringComparison.OrdinalIgnoreCase))
            {
                result.Checked = MixedState.On;
            }

            if (string.Equals(node.Pressed, "mixed", StringComparison.OrdinalIgnoreCase))
            {
                result.Pressed = MixedState.Mixed;
            }
            else if (string.Equals(node.Pressed, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.Pressed, "on", StringComparison.OrdinalIgnoreCase))
            {
                result.Pressed = MixedState.On;
            }

            List<AccessibilitySnapshotResult> children = new List<AccessibilitySnapshotResult>();
            for (int i = 0; i < node.Children.Count; i++)
            {
                AccessibilitySnapshotResult child = SnapshotFromTemplate(node.Children[i]);
                if (child != null)
                {
                    children.Add(child);
                }
            }

            result.Children = children;
            return result;
        }

        private static bool OfficialYamlMatches(string received, string expected)
        {
            string left = AriaSnapshotTemplateParser.Unshift(received ?? string.Empty);
            string right = expected ?? string.Empty;
            if (string.Equals(left, right, StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(right)
                && left.Contains(right, StringComparison.Ordinal))
            {
                return true;
            }

            string leftNorm = AriaSnapshotTemplateParser.NormalizeWhiteSpace(left);
            string rightNorm = AriaSnapshotTemplateParser.NormalizeWhiteSpace(right);
            return !string.IsNullOrWhiteSpace(rightNorm)
                && leftNorm.Contains(rightNorm, StringComparison.Ordinal);
        }

        private static async Task MatchContainsAsync(
            IPage page,
            IElementHandle root,
            string expected,
            bool? exact,
            float? timeout,
            bool negate,
            string header,
            string expectLog,
            ILocator locator,
            AbortSignal signal)
        {
            int timeoutMs = TimeoutSettings.ExpectTimeoutMs(timeout);
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            string last = string.Empty;
            while (true)
            {
                if (ExpectAbort.TryMidAbort(signal, out string abortReason))
                {
                    throw ExpectException.Fail(
                        header + "\n\n  - operation was aborted: " + abortReason + "\n",
                        actual: last,
                        expected: expected,
                        name: "toMatchAriaSnapshot",
                        pass: false,
                        timeoutMs,
                        ariaSnapshot: last);
                }

                if (page != null)
                {
                    await LocatorHandlers.RunAsync(page, timeout).ConfigureAwait(false);
                }

                IElementHandle handle = await ResolveRootAsync(page, locator, root).ConfigureAwait(false);
                AccessibilitySnapshotResult snapshot = handle == null && locator != null
                    ? null
                    : await CaptureAsync(page, handle).ConfigureAwait(false);
                last = AriaSnapshotYaml.Format(snapshot);
                bool ok = exact == true
                    ? last == expected
                    : last.Contains(expected, StringComparison.Ordinal);
                if (negate ? !ok : ok)
                {
                    return;
                }

                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    throw Mismatch(
                        header,
                        locator == null ? null : locator.ToString(),
                        expected,
                        last,
                        timeoutMs,
                        expectLog);
                }

                await ExpectAbort.DelayOrAbortAsync(signal).ConfigureAwait(false);
            }
        }

        private static async Task<IElementHandle> ResolveRootAsync(IPage page, ILocator locator, IElementHandle root)
        {
            if (root != null)
            {
                return root;
            }

            if (locator == null)
            {
                return null;
            }

            IReadOnlyList<IElementHandle> all;
            try
            {
                all = await locator.ElementHandlesAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is PlaywrightSharpException || ex is TimeoutException)
            {
                return null;
            }

            if (all == null || all.Count == 0)
            {
                return null;
            }

            if (all.Count > 1)
            {
                throw new PlaywrightSharpException(
                    await StrictModeViolation.FormatAsync(locator.ToString(), all).ConfigureAwait(false));
            }

            return all[0];
        }

        private static bool IsWebKitPage(IPage page)
        {
            return page != null
                && string.Equals(page.GetType().Name, "WKPage", StringComparison.Ordinal);
        }

        private static async Task<AccessibilitySnapshotResult> CaptureDomAriaAsync(IPage page, IElementHandle root)
        {
            try
            {
                string json = root != null
                    ? await root.EvaluateAsync<string>(CollectDomAriaTreeFunction).ConfigureAwait(false)
                    : page != null
                        ? await page.EvaluateAsync<string>(CollectDomAriaTreeFunction).ConfigureAwait(false)
                        : null;
                if (string.IsNullOrEmpty(json))
                {
                    return null;
                }

                return ReadDomAriaNode(JsonSerializer.Deserialize<JsonElement>(json));
            }
            catch (Exception ex) when (ex is PlaywrightSharpException || ex is TimeoutException || ex is JsonException || ex is ArgumentNullException)
            {
                return null;
            }
        }

        private static AccessibilitySnapshotResult ReadDomAriaNode(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            AccessibilitySnapshotResult node = new AccessibilitySnapshotResult
            {
                Role = ReadJsonString(element, "role"),
                Name = ReadJsonString(element, "name"),
                Value = ReadJsonString(element, "value"),
                Url = ReadJsonString(element, "url"),
                Placeholder = ReadJsonString(element, "placeholder"),
                Invalid = ReadJsonString(element, "invalid"),
            };

            if (element.TryGetProperty("selected", out JsonElement selected) && selected.ValueKind == JsonValueKind.True)
            {
                node.Selected = true;
            }

            if (element.TryGetProperty("disabled", out JsonElement disabled) && disabled.ValueKind == JsonValueKind.True)
            {
                node.Disabled = true;
            }

            if (element.TryGetProperty("expanded", out JsonElement expanded) && expanded.ValueKind == JsonValueKind.True)
            {
                node.Expanded = true;
            }

            if (element.TryGetProperty("checked", out JsonElement checkedEl))
            {
                string checkedValue = checkedEl.ValueKind == JsonValueKind.String
                    ? checkedEl.GetString()
                    : checkedEl.ValueKind == JsonValueKind.True ? "true" : string.Empty;
                if (string.Equals(checkedValue, "mixed", StringComparison.OrdinalIgnoreCase))
                {
                    node.Checked = MixedState.Mixed;
                }
                else if (string.Equals(checkedValue, "true", StringComparison.OrdinalIgnoreCase))
                {
                    node.Checked = MixedState.On;
                }
            }

            if (element.TryGetProperty("level", out JsonElement level) && level.TryGetInt32(out int headingLevel))
            {
                node.Level = headingLevel;
            }

            if (element.TryGetProperty("pressed", out JsonElement pressed))
            {
                string pressedValue = pressed.ValueKind == JsonValueKind.String ? pressed.GetString() : string.Empty;
                if (string.Equals(pressedValue, "mixed", StringComparison.OrdinalIgnoreCase))
                {
                    node.Pressed = MixedState.Mixed;
                }
                else if (string.Equals(pressedValue, "true", StringComparison.OrdinalIgnoreCase))
                {
                    node.Pressed = MixedState.On;
                }
            }

            if (element.TryGetProperty("children", out JsonElement children)
                && children.ValueKind == JsonValueKind.Array)
            {
                List<AccessibilitySnapshotResult> list = new List<AccessibilitySnapshotResult>();
                foreach (JsonElement child in children.EnumerateArray())
                {
                    AccessibilitySnapshotResult parsed = ReadDomAriaNode(child);
                    if (parsed != null)
                    {
                        list.Add(parsed);
                    }
                }

                node.Children = list;
            }

            return node;
        }

        private static string ReadJsonString(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            {
                return string.Empty;
            }

            return value.GetString() ?? string.Empty;
        }

        private static async Task<AccessibilitySnapshotResult> CaptureAsync(IPage page, IElementHandle root)
        {
            if (page is not IHasPageExtras)
            {
                return null;
            }

            try
            {
                return await page
                    .SnapshotAccessibilityAsync(interestingOnly: false, root: root)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is PlaywrightSharpException || ex is TimeoutException || ex is ArgumentNullException)
            {
                return null;
            }
        }

        private static async Task EnrichAsync(IPage page, IElementHandle root, AccessibilitySnapshotResult snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            DomState state = await CollectDomStateAsync(page, root).ConfigureAwait(false);
            await MergeSimpleDomAsync(page, root, state).ConfigureAwait(false);
            int linkIndex = 0;
            int rowIndex = 0;
            int valueIndex = 0;
            int pressedIndex = 0;
            AssignDomState(snapshot, state, ref linkIndex, ref rowIndex, ref valueIndex, ref pressedIndex);
            PromoteRowSelected(snapshot);
            await InsertDomTextNodesAsync(page, root, snapshot).ConfigureAwait(false);
            PrepareExpectTree(snapshot);
        }

        private static async Task InsertDomTextNodesAsync(IPage page, IElementHandle root, AccessibilitySnapshotResult snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            try
            {
                string json = root != null
                    ? await root.EvaluateAsync<string>(CollectBlockTextFunction).ConfigureAwait(false)
                    : page != null
                        ? await page.EvaluateAsync<string>(CollectBlockTextFunction).ConfigureAwait(false)
                        : null;
                if (string.IsNullOrEmpty(json))
                {
                    return;
                }

                List<DomTextItem> items = JsonSerializer.Deserialize<List<DomTextItem>>(json, DomStateJson);
                if (items == null || items.Count == 0)
                {
                    return;
                }

                InsertMissingTextChildren(snapshot, items);
            }
            catch (Exception ex) when (ex is PlaywrightSharpException || ex is TimeoutException || ex is JsonException)
            {
            }
        }

        private static void InsertMissingTextChildren(AccessibilitySnapshotResult node, List<DomTextItem> items)
        {
            if (node == null || items == null || items.Count == 0)
            {
                return;
            }

            AccessibilitySnapshotResult host = FindTextHost(node);
            List<AccessibilitySnapshotResult> remaining = new List<AccessibilitySnapshotResult>();
            if (host.Children != null)
            {
                remaining.AddRange(host.Children);
            }

            HashSet<string> present = new HashSet<string>(StringComparer.Ordinal);
            CollectPresentText(node, present);
            List<AccessibilitySnapshotResult> rebuilt = new List<AccessibilitySnapshotResult>();
            for (int i = 0; i < items.Count; i++)
            {
                DomTextItem item = items[i];
                string text = AriaSnapshotTemplateParser.NormalizeWhiteSpace(item.Text);
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                if (string.Equals(item.Kind, "text", StringComparison.OrdinalIgnoreCase)
                    && !present.Contains(text))
                {
                    rebuilt.Add(new AccessibilitySnapshotResult
                    {
                        Role = "text",
                        Name = text,
                    });
                    present.Add(text);
                    continue;
                }

                if (string.Equals(item.Kind, "table", StringComparison.OrdinalIgnoreCase)
                    && item.Rows != null
                    && item.Rows.Count > 0)
                {
                    int tableIndex = FindRoleChild(remaining, "table");
                    if (tableIndex < 0)
                    {
                        tableIndex = FindRoleChild(remaining, "LayoutTable");
                    }

                    if (tableIndex < 0)
                    {
                        rebuilt.Add(BuildDomTable(item.Rows));
                        continue;
                    }
                }

                if (string.Equals(item.Kind, "progressbar", StringComparison.OrdinalIgnoreCase))
                {
                    int bar = FindRoleChild(remaining, "progressbar");
                    AccessibilitySnapshotResult progressbar = bar >= 0
                        ? remaining[bar]
                        : new AccessibilitySnapshotResult { Role = "progressbar" };
                    if (string.IsNullOrEmpty(progressbar.Name))
                    {
                        progressbar.Name = text;
                    }

                    if (!string.IsNullOrEmpty(item.Value))
                    {
                        progressbar.Value = item.Value;
                    }

                    rebuilt.Add(progressbar);
                    if (bar >= 0)
                    {
                        remaining.RemoveAt(bar);
                    }

                    remaining.RemoveAll(child =>
                        string.Equals(child.Role, "text", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(child.Role, "StaticText", StringComparison.OrdinalIgnoreCase));
                    present.Add(text);
                    continue;
                }

                if (string.Equals(item.Kind, "paragraph", StringComparison.OrdinalIgnoreCase))
                {
                    int paragraph = FindRoleChild(remaining, "paragraph");
                    if (paragraph >= 0)
                    {
                        if (string.IsNullOrEmpty(remaining[paragraph].Name))
                        {
                            remaining[paragraph].Name = text;
                        }

                        rebuilt.Add(remaining[paragraph]);
                        remaining.RemoveAt(paragraph);
                        present.Add(text);
                        continue;
                    }
                }

                int found = FindNamedChild(remaining, text);
                if (found >= 0)
                {
                    rebuilt.Add(remaining[found]);
                    remaining.RemoveAt(found);
                }
            }

            rebuilt.AddRange(remaining);
            host.Children = rebuilt;
        }

        private static AccessibilitySnapshotResult BuildDomTable(List<DomTableRow> rows)
        {
            List<AccessibilitySnapshotResult> axRows = new List<AccessibilitySnapshotResult>();
            for (int i = 0; i < rows.Count; i++)
            {
                DomTableRow row = rows[i];
                List<AccessibilitySnapshotResult> cells = new List<AccessibilitySnapshotResult>();
                if (row.Cells != null)
                {
                    for (int c = 0; c < row.Cells.Count; c++)
                    {
                        cells.Add(new AccessibilitySnapshotResult
                        {
                            Role = "cell",
                            Name = row.Cells[c] ?? string.Empty,
                        });
                    }
                }

                axRows.Add(new AccessibilitySnapshotResult
                {
                    Role = "row",
                    Name = row.Name ?? string.Empty,
                    Selected = row.Selected,
                    Children = cells,
                });
            }

            return new AccessibilitySnapshotResult
            {
                Role = "table",
                Children = new[]
                {
                    new AccessibilitySnapshotResult
                    {
                        Role = "rowgroup",
                        Children = axRows,
                    },
                },
            };
        }

        private static int FindRoleChild(List<AccessibilitySnapshotResult> children, string role)
        {
            for (int i = 0; i < children.Count; i++)
            {
                if (string.Equals(children[i].Role, role, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindNamedChild(List<AccessibilitySnapshotResult> children, string text)
        {
            for (int i = 0; i < children.Count; i++)
            {
                string name = AriaSnapshotTemplateParser.NormalizeWhiteSpace(children[i].Name ?? string.Empty);
                if (!string.IsNullOrEmpty(name)
                    && (string.Equals(name, text, StringComparison.Ordinal) || text.StartsWith(name, StringComparison.Ordinal)))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void CollectPresentText(AccessibilitySnapshotResult node, HashSet<string> present)
        {
            if (node == null)
            {
                return;
            }

            string name = AriaSnapshotTemplateParser.NormalizeWhiteSpace(node.Name ?? string.Empty);
            if (!string.IsNullOrEmpty(name))
            {
                present.Add(name);
            }

            if (node.Children == null)
            {
                return;
            }

            foreach (AccessibilitySnapshotResult child in node.Children)
            {
                CollectPresentText(child, present);
            }
        }

        private static AccessibilitySnapshotResult FindTextHost(AccessibilitySnapshotResult node)
        {
            AccessibilitySnapshotResult current = node;
            while (current != null
                && IsDocumentHost(current.Role)
                && current.Children != null)
            {
                AccessibilitySnapshotResult only = null;
                int count = 0;
                foreach (AccessibilitySnapshotResult child in current.Children)
                {
                    if (child == null)
                    {
                        continue;
                    }

                    only = child;
                    count++;
                    if (count > 1)
                    {
                        return current;
                    }
                }

                if (count == 1 && IsDocumentHost(only.Role))
                {
                    current = only;
                    continue;
                }

                return current;
            }

            return node;
        }

        private static bool IsDocumentHost(string role)
        {
            return string.Equals(role, "WebArea", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "document", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "HTML", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "body", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "generic", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrEmpty(role);
        }

        private static void PrepareExpectTree(AccessibilitySnapshotResult node)
        {
            if (node == null)
            {
                return;
            }

            NormalizeExpectNode(node);
            if (node.Children == null)
            {
                return;
            }

            List<AccessibilitySnapshotResult> children = new List<AccessibilitySnapshotResult>();
            foreach (AccessibilitySnapshotResult child in node.Children)
            {
                if (child == null)
                {
                    continue;
                }

                PrepareExpectTree(child);
                AppendVisibleChild(children, child);
            }

            if (IsTableRole(node.Role))
            {
                children = WrapRowsInRowgroup(children);
            }

            node.Children = children;
        }

        private static void NormalizeExpectNode(AccessibilitySnapshotResult node)
        {
            if (IsRowRole(node.Role) && string.IsNullOrEmpty(node.Name))
            {
                node.Name = ConcatAccessibleName(node);
            }

            if (!IsSelectedRole(node.Role))
            {
                node.Selected = false;
            }

            if (IsWidgetWithValue(node.Role))
            {
                string text = ConcatChildText(node);
                string name = AriaSnapshotTemplateParser.NormalizeWhiteSpace(node.Name ?? string.Empty);
                if (!string.IsNullOrEmpty(text)
                    && !string.Equals(text, name, StringComparison.Ordinal)
                    && (string.IsNullOrEmpty(node.Value) || IsProgressbarRole(node.Role)))
                {
                    node.Value = text;
                }
                else if (string.Equals(
                    AriaSnapshotTemplateParser.NormalizeWhiteSpace(node.Value ?? string.Empty),
                    name,
                    StringComparison.Ordinal)
                    && !string.IsNullOrEmpty(name))
                {
                    node.Value = null;
                }
            }
        }

        private static void AppendVisibleChild(List<AccessibilitySnapshotResult> children, AccessibilitySnapshotResult child)
        {
            if (IsSkippedExpectChild(child))
            {
                if (IsNamelessWrapper(child) && child.Children != null)
                {
                    foreach (AccessibilitySnapshotResult nested in child.Children)
                    {
                        if (nested != null)
                        {
                            AppendVisibleChild(children, nested);
                        }
                    }
                }

                return;
            }

            children.Add(child);
        }

        private static List<AccessibilitySnapshotResult> WrapRowsInRowgroup(List<AccessibilitySnapshotResult> children)
        {
            List<AccessibilitySnapshotResult> rows = new List<AccessibilitySnapshotResult>();
            List<AccessibilitySnapshotResult> others = new List<AccessibilitySnapshotResult>();
            bool hasRowgroup = false;
            for (int i = 0; i < children.Count; i++)
            {
                if (IsRowRole(children[i].Role))
                {
                    rows.Add(children[i]);
                }
                else
                {
                    if (string.Equals(children[i].Role, "rowgroup", StringComparison.OrdinalIgnoreCase))
                    {
                        hasRowgroup = true;
                    }

                    others.Add(children[i]);
                }
            }

            if (hasRowgroup || rows.Count == 0)
            {
                return children;
            }

            AccessibilitySnapshotResult rowgroup = new AccessibilitySnapshotResult
            {
                Role = "rowgroup",
                Children = rows,
            };
            others.Add(rowgroup);
            return others;
        }

        private static bool IsSkippedExpectChild(AccessibilitySnapshotResult node)
        {
            if (node == null)
            {
                return true;
            }

            if (IsNamelessWrapper(node))
            {
                return true;
            }

            string role = node.Role ?? string.Empty;
            if (string.Equals(role, "ListMarker", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "LineBreak", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Ignored", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "InlineTextBox", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if ((string.Equals(role, "StaticText", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(role, "text", StringComparison.OrdinalIgnoreCase))
                && string.IsNullOrWhiteSpace(node.Name)
                && string.IsNullOrWhiteSpace(node.Value)
                && !HasChildren(node))
            {
                return true;
            }

            return false;
        }

        private static bool IsNamelessWrapper(AccessibilitySnapshotResult node)
        {
            if (node == null || !string.IsNullOrEmpty(node.Name))
            {
                return false;
            }

            string role = node.Role ?? string.Empty;
            return string.Equals(role, "generic", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "none", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "presentation", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTableRole(string role)
        {
            return string.Equals(role, "table", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "grid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "treegrid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "LayoutTable", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRowRole(string role)
        {
            return string.Equals(role, "row", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "LayoutTableRow", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSelectedRole(string role)
        {
            return string.Equals(role, "gridcell", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "option", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "row", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "LayoutTableRow", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "tab", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "rowheader", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "columnheader", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "treeitem", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWidgetWithValue(string role)
        {
            return string.Equals(role, "textbox", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "searchbox", StringComparison.OrdinalIgnoreCase)
                || IsProgressbarRole(role);
        }

        private static bool IsProgressbarRole(string role)
        {
            return string.Equals(role, "progressbar", StringComparison.OrdinalIgnoreCase);
        }

        private static string ConcatChildText(AccessibilitySnapshotResult node)
        {
            if (node == null || !HasChildren(node))
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            foreach (AccessibilitySnapshotResult child in node.Children)
            {
                if (child == null || (IsSkippedExpectChild(child) && !IsNamelessWrapper(child)))
                {
                    continue;
                }

                string text = ConcatAccessibleName(child);
                if (!string.IsNullOrEmpty(text))
                {
                    parts.Add(text);
                }
            }

            return AriaSnapshotTemplateParser.NormalizeWhiteSpace(string.Join(" ", parts));
        }

        private static string ConcatAccessibleName(AccessibilitySnapshotResult node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            if (!HasChildren(node))
            {
                return AriaSnapshotTemplateParser.NormalizeWhiteSpace(node.Value ?? node.Name ?? string.Empty);
            }

            List<string> parts = new List<string>();
            foreach (AccessibilitySnapshotResult child in node.Children)
            {
                if (child == null || (IsSkippedExpectChild(child) && !IsNamelessWrapper(child)))
                {
                    continue;
                }

                string text = ConcatAccessibleName(child);
                if (!string.IsNullOrEmpty(text))
                {
                    parts.Add(text);
                }
            }

            if (parts.Count == 0)
            {
                return AriaSnapshotTemplateParser.NormalizeWhiteSpace(node.Value ?? node.Name ?? string.Empty);
            }

            return AriaSnapshotTemplateParser.NormalizeWhiteSpace(string.Join(" ", parts));
        }

        private static void PromoteRowSelected(AccessibilitySnapshotResult node)
        {
            if (node?.Children == null)
            {
                return;
            }

            foreach (AccessibilitySnapshotResult child in node.Children)
            {
                PromoteRowSelected(child);
                if (IsRowRole(node.Role) && child.Selected)
                {
                    node.Selected = true;
                }
            }
        }

        private static async Task MergeSimpleDomAsync(IPage page, IElementHandle root, DomState state)
        {
            try
            {
                string selected = root != null
                    ? await root.EvaluateAsync<string>(
                        @"el => JSON.stringify(Array.from((el || document).querySelectorAll('tr,[role=row]')).map(r => r.getAttribute('aria-selected') || 'false'))")
                        .ConfigureAwait(false)
                    : await page.EvaluateAsync<string>(
                        @"() => JSON.stringify(Array.from(document.querySelectorAll('tr,[role=row]')).map(r => r.getAttribute('aria-selected') || 'false'))")
                        .ConfigureAwait(false);
                if (!string.IsNullOrEmpty(selected) && (state.Rows == null || state.Rows.Count == 0))
                {
                    state.Rows = JsonSerializer.Deserialize<List<string>>(selected, DomStateJson) ?? state.Rows;
                }

                string invalids = root != null
                    ? await root.EvaluateAsync<string>(
                        @"el => JSON.stringify(Array.from((el || document).querySelectorAll('input,textarea,[role=textbox],[role=progressbar]')).map(i => ({ name: (i.getAttribute('aria-label') || i.getAttribute('placeholder') || '').trim(), invalid: i.getAttribute('aria-invalid') || '', value: (i.value != null ? String(i.value) : String(i.innerText || '')).trim() })))")
                        .ConfigureAwait(false)
                    : await page.EvaluateAsync<string>(
                        @"() => JSON.stringify(Array.from(document.querySelectorAll('input,textarea,[role=textbox],[role=progressbar]')).map(i => ({ name: (i.getAttribute('aria-label') || i.getAttribute('placeholder') || '').trim(), invalid: i.getAttribute('aria-invalid') || '', value: (i.value != null ? String(i.value) : String(i.innerText || '')).trim() })))")
                        .ConfigureAwait(false);
                if (!string.IsNullOrEmpty(invalids) && (state.Values == null || state.Values.Count == 0))
                {
                    state.Values = JsonSerializer.Deserialize<List<DomValue>>(invalids, DomStateJson) ?? state.Values;
                }
            }
            catch (Exception ex) when (ex is PlaywrightSharpException || ex is TimeoutException || ex is JsonException)
            {
            }
        }

        private static async Task<DomState> CollectDomStateAsync(IPage page, IElementHandle root)
        {
            try
            {
                string json = root != null
                    ? await root.EvaluateAsync<string>(CollectDomStateFunction).ConfigureAwait(false)
                    : page != null
                        ? await page.EvaluateAsync<string>(CollectDomStateFunction).ConfigureAwait(false)
                        : null;
                if (!string.IsNullOrEmpty(json))
                {
                    return JsonSerializer.Deserialize<DomState>(json, DomStateJson) ?? new DomState();
                }
            }
            catch (Exception ex) when (ex is PlaywrightSharpException || ex is TimeoutException || ex is JsonException)
            {
            }

            return new DomState();
        }

        private static bool HasChildren(AccessibilitySnapshotResult node)
        {
            if (node?.Children == null)
            {
                return false;
            }

            foreach (AccessibilitySnapshotResult child in node.Children)
            {
                if (child != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssignDomState(
            AccessibilitySnapshotResult node,
            DomState state,
            ref int linkIndex,
            ref int rowIndex,
            ref int valueIndex,
            ref int pressedIndex)
        {
            if (node == null || state == null)
            {
                return;
            }

            if (string.Equals(node.Role, "link", StringComparison.OrdinalIgnoreCase)
                && state.Links != null
                && linkIndex < state.Links.Count)
            {
                node.Url = state.Links[linkIndex] ?? string.Empty;
                linkIndex++;
            }

            if (IsRowRole(node.Role)
                && state.Rows != null
                && rowIndex < state.Rows.Count)
            {
                if (string.Equals(state.Rows[rowIndex], "true", StringComparison.OrdinalIgnoreCase))
                {
                    node.Selected = true;
                }

                rowIndex++;
            }

            ApplyDomValue(node, state, ref valueIndex);
            ApplyDomPressed(node, state, ref pressedIndex);

            if (state.Attrs != null)
            {
                string name = AriaSnapshotTemplateParser.NormalizeWhiteSpace(node.Name ?? string.Empty);
                for (int i = 0; i < state.Attrs.Count; i++)
                {
                    DomAttr attr = state.Attrs[i];
                    if (!string.Equals(
                        name,
                        AriaSnapshotTemplateParser.NormalizeWhiteSpace(attr.Name ?? string.Empty),
                        StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(attr.Invalid))
                    {
                        node.Invalid = attr.Invalid;
                    }

                    if (IsSelectedRole(node.Role)
                        && string.Equals(attr.Selected, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        node.Selected = true;
                    }

                    break;
                }
            }

            if (node.Children == null)
            {
                return;
            }

            foreach (AccessibilitySnapshotResult child in node.Children)
            {
                AssignDomState(child, state, ref linkIndex, ref rowIndex, ref valueIndex, ref pressedIndex);
            }
        }

        private static void ApplyDomPressed(AccessibilitySnapshotResult node, DomState state, ref int pressedIndex)
        {
            if (!string.Equals(node.Role, "button", StringComparison.OrdinalIgnoreCase)
                || state.Pressed == null
                || pressedIndex >= state.Pressed.Count)
            {
                return;
            }

            string value = state.Pressed[pressedIndex];
            pressedIndex++;
            if (string.Equals(value, "mixed", StringComparison.OrdinalIgnoreCase))
            {
                node.Pressed = MixedState.Mixed;
            }
            else if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                node.Pressed = MixedState.On;
            }
            else if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            {
                node.Pressed = MixedState.Off;
            }
        }

        private static void ApplyDomValue(AccessibilitySnapshotResult node, DomState state, ref int valueIndex)
        {
            if (!IsWidgetWithValue(node.Role) || state.Values == null || state.Values.Count == 0)
            {
                return;
            }

            string name = AriaSnapshotTemplateParser.NormalizeWhiteSpace(node.Name ?? string.Empty);
            DomValue matched = null;
            for (int i = 0; i < state.Values.Count; i++)
            {
                DomValue candidate = state.Values[i];
                if (string.Equals(
                    name,
                    AriaSnapshotTemplateParser.NormalizeWhiteSpace(candidate.Name ?? string.Empty),
                    StringComparison.Ordinal))
                {
                    matched = candidate;
                    break;
                }
            }

            if (matched == null && valueIndex < state.Values.Count)
            {
                matched = state.Values[valueIndex];
                valueIndex++;
            }

            if (matched == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(matched.Invalid))
            {
                node.Invalid = matched.Invalid;
            }

            if (!string.IsNullOrEmpty(matched.Value)
                && (string.IsNullOrEmpty(node.Value) || IsProgressbarRole(node.Role)))
            {
                node.Value = matched.Value;
            }

            if (!string.IsNullOrEmpty(matched.Placeholder)
                && !string.Equals(
                    AriaSnapshotTemplateParser.NormalizeWhiteSpace(matched.Placeholder),
                    AriaSnapshotTemplateParser.NormalizeWhiteSpace(node.Name ?? string.Empty),
                    StringComparison.Ordinal))
            {
                node.Placeholder = matched.Placeholder;
            }
        }

        private static ExpectException ParseFailed(string header, string expected, string error, int timeoutMs)
        {
            string unshifted = AriaSnapshotTemplateParser.Unshift(expected);
            StringBuilder log = new StringBuilder();
            log.Append(header);
            log.Append("\n\nExpected: ");
            log.Append(PrintExpected(unshifted));
            if (HasMixedDashIndent(unshifted))
            {
                log.Append('\n');
                log.Append(unshifted);
            }

            log.Append("\nError: ");
            log.Append(error ?? string.Empty);
            if (error == null || !error.EndsWith('\n'))
            {
                log.Append('\n');
            }

            log.Append("\nCall log:\n  - Expect \"toMatchAriaSnapshot\" with timeout ");
            log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
            log.Append("ms\n");
            return ExpectException.Fail(
                log.ToString(),
                actual: null,
                expected: unshifted,
                name: "toMatchAriaSnapshot",
                pass: false,
                timeoutMs,
                ariaSnapshot: null);
        }

        private static ExpectException Mismatch(
            string header,
            string locator,
            string expected,
            string received,
            int timeoutMs,
            string expectLog)
        {
            StringBuilder log = new StringBuilder();
            log.Append(header);
            log.Append('\n');
            if (!string.IsNullOrEmpty(locator))
            {
                log.Append("\nLocator: ");
                log.Append(locator);
                log.Append('\n');
            }

            log.Append('\n');
            log.Append("Timeout: ");
            log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
            log.Append("ms\n");
            log.Append(PrintDiff(expected ?? string.Empty, received ?? string.Empty));
            log.Append("\nCall log:\n  - Expect \"");
            log.Append(expectLog);
            log.Append("\" with timeout ");
            log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
            log.Append("ms\n  - unexpected value \"");
            log.Append(received ?? string.Empty);
            log.Append("\"\nTimeout:  ");
            log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
            log.Append("ms\n");
            return ExpectException.Fail(
                log.ToString(),
                actual: received,
                expected: expected,
                name: "toMatchAriaSnapshot",
                pass: false,
                timeoutMs,
                ariaSnapshot: null);
        }

        private static bool HasMixedDashIndent(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            int? seen = null;
            string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (!trimmed.StartsWith('-'))
                {
                    continue;
                }

                int pad = lines[i].Length - trimmed.Length;
                if (seen == null)
                {
                    seen = pad;
                }
                else if (seen.Value != pad)
                {
                    return true;
                }
            }

            return false;
        }

        private static string PrintExpected(string value)
        {
            return "\"" + (value ?? string.Empty)
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        }

        private static string PrintDiff(string expected, string received)
        {
            string[] exp = SplitLines(expected);
            string[] rec = SplitLines(received);
            StringBuilder builder = new StringBuilder();
            builder.Append("- Expected  - ");
            builder.Append(exp.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append("\n+ Received  + ");
            builder.Append(rec.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append("\n\n");
            for (int i = 0; i < exp.Length; i++)
            {
                builder.Append("- ");
                builder.Append(exp[i]);
                builder.Append('\n');
            }

            for (int i = 0; i < rec.Length; i++)
            {
                builder.Append("+ ");
                builder.Append(rec[i]);
                builder.Append('\n');
            }

            return builder.ToString();
        }

        private static string[] SplitLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Array.Empty<string>();
            }

            return text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        }

        private sealed class DomState
        {
            public List<string> Links { get; set; } = new List<string>();

            public List<DomAttr> Attrs { get; set; } = new List<DomAttr>();

            public List<string> Rows { get; set; } = new List<string>();

            public List<DomValue> Values { get; set; } = new List<DomValue>();

            public List<string> Pressed { get; set; } = new List<string>();
        }

        private sealed class DomValue
        {
            public string Name { get; set; }

            public string Invalid { get; set; }

            public string Value { get; set; }

            public string Placeholder { get; set; }
        }

        private sealed class DomAttr
        {
            public string Name { get; set; }

            public string Invalid { get; set; }

            public string Selected { get; set; }
        }

        private sealed class DomTextItem
        {
            public string Kind { get; set; }

            public string Text { get; set; }

            public string Value { get; set; }

            public List<DomTableRow> Rows { get; set; }
        }

        private sealed class DomTableRow
        {
            public bool Selected { get; set; }

            public string Name { get; set; }

            public List<string> Cells { get; set; }
        }
    }
}
