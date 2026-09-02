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
using System.Text.RegularExpressions;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Page-side finder functions for locator-less getBy* helpers.
    /// Each string is a JS function declaration suitable for <c>callFunctionOn</c> or
    /// for wrapping as an IIFE expression.
    /// </summary>
    internal static class GetBySelectorScript
    {
        /// <summary>
        /// <c>(text, exact) => Element | null</c> — innermost element whose normalized
        /// text contains (lax) or equals (exact) the query. Skips script/style/head.
        /// </summary>
        internal const string FindByText = @"function(text, exact) {
    const skip = { SCRIPT: 1, STYLE: 1, HEAD: 1, NOSCRIPT: 1 };
    const normalize = (s) => String(s || '').replace(/\s+/g, ' ').trim();
    const needle = exact ? normalize(text) : normalize(text).toLowerCase();
    let found = null;
    const matches = (el) => {
        let raw = '';
        if (el.tagName === 'INPUT' && /^(submit|button|reset)$/i.test(el.type || ''))
            raw = el.value || '';
        else
            raw = el.innerText || el.textContent || '';
        const hay = exact ? normalize(raw) : normalize(raw).toLowerCase();
        return exact ? hay === needle : hay.indexOf(needle) !== -1;
    };
    const visit = (el) => {
        if (!el || skip[el.tagName]) return false;
        let childHit = false;
        const children = el.children || [];
        for (let i = 0; i < children.length; i++)
            childHit = visit(children[i]) || childHit;
        if (matches(el) && !childHit) {
            if (!found) found = el;
            return true;
        }
        return childHit || matches(el);
    };
    visit(document.documentElement);
    return found;
}";

        /// <summary>
        /// <c>(role, name, exact, options) => Element | null</c> — first element in document
        /// order whose implicit or explicit ARIA role matches, optionally filtered
        /// by accessible name and role states.
        /// </summary>
        internal const string FindByRole = @"function(role, name, exact, options) {
    const want = String(role || '').toLowerCase();
    const implicitRole = (el) => {
        if (el.hasAttribute('role')) return (el.getAttribute('role') || '').toLowerCase();
        const tag = el.tagName;
        if (tag === 'BUTTON') return 'button';
        if (tag === 'A' && el.hasAttribute('href')) return 'link';
        if (tag === 'TEXTAREA') return 'textbox';
        if (tag === 'SELECT') return 'combobox';
        if (tag === 'IMG') return 'img';
        if (tag === 'H1' || tag === 'H2' || tag === 'H3' || tag === 'H4' || tag === 'H5' || tag === 'H6') return 'heading';
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
    const accessibleName = (el) => {
        const labelled = el.getAttribute('aria-labelledby');
        if (labelled) {
            const parts = String(labelled).split(/\s+/).map((id) => {
                const n = document.getElementById(id);
                return n ? (n.innerText || n.textContent || '') : '';
            });
            const joined = parts.join(' ').replace(/\s+/g, ' ').trim();
            if (joined) return joined;
        }
        const label = el.getAttribute('aria-label');
        if (label) return label.trim();
        if (el.labels && el.labels.length)
            return (el.labels[0].innerText || el.labels[0].textContent || '').replace(/\s+/g, ' ').trim();
        if (el.tagName === 'IMG') return (el.getAttribute('alt') || '').trim();
        if (el.tagName === 'INPUT' && /^(submit|button|reset)$/i.test(el.type || ''))
            return (el.value || '').trim();
        return (el.innerText || el.textContent || '').replace(/\s+/g, ' ').trim();
    };
    const accessibleDescription = (el) => {
        const aria = el.getAttribute('aria-description');
        if (aria) return String(aria).replace(/\s+/g, ' ').trim();
        const ids = String(el.getAttribute('aria-describedby') || '').split(/\s+/);
        const parts = [];
        for (let i = 0; i < ids.length; i++) {
            const id = ids[i];
            if (!id) continue;
            const ref = document.getElementById(id);
            if (ref) parts.push(String(ref.innerText || ref.textContent || '').trim());
        }
        if (parts.length) return parts.join(' ').replace(/\s+/g, ' ').trim();
        return String(el.getAttribute('title') || '').replace(/\s+/g, ' ').trim();
    };
    options = options || {};
    const all = [];
    const visit = (root) => {
        const nodes = root.querySelectorAll('*');
        for (let i = 0; i < nodes.length; i++) {
            const node = nodes[i];
            all.push(node);
            if (node.shadowRoot) {
                visit(node.shadowRoot);
            }
        }
    };
    visit(document);
    for (let i = 0; i < all.length; i++) {
        const el = all[i];
        if (implicitRole(el) !== want) continue;
        if (options.checked !== undefined && options.checked !== null) {
            let isChecked = el.checked === true;
            const aria = String(el.getAttribute('aria-checked') || '').toLowerCase();
            if (aria === 'true') isChecked = true;
            else if (aria === 'false') isChecked = false;
            if (isChecked !== !!options.checked) continue;
        }
        if (options.disabled !== undefined && options.disabled !== null) {
            const isDisabled = el.disabled === true || String(el.getAttribute('aria-disabled') || '').toLowerCase() === 'true';
            if (isDisabled !== !!options.disabled) continue;
        }
        if (options.expanded !== undefined && options.expanded !== null) {
            const isExpanded = String(el.getAttribute('aria-expanded') || '').toLowerCase() === 'true';
            if (isExpanded !== !!options.expanded) continue;
        }
        if (options.includeHidden === false) {
            if (el.hidden || String(el.getAttribute('aria-hidden') || '').toLowerCase() === 'true') continue;
            const style = window.getComputedStyle(el);
            if (style.display === 'none' || style.visibility === 'hidden') continue;
        }
        if (options.level !== undefined && options.level !== null) {
            let level = 0;
            if (el.tagName === 'H1') level = 1;
            else if (el.tagName === 'H2') level = 2;
            else if (el.tagName === 'H3') level = 3;
            else if (el.tagName === 'H4') level = 4;
            else if (el.tagName === 'H5') level = 5;
            else if (el.tagName === 'H6') level = 6;
            const ariaLevel = parseInt(el.getAttribute('aria-level') || '0', 10);
            if (ariaLevel) level = ariaLevel;
            if (level !== options.level) continue;
        }
        if (options.pressed !== undefined && options.pressed !== null) {
            const isPressed = String(el.getAttribute('aria-pressed') || '').toLowerCase() === 'true';
            if (isPressed !== !!options.pressed) continue;
        }
        if (options.selected !== undefined && options.selected !== null) {
            let isSelected = el.selected === true;
            const ariaSelected = String(el.getAttribute('aria-selected') || '').toLowerCase();
            if (ariaSelected === 'true') isSelected = true;
            else if (ariaSelected === 'false') isSelected = false;
            if (isSelected !== !!options.selected) continue;
        }
        if (options.description != null && options.description !== '') {
            const desc = accessibleDescription(el);
            const needle = String(options.description).replace(/\s+/g, ' ').trim();
            const okDesc = exact ? desc === needle : desc.toLowerCase().indexOf(needle.toLowerCase()) !== -1;
            if (!okDesc) continue;
        }
        if (options.descriptionPattern != null && options.descriptionPattern !== '') {
            const re = new RegExp(options.descriptionPattern, options.descriptionFlags || '');
            re.lastIndex = 0;
            if (!re.test(accessibleDescription(el))) continue;
        }
        if (options.namePattern != null && options.namePattern !== '') {
            const re = new RegExp(options.namePattern, options.nameFlags || '');
            re.lastIndex = 0;
            if (!re.test(accessibleName(el))) continue;
        }
        if (name == null || name === '') return el;
        const acc = accessibleName(el);
        const needle = String(name).replace(/\s+/g, ' ').trim();
        const ok = exact ? acc === needle : acc.toLowerCase().indexOf(needle.toLowerCase()) !== -1;
        if (ok) return el;
    }
    return null;
}";

        /// <summary>
        /// <c>(text, exact) => Element | null</c> — control associated with a matching
        /// <c>label</c> or <c>aria-label</c>.
        /// </summary>
        internal const string FindByLabel = @"function(text, exact) {
    const needle = exact ? String(text || '').replace(/\s+/g, ' ').trim() : String(text || '').replace(/\s+/g, ' ').trim().toLowerCase();
    const ok = (raw) => {
        const hay = exact ? String(raw || '').replace(/\s+/g, ' ').trim() : String(raw || '').replace(/\s+/g, ' ').trim().toLowerCase();
        return exact ? hay === needle : hay.indexOf(needle) !== -1;
    };
    const labelledByText = (el) => {
        const labelled = el.getAttribute('aria-labelledby');
        if (!labelled) return '';
        const parts = String(labelled).split(/\s+/).map((id) => {
            const n = document.getElementById(id);
            return n ? (n.innerText || n.textContent || '') : '';
        });
        return parts.join(' ').replace(/\s+/g, ' ').trim();
    };
    const by = document.querySelectorAll('[aria-labelledby]');
    for (let i = 0; i < by.length; i++) {
        const textBy = labelledByText(by[i]);
        if (textBy && ok(textBy)) return by[i];
    }
    const labels = document.querySelectorAll('label');
    for (let i = 0; i < labels.length; i++) {
        const label = labels[i];
        if (!ok(label.innerText || label.textContent || '')) continue;
        const forId = label.getAttribute('for');
        if (forId) {
            const el = document.getElementById(forId);
            if (el && !labelledByText(el)) return el;
        }
        const control = label.querySelector('input, select, textarea, button');
        if (control && !labelledByText(control)) return control;
    }
    const labeled = document.querySelectorAll('[aria-label]');
    for (let i = 0; i < labeled.length; i++) {
        const el = labeled[i];
        if (labelledByText(el)) continue;
        const aria = el.getAttribute('aria-label');
        if (aria != null && String(aria).trim() !== '' && ok(aria)) return el;
    }
    return null;
}";

        /// <summary>
        /// <c>(text, exact) => Element | null</c> — first element whose placeholder matches.
        /// </summary>
        internal const string FindByPlaceholder = @"function(text, exact) {
    const needle = exact ? String(text) : String(text || '').replace(/\s+/g, ' ').trim().toLowerCase();
    const all = document.querySelectorAll('[placeholder]');
    for (let i = 0; i < all.length; i++) {
        const raw = all[i].getAttribute('placeholder') || '';
        const hay = exact ? raw : raw.replace(/\s+/g, ' ').trim().toLowerCase();
        if (exact ? hay === needle : hay.indexOf(needle) !== -1) return all[i];
    }
    return null;
}";

        /// <summary>
        /// <c>(text, exact) => Element | null</c> — first element whose alt text matches.
        /// </summary>
        internal const string FindByAltText = @"function(text, exact) {
    const needle = exact ? String(text) : String(text || '').replace(/\s+/g, ' ').trim().toLowerCase();
    const all = document.querySelectorAll('[alt]');
    for (let i = 0; i < all.length; i++) {
        const raw = all[i].getAttribute('alt') || '';
        const hay = exact ? raw : raw.replace(/\s+/g, ' ').trim().toLowerCase();
        if (exact ? hay === needle : hay.indexOf(needle) !== -1) return all[i];
    }
    return null;
}";

        /// <summary>
        /// <c>(text, exact) => Element | null</c> — first element whose title matches.
        /// </summary>
        internal const string FindByTitle = @"function(text, exact) {
    const needle = exact ? String(text) : String(text || '').replace(/\s+/g, ' ').trim().toLowerCase();
    const all = document.querySelectorAll('[title]');
    for (let i = 0; i < all.length; i++) {
        const raw = all[i].getAttribute('title') || '';
        const hay = exact ? raw : raw.replace(/\s+/g, ' ').trim().toLowerCase();
        if (exact ? hay === needle : hay.indexOf(needle) !== -1) return all[i];
    }
    return null;
}";

        private static string _testIdAttributeName = "data-testid";

        /// <summary>
        /// Builds the optional filter object passed as the fourth argument to
        /// <see cref="FindByRole"/>.
        /// </summary>
        /// <param name="checkedState">When set, require this checked state.</param>
        /// <param name="disabled">When set, require this disabled state.</param>
        /// <param name="expanded">When set, require this expanded state.</param>
        /// <param name="includeHidden">When <see langword="false"/>, skip hidden elements.</param>
        /// <param name="level">When set, require this heading level.</param>
        /// <param name="pressed">When set, require this pressed state.</param>
        /// <param name="selected">When set, require this selected state.</param>
        /// <param name="description">Optional accessible description filter.</param>
        /// <param name="descriptionRegex">Optional accessible description regular expression.</param>
        /// <param name="nameRegex">Optional accessible name regular expression.</param>
        /// <returns>A JSON-serializable options bag.</returns>
        internal static object RoleOptions(bool? checkedState = null, bool? disabled = null, bool? expanded = null, bool? includeHidden = null, int? level = null, bool? pressed = null, bool? selected = null, string description = null, Regex descriptionRegex = null, Regex nameRegex = null)
        {
            Dictionary<string, object> options = new Dictionary<string, object>(StringComparer.Ordinal);
            if (checkedState.HasValue)
            {
                options["checked"] = checkedState.Value;
            }

            if (disabled.HasValue)
            {
                options["disabled"] = disabled.Value;
            }

            if (expanded.HasValue)
            {
                options["expanded"] = expanded.Value;
            }

            if (includeHidden.HasValue)
            {
                options["includeHidden"] = includeHidden.Value;
            }

            if (level.HasValue)
            {
                options["level"] = level.Value;
            }

            if (pressed.HasValue)
            {
                options["pressed"] = pressed.Value;
            }

            if (selected.HasValue)
            {
                options["selected"] = selected.Value;
            }

            if (!string.IsNullOrEmpty(description))
            {
                options["description"] = description;
            }

            if (descriptionRegex != null)
            {
                options["descriptionPattern"] = GetByAllScript.Pattern(descriptionRegex);
                options["descriptionFlags"] = GetByAllScript.Flags(descriptionRegex);
            }

            if (nameRegex != null)
            {
                options["namePattern"] = GetByAllScript.Pattern(nameRegex);
                options["nameFlags"] = GetByAllScript.Flags(nameRegex);
            }

            return options;
        }

        /// <summary>
        /// Sets the attribute used by <see cref="TestIdSelector"/>. Defaults to
        /// <c>data-testid</c>.
        /// </summary>
        /// <param name="attributeName">
        /// HTML attribute name, for example <c>data-testid</c>. Official
        /// Playwright accepts a comma-separated list.
        /// </param>
        internal static void SetTestIdAttributeName(string attributeName)
        {
            if (string.IsNullOrEmpty(attributeName))
            {
                throw new ArgumentException("Test id attribute name must be non-empty.", nameof(attributeName));
            }

            List<string> names = ParseTestIdAttributeNames(attributeName);
            if (names.Count == 0)
            {
                throw new ArgumentException("Test id attribute name must be non-empty.", nameof(attributeName));
            }

            _testIdAttributeName = string.Join(",", names);
        }

        /// <summary>
        /// Builds a CSS selector that matches the configured test-id attribute exactly.
        /// Official <c>setTestIdAttribute</c> may list several names, matching any.
        /// </summary>
        /// <param name="testId">The test id value.</param>
        /// <returns>A CSS attribute selector.</returns>
        internal static string TestIdSelector(string testId)
        {
            string value = testId ?? string.Empty;
            string escaped = value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
            List<string> names = ParseTestIdAttributeNames(_testIdAttributeName);
            string[] parts = new string[names.Count];
            for (int i = 0; i < names.Count; i++)
            {
                parts[i] = "[" + names[i] + "=\"" + escaped + "\"]";
            }

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Attribute name used by <see cref="TestIdSelector"/> and
        /// <c>GetByTestId(Regex)</c>. Defaults to <c>data-testid</c>.
        /// </summary>
        /// <returns>The configured attribute name.</returns>
        internal static string TestIdAttributeName()
            => _testIdAttributeName;

        private static List<string> ParseTestIdAttributeNames(string attributeName)
        {
            List<string> names = new();
            string[] parts = attributeName.Split(',');
            foreach (string part in parts)
            {
                string name = part.Trim();
                if (name.Length == 0)
                {
                    continue;
                }

                if (!IsValidAttributeName(name))
                {
                    throw new ArgumentException("Test id attribute name is invalid.", nameof(attributeName));
                }

                names.Add(name);
            }

            return names;
        }

        private static bool IsValidAttributeName(string name)
        {
            char first = name[0];
            if (!char.IsLetter(first) && first != '_')
            {
                return false;
            }

            for (int i = 1; i < name.Length; i++)
            {
                char c = name[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != ':')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
