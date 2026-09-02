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
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official element-handle click actionability: attached, force visibility,
    /// and viewport classification. Mirrors <c>dom.ts</c> pointer-action errors.
    /// </summary>
    internal static class ClickAction
    {
        /// <summary>Official detached-node error.</summary>
        internal const string NotAttachedMessage = "Element is not attached to the DOM";

        /// <summary>Official force-click error when the node has no layout box.</summary>
        internal const string NotVisibleMessage = "Element is not visible";

        /// <summary>Official force-click error when the box has no in-viewport area.</summary>
        internal const string OutsideViewportMessage = "Element is outside of the viewport";

        /// <summary>
        /// Classifies a node for force-click: <c>ok</c>, <c>notconnected</c>,
        /// <c>notvisible</c>, or <c>notinviewport</c>. Does not reference
        /// <c>window.Node</c> so it still works after <c>delete window.Node</c>.
        /// </summary>
        internal const string ClassifyFunction = @"el => {
    if (!el || !el.isConnected) {
        return 'notconnected';
    }
    function rectsOf(node) {
        if (node.nodeType === 3) {
            const range = node.ownerDocument.createRange();
            range.selectNode(node);
            return range.getClientRects();
        }
        return node.getClientRects();
    }
    function boxOf(node) {
        if (node.nodeType === 3) {
            const range = node.ownerDocument.createRange();
            range.selectNode(node);
            return range.getBoundingClientRect();
        }
        return node.getBoundingClientRect();
    }
                const view = (el.ownerDocument && el.ownerDocument.defaultView) || window;
    const root = (el.ownerDocument && el.ownerDocument.documentElement) || document.documentElement;
    const vw = (view.innerWidth > 0 ? view.innerWidth : (root && root.clientWidth)) || 0;
    const vh = (view.innerHeight > 0 ? view.innerHeight : (root && root.clientHeight)) || 0;
    function clipArea(r) {
        const left = Math.min(Math.max(r.left, 0), vw);
        const right = Math.min(Math.max(r.right, 0), vw);
        const top = Math.min(Math.max(r.top, 0), vh);
        const bottom = Math.min(Math.max(r.bottom, 0), vh);
        return Math.max(0, right - left) * Math.max(0, bottom - top);
    }
    const rects = rectsOf(el);
    if (!rects || !rects.length) {
        const box = boxOf(el);
        if (box && (box.width > 0 || box.height > 0)) {
            return 'notinviewport';
        }
        return 'notvisible';
    }
    for (let i = 0; i < rects.length; i++) {
        if (clipArea(rects[i]) > 0.99) {
            return 'ok';
        }
    }
    return 'notinviewport';
}";

        /// <summary>
        /// Click point for element and text nodes. Optional <c>pos.x</c>/<c>pos.y</c>
        /// are offsets from the padding box (official <c>_offsetPoint</c>).
        /// </summary>
        internal const string PointFunction = @"(el, pos) => {
    function unionBox(node) {
        if (!node) {
            return null;
        }
        if (node.nodeType === 3) {
            const range = node.ownerDocument.createRange();
            range.selectNode(node);
            return range.getBoundingClientRect();
        }
        let style = null;
        try {
            style = ((node.ownerDocument && node.ownerDocument.defaultView) || window).getComputedStyle(node);
        } catch (e) {
        }
        if (style && style.display === 'contents') {
            const kids = node.childNodes || [];
            let left = Infinity;
            let top = Infinity;
            let right = -Infinity;
            let bottom = -Infinity;
            let found = false;
            for (let i = 0; i < kids.length; i++) {
                const b = unionBox(kids[i]);
                if (!b || (b.width <= 0 && b.height <= 0)) {
                    continue;
                }
                found = true;
                left = Math.min(left, b.left);
                top = Math.min(top, b.top);
                right = Math.max(right, b.right);
                bottom = Math.max(bottom, b.bottom);
            }
            if (found) {
                return { left: left, top: top, width: right - left, height: bottom - top, right: right, bottom: bottom };
            }
        }
        return node.getBoundingClientRect();
    }
    let r;
    if (el && el.nodeType === 3) {
        const range = el.ownerDocument.createRange();
        range.selectNode(el);
        r = range.getBoundingClientRect();
        if (pos) {
            const ox = pos.x != null ? pos.x : pos.X;
            const oy = pos.y != null ? pos.y : pos.Y;
            return [r.left + ox, r.top + oy];
        }
        return [r.left + r.width / 2, r.top + r.height / 2];
    }
    r = unionBox(el);
    if (pos) {
        const ox = pos.x != null ? pos.x : pos.X;
        const oy = pos.y != null ? pos.y : pos.Y;
        let bl = 0;
        let bt = 0;
        const view = (el.ownerDocument && el.ownerDocument.defaultView) || window;
        try {
            const style = view.getComputedStyle(el);
            bl = parseInt(style.borderLeftWidth || '', 10) || 0;
            bt = parseInt(style.borderTopWidth || '', 10) || 0;
        } catch (e) {
        }
        return [r.left + bl + ox, r.top + bt + oy];
    }
    if (el && typeof el.getBoxQuads === 'function') {
        try {
            const quads = el.getBoxQuads();
            if (quads && quads.length) {
                const q = quads[0];
                return [
                    (q.p1.x + q.p2.x + q.p3.x + q.p4.x) / 4,
                    (q.p1.y + q.p2.y + q.p3.y + q.p4.y) / 4
                ];
            }
        } catch (e2) {
        }
    }
    return [r.left + r.width / 2, r.top + r.height / 2];
}";

        /// <summary>
        /// Official enabled check: native form controls with <c>disabled</c>,
        /// after retargeting labels / button-like ancestors.
        /// </summary>
        internal const string IsDisabledFunction = @"el => {
    if (!el || !el.isConnected) {
        return 'detached';
    }
    let node = el.nodeType === 1 ? el : el.parentElement;
    if (!node) {
        return 'ok';
    }
    if (!node.matches || (!node.matches('input, textarea, select') && !node.isContentEditable)) {
        node = (node.closest && node.closest('button, [role=button], [role=checkbox], [role=radio]')) || node;
    }
    if (node.matches && !node.matches('a, input, textarea, button, select, [role=link], [role=button], [role=checkbox], [role=radio]') && !node.isContentEditable) {
        const label = node.closest && node.closest('label');
        if (label && label.control) {
            node = label.control;
        }
    }
    const tag = String(node.nodeName || '').toUpperCase();
    const native = tag === 'BUTTON' || tag === 'INPUT' || tag === 'SELECT' || tag === 'TEXTAREA' || tag === 'OPTION' || tag === 'OPTGROUP';
    if (native && (node.disabled || node.hasAttribute('disabled'))) {
        return 'disabled';
    }
    return 'ok';
}";

        /// <summary>
        /// Official <c>expectHitTarget</c>: after button/link retarget, return
        /// <c>ok</c>, <c>detached</c>, or a <c>previewNode</c> intercept string.
        /// </summary>
        internal const string HitTargetFunction = @"(el, point) => {
    if (!el || !el.isConnected) {
        return 'detached';
    }
    function parentElementOrShadowHost(element) {
        if (!element) {
            return null;
        }
        if (element.parentElement) {
            return element.parentElement;
        }
        if (element.parentNode && element.parentNode.nodeType === 11 && element.parentNode.host) {
            return element.parentNode.host;
        }
        return null;
    }
    function composedParent(node) {
        return (node && node.assignedSlot) || parentElementOrShadowHost(node);
    }
    function isComposedDescendant(node, ancestor) {
        let n = node;
        while (n) {
            if (n === ancestor) {
                return true;
            }
            n = composedParent(n);
        }
        return false;
    }
    function enclosingShadowRootOrDocument(element) {
        let node = element;
        while (node && node.parentNode) {
            node = node.parentNode;
        }
        if (node && (node.nodeType === 11 || node.nodeType === 9)) {
            return node;
        }
        return (element && element.ownerDocument) || document;
    }
    function previewNode(node) {
        if (!node) {
            return 'node';
        }
        const tag = String(node.nodeName || '').toLowerCase();
        const attrs = [];
        const list = node.attributes || [];
        for (let i = 0; i < list.length; i++) {
            const name = list[i].name;
            const value = list[i].value;
            if (name === 'style') {
                continue;
            }
            attrs.push(' ' + name + '=""' + value + '""');
        }
        attrs.sort(function (a, b) { return a.length - b.length; });
        const attrText = attrs.join('');
        const children = node.childNodes || [];
        if (children.length === 1 && children[0].nodeType === 3) {
            return '<' + tag + attrText + '>' + String(children[0].nodeValue || '') + '</' + tag + '>';
        }

        if (children.length) {
            return '<' + tag + attrText + '>\u2026</' + tag + '>';
        }

        return '<' + tag + attrText + '></' + tag + '>';
    }
    let target = el.nodeType === 1 ? el : el.parentElement;
    if (!target) {
        return 'blocked';
    }
    if (!target.matches || (!target.matches('input, textarea, select') && !target.isContentEditable)) {
        target = (target.closest && target.closest('button, [role=button], a, [role=link]')) || target;
    }
    const x = point[0];
    const y = point[1];
    const roots = [];
    let parentElement = target;
    while (parentElement) {
        const root = enclosingShadowRootOrDocument(parentElement);
        if (!root) {
            break;
        }
        roots.push(root);
        if (root.nodeType === 9) {
            break;
        }
        parentElement = root.host;
    }
    let hitElement = null;
    for (let index = roots.length - 1; index >= 0; index--) {
        const root = roots[index];
        let elements = [];
        let singleElement = null;
        try {
            elements = root.elementsFromPoint ? Array.from(root.elementsFromPoint(x, y) || []) : [];
            singleElement = root.elementFromPoint ? root.elementFromPoint(x, y) : null;
        } catch (e) {
            return 'blocked';
        }
        if (singleElement && elements[0] && parentElementOrShadowHost(singleElement) === elements[0]) {
            let style = null;
            try {
                style = ((singleElement.ownerDocument && singleElement.ownerDocument.defaultView) || window).getComputedStyle(singleElement);
            } catch (e2) {
            }
            if (style && style.display === 'contents') {
                elements.unshift(singleElement);
            }
        }
        if (elements[0] && elements[0].shadowRoot === root && elements[1] === singleElement) {
            elements.shift();
        }
        const innerElement = elements[0];
        if (!innerElement) {
            break;
        }
        hitElement = innerElement;
        if (index && innerElement !== roots[index - 1].host) {
            break;
        }
    }
    if (!hitElement) {
        return 'blocked';
    }
    const hitParents = [];
    while (hitElement && hitElement !== target) {
        hitParents.push(hitElement);
        hitElement = hitElement.assignedSlot || parentElementOrShadowHost(hitElement);
    }
    if (hitElement === target) {
        return 'ok';
    }

    const hitNode = hitParents[0];
    if (hitNode && target.querySelectorAll) {
        const slots = target.querySelectorAll('slot');
        for (let s = 0; s < slots.length; s++) {
            let assigned = [];
            try {
                assigned = slots[s].assignedNodes({ flatten: true }) || [];
            } catch (e3) {
            }
            for (let a = 0; a < assigned.length; a++) {
                const node = assigned[a];
                if (node === hitNode || (node.contains && node.contains(hitNode))) {
                    return 'ok';
                }
            }
        }
    }

    if (hitNode && hitNode === composedParent(target)) {
        const tag = String(hitNode.tagName || '').toLowerCase();
        if (tag !== 'body' && tag !== 'html') {
            try {
                const st = ((hitNode.ownerDocument && hitNode.ownerDocument.defaultView) || window).getComputedStyle(hitNode);
                if (st && st.transform && st.transform !== 'none') {
                    return 'ok';
                }
            } catch (e4) {
            }
        }
    }

    let description = previewNode(hitParents[0] || (target.ownerDocument && target.ownerDocument.documentElement));
    let element = target;
    while (element) {
        const common = hitParents.indexOf(element);
        if (common !== -1) {
            if (common > 1) {
                description += ' from ' + previewNode(hitParents[common - 1]) + ' subtree';
            }
            break;
        }
        element = parentElementOrShadowHost(element);
    }
    return description + ' intercepts pointer events';
}";

        /// <summary>
        /// Official <c>describeIFrameStyle</c>: padding+border offset, or
        /// <c>transformed</c> when a CSS transform blocks coordinate mapping.
        /// </summary>
        internal const string DescribeIFrameStyleFunction = @"iframe => {
    if (!iframe || !iframe.ownerDocument || !iframe.ownerDocument.defaultView) {
        return 'error:notconnected';
    }
    const defaultView = iframe.ownerDocument.defaultView;
    function parentElementOrShadowHost(element) {
        if (!element) {
            return null;
        }
        if (element.parentElement) {
            return element.parentElement;
        }
        if (element.parentNode && element.parentNode.nodeType === 11 && element.parentNode.host) {
            return element.parentNode.host;
        }
        return null;
    }
    for (let e = iframe; e; e = parentElementOrShadowHost(e)) {
        try {
            if (defaultView.getComputedStyle(e).transform !== 'none') {
                return 'transformed';
            }
        } catch (err) {
        }
    }
    return 'ok';
}";

        /// <summary>
        /// Thrown after hover when the hit target moved, so selector clicks retry.
        /// </summary>
        internal const string HitMissedMessage = "Hit target missed after hover";

        /// <summary>
        /// Layout box used for the stable-position wait.
        /// </summary>
        internal const string BoxFunction = @"el => {
    if (!el || !el.isConnected) {
        return null;
    }
    function unionBox(node) {
        if (node.nodeType === 3) {
            const range = node.ownerDocument.createRange();
            range.selectNode(node);
            return range.getBoundingClientRect();
        }
        let style = null;
        try {
            style = ((node.ownerDocument && node.ownerDocument.defaultView) || window).getComputedStyle(node);
        } catch (e) {
        }
        if (style && style.display === 'contents') {
            const kids = node.childNodes || [];
            let left = Infinity;
            let top = Infinity;
            let right = -Infinity;
            let bottom = -Infinity;
            let found = false;
            for (let i = 0; i < kids.length; i++) {
                const b = unionBox(kids[i]);
                if (!b || (b.width <= 0 && b.height <= 0)) {
                    continue;
                }
                found = true;
                left = Math.min(left, b.left);
                top = Math.min(top, b.top);
                right = Math.max(right, b.right);
                bottom = Math.max(bottom, b.bottom);
            }
            if (!found) {
                return node.getBoundingClientRect();
            }
            return { left: left, top: top, width: right - left, height: bottom - top, right: right, bottom: bottom };
        }
        return node.getBoundingClientRect();
    }
    const r = unionBox(el);
    if (!r) {
        return null;
    }
    return [r.left, r.top, r.width, r.height];
}";

        /// <summary>
        /// Scrolls an owner iframe into the parent viewport only when it is off-screen.
        /// </summary>
        internal const string ScrollFrameIfOffscreenFunction = @"el => {
    if (!el || !el.isConnected || !el.scrollIntoView) {
        return false;
    }
    const r = el.getBoundingClientRect();
    const view = (el.ownerDocument && el.ownerDocument.defaultView) || window;
    const root = (el.ownerDocument && el.ownerDocument.documentElement) || document.documentElement;
    const vw = (view.innerWidth > 0 ? view.innerWidth : (root && root.clientWidth)) || 0;
    const vh = (view.innerHeight > 0 ? view.innerHeight : (root && root.clientHeight)) || 0;
    const visible = r.bottom > 0 && r.right > 0 && r.top < vh && r.left < vw && (r.width > 0 || r.height > 0);
    if (visible) {
        return false;
    }
    el.scrollIntoView({ block: 'center', inline: 'center' });
    return true;
}";

        /// <summary>
        /// Scrolls with an official <c>scrollIntoView</c> alignment cycle.
        /// </summary>
        internal const string ScrollAlignedFunction = @"(el, align) => {
    if (!el || !el.isConnected) {
        return false;
    }
    const block = align || 'center';
    const view = (el.ownerDocument && el.ownerDocument.defaultView) || window;
    let node = el.nodeType === 1 ? el : (el.parentElement || el);
    try {
        const style = view.getComputedStyle(node);
        if (style && style.display === 'contents') {
            const kids = node.childNodes || [];
            for (let i = 0; i < kids.length; i++) {
                if (kids[i].nodeType === 1 && kids[i].scrollIntoView) {
                    kids[i].scrollIntoView({ block: block, inline: block });
                    return true;
                }
            }
            let left = Infinity;
            let top = Infinity;
            let right = -Infinity;
            let bottom = -Infinity;
            let found = false;
            for (let i = 0; i < kids.length; i++) {
                if (kids[i].nodeType !== 3) {
                    continue;
                }
                const range = node.ownerDocument.createRange();
                range.selectNode(kids[i]);
                const b = range.getBoundingClientRect();
                if (!b || (b.width <= 0 && b.height <= 0)) {
                    continue;
                }
                found = true;
                left = Math.min(left, b.left);
                top = Math.min(top, b.top);
                right = Math.max(right, b.right);
                bottom = Math.max(bottom, b.bottom);
            }
            if (found) {
                const root = (node.ownerDocument && node.ownerDocument.documentElement) || document.documentElement;
                const vw = (view.innerWidth > 0 ? view.innerWidth : (root && root.clientWidth)) || 0;
                const vh = (view.innerHeight > 0 ? view.innerHeight : (root && root.clientHeight)) || 0;
                view.scrollBy(left + ((right - left) / 2) - (vw / 2), top + ((bottom - top) / 2) - (vh / 2));
                return true;
            }
        }
    } catch (e) {
    }
    if (node && node.scrollIntoView) {
        node.scrollIntoView({ block: block, inline: block });
        return true;
    }
    return false;
}";

        /// <summary>Whether the handle is a DOM text node.</summary>
        internal const string IsTextNodeFunction = "el => !!(el && el.nodeType === 3)";

        /// <summary>Whether the handle is still in the document.</summary>
        internal const string IsConnectedFunction = "el => !!(el && el.isConnected)";

        /// <summary>
        /// Official-style visibility for click waits. Avoids
        /// <c>checkVisibility</c> and <c>window.Node</c> so
        /// <c>delete window.Node</c> still works.
        /// </summary>
        internal const string IsVisibleFunction = @"el => {
    if (!el || !el.isConnected) {
        return false;
    }
    if (el.nodeType === 3) {
        const range = el.ownerDocument.createRange();
        range.selectNode(el);
        const box = range.getBoundingClientRect();
        return !!(box && box.width > 0 && box.height > 0);
    }
    const view = el.ownerDocument && el.ownerDocument.defaultView;
    if (!view) {
        return true;
    }
    let style;
    try {
        style = view.getComputedStyle(el);
    } catch (e) {
        return true;
    }
    if (!style) {
        return true;
    }
    if (style.display === 'none' || style.visibility === 'hidden' || style.visibility === 'collapse') {
        return false;
    }
    if (style.display === 'contents') {
        const kids = el.childNodes || [];
        for (let i = 0; i < kids.length; i++) {
            const kid = kids[i];
            if (kid.nodeType === 3) {
                const range = el.ownerDocument.createRange();
                range.selectNode(kid);
                const box = range.getBoundingClientRect();
                if (box && box.width > 0 && box.height > 0) {
                    return true;
                }
            } else if (kid.nodeType === 1) {
                const box = kid.getBoundingClientRect();
                if (box && box.width > 0 && box.height > 0) {
                    return true;
                }
            }
        }
        return false;
    }
    let parent = el.parentElement;
    while (parent) {
        let parentStyle;
        try {
            parentStyle = view.getComputedStyle(parent);
        } catch (e) {
            break;
        }
        if (parentStyle && parentStyle.display === 'none') {
            return false;
        }
        parent = parent.parentElement;
    }
    const rect = el.getBoundingClientRect();
    return !!(rect && rect.width > 0 && rect.height > 0);
}";

        /// <summary>
        /// Scrolls an element or text-node range to the viewport center. Text
        /// nodes have no <c>scrollIntoView</c>.
        /// </summary>
        internal const string ScrollIntoViewFunction = @"el => {
    if (!el || !el.isConnected) {
        return false;
    }
    const view = (el.ownerDocument && el.ownerDocument.defaultView) || window;
    let r;
    if (el.nodeType === 3) {
        const range = el.ownerDocument.createRange();
        range.selectNode(el);
        r = range.getBoundingClientRect();
    } else {
        let style = null;
        try {
            style = view.getComputedStyle(el);
        } catch (e) {
        }
        if (style && style.display === 'contents') {
            const kids = el.childNodes || [];
            for (let i = 0; i < kids.length; i++) {
                if (kids[i].nodeType === 1 && kids[i].scrollIntoView) {
                    kids[i].scrollIntoView({ block: 'center', inline: 'center' });
                    return true;
                }
                if (kids[i].nodeType === 3) {
                    const range = el.ownerDocument.createRange();
                    range.selectNode(kids[i]);
                    r = range.getBoundingClientRect();
                    break;
                }
            }
        }
        if (r) {
        } else if (el.scrollIntoView) {
            el.scrollIntoView({ block: 'center', inline: 'center' });
            return true;
        } else {
            r = el.getBoundingClientRect();
        }
    }
    if (!r) {
        return false;
    }
    const root = (el.ownerDocument && el.ownerDocument.documentElement) || document.documentElement;
    const vw = (view.innerWidth > 0 ? view.innerWidth : (root && root.clientWidth)) || 0;
    const vh = (view.innerHeight > 0 ? view.innerHeight : (root && root.clientHeight)) || 0;
    view.scrollBy(r.left + (r.width / 2) - (vw / 2), r.top + (r.height / 2) - (vh / 2));
    return true;
}";

        /// <summary>
        /// Picks a frame-local click point from client rects, preferring a
        /// point that already hits the node (rotated / wrapped inlines).
        /// </summary>
        internal const string PickPointFunction = @"(el, pos) => {
    if (!el || !el.isConnected) {
        return null;
    }
    function composedParent(node) {
        return (node && node.assignedSlot) || (node && node.parentElement) || (node && node.getRootNode && node.getRootNode().host) || null;
    }
    function isComposedDescendant(node, ancestor) {
        let n = node;
        while (n) {
            if (n === ancestor) {
                return true;
            }
            n = composedParent(n);
        }
        return false;
    }
    let target = el.nodeType === 1 ? el : el.parentElement;
    if (target && target.matches && !target.matches('input, textarea, select') && !target.isContentEditable) {
        target = (target.closest && target.closest('button, [role=button], a, [role=link]')) || target;
    }
    const doc = el.ownerDocument || document;
    function hitOk(x, y) {
        let hit = null;
        try {
            hit = doc.elementFromPoint(x, y);
        } catch (e) {
            return false;
        }
        if (!hit) {
            return false;
        }
        if (isComposedDescendant(hit, target) || isComposedDescendant(hit, el)) {
            return true;
        }
        return false;
    }
    if (pos) {
        const ox = pos.x != null ? pos.x : pos.X;
        const oy = pos.y != null ? pos.y : pos.Y;
        let r;
        if (el.nodeType === 3) {
            const range = el.ownerDocument.createRange();
            range.selectNode(el);
            r = range.getBoundingClientRect();
            return [r.left + ox, r.top + oy];
        }
        r = el.getBoundingClientRect();
        let bl = 0;
        let bt = 0;
        const view = (el.ownerDocument && el.ownerDocument.defaultView) || window;
        try {
            const style = view.getComputedStyle(el);
            bl = parseInt(style.borderLeftWidth || '', 10) || 0;
            bt = parseInt(style.borderTopWidth || '', 10) || 0;
            if (style && style.display === 'contents') {
                const kids = el.childNodes || [];
                let left = Infinity;
                let top = Infinity;
                let found = false;
                for (let i = 0; i < kids.length; i++) {
                    let b = null;
                    if (kids[i].nodeType === 3) {
                        const range = el.ownerDocument.createRange();
                        range.selectNode(kids[i]);
                        b = range.getBoundingClientRect();
                    } else if (kids[i].nodeType === 1) {
                        b = kids[i].getBoundingClientRect();
                    }
                    if (!b || (b.width <= 0 && b.height <= 0)) {
                        continue;
                    }
                    found = true;
                    left = Math.min(left, b.left);
                    top = Math.min(top, b.top);
                }
                if (found) {
                    return [left + ox, top + oy];
                }
            }
        } catch (e) {
        }
        return [r.left + bl + ox, r.top + bt + oy];
    }
    const rects = [];
    if (el.nodeType === 3) {
        const range = el.ownerDocument.createRange();
        range.selectNode(el);
        const list = range.getClientRects();
        for (let i = 0; i < list.length; i++) {
            rects.push(list[i]);
        }
    } else {
        const list = el.getClientRects();
        for (let i = 0; i < list.length; i++) {
            rects.push(list[i]);
        }
        if (!rects.length) {
            let style = null;
            try {
                style = ((el.ownerDocument && el.ownerDocument.defaultView) || window).getComputedStyle(el);
            } catch (e) {
            }
            if (style && style.display === 'contents') {
                const kids = el.childNodes || [];
                for (let i = 0; i < kids.length; i++) {
                    if (kids[i].nodeType === 3) {
                        const range = el.ownerDocument.createRange();
                        range.selectNode(kids[i]);
                        const textRects = range.getClientRects();
                        for (let k = 0; k < textRects.length; k++) {
                            rects.push(textRects[k]);
                        }
                    } else if (kids[i].nodeType === 1) {
                        const kidRects = kids[i].getClientRects();
                        for (let k = 0; k < kidRects.length; k++) {
                            rects.push(kidRects[k]);
                        }
                    }
                }
            }
            if (!rects.length) {
                rects.push(el.getBoundingClientRect());
            }
        }
    }
    let fallback = null;
    for (let i = 0; i < rects.length; i++) {
        const r = rects[i];
        if (!r || (r.width <= 0 && r.height <= 0)) {
            continue;
        }
        const insetX = Math.min(1, r.width / 2);
        const insetY = Math.min(1, r.height / 2);
        const candidates = [
            [r.left + r.width / 2, r.top + r.height / 2],
            [r.left + insetX, r.top + insetY],
            [r.right - insetX, r.bottom - insetY],
            [r.left + insetX, r.bottom - insetY],
            [r.right - insetX, r.top + insetY]
        ];
        for (let gx = 1; gx <= 4; gx++) {
            for (let gy = 1; gy <= 4; gy++) {
                candidates.push([
                    r.left + (r.width * gx / 5),
                    r.top + (r.height * gy / 5)
                ]);
            }
        }
        for (let j = 0; j < candidates.length; j++) {
            const p = candidates[j];
            if (!fallback) {
                fallback = p;
            }
            if (hitOk(p[0], p[1])) {
                return p;
            }
        }
    }
    return fallback;
}";

        /// <summary>
        /// Scrolls an offset click point into the viewport and overflow parents.
        /// </summary>
        internal const string ScrollOffsetIntoViewFunction = @"(el, pos) => {
    if (!el || !el.isConnected || !pos) {
        return false;
    }
    const ox = pos.x != null ? pos.x : pos.X;
    const oy = pos.y != null ? pos.y : pos.Y;
    const view = (el.ownerDocument && el.ownerDocument.defaultView) || window;
    const root = (el.ownerDocument && el.ownerDocument.documentElement) || document.documentElement;
    function size() {
        return {
            w: (view.innerWidth > 0 ? view.innerWidth : (root && root.clientWidth)) || 0,
            h: (view.innerHeight > 0 ? view.innerHeight : (root && root.clientHeight)) || 0
        };
    }
    function point() {
        const r = el.getBoundingClientRect();
        let bl = 0;
        let bt = 0;
        try {
            const style = view.getComputedStyle(el);
            bl = parseInt(style.borderLeftWidth || '', 10) || 0;
            bt = parseInt(style.borderTopWidth || '', 10) || 0;
        } catch (e) {
        }
        return [r.left + bl + ox, r.top + bt + oy];
    }
    for (let i = 0; i < 8; i++) {
        const p = point();
        const vp = size();
        if (p[0] >= 1 && p[1] >= 1 && p[0] <= vp.w - 1 && p[1] <= vp.h - 1) {
            return true;
        }
        view.scrollBy(p[0] - (vp.w / 2), p[1] - (vp.h / 2));
        let node = el.parentElement;
        while (node) {
            let overflow = '';
            try {
                const s = view.getComputedStyle(node);
                overflow = (s.overflow || '') + (s.overflowX || '') + (s.overflowY || '');
            } catch (e) {
            }
            if (/auto|scroll|overlay/.test(overflow)) {
                const cr = node.getBoundingClientRect();
                node.scrollLeft += p[0] - (cr.left + cr.width / 2);
                node.scrollTop += p[1] - (cr.top + cr.height / 2);
            }
            node = node.parentElement;
        }
    }
    return true;
}";

        /// <summary>
        /// Maps a child-frame client point through an iframe's CSS transform
        /// into the parent viewport (including 2D/3D matrices).
        /// </summary>
        internal const string MapIFramePointFunction = @"(iframe, p) => {
    const fx = p[0];
    const fy = p[1];
    const style = getComputedStyle(iframe);
    const rect = iframe.getBoundingClientRect();
    const bl = (parseFloat(style.borderLeftWidth) || 0) + (parseFloat(style.paddingLeft) || 0);
    const bt = (parseFloat(style.borderTopWidth) || 0) + (parseFloat(style.paddingTop) || 0);
    let matrix;
    try {
        matrix = new DOMMatrix(style.transform);
    } catch (e) {
        matrix = new DOMMatrix();
    }
    const bw = iframe.offsetWidth || rect.width || 1;
    const bh = iframe.offsetHeight || rect.height || 1;
    const originParts = (style.transformOrigin || '').split(' ');
    const tox = parseFloat(originParts[0]) || (bw / 2);
    const toy = parseFloat(originParts[1]) || (bh / 2);
    const lx = bl + fx;
    const ly = bt + fy;
    const transformed = matrix.transformPoint(new DOMPoint(lx - tox, ly - toy));
    const corners = [
        matrix.transformPoint(new DOMPoint(0 - tox, 0 - toy)),
        matrix.transformPoint(new DOMPoint(bw - tox, 0 - toy)),
        matrix.transformPoint(new DOMPoint(bw - tox, bh - toy)),
        matrix.transformPoint(new DOMPoint(0 - tox, bh - toy))
    ];
    let minX = Infinity;
    let minY = Infinity;
    let maxX = -Infinity;
    let maxY = -Infinity;
    for (let i = 0; i < corners.length; i++) {
        const x = corners[i].x + tox;
        const y = corners[i].y + toy;
        if (x < minX) minX = x;
        if (y < minY) minY = y;
        if (x > maxX) maxX = x;
        if (y > maxY) maxY = y;
    }
    const localW = maxX - minX || 1;
    const localH = maxY - minY || 1;
    const sx = rect.width / localW;
    const sy = rect.height / localH;
    return [
        rect.left + ((transformed.x + tox) - minX) * sx,
        rect.top + ((transformed.y + toy) - minY) * sy
    ];
}";

        /// <summary>
        /// Public API name for click timeout prefixes. Page/frame/locator
        /// paths set this so handle clicks still say <c>elementHandle.click</c>.
        /// </summary>
        internal static readonly AsyncLocal<string> ApiName = new AsyncLocal<string>();

        /// <summary>
        /// Official non-strict multi-match preview, set by <see cref="StrictSelector"/>.
        /// </summary>
        internal static readonly AsyncLocal<string> ResolvedLog = new AsyncLocal<string>();

        /// <summary>
        /// Official <c>signal</c> for the in-flight click, set by locator/page click.
        /// </summary>
        internal static readonly AsyncLocal<AbortSignal> ActiveSignal = new AsyncLocal<AbortSignal>();

        private static readonly object SharedPointerGateKey = new object();

        private static readonly ConditionalWeakTable<object, SemaphoreSlim> PointerGates = new();

        private static readonly AsyncLocal<bool> InPointer = new AsyncLocal<bool>();

        /// <summary>
        /// Serializes pointer actions on the same page so they share one mouse.
        /// Different pages (including other browser contexts) run in parallel,
        /// matching official issue 29096.
        /// Nested handler clicks on the same call stack skip the gate.
        /// </summary>
        /// <param name="pageGate">The page whose mouse is used, or <see langword="null"/>.</param>
        /// <param name="action">The pointer action.</param>
        /// <returns>A task that completes when <paramref name="action"/> finishes.</returns>
        internal static async Task RunPointerAsync(object pageGate, Func<Task> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (InPointer.Value)
            {
                await action().ConfigureAwait(false);
                return;
            }

            SemaphoreSlim gate = PointerGates.GetValue(pageGate ?? SharedPointerGateKey, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync().ConfigureAwait(false);
            InPointer.Value = true;
            try
            {
                IPage page = pageGate as IPage;
                if (page != null)
                {
                    try
                    {
                        await page.BringToFrontAsync().ConfigureAwait(false);
                    }
                    catch (PlaywrightNativeException)
                    {
                    }
                }

                await action().ConfigureAwait(false);
            }
            finally
            {
                InPointer.Value = false;
                gate.Release();
            }
        }

        /// <summary>
        /// Waits for official click actionability: attached, visible, enabled,
        /// stable, scrolled into view, and hit-target, unless forced.
        /// </summary>
        /// <param name="handle">The element or text node to click.</param>
        /// <param name="force">When <see langword="true"/>, skip waits and throw on blocked layout.</param>
        /// <param name="timeout">Actionability timeout in milliseconds.</param>
        /// <returns>A task that completes when actionability passes.</returns>
        internal static Task PrepareAsync(IElementHandle handle, bool? force, float? timeout)
            => PrepareAsync(handle, force, timeout, trial: null);

        /// <summary>
        /// Waits for official click actionability: attached, visible, enabled,
        /// stable, scrolled into view, and hit-target, unless forced.
        /// </summary>
        /// <param name="handle">The element or text node to click.</param>
        /// <param name="force">When <see langword="true"/>, skip waits and throw on blocked layout.</param>
        /// <param name="timeout">Actionability timeout in milliseconds.</param>
        /// <param name="trial">When <see langword="true"/>, timeout text includes <c>trial run</c>.</param>
        /// <returns>A task that completes when actionability passes.</returns>
        internal static Task PrepareAsync(IElementHandle handle, bool? force, float? timeout, bool? trial)
            => PrepareAsync(handle, force, timeout, trial, position: null);

        /// <summary>
        /// Waits for official click actionability, scrolling an explicit
        /// <paramref name="position"/> into view when one is supplied.
        /// </summary>
        /// <param name="handle">The element or text node to click.</param>
        /// <param name="force">When <see langword="true"/>, skip waits and throw on blocked layout.</param>
        /// <param name="timeout">Actionability timeout in milliseconds.</param>
        /// <param name="trial">When <see langword="true"/>, timeout text includes <c>trial run</c>.</param>
        /// <param name="position">Optional padding-box offset to reveal.</param>
        /// <returns>A task that completes when actionability passes.</returns>
        internal static Task PrepareAsync(IElementHandle handle, bool? force, float? timeout, bool? trial, Position position)
            => PrepareAsync(handle, force, timeout, trial, position, scroll: default);

        /// <summary>
        /// Waits for official click actionability, honoring
        /// <paramref name="scroll"/>.
        /// </summary>
        /// <param name="handle">The element or text node to click.</param>
        /// <param name="force">When <see langword="true"/>, skip waits and throw on blocked layout.</param>
        /// <param name="timeout">Actionability timeout in milliseconds.</param>
        /// <param name="trial">When <see langword="true"/>, timeout text includes <c>trial run</c>.</param>
        /// <param name="position">Optional padding-box offset to reveal.</param>
        /// <param name="scroll">When <see cref="ActionScroll.None"/>, do not scroll.</param>
        /// <returns>A task that completes when actionability passes.</returns>
        internal static async Task PrepareAsync(IElementHandle handle, bool? force, float? timeout, bool? trial, Position position, ActionScroll scroll)
        {
            if (!await IsConnectedAsync(handle).ConfigureAwait(false))
            {
                throw new PlaywrightNativeException(NotAttachedMessage);
            }

            if (scroll != ActionScroll.None)
            {
                await ScrollOwnerFramesIntoViewAsync(handle).ConfigureAwait(false);
            }

            if (force == true)
            {
                if (position != null && scroll != ActionScroll.None)
                {
                    await ScrollOffsetIntoViewAsync(handle, position).ConfigureAwait(false);
                }

                await ThrowIfForceBlockedAsync(handle).ConfigureAwait(false);
                return;
            }

            if (await IsTextNodeAsync(handle).ConfigureAwait(false))
            {
                if (scroll != ActionScroll.None)
                {
                    await ScrollIntoViewAsync(handle).ConfigureAwait(false);
                }

                await ThrowIfForceBlockedAsync(handle).ConfigureAwait(false);
                return;
            }

            await WaitActionableAsync(handle, timeout, trial, position, scroll).ConfigureAwait(false);
        }

        /// <summary>
        /// When <paramref name="scroll"/> is <see cref="ActionScroll.None"/>,
        /// throws if the node has no in-viewport area.
        /// </summary>
        /// <param name="handle">The element to classify.</param>
        /// <param name="scroll">The scroll option.</param>
        /// <returns>A task that completes when the check finishes.</returns>
        internal static Task EnsureInViewportWhenNoScrollAsync(IElementHandle handle, ActionScroll scroll)
        {
            if (scroll != ActionScroll.None)
            {
                return Task.CompletedTask;
            }

            return ThrowIfForceBlockedAsync(handle);
        }

        /// <summary>
        /// Official click: wait for hit target, hover, re-check at the same
        /// point (layout may shift on <c>mousemove</c>), then press. Retries
        /// when hover misses.
        /// </summary>
        /// <param name="handle">The element to click.</param>
        /// <param name="force">When <see langword="true"/>, skip hit-target waits.</param>
        /// <param name="timeout">Actionability timeout in milliseconds.</param>
        /// <param name="trial">When <see langword="true"/>, stop after actionability.</param>
        /// <param name="position">Optional padding-box offset.</param>
        /// <param name="scroll">Scroll option.</param>
        /// <param name="moveAsync">Moves the mouse to the page point.</param>
        /// <param name="pressAsync">Dispatches down/up at the current mouse position.</param>
        /// <returns>A task that completes when the click finishes.</returns>
        internal static async Task PerformClickAsync(
            IElementHandle handle,
            bool? force,
            float? timeout,
            bool? trial,
            Position position,
            ActionScroll scroll,
            Func<double[], Task> moveAsync,
            Func<Task> pressAsync)
        {
            if (moveAsync == null)
            {
                throw new ArgumentNullException(nameof(moveAsync));
            }

            if (pressAsync == null)
            {
                throw new ArgumentNullException(nameof(pressAsync));
            }

            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();
            while (true)
            {
                float? remaining = timeoutMs == Timeout.Infinite
                    ? timeout
                    : RemainingTimeout(timeoutMs, sw);
                await PrepareAsync(handle, force, remaining, trial, position, scroll).ConfigureAwait(false);
                if (ActionTrial.IsTrial(trial))
                {
                    return;
                }

                ClickOffset offset = position == null ? null : new ClickOffset { X = position.X, Y = position.Y };
                string pointScript = force == true ? PointFunction : PickPointFunction;
                double[] localPoint = offset == null
                    ? await handle.EvaluateAsync<double[]>(pointScript).ConfigureAwait(false)
                    : await handle.EvaluateAsync<double[]>(pointScript, offset).ConfigureAwait(false);
                if (localPoint == null || localPoint.Length < 2)
                {
                    throw new PlaywrightNativeException("Unable to compute a click point for the element.");
                }

                double[] pagePoint = await MapToPageAsync(handle, localPoint).ConfigureAwait(false);
                await moveAsync(pagePoint).ConfigureAwait(false);
                if (force != true)
                {
                    string hit = await HitAtLocalPointAsync(handle, localPoint).ConfigureAwait(false);
                    if (hit != "ok")
                    {
                        if (hit == "detached")
                        {
                            throw new PlaywrightNativeException(NotAttachedMessage);
                        }

                        if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                        {
                            throw new TimeoutException(TimeoutMessage(timeoutMs, trial) + "\n    - " + hit + "\n");
                        }

                        await Task.Delay(500).ConfigureAwait(false);
                        continue;
                    }
                }

                await pressAsync().ConfigureAwait(false);
                return;
            }
        }

        /// <summary>
        /// Re-queries <paramref name="selector"/> and retries the click when the
        /// matched node detaches (official <c>page.click</c> / <c>frame.click</c>).
        /// </summary>
        /// <param name="querySelectorAsync">One-shot selector query.</param>
        /// <param name="selector">The selector.</param>
        /// <param name="onHandle">Click on the matched handle.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <param name="apiName">Name used in timeout and annotation messages.</param>
        /// <param name="scroll">Scroll option forwarded to the wait helper.</param>
        /// <returns>A task that completes when the click finishes.</returns>
        internal static async Task RunOnSelectorAsync(
            Func<string, Task<IElementHandle>> querySelectorAsync,
            string selector,
            Func<IElementHandle, Task> onHandle,
            float? timeout,
            string apiName,
            ActionScroll scroll)
        {
            if (onHandle == null)
            {
                throw new ArgumentNullException(nameof(onHandle));
            }

            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();
            int[] waits = { 0, 20, 100, 100, 500 };
            int retry = 0;

            while (true)
            {
                ThrowIfAborted(apiName);
                try
                {
                    await ElementQuery.WaitRunAsync(
                        querySelectorAsync,
                        selector,
                        onHandle,
                        RemainingTimeout(timeoutMs, sw),
                        apiName,
                        scroll).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex) when (IsRetryable(ex))
                {
                    if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                    {
                        throw;
                    }

                    int wait = waits[Math.Min(retry, waits.Length - 1)];
                    retry++;
                    if (wait > 0)
                    {
                        await DelayOrAbortAsync(wait).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// Holds exactly <paramref name="modifiers"/> for <paramref name="action"/>
        /// and restores previously pressed keys. <see langword="null"/> leaves
        /// the current keyboard state unchanged.
        /// </summary>
        /// <param name="modifiers">Requested modifiers, or <see langword="null"/> to omit.</param>
        /// <param name="currentlyPressed">Modifier keys held before the action.</param>
        /// <param name="downAsync">Key-down callback.</param>
        /// <param name="upAsync">Key-up callback.</param>
        /// <param name="action">The input action to wrap.</param>
        /// <returns>A task that completes when the action and restore finish.</returns>
        internal static async Task RunModifiersAsync(
            IEnumerable<KeyboardModifier> modifiers,
            IReadOnlyCollection<string> currentlyPressed,
            Func<string, Task> downAsync,
            Func<string, Task> upAsync,
            Func<Task> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (modifiers == null)
            {
                await action().ConfigureAwait(false);
                return;
            }

            HashSet<string> desired = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyboardModifier modifier in modifiers)
            {
                string key = ModifierKey(modifier);
                if (key != null)
                {
                    desired.Add(key);
                }
            }

            HashSet<string> held = new HashSet<string>(StringComparer.Ordinal);
            if (currentlyPressed != null)
            {
                foreach (string key in currentlyPressed)
                {
                    if (!string.IsNullOrEmpty(key))
                    {
                        held.Add(key);
                    }
                }
            }

            List<string> released = new List<string>();
            List<string> pressed = new List<string>();
            try
            {
                foreach (string key in held)
                {
                    if (!desired.Contains(key) && upAsync != null)
                    {
                        await upAsync(key).ConfigureAwait(false);
                        released.Add(key);
                    }
                }

                foreach (string key in desired)
                {
                    if (!held.Contains(key) && downAsync != null)
                    {
                        await downAsync(key).ConfigureAwait(false);
                        pressed.Add(key);
                    }
                }

                await action().ConfigureAwait(false);
            }
            finally
            {
                if (upAsync != null)
                {
                    for (int i = pressed.Count - 1; i >= 0; i--)
                    {
                        await upAsync(pressed[i]).ConfigureAwait(false);
                    }
                }

                if (downAsync != null)
                {
                    foreach (string key in released)
                    {
                        await downAsync(key).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// Returns whether <paramref name="ex"/> is the official detached-node
        /// failure that selector-based clicks should retry.
        /// </summary>
        /// <param name="ex">The exception from a handle click.</param>
        /// <returns><see langword="true"/> when the click should re-query.</returns>
        internal static bool IsRetryable(Exception ex)
        {
            if (ex == null)
            {
                return false;
            }

            if (ex is TimeoutException || ex is AbortError)
            {
                return false;
            }

            string message = ex.Message ?? string.Empty;
            return PlaywrightNativeException.IsDestroyedContext(ex)
                || message.Contains(NotAttachedMessage, StringComparison.Ordinal)
                || message.Contains(HitMissedMessage, StringComparison.Ordinal)
                || message.Contains("not attached", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Node is detached", StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns whether <paramref name="handle"/> is a text node.
        /// </summary>
        /// <param name="handle">The handle to inspect.</param>
        /// <returns><see langword="true"/> when the remote object is a text node.</returns>
        internal static async Task<bool> IsTextNodeAsync(IElementHandle handle)
        {
            try
            {
                return await handle.EvaluateAsync<bool>(IsTextNodeFunction).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                return false;
            }
        }

        /// <summary>
        /// Viewport click point for <paramref name="handle"/>, including text nodes.
        /// </summary>
        /// <param name="handle">The element or text node.</param>
        /// <param name="position">Optional offset from the top-left of the box.</param>
        /// <returns>A two-element <c>[x, y]</c> array in CSS pixels.</returns>
        internal static Task<double[]> PointAsync(IElementHandle handle, Position position)
            => PointAsync(handle, position, force: null);

        /// <summary>
        /// Viewport click point for <paramref name="handle"/>, including text nodes.
        /// A force click uses the layout center and does not search for a hit target.
        /// </summary>
        /// <param name="handle">The element or text node.</param>
        /// <param name="position">Optional offset from the top-left of the box.</param>
        /// <param name="force">When <see langword="true"/>, skip hit-target point picking.</param>
        /// <returns>A two-element <c>[x, y]</c> array in CSS pixels.</returns>
        internal static async Task<double[]> PointAsync(IElementHandle handle, Position position, bool? force)
        {
            if (position != null)
            {
                await ScrollOffsetIntoViewAsync(handle, position).ConfigureAwait(false);
            }

            ClickOffset offset = position == null ? null : new ClickOffset { X = position.X, Y = position.Y };
            string pointScript = force == true ? PointFunction : PickPointFunction;
            double[] point = offset == null
                ? await handle.EvaluateAsync<double[]>(pointScript).ConfigureAwait(false)
                : await handle.EvaluateAsync<double[]>(pointScript, offset).ConfigureAwait(false);

            if (point == null || point.Length < 2)
            {
                throw new PlaywrightNativeException("Unable to compute a click point for the element.");
            }

            return await MapToPageAsync(handle, point).ConfigureAwait(false);
        }

        /// <summary>
        /// Maps the raw input modifier set to key names for
        /// <see cref="RunModifiersAsync"/>.
        /// </summary>
        /// <param name="pressed">Currently held raw modifiers.</param>
        /// <returns>Key names such as <c>Shift</c>.</returns>
        internal static IReadOnlyCollection<string> HeldKeys(IEnumerable<Input.KeyboardModifier> pressed)
        {
            List<string> held = new List<string>();
            if (pressed == null)
            {
                return held;
            }

            foreach (Input.KeyboardModifier modifier in pressed)
            {
                if (modifier.HasFlag(Input.KeyboardModifier.Alt))
                {
                    held.Add("Alt");
                }

                if (modifier.HasFlag(Input.KeyboardModifier.Control))
                {
                    held.Add("Control");
                }

                if (modifier.HasFlag(Input.KeyboardModifier.Meta))
                {
                    held.Add("Meta");
                }

                if (modifier.HasFlag(Input.KeyboardModifier.Shift))
                {
                    held.Add("Shift");
                }
            }

            return held;
        }

        private static Task WaitActionableAsync(IElementHandle handle, float? timeout, bool? trial, Position position)
            => WaitActionableAsync(handle, timeout, trial, position, scroll: default);

        private static async Task WaitActionableAsync(IElementHandle handle, float? timeout, bool? trial, Position position, ActionScroll scroll)
        {
            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();

            // Official _retryPointerAction: try the current scroll first (no
            // movement when already visible), then end/center/start to escape
            // position:sticky overlays that cover the viewport center.
            string[] alignments = { "end", "center", "start", "nearest" };
            int retry = 0;
            double[] lastBox = null;
            int stableHits = 0;
            StringBuilder log = new StringBuilder();
            log.Append("Call log:\n");
            string resolved = await ResolveMultiplePreviewAsync(handle).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(resolved))
            {
                log.Append("  - ");
                log.Append(resolved);
                log.Append('\n');
            }

            int waitingForCount = 0;
            int hitLogs = 0;
            int unstableLogs = 0;
            bool hoverForHandlers = false;

            while (true)
            {
                ThrowIfAborted(ApiName.Value ?? "elementHandle.click", log);
                if (retry == 0)
                {
                    log.Append("  - attempting click action\n");
                    log.Append("    - waiting for element to be visible, enabled and stable\n");
                }
                else if (retry == 1)
                {
                    log.Append("  - retrying click action\n");
                    log.Append("    - waiting for element to be visible, enabled and stable\n");
                }

                waitingForCount++;

                IPage handlerPage = await TryGetPageAsync(handle).ConfigureAwait(false);
                if (LocatorHandlers.ShouldHover(handlerPage))
                {
                    hoverForHandlers = true;
                }

                await RunLocatorHandlersAsync(handle, timeoutMs, sw, trial, log).ConfigureAwait(false);

                if (!await IsConnectedAsync(handle).ConfigureAwait(false))
                {
                    throw new PlaywrightNativeException(NotAttachedMessage);
                }

                bool visible = false;
                try
                {
                    visible = await handle.EvaluateAsync<bool>(IsVisibleFunction).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException ex) when (ClosedTarget.IsClosed(ex))
                {
                    throw;
                }
                catch (PlaywrightNativeException)
                {
                    if (!await IsConnectedAsync(handle).ConfigureAwait(false))
                    {
                        throw new PlaywrightNativeException(NotAttachedMessage);
                    }
                }

                string disabledKind = "ok";
                try
                {
                    disabledKind = await handle.EvaluateAsync<string>(IsDisabledFunction).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException ex) when (ClosedTarget.IsClosed(ex))
                {
                    throw;
                }
                catch (PlaywrightNativeException)
                {
                    if (!await IsConnectedAsync(handle).ConfigureAwait(false))
                    {
                        throw new PlaywrightNativeException(NotAttachedMessage);
                    }
                }

                if (disabledKind == "detached")
                {
                    throw new PlaywrightNativeException(NotAttachedMessage);
                }

                bool disabled = disabledKind == "disabled";
                if (retry <= 1 && !visible)
                {
                    log.Append("    - element is not visible\n");
                }

                if (disabled)
                {
                    log.Append("    - element is not enabled\n");
                }

                bool stable = false;
                double[] box = null;
                try
                {
                    box = await handle.EvaluateAsync<double[]>(BoxFunction).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }

                bool boxMoved = lastBox != null && (box == null || box.Length < 4 || lastBox.Length < 4
                    || box[0] != lastBox[0] || box[1] != lastBox[1] || box[2] != lastBox[2] || box[3] != lastBox[3]);

                if (box != null && box.Length >= 4 && lastBox != null && lastBox.Length >= 4
                    && box[0] == lastBox[0] && box[1] == lastBox[1] && box[2] == lastBox[2] && box[3] == lastBox[3])
                {
                    stableHits++;
                    if (stableHits >= 2)
                    {
                        stable = true;
                    }
                }
                else
                {
                    stableHits = 0;
                }

                lastBox = box;

                if (visible && !disabled && boxMoved && unstableLogs < 2)
                {
                    log.Append("    - element is not stable\n");
                    unstableLogs++;
                }

                if (visible && !disabled && stable)
                {
                    if (position != null && scroll != ActionScroll.None)
                    {
                        await ScrollOffsetIntoViewAsync(handle, position).ConfigureAwait(false);
                    }

                    if (scroll == ActionScroll.None)
                    {
                        await ThrowIfForceBlockedAsync(handle).ConfigureAwait(false);
                    }

                    await HoverForLocatorHandlersAsync(handle, position, hoverForHandlers).ConfigureAwait(false);

                    string hit = await HitAtPickAsync(handle, position).ConfigureAwait(false);
                    if (hit == "detached")
                    {
                        throw new PlaywrightNativeException(NotAttachedMessage);
                    }

                    if (hit == "ok")
                    {
                        return;
                    }

                    bool hitDescribed = !string.IsNullOrEmpty(hit) && hit != "blocked";
                    if (hitDescribed && hitLogs < 2)
                    {
                        log.Append("    - ");
                        log.Append(hit);
                        log.Append('\n');
                        log.Append("    - waiting 500ms\n");
                        hitLogs++;
                    }

                    if (scroll != ActionScroll.None)
                    {
                        string align = alignments[retry % alignments.Length];
                        try
                        {
                            await handle.EvaluateAsync<bool>(ScrollAlignedFunction, align).ConfigureAwait(false);
                        }
                        catch (PlaywrightNativeException)
                        {
                        }
                    }

                    if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                    {
                        if (waitingForCount > 1)
                        {
                            log.Append("    ");
                            log.Append(waitingForCount.ToString(CultureInfo.InvariantCulture));
                            log.Append(" × waiting for element to be visible, enabled and stable\n");
                        }

                        throw new TimeoutException(TimeoutMessage(timeoutMs, trial) + "\n" + log);
                    }

                    retry++;
                    if (hitDescribed)
                    {
                        await DelayOrAbortAsync(500).ConfigureAwait(false);
                    }
                    else
                    {
                        await RafAsync(handle).ConfigureAwait(false);
                    }

                    continue;
                }

                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    if (waitingForCount > 1)
                    {
                        log.Append("    ");
                        log.Append(waitingForCount.ToString(CultureInfo.InvariantCulture));
                        log.Append(" × waiting for element to be visible, enabled and stable\n");
                    }

                    throw new TimeoutException(TimeoutMessage(timeoutMs, trial) + "\n" + log);
                }

                retry++;
                await RafAsync(handle).ConfigureAwait(false);
            }
        }

        private static async Task ScrollIntoViewAsync(IElementHandle handle)
        {
            try
            {
                await handle.EvaluateAsync<bool>(ScrollIntoViewFunction).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }
        }

        private static async Task ScrollOwnerFramesIntoViewAsync(IElementHandle handle)
        {
            IFrame owner = null;
            try
            {
                owner = await handle.OwnerFrameAsync().ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                return;
            }

            while (owner != null && owner.ParentFrame != null)
            {
                IElementHandle frameElement = null;
                try
                {
                    frameElement = await owner.FrameElementAsync().ConfigureAwait(false);
                    await frameElement.EvaluateAsync<bool>(ScrollFrameIfOffscreenFunction).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }
                finally
                {
                    if (frameElement != null)
                    {
                        await frameElement.DisposeAsync().ConfigureAwait(false);
                    }
                }

                owner = owner.ParentFrame;
            }
        }

        private static async Task ScrollOffsetIntoViewAsync(IElementHandle handle, Position position)
        {
            try
            {
                await handle.EvaluateAsync<bool>(
                    ScrollOffsetIntoViewFunction,
                    new ClickOffset { X = position.X, Y = position.Y }).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }
        }

        private static async Task<string> HitAtPickAsync(IElementHandle handle, Position position)
        {
            double[] localPoint = null;
            try
            {
                ClickOffset offset = position == null ? null : new ClickOffset { X = position.X, Y = position.Y };
                localPoint = offset == null
                    ? await handle.EvaluateAsync<double[]>(PointFunction).ConfigureAwait(false)
                    : await handle.EvaluateAsync<double[]>(PointFunction, offset).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                return "blocked";
            }

            if (localPoint == null || localPoint.Length < 2)
            {
                return "blocked";
            }

            return await HitAtLocalPointAsync(handle, localPoint).ConfigureAwait(false);
        }

        private static async Task<string> HitAtLocalPointAsync(IElementHandle handle, double[] localPoint)
        {
            string localHit;
            try
            {
                localHit = await handle.EvaluateAsync<string>(HitTargetFunction, localPoint).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                return "blocked";
            }

            if (localHit != "ok")
            {
                return localHit;
            }

            return await HitParentFramesAsync(handle, localPoint).ConfigureAwait(false);
        }

        private static async Task<string> HitParentFramesAsync(IElementHandle handle, double[] framePoint)
        {
            double[] point = framePoint;
            IFrame owner = null;
            try
            {
                owner = await handle.OwnerFrameAsync().ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                return "ok";
            }

            while (owner != null && owner.ParentFrame != null)
            {
                IElementHandle frameElement = null;
                try
                {
                    frameElement = await owner.FrameElementAsync().ConfigureAwait(false);
                    string style = await frameElement.EvaluateAsync<string>(DescribeIFrameStyleFunction).ConfigureAwait(false);
                    if (style == "error:notconnected")
                    {
                        return "detached";
                    }

                    if (style == "transformed")
                    {
                        return "ok";
                    }

                    double[] mapped = await frameElement.EvaluateAsync<double[]>(MapIFramePointFunction, point).ConfigureAwait(false);
                    if (mapped == null || mapped.Length < 2)
                    {
                        return "ok";
                    }

                    string parentHit = await frameElement.EvaluateAsync<string>(HitTargetFunction, mapped).ConfigureAwait(false);
                    if (parentHit != "ok")
                    {
                        return parentHit;
                    }

                    point = mapped;
                }
                catch (PlaywrightNativeException)
                {
                    return "ok";
                }
                finally
                {
                    if (frameElement != null)
                    {
                        await frameElement.DisposeAsync().ConfigureAwait(false);
                    }
                }

                owner = owner.ParentFrame;
            }

            return "ok";
        }

        private static async Task<double[]> MapToPageAsync(IElementHandle handle, double[] framePoint)
        {
            double[] point = framePoint;
            IFrame owner = null;
            try
            {
                owner = await handle.OwnerFrameAsync().ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }

            while (owner != null && owner.ParentFrame != null)
            {
                IElementHandle frameElement = null;
                try
                {
                    frameElement = await owner.FrameElementAsync().ConfigureAwait(false);
                    double[] mapped = await frameElement.EvaluateAsync<double[]>(MapIFramePointFunction, point).ConfigureAwait(false);
                    if (mapped != null && mapped.Length >= 2)
                    {
                        point = mapped;
                    }
                    else
                    {
                        (double offsetX, double offsetY) = await BoundingBoxHelper.OwnerFrameOffsetAsync(owner).ConfigureAwait(false);
                        point = new[] { point[0] + offsetX, point[1] + offsetY };
                        break;
                    }
                }
                catch (PlaywrightNativeException)
                {
                    (double offsetX, double offsetY) = await BoundingBoxHelper.OwnerFrameOffsetAsync(owner).ConfigureAwait(false);
                    return new[] { point[0] + offsetX, point[1] + offsetY };
                }
                finally
                {
                    if (frameElement != null)
                    {
                        await frameElement.DisposeAsync().ConfigureAwait(false);
                    }
                }

                owner = owner.ParentFrame;
            }

            return point;
        }

        private static async Task<bool> IsConnectedAsync(IElementHandle handle)
        {
            try
            {
                return await handle.EvaluateAsync<bool>(IsConnectedFunction).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException ex) when (ClosedTarget.IsClosed(ex))
            {
                throw;
            }
            catch (PlaywrightNativeException)
            {
                return false;
            }
        }

        private static async Task RunLocatorHandlersAsync(
            IElementHandle handle,
            int timeoutMs,
            Stopwatch sw,
            bool? trial,
            StringBuilder log)
        {
            IPage page = await TryGetPageAsync(handle).ConfigureAwait(false);
            if (page == null)
            {
                return;
            }

            float? remaining = RemainingTimeout(timeoutMs, sw);
            Task run = LocatorHandlers.RunAsync(page, remaining);
            if (timeoutMs == Timeout.Infinite)
            {
                await run.ConfigureAwait(false);
                return;
            }

            int waitMs = remaining.HasValue && remaining.Value > 0 ? (int)remaining.Value : 1;
            Task delay = Task.Delay(waitMs);
            Task finished = await Task.WhenAny(run, delay).ConfigureAwait(false);
            if (finished == run || run.IsCompleted)
            {
                await run.ConfigureAwait(false);
                return;
            }

            throw new TimeoutException(TimeoutMessage(timeoutMs, trial) + "\n" + log);
        }

        private static async Task HoverForLocatorHandlersAsync(IElementHandle handle, Position position, bool enabled)
        {
            if (!enabled)
            {
                return;
            }

            IPage page = await TryGetPageAsync(handle).ConfigureAwait(false);
            if (page?.Mouse == null)
            {
                return;
            }

            try
            {
                double[] point = await PointAsync(handle, position, force: null).ConfigureAwait(false);
                if (point != null && point.Length >= 2)
                {
                    await page.Mouse.MoveAsync((float)point[0], (float)point[1]).ConfigureAwait(false);
                }
            }
            catch (PlaywrightNativeException)
            {
            }
        }

        private static async Task<IPage> TryGetPageAsync(IElementHandle handle)
        {
            if (handle == null)
            {
                return null;
            }

            try
            {
                IFrame owner = await handle.OwnerFrameAsync().ConfigureAwait(false);
                return owner?.Page;
            }
            catch (PlaywrightNativeException)
            {
                return null;
            }
        }

        private static float? RemainingTimeout(int timeoutMs, Stopwatch sw)
        {
            if (timeoutMs == Timeout.Infinite)
            {
                return 0;
            }

            long left = timeoutMs - sw.ElapsedMilliseconds;
            return left < 1 ? 1 : left;
        }

        private static async Task<string> ResolveMultiplePreviewAsync(IElementHandle handle)
        {
            string stored = ResolvedLog.Value;
            if (!string.IsNullOrEmpty(stored))
            {
                return stored;
            }

            IReadOnlyList<string> progress = ActionProgress.Snapshot();
            if (progress.Count > 0)
            {
                return progress[0];
            }

            if (handle == null)
            {
                return null;
            }

            try
            {
                int count = await handle.EvaluateAsync<int>(@"el => {
    const text = String((el && el.textContent) || '').replace(/\s+/g, ' ').trim();
    const tag = el && el.tagName;
    if (!tag || !el.ownerDocument) return 1;
    const all = el.ownerDocument.getElementsByTagName(tag);
    let n = 0;
    for (let i = 0; i < all.length; i++) {
      const raw = String(all[i].textContent || '').replace(/\s+/g, ' ').trim();
      if (raw === text) n++;
    }
    return n;
}").ConfigureAwait(false);
                if (count < 2)
                {
                    return null;
                }

                string preview = await handle.EvaluateAsync<string>(RemoteObject.PreviewNodeFunction).ConfigureAwait(false);
                if (string.IsNullOrEmpty(preview))
                {
                    preview = "element";
                }

                return "locator resolved to " +
                    count.ToString(CultureInfo.InvariantCulture) +
                    " elements. Proceeding with the first one: " +
                    preview;
            }
            catch (PlaywrightNativeException)
            {
                return null;
            }
        }

        private static string TimeoutMessage(int timeoutMs, bool? trial)
        {
            string suffix = trial == true ? "click action (trial run)" : "click action";
            string apiName = ApiName.Value;
            if (string.IsNullOrEmpty(apiName))
            {
                apiName = "elementHandle.click";
            }

            return apiName + ": Timeout " + timeoutMs.ToString(CultureInfo.InvariantCulture) + "ms exceeded. waiting for " + suffix;
        }

        private static async Task RafAsync(IElementHandle handle)
        {
            // Do not wait on in-page requestAnimationFrame: after window.open
            // Chromium may pause rAF (and timers) on the background opener,
            // which would stall Runtime.evaluate forever and skip the action
            // timeout. Host-side delay still spaces stability samples.
            _ = handle;
            await DelayOrAbortAsync(16).ConfigureAwait(false);
        }

        private static void ThrowIfAborted(string apiName, StringBuilder log = null)
        {
            AbortSignal signal = ActiveSignal.Value;
            if (signal == null || !signal.Aborted)
            {
                return;
            }

            throw AbortError.InFlight(
                string.IsNullOrEmpty(apiName) ? "elementHandle.click" : apiName,
                signal,
                log?.ToString());
        }

        private static Task DelayOrAbortAsync(int milliseconds)
        {
            AbortSignal signal = ActiveSignal.Value;
            if (signal == null)
            {
                return Task.Delay(milliseconds);
            }

            if (signal.Aborted)
            {
                return Task.CompletedTask;
            }

            return Task.WhenAny(Task.Delay(milliseconds), signal.WhenAbortedAsync());
        }

        private static string ModifierKey(KeyboardModifier modifier)
            => modifier switch
            {
                KeyboardModifier.Alt => "Alt",
                KeyboardModifier.Control => "Control",
                KeyboardModifier.ControlOrMeta => OperatingSystem.IsMacOS() ? "Meta" : "Control",
                KeyboardModifier.Meta => "Meta",
                KeyboardModifier.Shift => "Shift",
                _ => null,
            };

        private static async Task ThrowIfForceBlockedAsync(IElementHandle handle)
        {
            string result;
            try
            {
                result = await handle.EvaluateAsync<string>(ClassifyFunction).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                throw new PlaywrightNativeException(NotAttachedMessage);
            }

            if (result == "notconnected")
            {
                throw new PlaywrightNativeException(NotAttachedMessage);
            }

            if (result == "notvisible")
            {
                throw new PlaywrightNativeException(NotVisibleMessage);
            }

            if (result == "notinviewport")
            {
                throw new PlaywrightNativeException(OutsideViewportMessage + "\nelement is outside of the viewport");
            }
        }

        private sealed class ClickOffset
        {
            public float X { get; set; }

            public float Y { get; set; }
        }
    }
}
