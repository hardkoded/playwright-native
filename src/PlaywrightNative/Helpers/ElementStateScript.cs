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
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Shared JS functions for checkbox / disabled / editable checks on element handles.
    /// </summary>
    internal static class ElementStateScript
    {
        /// <summary>
        /// JavaScript function <c>el => boolean</c>. Throws if the node is not a
        /// checkbox, radio, or checkable ARIA role.
        /// </summary>
        internal const string IsCheckedFunction = @"el => {
    const node = (function resolve(start) {
        let n = start;
        if (n && n.nodeName === 'LABEL' && n.control) {
            n = n.control;
        } else if (n && n.closest) {
            const type0 = String(n.type || '').toLowerCase();
            const role0 = String(n.getAttribute('role') || '').toLowerCase();
            const checkable0 = { checkbox: 1, menuitemcheckbox: 1, option: 1, radio: 1, switch: 1, menuitemradio: 1, treeitem: 1 };
            const selfOk = (n.nodeName === 'INPUT' && (type0 === 'checkbox' || type0 === 'radio')) || !!checkable0[role0];
            if (!selfOk) {
                const label = n.closest('label');
                if (label && label.control) {
                    n = label.control;
                }
            }
        }
        return n;
    })(el);
    if (!node) {
        throw new Error('Not a checkbox or radio button');
    }
    const type = String(node.type || '').toLowerCase();
    if (node.nodeName === 'INPUT' && (type === 'checkbox' || type === 'radio')) {
        return !!node.checked;
    }
    const role = String(node.getAttribute('role') || '').toLowerCase();
    const checkable = { checkbox: 1, menuitemcheckbox: 1, option: 1, radio: 1, switch: 1, menuitemradio: 1, treeitem: 1 };
    if (checkable[role]) {
        return String(node.getAttribute('aria-checked') || '').toLowerCase() === 'true';
    }
    throw new Error('Not a checkbox or radio button');
}";

        /// <summary>
        /// JavaScript function <c>el => boolean</c>. Native
        /// <c>&lt;input type="radio"&gt;</c> only — ARIA radios can be unchecked.
        /// Follows <c>&lt;label&gt;</c> to its control.
        /// </summary>
        internal const string IsNativeRadioFunction = @"el => {
    let node = el;
    if (node && node.nodeName === 'LABEL' && node.control) {
        node = node.control;
    }
    return !!node && node.nodeName === 'INPUT' && String(node.type || '').toLowerCase() === 'radio';
}";

        /// <summary>
        /// JavaScript function <c>(el, spec) => boolean</c> for expect
        /// <c>toBeChecked</c>. <c>spec.indeterminate === true</c> matches
        /// <c>el.indeterminate</c>; otherwise matches <c>el.checked</c> /
        /// <c>aria-checked</c> against <c>spec.checked</c> (default <c>true</c>).
        /// </summary>
        internal const string MatchesCheckedStateFunction = @"(el, spec) => {
    let node = el;
    if (node && node.nodeName === 'LABEL' && node.control) {
        node = node.control;
    }
    if (!node) {
        throw new Error('Not a checkbox or radio button');
    }
    const type = String(node.type || '').toLowerCase();
    const isInput = node.nodeName === 'INPUT' && (type === 'checkbox' || type === 'radio');
    const role = String(node.getAttribute('role') || '').toLowerCase();
    const checkable = { checkbox: 1, menuitemcheckbox: 1, option: 1, radio: 1, switch: 1, menuitemradio: 1, treeitem: 1 };
    if (!isInput && !checkable[role]) {
        throw new Error('Not a checkbox or radio button');
    }
    if (spec && spec.indeterminate === true) {
        return !!node.indeterminate;
    }
    const want = spec && spec.checked === false ? false : true;
    const actual = isInput
        ? !!node.checked
        : String(node.getAttribute('aria-checked') || '').toLowerCase() === 'true';
    return actual === want;
}";

        /// <summary>
        /// Official <c>previewNode</c>: boolean attrs, skip style, sort by
        /// length, auto-close void tags.
        /// </summary>
        internal const string PreviewNodeFunction = @"el => {
    if (!el) {
        return 'element';
    }
    if (el.nodeType === 3) {
        return '#text=' + String(el.nodeValue || '');
    }
    if (!el.tagName) {
        return '<' + String(el.nodeName || 'node').toLowerCase() + ' />';
    }
    const autoClose = { AREA:1, BASE:1, BR:1, COL:1, EMBED:1, HR:1, IMG:1, INPUT:1, KEYGEN:1, LINK:1, META:1, PARAM:1, SOURCE:1, TRACK:1, WBR:1 };
    const booleanAttr = { checked:1, disabled:1, hidden:1, readonly:1, required:1, selected:1, multiple:1 };
    const attrs = [];
    const list = el.attributes || [];
    for (let i = 0; i < list.length; i++) {
        const name = list[i].name;
        const value = list[i].value;
        if (name === 'style') {
            continue;
        }
        if (!value && booleanAttr[name]) {
            attrs.push(' ' + name);
        } else {
            attrs.push(' ' + name + '=""' + value + '""');
        }
    }
    attrs.sort((a, b) => a.length - b.length);
    const attrText = attrs.join('');
    const tag = String(el.tagName).toLowerCase();
    if (autoClose[el.tagName]) {
        return '<' + tag + attrText + '/>';
    }
    const kids = el.childNodes || [];
    let onlyText = kids.length <= 5;
    for (let i = 0; i < kids.length; i++) {
        onlyText = onlyText && kids[i].nodeType === 3;
    }
    const text = onlyText ? String(el.textContent || '') : (kids.length ? '\u2026' : '');
    return '<' + tag + attrText + '>' + text + '</' + tag + '>';
}";

        /// <summary>
        /// Official <c>_activelyFocused</c>: the node is the active element of
        /// its root (document or shadow) and the document has focus.
        /// </summary>
        internal const string IsFocusedFunction = @"el => {
    if (!el || !el.getRootNode) {
        return false;
    }
    const root = el.getRootNode();
    const active = root && root.activeElement;
    return active === el && !!el.ownerDocument && !!el.ownerDocument.hasFocus();
}";

        /// <summary>
        /// Official received label for <c>to.be.checked</c>.
        /// </summary>
        internal const string CheckedReceivedFunction = @"el => {
    let node = el;
    if (node && node.nodeName === 'LABEL' && node.control) {
        node = node.control;
    }
    if (!node) {
        return 'unchecked';
    }
    if (node.indeterminate) {
        return 'indeterminate';
    }
    const type = String(node.type || '').toLowerCase();
    const isInput = node.nodeName === 'INPUT' && (type === 'checkbox' || type === 'radio');
    const role = String(node.getAttribute('role') || '').toLowerCase();
    const checkable = { checkbox: 1, menuitemcheckbox: 1, option: 1, radio: 1, switch: 1, menuitemradio: 1, treeitem: 1 };
    if (!isInput && !checkable[role]) {
        return 'unchecked';
    }
    const actual = isInput
        ? !!node.checked
        : String(node.getAttribute('aria-checked') || '').toLowerCase() === 'true';
    return actual ? 'checked' : 'unchecked';
}";

        /// <summary>
        /// JavaScript function <c>el => boolean</c>.
        /// </summary>
        /// <summary>
        /// Follows a <c>&lt;label&gt;</c> to its associated control.
        /// </summary>
        internal const string RetargetFollowLabelFunction = @"el => (el && el.nodeName === 'LABEL' && el.control) ? el.control : el";

        /// <summary>
        /// JavaScript function <c>el => boolean</c>. Follows a label to its
        /// control, then treats native <c>disabled</c> (including
        /// <c>&lt;fieldset&gt;</c> / button ancestors) and
        /// <c>aria-disabled</c> like official Playwright.
        /// </summary>
        internal const string IsEnabledFunction = @"el => {
    if (el && el.nodeName === 'LABEL' && el.control) {
        el = el.control;
    }
    const kAriaDisabledRoles = { application: 1, button: 1, composite: 1, gridcell: 1, group: 1, input: 1, link: 1, menuitem: 1, scrollbar: 1, separator: 1, tab: 1, checkbox: 1, columnheader: 1, combobox: 1, grid: 1, listbox: 1, menu: 1, menubar: 1, menuitemcheckbox: 1, menuitemradio: 1, option: 1, radio: 1, radiogroup: 1, row: 1, rowheader: 1, searchbox: 1, select: 1, slider: 1, spinbutton: 1, switch: 1, tablist: 1, textbox: 1, toolbar: 1, tree: 1, treegrid: 1, treeitem: 1 };
    function implicitRole(node) {
        const tag = node && node.nodeName;
        if (tag === 'BUTTON') return 'button';
        if (tag === 'H1' || tag === 'H2' || tag === 'H3' || tag === 'H4' || tag === 'H5' || tag === 'H6') return 'heading';
        if (tag === 'A' && node.hasAttribute('href')) return 'link';
        if (tag === 'SELECT') return node.hasAttribute('multiple') || Number(node.size) > 1 ? 'listbox' : 'combobox';
        if (tag === 'TEXTAREA') return 'textbox';
        if (tag === 'INPUT') {
            const t = String(node.type || '').toLowerCase();
            if (t === 'checkbox') return 'checkbox';
            if (t === 'radio') return 'radio';
            if (t === 'button' || t === 'submit' || t === 'reset' || t === 'image' || t === 'file') return 'button';
            if (t === 'hidden') return '';
            return 'textbox';
        }
        return '';
    }
    function getAriaRole(node) {
        const roles = String(node.getAttribute('role') || '').split(' ');
        for (let i = 0; i < roles.length; i++) {
            const r = roles[i].trim();
            if (r) return r;
        }
        return implicitRole(node);
    }
    function belongsToDisabledOptGroup(node) {
        return node && node.nodeName === 'OPTION' && !!node.closest('OPTGROUP[DISABLED]');
    }
    function belongsToDisabledFieldSet(node) {
        const fieldSet = node && node.closest && node.closest('FIELDSET[DISABLED]');
        if (!fieldSet) return false;
        const legend = fieldSet.querySelector(':scope > LEGEND');
        return !legend || !legend.contains(node);
    }
    function isNativelyDisabled(node) {
        const tag = node && node.nodeName;
        const isNative = tag === 'BUTTON' || tag === 'INPUT' || tag === 'SELECT' || tag === 'TEXTAREA' || tag === 'OPTION' || tag === 'OPTGROUP';
        return isNative && (node.hasAttribute('disabled') || belongsToDisabledOptGroup(node) || belongsToDisabledFieldSet(node));
    }
    function parentOrShadowHost(node) {
        if (!node) return null;
        if (node.parentElement) return node.parentElement;
        if (node.parentNode && node.parentNode.nodeType === 11 && node.parentNode.host) return node.parentNode.host;
        return null;
    }
    function hasAriaDisabledInChain(node) {
        if (!node) return false;
        const value = String(node.getAttribute('aria-disabled') || '').toLowerCase();
        if (value === 'true') return true;
        if (value === 'false') return false;
        return hasAriaDisabledInChain(parentOrShadowHost(node));
    }
    function hasExplicitAriaDisabled(node) {
        if (!node || !kAriaDisabledRoles[getAriaRole(node) || '']) return false;
        return hasAriaDisabledInChain(node);
    }
    return !isNativelyDisabled(el) && !hasExplicitAriaDisabled(el);
}";

        /// <summary>
        /// JavaScript function <c>el => boolean</c>. Inverse of
        /// <see cref="IsEnabledFunction"/>.
        /// </summary>
        internal const string IsDisabledFunction = @"el => {
    const isEnabled = " + IsEnabledFunction + @";
    return !isEnabled(el);
}";

        /// <summary>
        /// JavaScript function <c>el => boolean</c>.
        /// </summary>
        internal const string IsEditableFunction = @"el => {
    if (el && el.nodeName === 'LABEL' && el.control) {
        el = el.control;
    }
    const tag = el.nodeName;
    if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') {
        if (el.readOnly || el.disabled) {
            return false;
        }
        return true;
    }
    if (el.isContentEditable) {
        return true;
    }
    const role = String(el.getAttribute('role') || '').toLowerCase();
    const readonlyRoles = {
        checkbox: 1, combobox: 1, grid: 1, gridcell: 1, listbox: 1, radiogroup: 1,
        slider: 1, spinbutton: 1, switch: 1, textbox: 1, treegrid: 1, searchbox: 1,
        menuitemcheckbox: 1, menuitemradio: 1, radio: 1, scrollbar: 1, option: 1, treeitem: 1
    };
    if (readonlyRoles[role]) {
        if (String(el.getAttribute('aria-readonly') || '').toLowerCase() === 'true' || el.disabled) {
            return false;
        }
        return true;
    }
    throw new Error('Element is not an <input>, <textarea>, <select> or [contenteditable] and does not have a role allowing [aria-readonly]');
}";

        /// <summary>
        /// JavaScript function <c>(el, value) => boolean</c> that fills input/textarea/contenteditable.
        /// Mirrors official injected <c>fill</c>: unsupported types, number text, and
        /// color/date/range/month/week/time/datetime-local malformed values throw the
        /// same messages. Value-set types dispatch composed <c>input</c> and non-composed
        /// <c>change</c>. Contenteditable uses select + <c>insertText</c> so newlines and
        /// <c>beforeinput</c> handlers match upstream.
        /// </summary>
        internal const string FillFunction = @"(el, value, preventScroll) => {
    if (el && el.nodeName === 'LABEL' && el.control) {
        el = el.control;
    }
    function stackless(message) {
        const err = new Error(message);
        err.stack = '';
        return err;
    }
    const focusOptions = preventScroll === true ? { preventScroll: true } : undefined;
    if (!el || !el.isConnected) {
        throw stackless('Node is detached from document');
    }
    const tag = el.nodeName.toLowerCase();
    if (tag === 'input') {
        const type = String(el.type || '').toLowerCase();
        const setValue = { color: 1, date: 1, time: 1, 'datetime-local': 1, month: 1, range: 1, week: 1 };
        const typeInto = { '': 1, email: 1, number: 1, password: 1, search: 1, tel: 1, text: 1, url: 1 };
        if (!typeInto[type] && !setValue[type]) {
            throw stackless('Input of type ""' + type + '"" cannot be filled');
        }
        value = value == null ? '' : String(value);
        if (type === 'number') {
            value = value.trim();
            if (isNaN(Number(value))) {
                throw stackless('Cannot type text into input[type=number]');
            }
        }
        if (type === 'color') {
            value = value.toLowerCase();
        }
        if (setValue[type]) {
            value = value.trim();
            el.focus(focusOptions);
            el.value = value;
            if (el.value !== value) {
                throw stackless('Malformed value');
            }
            el.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
            el.dispatchEvent(new Event('change', { bubbles: true }));
            return true;
        }
        el.focus(focusOptions);
        el.value = value;
        el.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
        el.dispatchEvent(new Event('change', { bubbles: true }));
        return true;
    }
    if (tag === 'textarea') {
        el.focus(focusOptions);
        el.value = value == null ? '' : String(value);
        el.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
        el.dispatchEvent(new Event('change', { bubbles: true }));
        return true;
    }
    if (el.isContentEditable) {
        el.focus(focusOptions);
        const doc = el.ownerDocument;
        const sel = doc.defaultView && doc.defaultView.getSelection();
        if (sel) {
            const range = doc.createRange();
            range.selectNodeContents(el);
            sel.removeAllRanges();
            sel.addRange(range);
        }
        value = value == null ? '' : String(value);
        let inserted = false;
        try {
            inserted = typeof doc.execCommand === 'function' && doc.execCommand('insertText', false, value);
        } catch (e) {
            inserted = false;
        }
        if (!inserted) {
            el.textContent = value;
            el.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
        }
        return true;
    }
    throw stackless('Element is not an <input>, <textarea> or [contenteditable] element');
}";

        /// <summary>
        /// JavaScript function <c>el => boolean</c> that reports whether the node is a
        /// focusable area (rendered and <c>tabIndex &gt;= 0</c> or contenteditable).
        /// Official <c>page.focus</c> does not wait for a non-empty box, so a
        /// <c>tabIndex=0</c> element with zero height is still focusable.
        /// </summary>
        internal const string IsFocusableAreaFunction = @"el => {
    if (!el || !el.isConnected) return false;
    if (el.nodeType !== Node.ELEMENT_NODE) return false;
    const style = window.getComputedStyle(el);
    if (!style || style.display === 'none') return false;
    if (el.disabled) return false;
    if (el.hidden) return false;
    if (typeof el.tabIndex === 'number' && el.tabIndex >= 0) return true;
    if (el.isContentEditable) return true;
    return false;
}";

        /// <summary>
        /// JavaScript function <c>(el, preventScroll) => boolean</c> that focuses the node.
        /// Mirrors upstream <c>injected.focusNode</c>: refuse detached/non-element nodes
        /// and call <c>focus()</c> twice (Firefox). Non-focusable nodes are a no-op, so
        /// the previously focused element keeps focus.
        /// </summary>
        internal const string FocusFunction = @"(el, preventScroll) => {
    if (!el.isConnected) throw new Error('Node is detached from document');
    if (el.nodeType !== Node.ELEMENT_NODE) throw new Error('Node is not an element');
    const focusOptions = preventScroll === true ? { preventScroll: true } : undefined;
    el.focus(focusOptions);
    el.focus(focusOptions);
    return true;
}";

        /// <summary>
        /// JavaScript function <c>(el, preventScroll) => boolean</c> used by
        /// <c>ElementHandle.type</c> and <c>ElementHandle.press</c>. Mirrors upstream
        /// <c>injected.focusNode</c> with <c>resetSelectionIfNotFocused</c>: if the
        /// input was not already focused, move the caret to the start so existing
        /// text is not replaced.
        /// </summary>
        internal const string FocusForTypeFunction = @"(el, preventScroll) => {
    if (!el.isConnected) throw new Error('Node is detached from document');
    if (el.nodeType !== Node.ELEMENT_NODE) throw new Error('Node is not an element');
    const root = el.getRootNode ? el.getRootNode() : (el.ownerDocument || document);
    const activeElement = root && root.activeElement;
    const wasFocused = activeElement === el && !!el.ownerDocument && el.ownerDocument.hasFocus();
    if (el.isContentEditable && !wasFocused && activeElement && activeElement.blur) {
        activeElement.blur();
    }
    const focusOptions = preventScroll === true ? { preventScroll: true } : undefined;
    el.focus(focusOptions);
    el.focus(focusOptions);
    if (!wasFocused && el.nodeName.toLowerCase() === 'input') {
        try {
            el.setSelectionRange(0, 0);
        } catch (e) {
        }
    }
    return true;
}";

        /// <summary>
        /// JavaScript function <c>el => string</c> that snapshots ancestor
        /// <c>scrollTop</c>/<c>scrollLeft</c> so a later restore can undo caret scroll.
        /// </summary>
        internal const string CaptureAncestorScrollsFunction = @"el => {
    const out = [];
    let n = el;
    while (n) {
        out.push({ t: n.scrollTop || 0, l: n.scrollLeft || 0 });
        n = n.parentNode;
    }
    const se = el.ownerDocument && el.ownerDocument.scrollingElement;
    if (se) {
        out.push({ t: se.scrollTop || 0, l: se.scrollLeft || 0 });
    }
    return JSON.stringify(out);
}";

        /// <summary>
        /// JavaScript function <c>(el, json) => boolean</c> that restores the snapshot
        /// from <see cref="CaptureAncestorScrollsFunction"/>.
        /// </summary>
        internal const string RestoreAncestorScrollsFunction = @"(el, json) => {
    const saved = JSON.parse(json);
    let i = 0;
    let n = el;
    while (n && i < saved.length) {
        if (saved[i]) {
            n.scrollTop = saved[i].t;
            n.scrollLeft = saved[i].l;
        }
        n = n.parentNode;
        i++;
    }
    const se = el.ownerDocument && el.ownerDocument.scrollingElement;
    if (se && i < saved.length && saved[i]) {
        se.scrollTop = saved[i].t;
        se.scrollLeft = saved[i].l;
    }
    return true;
}";

        /// <summary>
        /// JavaScript function <c>el => boolean</c> that clicks the node.
        /// </summary>
        internal const string ClickFunction = @"el => { el.click(); return true; }";

        /// <summary>
        /// JavaScript function <c>el => boolean</c> that checks a checkbox/radio if needed.
        /// </summary>
        internal const string CheckFunction = @"el => { if (el && el.nodeName === 'LABEL' && el.control) el = el.control; if (!el.checked) el.click(); return true; }";

        /// <summary>
        /// JavaScript function <c>el => boolean</c> that unchecks a checkbox if needed.
        /// </summary>
        internal const string UncheckFunction = @"el => { if (el && el.nodeName === 'LABEL' && el.control) el = el.control; if (el.checked) el.click(); return true; }";

        /// <summary>
        /// JavaScript function <c>el => boolean</c> that dispatches a dblclick.
        /// </summary>
        internal const string DblClickFunction = @"el => { el.dispatchEvent(new MouseEvent('dblclick', { bubbles: true, cancelable: true, view: window })); return true; }";

        /// <summary>
        /// JavaScript function <c>el => boolean</c> that dispatches mouseover/mouseenter.
        /// </summary>
        internal const string HoverFunction = @"el => {
    el.dispatchEvent(new MouseEvent('mouseover', { bubbles: true, cancelable: true, view: window }));
    el.dispatchEvent(new MouseEvent('mouseenter', { bubbles: true, cancelable: true, view: window }));
    return true;
}";

        /// <summary>
        /// JavaScript function <c>(el, json) => string</c>. <paramref name="json"/> is a
        /// JSON array of <c>{value,label,index,valueOrLabel}</c> descriptors.
        /// Returns a JSON object <c>{status,values,reason,message}</c> so the C#
        /// helper can wait for missing or disabled options.
        /// </summary>
        internal const string SelectOptionFromJsonFunction = @"(el, json) => {
    if (el && el.nodeName === 'LABEL' && el.control) {
        el = el.control;
    }
    function normalize(text) {
        return String(text == null ? '' : text).replace(/\u200b/g, '').replace(/\s+/g, ' ').trim();
    }
    function isOptionDisabled(option) {
        if (option.hasAttribute('disabled')) {
            return true;
        }
        const group = option.closest ? option.closest('optgroup') : option.parentNode;
        if (group && String(group.nodeName).toLowerCase() === 'optgroup' && group.hasAttribute('disabled')) {
            return true;
        }
        return false;
    }
    function matches(option, desc) {
        if (!desc) {
            return false;
        }
        if (typeof desc === 'string') {
            return option.value === desc || normalize(option.label) === normalize(desc);
        }
        if (desc.valueOrLabel != null) {
            return option.value === desc.valueOrLabel || normalize(option.label) === normalize(desc.valueOrLabel);
        }
        const valueMatches = desc.value == null || option.value === desc.value;
        const labelMatches = desc.label == null || normalize(option.label) === normalize(desc.label);
        const indexMatches = desc.index == null || option.index === Number(desc.index);
        return valueMatches && labelMatches && indexMatches;
    }
    function fire(node, type, bubbles, composed) {
        let event;
        try {
            event = new Event(type, { bubbles: bubbles, composed: composed });
        } catch (e) {
            event = node.ownerDocument.createEvent('Event');
            event.initEvent(type, bubbles, true);
            try {
                Object.defineProperty(event, 'composed', { configurable: true, value: composed });
            } catch (e2) {
            }
        }
        node.dispatchEvent(event);
    }
    function pack(status, extra) {
        const payload = { status: status };
        if (extra) {
            if (extra.values) {
                payload.values = extra.values;
            }
            if (extra.reason) {
                payload.reason = extra.reason;
            }
            if (extra.message) {
                payload.message = extra.message;
            }
        }
        return JSON.stringify(payload);
    }

    let node = el;
    if (node && !node.isConnected) {
        const doc = node.ownerDocument || document;
        const replacement = doc.querySelector('select');
        if (replacement) {
            node = replacement;
        }
    }
    if (!node || String(node.nodeName).toLowerCase() !== 'select') {
        return pack('error', { message: 'Element is not a <select> element' });
    }
    if (node.disabled) {
        return pack('wait', { reason: 'enabled' });
    }

    let descriptors = [];
    try {
        descriptors = JSON.parse(json);
    } catch (e) {
        descriptors = [];
    }
    const list = Array.isArray(descriptors) ? descriptors : [];
    const options = Array.from(node.options);

    if (list.length === 0) {
        node.selectedIndex = -1;
        for (let i = 0; i < options.length; i++) {
            options[i].selected = false;
        }
        fire(node, 'input', true, true);
        fire(node, 'change', true, false);
        return pack('ok', { values: [] });
    }

    const selected = [];
    if (node.multiple) {
        for (let d = 0; d < list.length; d++) {
            const found = [];
            for (let i = 0; i < options.length; i++) {
                if (matches(options[i], list[d])) {
                    found.push(options[i]);
                }
            }
            if (found.length === 0) {
                return pack('wait', { reason: 'missing' });
            }
            const enabled = [];
            for (let i = 0; i < found.length; i++) {
                if (!isOptionDisabled(found[i])) {
                    enabled.push(found[i]);
                }
            }
            if (enabled.length === 0) {
                return pack('wait', { reason: 'notenabled' });
            }
            for (let i = 0; i < enabled.length; i++) {
                if (selected.indexOf(enabled[i]) < 0) {
                    selected.push(enabled[i]);
                }
            }
        }
    } else {
        let matched = null;
        let sawDisabled = false;
        for (let d = 0; d < list.length; d++) {
            const found = [];
            for (let i = 0; i < options.length; i++) {
                if (matches(options[i], list[d])) {
                    found.push(options[i]);
                }
            }
            if (found.length === 0) {
                continue;
            }
            const enabled = [];
            for (let i = 0; i < found.length; i++) {
                if (!isOptionDisabled(found[i])) {
                    enabled.push(found[i]);
                }
            }
            if (enabled.length === 0) {
                sawDisabled = true;
                continue;
            }
            matched = enabled[0];
            break;
        }
        if (!matched) {
            return pack('wait', { reason: sawDisabled ? 'notenabled' : 'missing' });
        }
        selected.push(matched);
    }

    for (let i = 0; i < options.length; i++) {
        options[i].selected = false;
    }
    const values = [];
    for (let i = 0; i < selected.length; i++) {
        selected[i].selected = true;
        values.push(selected[i].value);
    }
    fire(node, 'input', true, true);
    fire(node, 'change', true, false);
    return pack('ok', { values: values });
}";

        /// <summary>
        /// JavaScript function <c>(node, fileData) => boolean</c> that assigns
        /// <c>input.files</c> from an array of
        /// <c>{name, mimeType, buffer, lastModified, webkitRelativePath}</c>
        /// (buffer is base64).
        /// </summary>
        internal const string AssignInputFilesFromDataFunction = @"(node, fileData) => {
    if (node.tagName !== 'INPUT' || node.type !== 'file') {
        throw new Error('Element is not an <input type=""file"">');
    }
    const files = (fileData || []).map(f => {
        const binary = atob(f.buffer || '');
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
        }
        const opts = { type: f.mimeType || 'application/octet-stream' };
        if (typeof f.lastModified === 'number') {
            opts.lastModified = f.lastModified;
        }
        const file = new File([bytes], f.name, opts);
        if (f.webkitRelativePath) {
            Object.defineProperty(file, 'webkitRelativePath', {
                configurable: true,
                enumerable: true,
                writable: false,
                value: f.webkitRelativePath,
            });
        }
        return file;
    });
    const dt = new DataTransfer();
    for (const file of files) {
        dt.items.add(file);
    }
    node.files = dt.files;
    for (let i = 0; i < files.length; i++) {
        const relative = fileData[i] && fileData[i].webkitRelativePath;
        if (relative && node.files[i]) {
            Object.defineProperty(node.files[i], 'webkitRelativePath', {
                configurable: true,
                enumerable: true,
                writable: false,
                value: relative,
            });
        }
    }
    node.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
    node.dispatchEvent(new Event('change', { bubbles: true }));
    return true;
}";

        /// <summary>
        /// JavaScript function <c>(el, json) => boolean</c> that assigns <c>input.files</c>
        /// from a JSON array of <c>{name, mimeType, buffer, lastModified, webkitRelativePath}</c>
        /// (buffer is base64).
        /// </summary>
        internal const string SetInputFilesFromJsonFunction = @"(el, json) => {
    const fileData = JSON.parse(json);
    const assign = " + AssignInputFilesFromDataFunction + @";
    return assign(el, fileData);
}";

        /// <summary>
        /// JavaScript function <c>(el, json) => boolean</c> that dispatches
        /// <c>dragenter</c>, <c>dragover</c>, and <c>drop</c> with a
        /// <c>DataTransfer</c> built from files and MIME data.
        /// </summary>
        internal const string DropPayloadFunction = @"(el, json) => {
    if (!el || !el.isConnected) {
        throw new Error('Node is detached from document');
    }
    const spec = JSON.parse(json);
    const dt = new DataTransfer();
    const files = spec.files || [];
    for (let i = 0; i < files.length; i++) {
        const f = files[i];
        const binary = atob(f.buffer || '');
        const bytes = new Uint8Array(binary.length);
        for (let j = 0; j < binary.length; j++) {
            bytes[j] = binary.charCodeAt(j);
        }
        dt.items.add(new File([bytes], f.name, { type: f.mimeType || 'application/octet-stream' }));
    }
    const data = spec.data || [];
    for (let i = 0; i < data.length; i++) {
        dt.setData(data[i].type, data[i].value);
    }
    const types = ['dragenter', 'dragover', 'drop'];
    for (let i = 0; i < types.length; i++) {
        el.dispatchEvent(new DragEvent(types[i], { bubbles: true, cancelable: true, dataTransfer: dt }));
    }
    return true;
}";

        /// <summary>
        /// JavaScript function <c>el => boolean</c> that scrolls the element into view.
        /// </summary>
        internal const string ScrollIntoViewIfNeededFunction = @"el => {
    if (!el || !el.isConnected) {
        throw new Error('Node is detached from document');
    }
    if (typeof el.scrollIntoViewIfNeeded === 'function') {
        el.scrollIntoViewIfNeeded(true);
    } else {
        el.scrollIntoView({ block: 'center', inline: 'center' });
    }
    return true;
}";

        /// <summary>
        /// JavaScript function <c>el => boolean</c> that selects the element's text.
        /// </summary>
        internal const string SelectTextFunction = @"(el, preventScroll) => {
    if (el && el.nodeName === 'LABEL' && el.control) {
        el = el.control;
    }
    if (!el || !el.isConnected) {
        throw new Error('Node is detached from document');
    }
    const focusOptions = preventScroll === true ? { preventScroll: true } : undefined;
    const tag = el.nodeName.toLowerCase();
    if (tag === 'input') {
        el.select();
        el.focus(focusOptions);
        return true;
    }
    if (tag === 'textarea') {
        el.selectionStart = 0;
        el.selectionEnd = el.value.length;
        el.focus(focusOptions);
        return true;
    }
    el.focus(focusOptions);
    const range = el.ownerDocument.createRange();
    range.selectNodeContents(el);
    const selection = el.ownerDocument.defaultView && el.ownerDocument.defaultView.getSelection();
    if (selection) {
        selection.removeAllRanges();
        selection.addRange(range);
    }
    return true;
}";

        /// <summary>
        /// JavaScript function <c>(el, json) => boolean</c> that marks the element
        /// (<c>data-pw-highlight</c> + outline) and draws official
        /// <c>x-pw-highlight</c> / <c>x-pw-tooltip</c> overlays. <c>json</c> is
        /// <c>{ tooltip, style, id }</c>.
        /// </summary>
        internal const string HighlightFunction = @"(el, json) => {
    if (!el || !el.isConnected) {
        throw new Error('Node is detached from document');
    }
    let payload = {};
    try {
        payload = JSON.parse(String(json || '{}')) || {};
    } catch (e) {
        payload = {};
    }
    const tooltipText = payload.tooltip != null ? String(payload.tooltip) : '';
    const style = payload.style != null ? String(payload.style) : '';
    const id = payload.id != null ? String(payload.id) : tooltipText;
    const existing = document.querySelectorAll('x-pw-highlight, x-pw-tooltip');
    for (let i = 0; i < existing.length; i++) {
        if (existing[i].getAttribute('data-pw-hl') === id) {
            existing[i].remove();
        }
    }
    el.setAttribute('data-pw-highlight', 'true');
    el.setAttribute('data-pw-hl', id);
    if (style) {
        el.style.cssText += ';' + style;
    } else {
        el.style.outline = '2px solid rgb(255, 0, 0)';
    }
    const box = el.getBoundingClientRect();
    const highlight = document.createElement('x-pw-highlight');
    highlight.setAttribute('data-pw-hl', id);
    highlight.style.display = 'block';
    highlight.style.position = 'fixed';
    highlight.style.left = box.x + 'px';
    highlight.style.top = box.y + 'px';
    highlight.style.width = box.width + 'px';
    highlight.style.height = box.height + 'px';
    highlight.style.backgroundColor = '#6fa8dc7f';
    highlight.style.pointerEvents = 'none';
    highlight.style.zIndex = '2147483646';
    if (style) {
        highlight.style.cssText += ';' + style;
    }
    document.documentElement.appendChild(highlight);
    const tooltip = document.createElement('x-pw-tooltip');
    tooltip.setAttribute('data-pw-hl', id);
    tooltip.textContent = tooltipText;
    tooltip.style.display = 'block';
    tooltip.style.position = 'fixed';
    tooltip.style.left = Math.max(5, box.left) + 'px';
    tooltip.style.top = (box.bottom + 5) + 'px';
    tooltip.style.pointerEvents = 'none';
    tooltip.style.zIndex = '2147483647';
    document.documentElement.appendChild(tooltip);
    return true;
}";

        /// <summary>
        /// JavaScript function <c>id => undefined</c> that removes one locator
        /// highlight by overlay / element id.
        /// </summary>
        internal const string HideHighlightByIdFunction = @"id => {
    const key = String(id || '');
    const nodes = document.querySelectorAll('x-pw-highlight, x-pw-tooltip, [data-pw-hl]');
    for (let i = 0; i < nodes.length; i++) {
        if (nodes[i].getAttribute('data-pw-hl') !== key) {
            continue;
        }
        const tag = String(nodes[i].tagName || '').toUpperCase();
        if (tag === 'X-PW-HIGHLIGHT' || tag === 'X-PW-TOOLTIP') {
            nodes[i].remove();
        } else {
            nodes[i].removeAttribute('data-pw-highlight');
            nodes[i].removeAttribute('data-pw-hl');
            nodes[i].style.outline = '';
        }
    }
}";

        /// <summary>
        /// JavaScript function <c>el => boolean</c> that removes a locator highlight.
        /// </summary>
        internal const string HideHighlightFunction = @"el => {
    const nodes = document.querySelectorAll('x-pw-highlight, x-pw-tooltip, [data-pw-highlight]');
    for (let i = 0; i < nodes.length; i++) {
        if (nodes[i].hasAttribute && nodes[i].hasAttribute('data-pw-highlight')) {
            nodes[i].removeAttribute('data-pw-highlight');
            nodes[i].removeAttribute('data-pw-hl');
            nodes[i].style.outline = '';
        } else {
            nodes[i].remove();
        }
    }
    return true;
}";

        /// <summary>
        /// JavaScript function that clears every locator highlight on the page.
        /// </summary>
        internal const string HideAllHighlightsFunction = @"() => {
    const nodes = document.querySelectorAll('x-pw-highlight, x-pw-tooltip, [data-pw-highlight]');
    for (let i = 0; i < nodes.length; i++) {
        if (nodes[i].hasAttribute && nodes[i].hasAttribute('data-pw-highlight')) {
            nodes[i].removeAttribute('data-pw-highlight');
            nodes[i].removeAttribute('data-pw-hl');
            nodes[i].style.outline = '';
        } else {
            nodes[i].remove();
        }
    }
}";

        /// <summary>
        /// JavaScript function <c>(el, testIdAttr) => string[]</c> used by
        /// <see cref="ILocator.NormalizeAsync"/>. Returns test id, role, accessible
        /// name, placeholder, alt, title, and id.
        /// </summary>
        internal const string NormalizeHintFunction = @"(el, testIdAttr) => {
    function implicitRole(node) {
        const tag = String(node.tagName || '').toLowerCase();
        const type = String(node.getAttribute('type') || '').toLowerCase();
        if (tag === 'button') {
            return 'button';
        }
        if (tag === 'a' && node.hasAttribute('href')) {
            return 'link';
        }
        if (tag === 'img') {
            return 'img';
        }
        if (tag === 'textarea') {
            return 'textbox';
        }
        if (tag === 'select') {
            return 'combobox';
        }
        if (tag === 'h1' || tag === 'h2' || tag === 'h3' || tag === 'h4' || tag === 'h5' || tag === 'h6') {
            return 'heading';
        }
        if (tag === 'input') {
            if (type === 'checkbox') {
                return 'checkbox';
            }
            if (type === 'radio') {
                return 'radio';
            }
            if (type === 'submit' || type === 'button' || type === 'reset') {
                return 'button';
            }
            return 'textbox';
        }
        return '';
    }

    const testId = el.getAttribute(testIdAttr) || '';
    const role = el.getAttribute('role') || implicitRole(el);
    let name = String(el.getAttribute('aria-label') || '').trim();
    if (!name && (role === 'button' || role === 'link' || role === 'heading')) {
        name = String(el.innerText || el.textContent || '').trim();
    }
    return [
        testId,
        role,
        name,
        el.getAttribute('placeholder') || '',
        el.getAttribute('alt') || '',
        el.getAttribute('title') || '',
        el.id || ''
    ];
}";

        /// <summary>
        /// JavaScript function <c>el => boolean</c> that blurs the element.
        /// </summary>
        internal const string BlurFunction = @"el => {
    if (!el || !el.isConnected) {
        throw new Error('Node is detached from document');
    }
    el.blur();
    return true;
}";

        /// <summary>
        /// JavaScript function <c>el => string</c> that reads <c>value</c>
        /// from input, textarea, or select elements.
        /// </summary>
        internal const string InputValueFunction = @"el => {
    if (el && el.nodeName === 'LABEL' && el.control) {
        el = el.control;
    }
    const tag = el && el.nodeName;
    if (tag !== 'INPUT' && tag !== 'TEXTAREA' && tag !== 'SELECT') {
        throw new Error('Node is not an <input>, <textarea> or <select> element');
    }
    return el.value == null ? '' : String(el.value);
}";

        /// <summary>
        /// JavaScript function <c>el => string</c>. Throws when the node is not
        /// an <c>HTMLElement</c> (for example an SVG element).
        /// </summary>
        internal const string InnerTextFunction = @"el => {
    const view = el && el.ownerDocument && el.ownerDocument.defaultView;
    if (!view || !(el instanceof view.HTMLElement)) {
        throw new Error('Node is not an HTMLElement');
    }
    return el.innerText;
}";

        /// <summary>
        /// Expression using an in-scope <c>el</c> for atomic selector reads.
        /// Throws when the node is not an <c>HTMLElement</c>.
        /// </summary>
        internal const string InnerTextValueExpression = @"(() => {
    const view = el && el.ownerDocument && el.ownerDocument.defaultView;
    if (!view || !(el instanceof view.HTMLElement)) {
        throw new Error('Node is not an HTMLElement');
    }
    return el.innerText;
})()";

        /// <summary>
        /// JavaScript function <c>(el, spec) => boolean</c> that compares
        /// <c>el[spec.name]</c> to <c>spec.expected</c> via JSON.
        /// </summary>
        /// <summary>
        /// JavaScript function <c>el => string</c> matching official
        /// <c>to.have.accessible.name</c>: labelledby, aria-label, labels,
        /// img alt, input value, then text content, with whitespace flattened.
        /// </summary>
        internal const string AccessibleNameFunction = @"el => {
    if (!el) {
        return '';
    }
    const normalize = (text) => String(text || '').replace(/[\u200b\u00ad]/g, '').replace(/\s+/g, ' ').trim();
    const labelled = el.getAttribute('aria-labelledby');
    if (labelled) {
        const ids = String(labelled).split(/\s+/);
        const parts = [];
        for (let i = 0; i < ids.length; i++) {
            const id = ids[i];
            if (!id) {
                continue;
            }
            const ref = el.ownerDocument.getElementById(id);
            if (ref) {
                parts.push(String(ref.textContent || ''));
            }
        }
        const joined = parts.join(' ');
        if (normalize(joined)) {
            return normalize(joined);
        }
    }
    const ariaLabel = el.getAttribute('aria-label');
    if (ariaLabel && String(ariaLabel).trim()) {
        return normalize(ariaLabel);
    }
    if (el.labels && el.labels.length) {
        let t = '';
        for (let i = 0; i < el.labels.length; i++) {
            t += String(el.labels[i].textContent || '') + ' ';
        }
        if (normalize(t)) {
            return normalize(t);
        }
    }
    const tag = String(el.tagName || '').toUpperCase();
    if (tag === 'IMG') {
        return normalize(el.getAttribute('alt') || '');
    }
    if (tag === 'INPUT') {
        const type = String(el.type || '').toLowerCase();
        if (type === 'submit' || type === 'button' || type === 'reset') {
            return normalize(el.value || '');
        }
    }
    return normalize(el.textContent || '');
}";

        /// <summary>
        /// JavaScript function <c>el => string</c> matching official
        /// <c>to.have.accessible.description</c>: <c>aria-describedby</c>,
        /// then <c>aria-description</c>, then <c>title</c>.
        /// </summary>
        internal const string AccessibleDescriptionFunction = @"el => {
    if (!el) {
        return '';
    }
    const normalize = (text) => String(text || '').replace(/[\u200b\u00ad]/g, '').replace(/\s+/g, ' ').trim();
    if (el.hasAttribute('aria-describedby')) {
        const ids = String(el.getAttribute('aria-describedby') || '').split(/\s+/);
        const parts = [];
        for (let i = 0; i < ids.length; i++) {
            const id = ids[i];
            if (!id) {
                continue;
            }
            const ref = el.ownerDocument.getElementById(id);
            if (ref) {
                parts.push(String(ref.textContent || ''));
            }
        }
        return normalize(parts.join(' '));
    }
    if (el.hasAttribute('aria-description')) {
        return normalize(el.getAttribute('aria-description') || '');
    }
    return normalize(el.getAttribute('title') || '');
}";

        /// <summary>
        /// JavaScript function <c>el => string</c> matching official
        /// accessible error text from <c>aria-errormessage</c> when the
        /// control is ARIA-invalid or natively invalid.
        /// </summary>
        internal const string AccessibleErrorMessageFunction = @"el => {
    if (!el) {
        return '';
    }
    const ariaInvalid = el.getAttribute('aria-invalid');
    let invalid = !!(ariaInvalid && ariaInvalid.trim() !== '' && ariaInvalid.toLowerCase() !== 'false');
    if (!invalid && el.validity && el.validity.valid === false) {
        invalid = true;
    }
    if (!invalid) {
        return '';
    }
    const ids = String(el.getAttribute('aria-errormessage') || '').split(/\s+/);
    const parts = [];
    for (let i = 0; i < ids.length; i++) {
        const id = ids[i];
        if (!id) {
            continue;
        }
        const ref = el.ownerDocument.getElementById(id);
        if (ref) {
            parts.push(String(ref.innerText || ref.textContent || '').trim());
        }
    }
    return parts.join(' ').trim();
}";

        /// <summary>
        /// JavaScript function <c>el => boolean</c> matching official
        /// <c>to.be.empty</c>: empty <c>value</c> on input/textarea, else
        /// trimmed <c>textContent</c>.
        /// </summary>
        internal const string IsEmptyFunction = @"el => {
    if (!el) {
        return false;
    }
    if (el.nodeName === 'INPUT' || el.nodeName === 'TEXTAREA') {
        return !(el.value);
    }
    const text = el.textContent ? String(el.textContent).trim() : '';
    return !text;
}";

        /// <summary>
        /// JavaScript function <c>(el, names) => boolean</c> matching official
        /// <c>to.contain.class</c>: every whitespace-separated token is in
        /// <c>classList</c>.
        /// </summary>
        internal const string ContainsClassFunction = @"(el, names) => {
    if (!el || !el.classList) {
        return false;
    }
    const list = String(names || '').split(/\s+/).filter(Boolean);
    return list.every(n => el.classList.contains(n));
}";

        /// <summary>
        /// JavaScript function <c>(el, expected) => boolean</c> matching official
        /// <c>to.have.values</c> on <c>select[multiple]</c>.
        /// </summary>
        internal const string HasSelectedValuesFunction = @"(el, expected) => {
    if (el && el.nodeName === 'LABEL' && el.control) {
        el = el.control;
    }
    if (!el || el.nodeName !== 'SELECT' || !el.multiple) {
        throw new Error('Not a select element with a multiple attribute');
    }
    const received = Array.from(el.selectedOptions).map(o => o.value);
    if (!expected || received.length !== expected.length) {
        return false;
    }
    for (let i = 0; i < expected.length; i++) {
        if (received[i] !== expected[i]) {
            return false;
        }
    }
    return true;
}";

        /// <summary>
        /// JavaScript function <c>(el) => string[]</c> returning selected option
        /// values from a <c>select[multiple]</c>.
        /// </summary>
        internal const string SelectedValuesFunction = @"(el) => {
    if (el && el.nodeName === 'LABEL' && el.control) {
        el = el.control;
    }
    if (!el || el.nodeName !== 'SELECT' || !el.multiple) {
        throw new Error('Not a select element with a multiple attribute');
    }
    return Array.from(el.selectedOptions).map(o => o.value);
}";

        /// <summary>
        /// JavaScript function <c>(el, spec) => boolean</c> that compares
        /// <c>el[spec.name]</c> to <c>spec.expected</c> via JSON.
        /// </summary>
        internal const string HasJSPropertyFunction = @"(el, spec) => {
    if (!el || !spec) {
        return false;
    }
    const convert = (v) => {
        if (v instanceof Date) {
            return v.getTime();
        }
        if (typeof v === 'string' && /^\d{4}-\d{2}-\d{2}T/.test(v)) {
            const ms = Date.parse(v);
            return Number.isNaN(ms) ? v : ms;
        }
        if (v && typeof v === 'object') {
            const out = Array.isArray(v) ? [] : {};
            const keys = Object.keys(v);
            for (let i = 0; i < keys.length; i++) {
                out[keys[i]] = convert(v[keys[i]]);
            }
            return out;
        }
        return v;
    };
    let cur = el;
    const parts = String(spec.name || '').split('.');
    for (let i = 0; i < parts.length; i++) {
        if (cur == null) {
            cur = undefined;
            break;
        }
        cur = cur[parts[i]];
    }
    if (spec.expected == null && (cur === undefined || cur === null)) {
        return true;
    }
    return JSON.stringify(convert(cur)) === JSON.stringify(convert(spec.expected));
}";

        /// <summary>
        /// JavaScript function <c>(el, name) => string</c> that prints a JS
        /// property the way official <c>toHaveJSProperty</c> fail lines do.
        /// </summary>
        internal const string ReadJSPropertyPrintedFunction = @"(el, name) => {
    let cur = el;
    const parts = String(name || '').split('.');
    for (let i = 0; i < parts.length; i++) {
        if (cur == null) {
            return 'undefined';
        }
        cur = cur[parts[i]];
    }
    if (cur === undefined) {
        return 'undefined';
    }
    if (cur === null) {
        return 'null';
    }
    if (typeof cur === 'string') {
        return JSON.stringify(cur);
    }
    if (typeof cur === 'number' || typeof cur === 'boolean') {
        return String(cur);
    }
    const convert = (v) => {
        if (v instanceof Date) {
            return v.getTime();
        }
        if (v && typeof v === 'object') {
            const out = Array.isArray(v) ? [] : {};
            const keys = Object.keys(v);
            for (let i = 0; i < keys.length; i++) {
                out[keys[i]] = convert(v[keys[i]]);
            }
            return out;
        }
        return v;
    };
    return JSON.stringify(convert(cur));
}";

        /// <summary>
        /// JavaScript function <c>(el, ratio) => boolean</c> that reports whether
        /// the element's visible intersection with the viewport meets <c>ratio</c>
        /// (0 means any overlapping pixel).
        /// </summary>
        internal const string IsInViewportFunction = @"(el, ratio) => {
    if (!el || typeof el.getBoundingClientRect !== 'function') {
        return false;
    }
    const box = el.getBoundingClientRect();
    const doc = el.ownerDocument || document;
    const win = doc.defaultView || window;
    const root = doc.documentElement;
    const vw = (win && win.innerWidth) || (root && root.clientWidth) || 0;
    const vh = (win && win.innerHeight) || (root && root.clientHeight) || 0;
    const visibleWidth = Math.min(box.right, vw) - Math.max(box.left, 0);
    const visibleHeight = Math.min(box.bottom, vh) - Math.max(box.top, 0);
    const visible = Math.max(0, visibleWidth) * Math.max(0, visibleHeight);
    const total = box.width * box.height;
    const need = ratio == null ? 0 : Number(ratio);
    if (need <= 0) {
        return visible > 0;
    }
    return total === 0 ? visible > 0 : (visible / total) >= need;
}";

        /// <summary>
        /// Official <c>elementText().full</c>: shadow-piercing text used by
        /// <c>toHaveText</c> / <c>toContainText</c> when
        /// <c>useInnerText</c> is not set. Skips script / noscript / style /
        /// <c>document.head</c>. Submit/button/reset inputs use
        /// <c>value</c>.
        /// </summary>
        internal const string ElementTextFullFunction = @"el => {
    const skip = (n) => {
        if (!n) {
            return true;
        }
        const doc = n.ownerDocument;
        return n.nodeName === 'SCRIPT' || n.nodeName === 'NOSCRIPT' || n.nodeName === 'STYLE' || (doc && doc.head && doc.head.contains(n));
    };
    const walk = (root) => {
        if (skip(root)) {
            return '';
        }
        if (root.nodeName === 'INPUT' && /^(submit|button|reset)$/i.test(String(root.type || ''))) {
            return String(root.value || '');
        }
        let full = '';
        for (let child = root.firstChild; child; child = child.nextSibling) {
            if (child.nodeType === 3) {
                full += child.nodeValue || '';
            } else if (child.nodeType === 8) {
                continue;
            } else if (child.nodeType === 1) {
                full += walk(child);
            }
        }
        if (root.shadowRoot) {
            full += walk(root.shadowRoot);
        }
        return full;
    };
    return walk(el);
}";
    }
}
