// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
using System;
using System.Text.RegularExpressions;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Page-side finders that return every matching element (for locator GetBy*).
    /// </summary>
    internal static class GetByAllScript
    {
        /// <summary>
        /// <c>(text, exact) => Element[]</c> — innermost elements whose text matches.
        /// </summary>
        internal const string FindAllByText = @"function(text, exact) {
    const skip = { SCRIPT: 1, STYLE: 1, HEAD: 1, NOSCRIPT: 1 };
    const normalize = (s) => String(s || '').replace(/\s+/g, ' ').trim();
    const needle = exact ? normalize(text) : normalize(text).toLowerCase();
    const found = [];
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
            found.push(el);
            return true;
        }
        return childHit || matches(el);
    };
    visit(document.documentElement);
    return found;
}";

        /// <summary>
        /// <c>(pattern, flags) => Element[]</c> — innermost elements whose text
        /// matches the JavaScript <c>RegExp</c>.
        /// </summary>
        internal const string FindAllByTextRegex = @"function(pattern, flags) {
    const skip = { SCRIPT: 1, STYLE: 1, HEAD: 1, NOSCRIPT: 1 };
    const re = new RegExp(pattern, flags || '');
    const found = [];
    const matches = (el) => {
        let raw = '';
        if (el.tagName === 'INPUT' && /^(submit|button|reset)$/i.test(el.type || ''))
            raw = el.value || '';
        else
            raw = el.innerText || el.textContent || '';
        re.lastIndex = 0;
        return re.test(raw);
    };
    const visit = (el) => {
        if (!el || skip[el.tagName]) return false;
        let childHit = false;
        const children = el.children || [];
        for (let i = 0; i < children.length; i++)
            childHit = visit(children[i]) || childHit;
        if (matches(el) && !childHit) {
            found.push(el);
            return true;
        }
        return childHit || matches(el);
    };
    visit(document.documentElement);
    return found;
}";

        /// <summary>
        /// <c>(role, name, exact, options) => Element[]</c>.
        /// </summary>
        internal const string FindAllByRole = @"function(role, name, exact, options) {
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
        const slots = el.querySelectorAll ? el.querySelectorAll('slot') : [];
        let slotted = '';
        for (let s = 0; s < slots.length; s++) {
            const nodes = slots[s].assignedNodes ? slots[s].assignedNodes({ flatten: true }) : [];
            for (let n = 0; n < nodes.length; n++) {
                slotted += nodes[n].textContent || '';
            }
        }
        if (slotted.replace(/\s+/g, ' ').trim()) {
            return slotted.replace(/\s+/g, ' ').trim();
        }
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
    const found = [];
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
        if (name == null || name === '') { found.push(el); continue; }
        const acc = accessibleName(el);
        const needle = String(name).replace(/\s+/g, ' ').trim();
        const ok = exact ? acc === needle : acc.toLowerCase().indexOf(needle.toLowerCase()) !== -1;
        if (ok) found.push(el);
    }
    return found;
}";

        /// <summary>
        /// <c>(text, exact) => Element[]</c>.
        /// </summary>
        internal const string FindAllByLabel = @"function(text, exact) {
    const needle = exact ? String(text) : String(text || '').replace(/\s+/g, ' ').trim().toLowerCase();
    const ok = (raw) => {
        const hay = exact ? String(raw || '').replace(/\s+/g, ' ').trim() : String(raw || '').replace(/\s+/g, ' ').trim().toLowerCase();
        return exact ? hay === String(text || '').replace(/\s+/g, ' ').trim() : hay.indexOf(needle) !== -1;
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
    const found = [];
    const seen = new Set();
    const add = (el) => { if (el && !seen.has(el)) { seen.add(el); found.push(el); } };
    const by = document.querySelectorAll('[aria-labelledby]');
    for (let i = 0; i < by.length; i++) {
        const textBy = labelledByText(by[i]);
        if (textBy && ok(textBy)) add(by[i]);
    }
    const labels = document.querySelectorAll('label');
    for (let i = 0; i < labels.length; i++) {
        const label = labels[i];
        if (!ok(label.innerText || label.textContent || '')) continue;
        const forId = label.getAttribute('for');
        if (forId) {
            const el = document.getElementById(forId);
            if (el && !labelledByText(el)) add(el);
            continue;
        }
        const control = label.querySelector('input, select, textarea, button');
        if (control && !labelledByText(control)) add(control);
    }
    const labeled = document.querySelectorAll('[aria-label]');
    for (let i = 0; i < labeled.length; i++) {
        const el = labeled[i];
        if (labelledByText(el)) continue;
        const aria = el.getAttribute('aria-label');
        if (aria != null && String(aria).trim() !== '' && ok(aria)) add(el);
    }
    return found;
}";

        /// <summary>
        /// <c>(pattern, flags) => Element[]</c>.
        /// </summary>
        internal const string FindAllByLabelRegex = @"function(pattern, flags) {
    const re = new RegExp(pattern, flags || '');
    const ok = (raw) => {
        re.lastIndex = 0;
        return re.test(String(raw || ''));
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
    const found = [];
    const seen = new Set();
    const add = (el) => { if (el && !seen.has(el)) { seen.add(el); found.push(el); } };
    const by = document.querySelectorAll('[aria-labelledby]');
    for (let i = 0; i < by.length; i++) {
        const textBy = labelledByText(by[i]);
        if (textBy && ok(textBy)) add(by[i]);
    }
    const labels = document.querySelectorAll('label');
    for (let i = 0; i < labels.length; i++) {
        const label = labels[i];
        if (!ok(label.innerText || label.textContent || '')) continue;
        const forId = label.getAttribute('for');
        if (forId) {
            const el = document.getElementById(forId);
            if (el && !labelledByText(el)) add(el);
            continue;
        }
        const control = label.querySelector('input, select, textarea, button');
        if (control && !labelledByText(control)) add(control);
    }
    const labeled = document.querySelectorAll('[aria-label]');
    for (let i = 0; i < labeled.length; i++) {
        const el = labeled[i];
        if (labelledByText(el)) continue;
        const aria = el.getAttribute('aria-label');
        if (aria != null && String(aria).trim() !== '' && ok(aria)) add(el);
    }
    return found;
}";

        /// <summary>
        /// <c>(attr, text, exact) => Element[]</c>.
        /// </summary>
        internal const string FindAllByAttribute = @"function(attr, text, exact) {
    const needle = exact ? String(text) : String(text || '').replace(/\s+/g, ' ').trim().toLowerCase();
    const all = document.querySelectorAll('[' + attr + ']');
    const found = [];
    for (let i = 0; i < all.length; i++) {
        const raw = all[i].getAttribute(attr) || '';
        const hay = exact ? raw : raw.replace(/\s+/g, ' ').trim().toLowerCase();
        if (exact ? hay === needle : hay.indexOf(needle) !== -1) found.push(all[i]);
    }
    return found;
}";

        /// <summary>
        /// <c>(attr, pattern, flags) => Element[]</c>.
        /// </summary>
        internal const string FindAllByAttributeRegex = @"function(attr, pattern, flags) {
    const names = String(attr || '').split(',').map(s => s.trim()).filter(Boolean);
    const re = new RegExp(pattern, flags || '');
    const selector = names.map(n => '[' + n + ']').join(',');
    const all = selector ? document.querySelectorAll(selector) : [];
    const found = [];
    for (let i = 0; i < all.length; i++) {
        const el = all[i];
        let hit = false;
        for (let n = 0; n < names.length && !hit; n++) {
            re.lastIndex = 0;
            if (re.test(el.getAttribute(names[n]) || '')) hit = true;
        }
        if (hit) found.push(el);
    }
    return found;
}";

        /// <summary>
        /// Pattern string passed to <see cref="FindAllByTextRegex"/>.
        /// </summary>
        /// <param name="regex">The .NET regular expression.</param>
        /// <returns>The pattern.</returns>
        internal static string Pattern(Regex regex)
        {
            ArgumentNullException.ThrowIfNull(regex);
            return regex.ToString();
        }

        /// <summary>
        /// JavaScript <c>RegExp</c> flags for <paramref name="regex"/>.
        /// </summary>
        /// <param name="regex">The .NET regular expression.</param>
        /// <returns>A flags string such as <c>i</c> or <c>im</c>.</returns>
        internal static string Flags(Regex regex)
        {
            ArgumentNullException.ThrowIfNull(regex);
            string flags = string.Empty;
            if ((regex.Options & RegexOptions.IgnoreCase) != 0)
            {
                flags += "i";
            }

            if ((regex.Options & RegexOptions.Multiline) != 0)
            {
                flags += "m";
            }

            if ((regex.Options & RegexOptions.Singleline) != 0)
            {
                flags += "s";
            }

            return flags;
        }
    }
}
