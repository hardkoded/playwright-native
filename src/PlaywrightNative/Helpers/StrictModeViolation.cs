/*
 * Copyright (c) Microsoft Corporation.
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
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official injected <c>strictModeViolationError</c> formatting: element
    /// <c>previewNode</c> lines plus codegen <c>aka</c> locators (getByText,
    /// getByRole, escaped CSS, <c>:nth-child()</c>).
    /// </summary>
    internal static class StrictModeViolation
    {
        /// <summary>
        /// Page-side helpers used both from element-handle evaluate and from
        /// in-page actions such as <c>dispatchEvent</c>.
        /// </summary>
        internal const string GeneratorSource = @"
  const oneLine = s => String(s).replace(/\n/g, '\u21B5').replace(/\t/g, '\u21C6');
  const trimEllipsis = (input, cap) => {
    input = String(input);
    const chars = [...input];
    if (chars.length > cap) return chars.slice(0, cap - 1).join('') + '\u2026';
    return chars.join('');
  };
  const previewNode = (node) => {
    if (!node) return 'node';
    if (node.nodeType === 3) return oneLine('#text=' + (node.nodeValue || ''));
    if (node.nodeType !== 1) return oneLine('<' + String(node.nodeName).toLowerCase() + ' />');
    const booleanAttributes = { checked: 1, selected: 1, disabled: 1, readonly: 1, multiple: 1 };
    const autoClosingTags = {
      AREA: 1, BASE: 1, BR: 1, COL: 1, COMMAND: 1, EMBED: 1, HR: 1, IMG: 1, INPUT: 1,
      KEYGEN: 1, LINK: 1, MENUITEM: 1, META: 1, PARAM: 1, SOURCE: 1, TRACK: 1, WBR: 1
    };
    const attrs = [];
    const list = node.attributes || [];
    for (let i = 0; i < list.length; i++) {
      const name = list[i].name;
      const value = list[i].value;
      if (name === 'style') continue;
      if (!value && booleanAttributes[name]) attrs.push(' ' + name);
      else attrs.push(' ' + name + '=""' + value + '""');
    }
    attrs.sort((a, b) => a.length - b.length);
    const attrText = trimEllipsis(attrs.join(''), 500);
    const tag = String(node.nodeName).toLowerCase();
    if (autoClosingTags[node.nodeName]) return oneLine('<' + tag + attrText + '/>');
    const children = node.childNodes;
    let onlyText = false;
    if (children.length <= 5) {
      onlyText = true;
      for (let i = 0; i < children.length; i++)
        onlyText = onlyText && children[i].nodeType === 3;
    }
    const text = onlyText ? (node.textContent || '') : (children.length ? '\u2026' : '');
    return oneLine('<' + tag + attrText + '>' + trimEllipsis(text, 50) + '</' + tag + '>');
  };
  const q = (s) => ""'"" + String(s).replace(/\\/g, '\\\\').replace(/'/g, ""\\'"") + ""'"";
  const normalize = (s) => String(s || '').replace(/\s+/g, ' ').trim();
  const escapeNodeName = (node) => String(node.nodeName).toLocaleLowerCase().replace(/[:.]/g, ch => '\\' + ch);
  const cssEscapeChar = (s, i) => {
    const c = s.charCodeAt(i);
    if (c === 0x0000) return '\uFFFD';
    if ((c >= 0x0001 && c <= 0x001f) || (c >= 0x0030 && c <= 0x0039 && (i === 0 || (i === 1 && s.charCodeAt(0) === 0x002d))))
      return '\\' + c.toString(16) + ' ';
    if (i === 0 && c === 0x002d && s.length === 1) return '\\' + s.charAt(i);
    if (c >= 0x0080 || c === 0x002d || c === 0x005f || (c >= 0x0030 && c <= 0x0039) ||
        (c >= 0x0041 && c <= 0x005a) || (c >= 0x0061 && c <= 0x007a))
      return s.charAt(i);
    return '\\' + s.charAt(i);
  };
  const escapeClassName = (className) => {
    let result = '';
    for (let i = 0; i < className.length; i++) result += cssEscapeChar(className, i);
    return result;
  };
  const implicitRole = (el) => {
    if (el.hasAttribute('role')) return (el.getAttribute('role') || '').toLowerCase();
    const tag = el.tagName;
    if (tag === 'BUTTON') return 'button';
    if (tag === 'A' && el.hasAttribute('href')) return 'link';
    if (tag === 'TEXTAREA') return 'textbox';
    if (tag === 'SELECT') return 'combobox';
    if (tag === 'INPUT') {
      const t = (el.type || 'text').toLowerCase();
      if (t === 'submit' || t === 'button' || t === 'reset' || t === 'image') return 'button';
      if (t === 'checkbox') return 'checkbox';
      if (t === 'radio') return 'radio';
      if (t === 'hidden') return '';
      return 'textbox';
    }
    return '';
  };
  const findByText = (text) => {
    const needle = normalize(text);
    const hits = [];
    const all = document.querySelectorAll('*');
    for (let i = 0; i < all.length; i++) {
      const t = normalize(all[i].textContent || '');
      if (t && t === needle) hits.push(all[i]);
    }
    return hits.filter(el => !hits.some(o => o !== el && el.contains(o)));
  };
  const findByRole = (role) => {
    const out = [];
    const all = document.querySelectorAll('*');
    for (let i = 0; i < all.length; i++) {
      if (implicitRole(all[i]) === role) out.push(all[i]);
    }
    return out;
  };
  const uniqueCssFor = (el) => {
    if (!el || el.nodeType !== 1) return '';
    const tag = escapeNodeName(el);
    try {
      const hits = document.querySelectorAll(tag);
      if (hits.length === 1 && hits[0] === el) return tag;
    } catch (e) {}
    return '';
  };
  const cssFallback = (target) => {
    const root = target.ownerDocument;
    const tokens = [];
    const uniqueCSSSelector = (prefix) => {
      const path = tokens.slice();
      if (prefix) path.unshift(prefix);
      const selector = path.join(' > ');
      try {
        return root.querySelector(selector) === target ? selector : undefined;
      } catch (e) {
        return undefined;
      }
    };
    for (let element = target; element && element !== root; element = element.parentElement) {
      let bestTokenForLevel = '';
      if (element.id) {
        const idToken = /^[a-zA-Z][a-zA-Z0-9\-\_]+$/.test(element.id) ? '#' + element.id : '[id=""' + element.id + '""]';
        const selector = uniqueCSSSelector(idToken);
        if (selector) return selector;
        bestTokenForLevel = idToken;
      }
      const parent = element.parentNode;
      const classes = [];
      if (element.classList) {
        for (let c = 0; c < element.classList.length; c++) {
          const raw = element.classList[c];
          if (/^[A-Za-z_][A-Za-z0-9_-]*$/.test(raw))
            classes.push(escapeClassName(raw));
        }
      }
      for (let i = 0; i < classes.length; i++) {
        const token = '.' + classes.slice(0, i + 1).join('.');
        const selector = uniqueCSSSelector(token);
        if (selector) return selector;
        if (!bestTokenForLevel && parent) {
          try {
            if (parent.querySelectorAll(token).length === 1) bestTokenForLevel = token;
          } catch (e) {}
        }
      }
      if (parent && parent.children) {
        const siblings = parent.children;
        const nodeName = element.nodeName;
        let sameTagCount = 0;
        let sameTagIndex = -1;
        let childIndex = -1;
        for (let s = 0; s < siblings.length; s++) {
          if (siblings[s] === element) childIndex = s;
          if (siblings[s].nodeName === nodeName) {
            if (siblings[s] === element) sameTagIndex = sameTagCount;
            sameTagCount++;
          }
        }
        const token = sameTagIndex === 0 ? escapeNodeName(element) : escapeNodeName(element) + ':nth-child(' + (1 + childIndex) + ')';
        const selector = uniqueCSSSelector(token);
        if (selector) return selector;
        if (!bestTokenForLevel) bestTokenForLevel = token;
      } else if (!bestTokenForLevel) {
        bestTokenForLevel = escapeNodeName(element);
      }
      tokens.unshift(bestTokenForLevel);
    }
    return tokens.join(' > ');
  };
  const generateLocator = (el) => {
    const text = normalize(el.textContent || '');
    if (text && text.length <= 80) {
      const byText = findByText(text);
      if (byText.length === 1 && byText[0] === el)
        return 'getByText(' + q(text) + ')';
      if (byText.length > 1 && byText[0] === el)
        return 'getByText(' + q(text) + ').first()';
      if (byText.length > 1 && byText[0] !== el) {
        let anc = el.parentElement;
        while (anc && anc !== document.documentElement) {
          const ancTag = escapeNodeName(anc);
          let ancHits = [];
          try { ancHits = Array.prototype.slice.call(document.querySelectorAll(ancTag)); } catch (e) {}
          const withText = ancHits.filter(a => normalize(a.textContent || '').indexOf(text) !== -1);
          if (withText.length === 1)
            return 'locator(' + q(ancTag) + ').filter({ hasText: ' + q(text) + ' })';
          anc = anc.parentElement;
        }
      }
    }
    const role = implicitRole(el);
    if (role) {
      const byRole = findByRole(role);
      if (byRole.length === 1 && byRole[0] === el)
        return 'getByRole(' + q(role) + ')';
      if (byRole.length > 1) {
        let parent = el.parentElement;
        while (parent && parent !== document.documentElement) {
          const scoped = byRole.filter(e => parent.contains(e) && e !== parent);
          if (scoped.length === 1 && scoped[0] === el) {
            const parentCss = uniqueCssFor(parent);
            if (parentCss)
              return 'locator(' + q(parentCss) + ').getByRole(' + q(role) + ')';
          }
          parent = parent.parentElement;
        }
        if (byRole[0] === el)
          return 'getByRole(' + q(role) + ').first()';
      }
    }
    const tag = escapeNodeName(el);
    let sameTag = [];
    try { sameTag = Array.prototype.slice.call(document.querySelectorAll(tag)); } catch (e) {}
    if (sameTag.length === 1 && sameTag[0] === el)
      return 'locator(' + q(tag) + ')';
    if (sameTag.length > 1 && sameTag[0] === el)
      return 'locator(' + q(tag) + ').first()';
    if (el.parentElement) {
      const combo = escapeNodeName(el.parentElement) + ' ' + tag;
      try {
        const hits = document.querySelectorAll(combo);
        if (hits.length === 1 && hits[0] === el)
          return 'locator(' + q(combo) + ')';
      } catch (e) {}
    }
    return 'locator(' + q(cssFallback(el)) + ')';
  };
  const generateLine = (el) => {
    try {
      return previewNode(el) + ' aka ' + generateLocator(el);
    } catch (e) {
      try { return previewNode(el); } catch (e2) { return 'node'; }
    }
  };
  const formatStrict = (label, all) => {
    const list = all || [];
    let msg = 'strict mode violation: ' + label + ' resolved to ' + list.length + ' elements:';
    const n = Math.min(list.length, 10);
    for (let i = 0; i < n; i++)
      msg += '\n    ' + (i + 1) + ') ' + generateLine(list[i]);
    if (n < list.length) msg += '\n    ...';
    return msg + '\n';
  };
";

        /// <summary>
        /// <c>(el) => string</c> — official <c>N) preview aka locator</c> line body.
        /// </summary>
        internal const string GenerateLineFunction = "el => {" + GeneratorSource + " return generateLine(el); }";

        /// <summary>
        /// Quotes <paramref name="selector"/> as official <c>locator('...')</c>.
        /// </summary>
        /// <param name="selector">Raw selector, including engine prefixes.</param>
        /// <returns>A JavaScript locator expression.</returns>
        internal static string QuoteLocator(string selector)
        {
            string escaped = (selector ?? string.Empty)
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("'", "\\'", StringComparison.Ordinal);
            return "locator('" + escaped + "')";
        }

        /// <summary>
        /// Builds the official strict-mode violation message, including
        /// preview / <c>aka</c> lines for each match.
        /// </summary>
        /// <param name="label">Locator text shown before <c>resolved to</c>.</param>
        /// <param name="matches">Matching elements in document order.</param>
        /// <returns>The formatted exception message.</returns>
        internal static async Task<string> FormatAsync(string label, IReadOnlyList<IElementHandle> matches)
        {
            int count = matches == null ? 0 : matches.Count;
            StringBuilder builder = new StringBuilder();
            builder.Append("strict mode violation: ");
            builder.Append(string.IsNullOrEmpty(label) ? "locator" : label);
            builder.Append(" resolved to ");
            builder.Append(count.ToString(CultureInfo.InvariantCulture));
            builder.Append(" elements:");
            if (matches != null)
            {
                int limit = Math.Min(count, 10);
                for (int i = 0; i < limit; i++)
                {
                    string line = await TryGenerateLineAsync(matches[i]).ConfigureAwait(false);
                    builder.Append("\n    ");
                    builder.Append((i + 1).ToString(CultureInfo.InvariantCulture));
                    builder.Append(") ");
                    builder.Append(line);
                }

                if (count > limit)
                {
                    builder.Append("\n    ...");
                }
            }

            builder.Append('\n');
            return builder.ToString();
        }

        private static async Task<string> TryGenerateLineAsync(IElementHandle handle)
        {
            if (handle == null)
            {
                return "node";
            }

            try
            {
                string line = await handle.EvaluateAsync<string>(GenerateLineFunction).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(line))
                {
                    return line;
                }
            }
            catch (PlaywrightNativeException)
            {
            }

            try
            {
                string preview = await handle.EvaluateAsync<string>(RemoteObject.PreviewNodeFunction).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(preview))
                {
                    return preview;
                }
            }
            catch (PlaywrightNativeException)
            {
            }

            return "node";
        }
    }
}
