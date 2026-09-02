/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// CSS query helpers that pierce open shadow roots, matching Playwright locator
    /// behavior for selectors such as <c>shadow-element &gt; .editor</c>.
    /// </summary>
    internal static class ShadowPiercingQuery
    {
        /// <summary>
        /// Function declaration: <c>(selector) =&gt; Element | null</c>.
        /// Tries the light DOM first, then open shadow trees (including host &gt; child).
        /// </summary>
        internal const string QueryFunction = @"selector => {
  const toArray = (list) => { const out = []; if (!list) return out; for (let i = 0; i < list.length; i++) out.push(list[i]); return out; };
  const nthParsed = (() => {
    const idx = selector.lastIndexOf('>>');
    if (idx < 0) return { sel: selector, nth: null };
    const tail = selector.slice(idx + 2).trim();
    const m = /^nth\s*=\s*(-?\d+)$/.exec(tail);
    return m ? { sel: selector.slice(0, idx).trim(), nth: Number(m[1]) } : { sel: selector, nth: null };
  })();
  selector = nthParsed.sel;
  if (nthParsed.nth != null) {
    const listed = (() => { try { return toArray(document.querySelectorAll(selector)); } catch (e) { return []; } })();
    const i = nthParsed.nth < 0 ? listed.length + nthParsed.nth : nthParsed.nth;
    return listed[i] || null;
  }
  const tryOne = (root, sel) => { try { return root.querySelector(sel); } catch (e) { return null; } };
  const cross = (host, sel) => {
    if (!host.shadowRoot) return null;
    let prefix = '';
    let rest = '';
    let direct = false;
    const gt = sel.indexOf('>');
    if (gt > 0) {
      prefix = sel.slice(0, gt).trim();
      rest = sel.slice(gt + 1).trim();
      direct = true;
    } else {
      const sp = sel.search(/\s+/);
      if (sp < 0) return null;
      prefix = sel.slice(0, sp).trim();
      rest = sel.slice(sp).trim();
    }
    if (!prefix || !rest) return null;
    try { if (!host.matches(prefix)) return null; } catch (e) { return null; }
    try {
      if (direct) {
        const kids = toArray(host.shadowRoot.children);
        for (const kid of kids) {
          if (kid.matches(rest)) return kid;
        }
        return null;
      }
      return host.shadowRoot.querySelector(rest);
    } catch (e) { return null; }
  };
  const walk = (root) => {
    let hit = tryOne(root, selector);
    if (hit) return hit;
    const els = root.querySelectorAll ? root.querySelectorAll('*') : [];
    for (const el of els) {
      if (!el.shadowRoot) continue;
      hit = tryOne(el.shadowRoot, selector) || cross(el, selector) || walk(el.shadowRoot);
      if (hit) return hit;
    }
    return null;
  };
  const found = walk(document);
  if (found) return found;
  const idMatch = /^#([A-Za-z_][\\w-]*)$/.exec(String(selector || '').trim());
  if (idMatch) {
    const low = idMatch[1].toLowerCase();
    const all = document.querySelectorAll('[id]');
    for (let i = 0; i < all.length; i++) {
      if (String(all[i].id || '').toLowerCase() === low) return all[i];
    }
  }
  return null;
}";

        /// <summary>
        /// Function declaration: <c>(selector) =&gt; Element[]</c>.
        /// Light-DOM matches win when present; otherwise open shadow trees are searched.
        /// </summary>
        internal const string QueryAllFunction = @"selector => {
  const toArray = (list) => { const out = []; if (!list) return out; for (let i = 0; i < list.length; i++) out.push(list[i]); return out; };
  const nthParsed = (() => {
    const idx = selector.lastIndexOf('>>');
    if (idx < 0) return { sel: selector, nth: null };
    const tail = selector.slice(idx + 2).trim();
    const m = /^nth\s*=\s*(-?\d+)$/.exec(tail);
    return m ? { sel: selector.slice(0, idx).trim(), nth: Number(m[1]) } : { sel: selector, nth: null };
  })();
  selector = nthParsed.sel;
  const tryAll = (root, sel) => { try { return toArray(root.querySelectorAll(sel)); } catch (e) { return []; } };
  const cross = (host, sel) => {
    if (!host.shadowRoot) return null;
    let prefix = '';
    let rest = '';
    let direct = false;
    const gt = sel.indexOf('>');
    if (gt > 0) {
      prefix = sel.slice(0, gt).trim();
      rest = sel.slice(gt + 1).trim();
      direct = true;
    } else {
      const sp = sel.search(/\s+/);
      if (sp < 0) return null;
      prefix = sel.slice(0, sp).trim();
      rest = sel.slice(sp).trim();
    }
    if (!prefix || !rest) return null;
    try { if (!host.matches(prefix)) return null; } catch (e) { return null; }
    try {
      if (direct) {
        const kids = toArray(host.shadowRoot.children);
        for (const kid of kids) {
          if (kid.matches(rest)) return kid;
        }
        return null;
      }
      return host.shadowRoot.querySelector(rest);
    } catch (e) { return null; }
  };
  const pickNth = (list) => {
    if (nthParsed.nth == null) return list;
    const i = nthParsed.nth < 0 ? list.length + nthParsed.nth : nthParsed.nth;
    const hit = list[i];
    return hit ? [hit] : [];
  };
  const light = tryAll(document, selector);
  if (light.length) return pickNth(light);
  const idMatch = /^#([A-Za-z_][\\w-]*)$/.exec(String(selector || '').trim());
  if (idMatch) {
    const low = idMatch[1].toLowerCase();
    const byId = [];
    const all = document.querySelectorAll('[id]');
    for (let i = 0; i < all.length; i++) {
      if (String(all[i].id || '').toLowerCase() === low) byId.push(all[i]);
    }
    if (byId.length) return pickNth(byId);
  }
  const out = [];
  const seen = new Set();
  const add = (el) => { if (el && !seen.has(el)) { seen.add(el); out.push(el); } };
  const walk = (root) => {
    if (!root) return;
    for (const el of tryAll(root, selector)) add(el);
    const els = root.querySelectorAll ? root.querySelectorAll('*') : [];
    for (const el of els) {
      if (!el.shadowRoot) continue;
      for (const hit of tryAll(el.shadowRoot, selector)) add(hit);
      add(cross(el, selector));
      walk(el.shadowRoot);
    }
  };
  walk(document);
  return pickNth(out);
}";
    }
}
