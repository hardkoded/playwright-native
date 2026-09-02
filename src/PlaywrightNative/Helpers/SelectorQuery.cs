/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official Playwright selector-chain helpers (<c>&gt;&gt;</c>, <c>*css=</c>,
    /// <c>xpath=</c>) and locator option application.
    /// </summary>
    internal static class SelectorQuery
    {
        /// <summary>
        /// Engine that understands <c>div &gt;&gt; p</c>, <c>*css=div &gt;&gt; p</c>,
        /// <c>css=</c>, and <c>xpath=</c>.
        /// </summary>
        internal const string ChainEngineScript = @"(() => {
  const dq = String.fromCharCode(34);
  const sq = String.fromCharCode(39);
  const splitChain = (selector) => {
    const parts = [];
    let current = '';
    let quote = null;
    for (let i = 0; i < selector.length; i++) {
      const c = selector[i];
      if (quote) {
        if (c === '\\' && i + 1 < selector.length) {
          current += c + selector[++i];
          continue;
        }
        if (c === quote) quote = null;
        current += c;
        continue;
      }
      if (c === dq || c === sq || c === '`') {
        const trimmed = current.replace(/^\s+/, '');
        const afterEq = /=\s*$/.test(current);
        const afterOpen = /\(\s*$/.test(current);
        if (!trimmed.length || afterEq || afterOpen) quote = c;
        current += c;
        continue;
      }
      if (c === '>' && selector[i + 1] === '>') {
        parts.push(current.trim());
        current = '';
        i++;
        continue;
      }
      current += c;
    }
    if (current.trim()) parts.push(current.trim());
    return parts;
  };

  const customEngines = {};
  const official = { css: 1, xpath: 1, text: 1, 'text:light': 1, id: 1, 'data-test': 1, 'data-testid': 1, 'data-test-id': 1, nth: 1, visible: 1, 'internal:has': 1, 'internal:has-not': 1, 'internal:and': 1, 'internal:or': 1, 'internal:chain': 1, 'id:light': 1, 'data-test:light': 1, 'data-testid:light': 1, 'data-test-id:light': 1, 'css:light': 1, 'xpath:light': 1, role: 1, 'internal:role': 1, 'aria-ref': 1 };
  const cssEscapeAttr = (s) => String(s).replace(/\\/g, '\\\\').replace(new RegExp(dq, 'g'), '\\' + dq);

  const parsePart = (part) => {
    let capture = false;
    let rest = (part || '').trim();
    if (rest.charAt(0) === '*' && rest.length > 1) {
      const after = rest.slice(1);
      const cap = /^([a-zA-Z_][\w:-]*)\s*=/.exec(after);
      if (cap && official[cap[1]]) {
        capture = true;
        rest = after.trim();
      }
    }
    if (capture && (rest.charAt(0) === '=' || !rest))
      throw new Error('Unknown engine ' + dq + dq + ' while parsing selector ' + part);
    if (rest === dq || rest === sq)
      throw new Error('Unclosed quote in selector');
    if (rest.length >= 2) {
      const q = rest.charAt(0);
      if ((q === dq || q === sq) && rest.charAt(rest.length - 1) === q)
        return { capture, engine: 'text', body: rest };
    }
    // Official auto-detect: //, leading parentheses then //, or .. (parent).
    if (rest.indexOf('..') === 0 || /^\(*\/\//.test(rest))
      return { capture, engine: 'xpath', body: rest };
    let engine = 'css';
    let body = rest;
    const m = /^([a-zA-Z_][\w:-]*)\s*=([\s\S]*)$/.exec(rest);
    if (m) {
      engine = m[1];
      body = m[2];
      if (!official[engine] && !customEngines[engine])
        throw new Error('Unknown engine ' + dq + engine + dq + ' while parsing selector ' + part);
    }
    return { capture, engine, body };
  };

  const parseHasText = (sel) => {
    const texts = [];
    let rest = String(sel || '');
    const needle = ':has-text(';
    while (true) {
      const idx = rest.lastIndexOf(needle);
      if (idx < 0) break;
      let after = rest.slice(idx + needle.length);
      while (after.charAt(0) === ' ') after = after.slice(1);
      const q = after.charAt(0);
      if (q !== dq && q !== sq) break;
      let i = 1;
      let text = '';
      while (i < after.length) {
        const c = after.charAt(i);
        if (c === '\\' && i + 1 < after.length) {
          text += after.charAt(i + 1);
          i += 2;
          continue;
        }
        if (c === q) break;
        text += c;
        i++;
      }
      let close = after.slice(i + 1);
      while (close.charAt(0) === ' ') close = close.slice(1);
      if (close.charAt(0) !== ')') break;
      texts.push(text);
      rest = rest.slice(0, idx) || '*';
    }
    return { css: rest, texts: texts };
  };

  const elementHasText = (el, needle) => {
    const raw = (el && (el.innerText || el.textContent)) || '';
    return normalize(raw).toLowerCase().indexOf(normalize(needle).toLowerCase()) !== -1;
  };

  const tryAllCss = (r, sel) => {
    try { return Array.from((r.querySelectorAll && r.querySelectorAll(sel)) || []); }
    catch (e) {
      if (String(sel).indexOf('!') >= 0)
        throw new Error('Unexpected token ' + dq + '!' + dq + ' while parsing css selector ' + dq + sel + dq);
      if (String(sel).indexOf(']') >= 0 && String(sel).indexOf('[') < 0)
        throw new Error('Unexpected token ' + dq + ']' + dq + ' while parsing css selector ' + dq + sel + dq);
      if (String(sel).indexOf('##') >= 0)
        throw new Error('Unexpected token ' + dq + '#' + dq + ' while parsing css selector ' + dq + sel + dq + '. Did you mean to CSS.escape it?');
      return [];
    }
  };

  const matchesSimple = (el, sel) => {
    if (!el || el.nodeType !== 1) return false;
    try { return el.matches(sel); } catch (e) { return false; }
  };

  const queryIdCI = (root, id) => {
    const low = String(id || '').toLowerCase();
    const out = [];
    const seen = new Set();
    const consider = (el) => {
      if (el && el.nodeType === 1 && !seen.has(el) && String(el.id || '').toLowerCase() === low) {
        seen.add(el);
        out.push(el);
      }
    };
    const walk = (r) => {
      if (!r) return;
      if (r.nodeType === 1) consider(r);
      const els = r.querySelectorAll ? r.querySelectorAll('[id]') : [];
      for (let i = 0; i < els.length; i++) consider(els[i]);
      const all = r.querySelectorAll ? r.querySelectorAll('*') : [];
      for (let i = 0; i < all.length; i++) {
        if (all[i].shadowRoot) walk(all[i].shadowRoot);
      }
      if (r.shadowRoot) walk(r.shadowRoot);
    };
    walk(root);
    return out;
  };

  const pierceSimple = (root, sel) => {
    const parsedFns = parseTextFns(sel);
    const cssSel = parsedFns.css || '*';
    const out = [];
    const seen = new Set();
    const add = (els) => {
      for (let i = 0; i < els.length; i++) {
        const el = els[i];
        if (el && el.nodeType === 1 && !seen.has(el) && parsedFns.fns.every((fn) => matchTextFn(el, fn))) { seen.add(el); out.push(el); }
      }
    };
    add(tryAllCss(root, cssSel));
    const walk = (r) => {
      if (!r) return;
      if (r.shadowRoot) {
        add(tryAllCss(r.shadowRoot, cssSel));
        walk(r.shadowRoot);
      }
      const els = r.querySelectorAll ? r.querySelectorAll('*') : [];
      for (let i = 0; i < els.length; i++) {
        if (!els[i].shadowRoot) continue;
        add(tryAllCss(els[i].shadowRoot, cssSel));
        walk(els[i].shadowRoot);
      }
    };
    walk(root);
    if (!out.length) {
      const m = /^#([A-Za-z_][\\w-]*)$/.exec(String(cssSel || '').trim());
      if (m) add(queryIdCI(root, m[1]));
    }
    return out;
  };

  const splitCssSteps = (sel) => {
    const steps = [];
    let current = '';
    let depth = 0;
    let quote = null;
    let combo = null;
    const flush = (next) => {
      const t = current.trim();
      if (t) steps.push({ combo: combo, simple: t });
      current = '';
      combo = next;
    };
    for (let i = 0; i < sel.length; i++) {
      const c = sel[i];
      if (quote) {
        current += c;
        if (c === '\\' && i + 1 < sel.length) { current += sel[++i]; continue; }
        if (c === quote) quote = null;
        continue;
      }
      if (c === dq || c === sq) { quote = c; current += c; continue; }
      if (c === '(') depth++;
      if (c === ')') depth--;
      if (c === '[') depth++;
      if (c === ']') depth--;
      if (depth === 0) {
        if (c === '>') { flush('>'); continue; }
        if (c === '+') { flush('+'); continue; }
        if (c === '~' && sel[i + 1] !== '=') { flush('~'); continue; }
        if (c === ' ' || c === '\t' || c === '\n' || c === '\r') {
          let k = i;
          while (k < sel.length && /[ \\t\\n\\r]/.test(sel[k])) k++;
          const n = sel[k];
          if (n === '>' || n === '+' || (n === '~' && sel[k + 1] !== '=')) {
            i = k - 1;
            continue;
          }
          if (current.trim()) flush(' ');
          i = k - 1;
          continue;
        }
      }
      current += c;
    }
    const t = current.trim();
    if (t) steps.push({ combo: combo, simple: t });
    return steps;
  };

  const scopeElement = (root) => {
    if (!root) return null;
    if (root.nodeType === 9) return root.documentElement;
    if (root.nodeType === 1) return root;
    return null;
  };

  const matchesScopeSimple = (el, simple, scopeRoot) => {
    const scopeEl = scopeElement(scopeRoot);
    if (!el || !scopeEl || el !== scopeEl) return false;
    const rest = String(simple || '').replace(/:scope\b/g, '').trim();
    if (!rest) return true;
    return matchesSimple(el, rest);
  };

  const queryCss = (root, body) => {
    try {
      if (!root) return [];
      const css = absolutize(body) || '*';
      const commaParts = splitCommaArgs(css);
      if (commaParts.length > 1) {
        const seen = new Set();
        const union = [];
        for (let p = 0; p < commaParts.length; p++) {
          const hits = queryCss(root, commaParts[p]);
          for (let h = 0; h < hits.length; h++) {
            if (hits[h] && !seen.has(hits[h])) { seen.add(hits[h]); union.push(hits[h]); }
          }
        }
        return sortInDomOrder(union);
      }
      const steps = splitCssSteps(css);
      if (!steps.length) return [];
      const hasScope = css.indexOf(':scope') >= 0;
      const matchOne = (el, simple) => {
        const parsedFns = parseTextFns(simple);
        const css = parsedFns.css || '*';
        const ok = simple.indexOf(':scope') >= 0 ? matchesScopeSimple(el, css, root) : matchesSimple(el, css);
        if (!ok) return false;
        return parsedFns.fns.every((fn) => matchTextFn(el, fn));
      };
      const isScopeSubject = (simple) => {
        if (simple.indexOf(':scope') < 0 || simple.indexOf(':not(') >= 0) return false;
        return simple.indexOf(':scope') === 0 || /^[a-zA-Z][\w-]*:scope\b/.test(simple) || /^[#.][^\s:[\]()]*:scope\b/.test(simple);
      };
      const se = scopeElement(root);
      const firstIsScope = isScopeSubject(steps[0].simple);
      let laterScope = false;
      for (let i = 1; i < steps.length; i++) {
        if (steps[i].simple.indexOf(':scope') >= 0) { laterScope = true; break; }
      }
      let current = [];
      if (hasScope && se && steps.length === 1 && (matchesSimple(se, css) || matchOne(se, steps[0].simple))) {
        current = [se];
      } else if (firstIsScope) {
        if (matchOne(se, steps[0].simple)) current = se ? [se] : [];
      } else if (laterScope && root && root.nodeType === 1 && root.parentNode) {
        current = pierceSimple(root.parentNode, steps[0].simple);
      } else {
        current = pierceSimple(root, steps[0].simple);
      }
      for (let s = 1; s < steps.length; s++) {
        const next = [];
        const seen = new Set();
        const add = (el) => { if (el && el.nodeType === 1 && !seen.has(el)) { seen.add(el); next.push(el); } };
        const simple = steps[s].simple;
        const comb = steps[s].combo;
        for (let r = 0; r < current.length; r++) {
          const el = current[r];
          if (comb === '>') {
            const kids = el.children || [];
            for (let k = 0; k < kids.length; k++) {
              if (matchOne(kids[k], simple)) add(kids[k]);
            }
            const shadowKids = el.shadowRoot && el.shadowRoot.children ? el.shadowRoot.children : [];
            for (let k = 0; k < shadowKids.length; k++) {
              if (matchOne(shadowKids[k], simple)) add(shadowKids[k]);
            }
          } else if (comb === '+') {
            const sib = el.nextElementSibling;
            if (matchOne(sib, simple)) add(sib);
          } else if (comb === '~') {
            for (let sib = el.nextElementSibling; sib; sib = sib.nextElementSibling) {
              if (matchOne(sib, simple)) add(sib);
            }
          } else if (simple.indexOf(':scope') >= 0) {
            const se = scopeElement(root);
            if (se && el !== se) {
              let contained = false;
              try { contained = !!(el.contains && el.contains(se)); } catch (e) { contained = false; }
              if (!contained && el.shadowRoot) {
                try { contained = !!(el.shadowRoot.contains && el.shadowRoot.contains(se)); } catch (e) { contained = false; }
              }
              if (contained && matchOne(se, simple)) add(se);
            }
          } else {
            const hits = pierceSimple(el, simple);
            for (let h = 0; h < hits.length; h++) add(hits[h]);
          }
        }
        current = next;
      }
      return current;
    } catch (e) {
      const msg = String(e && e.message ? e.message : e);
      if (msg.indexOf('Unexpected token') === 0 || msg.indexOf('Unknown engine') === 0 || msg.indexOf('Malformed selector:') === 0 || msg.indexOf(dq + 'internal:has' + dq) === 0 || msg.indexOf('engine expects') >= 0 || msg.indexOf('Unclosed quote') === 0)
        throw e;
      return [];
    }
  };

  const queryXPath = (root, body) => {
    const original = String(body || '');
    try {
      const doc = root.nodeType === 9 ? root : root.ownerDocument;
      if (!doc || !doc.evaluate) return [];
      let expr = original;
      if (expr.charAt(0) === '/' && root.nodeType !== 9)
        expr = '.' + expr;
      const result = doc.evaluate(expr, root, null, XPathResult.ORDERED_NODE_SNAPSHOT_TYPE, null);
      const out = [];
      for (let i = 0; i < result.snapshotLength; i++) {
        const n = result.snapshotItem(i);
        if (n && n.nodeType === 1) out.push(n);
      }
      return out;
    } catch (e) {
      throw new Error(String(e && e.message ? e.message : e) + ' ' + original.replace(new RegExp(sq, 'g'), String.fromCharCode(92) + sq));
    }
  };

  const normalize = (s) => String(s || '').replace(/\s+/g, ' ').trim();

  let textCache = new Map();
  function resetTextCache() { textCache = new Map(); }

  function shouldSkipForTextMatching(element) {
    if (!element) return true;
    const doc = element.nodeType === 9 ? element : element.ownerDocument;
    return element.nodeName === 'HEAD' || element.nodeName === 'SCRIPT' || element.nodeName === 'NOSCRIPT' || element.nodeName === 'STYLE' || !!(doc && doc.head && doc.head.contains(element));
  }

  function elementText(root) {
    let value = textCache.get(root);
    if (value !== undefined) return value;
    value = { full: '', normalized: '', immediate: [] };
    if (!shouldSkipForTextMatching(root)) {
      let currentImmediate = '';
      if (root && root.nodeName === 'INPUT' && /^(submit|button|reset)$/i.test(root.type || '')) {
        value = { full: root.value || '', normalized: normalize(root.value), immediate: [root.value || ''] };
      } else {
        for (let child = root.firstChild; child; child = child.nextSibling) {
          if (child.nodeType === 3) {
            value.full += child.nodeValue || '';
            currentImmediate += child.nodeValue || '';
          } else if (child.nodeType === 8) {
            continue;
          } else {
            if (currentImmediate) value.immediate.push(currentImmediate);
            currentImmediate = '';
            if (child.nodeType === 1)
              value.full += elementText(child).full;
          }
        }
        if (currentImmediate) value.immediate.push(currentImmediate);
        if (root.shadowRoot)
          value.full += elementText(root.shadowRoot).full;
        if (value.full)
          value.normalized = normalize(value.full);
      }
    }
    textCache.set(root, value);
    return value;
  }

  function elementMatchesText(element, matcher) {
    if (shouldSkipForTextMatching(element)) return 'none';
    if (!matcher(elementText(element))) return 'none';
    for (let child = element.firstChild; child; child = child.nextSibling) {
      if (child.nodeType === 1 && matcher(elementText(child)))
        return 'selfAndChildren';
    }
    if (element.shadowRoot && matcher(elementText(element.shadowRoot)))
      return 'selfAndChildren';
    return 'self';
  }

  function cssUnquote(s) {
    s = s.substring(1, s.length - 1);
    if (s.indexOf('\\') < 0) return s;
    const r = [];
    let i = 0;
    while (i < s.length) {
      if (s[i] === '\\' && i + 1 < s.length) i++;
      r.push(s[i++]);
    }
    return r.join('');
  }

  function cssUnescapeQuoted(quoted) {
    let s = quoted.slice(1, -1);
    let out = '';
    for (let i = 0; i < s.length; i++) {
      if (s[i] !== '\\' || i + 1 >= s.length) { out += s[i]; continue; }
      const n = s[i + 1];
      if (/[0-9a-fA-F]/.test(n)) {
        let hex = '';
        let j = i + 1;
        while (j < s.length && hex.length < 6 && /[0-9a-fA-F]/.test(s[j])) hex += s[j++];
        if (s[j] === ' ') j++;
        out += String.fromCharCode(parseInt(hex, 16));
        i = j - 1;
        continue;
      }
      out += n;
      i++;
    }
    return out;
  }

  function createTextMatcher(selector, internal) {
    selector = String(selector || '');
    if (selector[0] === '/' && selector.lastIndexOf('/') > 0) {
      const lastSlash = selector.lastIndexOf('/');
      const re = new RegExp(selector.substring(1, lastSlash), selector.substring(lastSlash + 1));
      return { matcher: (et) => { re.lastIndex = 0; return re.test(et.full); }, kind: 'regex' };
    }
    let strict = false;
    if (selector.length > 1 && selector[0] === dq && selector[selector.length - 1] === dq) {
      selector = cssUnquote(selector);
      strict = true;
    } else if (selector.length > 1 && selector[0] === sq && selector[selector.length - 1] === sq) {
      selector = cssUnquote(selector);
      strict = true;
    }
    selector = normalize(selector);
    if (strict) {
      if (internal)
        return { kind: 'strict', matcher: (et) => et.normalized === selector };
      return {
        kind: 'strict',
        matcher: (et) => {
          if (!selector && !et.immediate.length) return true;
          return et.immediate.some((s) => normalize(s) === selector);
        }
      };
    }
    selector = selector.toLowerCase();
    return { kind: 'lax', matcher: (et) => et.normalized.toLowerCase().indexOf(selector) !== -1 };
  }

  function parseCssStringArgs(inner, name) {
    const parts = splitCommaArgs(String(inner || ''));
    const args = [];
    for (let i = 0; i < parts.length; i++) {
      const t = parts[i].trim();
      if (t.length >= 2 && (t[0] === dq || t[0] === sq) && t[t.length - 1] === t[0])
        args.push(cssUnescapeQuoted(t));
      else if (name === 'text-matches')
        throw new Error(dq + 'text-matches' + dq + ' engine expects a regexp body and optional regexp flags');
      else
        throw new Error(dq + name + dq + ' engine expects a single string');
    }
    if ((name === 'text' || name === 'has-text' || name === 'text-is') && args.length !== 1)
      throw new Error(dq + name + dq + ' engine expects a single string');
    if (name === 'text-matches' && (args.length === 0 || args.length > 2))
      throw new Error(dq + 'text-matches' + dq + ' engine expects a regexp body and optional regexp flags');
    return args;
  }

  function parseTextFns(simple) {
    const fns = [];
    let rest = String(simple || '');
    const names = ['text-matches', 'text-is', 'has-text', 'text'];
    let again = true;
    while (again) {
      again = false;
      for (let n = 0; n < names.length; n++) {
        const call = unwrapLastCall(rest, names[n]);
        if (!call || afterHasCombinator(call.after)) continue;
        fns.push({ name: names[n], args: parseCssStringArgs(call.inner, names[n]) });
        rest = compoundAfterPeel(call.before, call.after);
        again = true;
        break;
      }
    }
    return { css: rest || '*', fns: fns };
  }

  function matchTextFn(el, fn) {
    if (!el || el.nodeType !== 1) return false;
    if (fn.name === 'has-text') {
      if (shouldSkipForTextMatching(el)) return false;
      const text = normalize(fn.args[0]).toLowerCase();
      return elementText(el).normalized.toLowerCase().indexOf(text) !== -1;
    }
    if (fn.name === 'text') {
      const text = normalize(fn.args[0]).toLowerCase();
      const matcher = (et) => et.normalized.toLowerCase().indexOf(text) !== -1;
      return elementMatchesText(el, matcher) === 'self';
    }
    if (fn.name === 'text-is') {
      const text = normalize(fn.args[0]);
      const matcher = (et) => {
        if (!text && !et.immediate.length) return true;
        return et.immediate.some((s) => normalize(s) === text);
      };
      return elementMatchesText(el, matcher) !== 'none';
    }
    if (fn.name === 'text-matches') {
      const re = new RegExp(fn.args[0], fn.args[1]);
      const matcher = (et) => { re.lastIndex = 0; return re.test(et.full); };
      return elementMatchesText(el, matcher) === 'self';
    }
    return false;
  }

  const queryText = (root, selector, pierceShadow) => {
    const parsed = createTextMatcher(selector, false);
    const result = [];
    let lastDidNotMatchSelf = null;
    const appendElement = (element) => {
      if (parsed.kind === 'lax' && lastDidNotMatchSelf && lastDidNotMatchSelf.contains && lastDidNotMatchSelf.contains(element))
        return;
      const matches = elementMatchesText(element, parsed.matcher);
      if (matches === 'none') lastDidNotMatchSelf = element;
      if (matches === 'self' || (matches === 'selfAndChildren' && parsed.kind === 'strict'))
        result.push(element);
    };
    if (root && root.nodeType === 1) appendElement(root);
    else if (root && root.nodeType === 9 && root.documentElement) appendElement(root.documentElement);
    const walk = (r) => {
      if (!r) return;
      const kids = r.children || [];
      for (let i = 0; i < kids.length; i++) {
        appendElement(kids[i]);
        walk(kids[i]);
      }
      if (pierceShadow !== false && r.shadowRoot) {
        const sk = r.shadowRoot.children || [];
        for (let i = 0; i < sk.length; i++) {
          appendElement(sk[i]);
          walk(sk[i]);
        }
      }
    };
    if (root) walk(root.nodeType === 9 ? root.documentElement : root);
    return result;
  };

" + RoleSelectorEngine.Functions + @"
  const queryPart = (roots, part) => {
    const parsed = parsePart(part);
    const out = [];
    const seen = new Set();
    for (let r = 0; r < roots.length; r++) {
      const root = roots[r];
      let hits = [];
      if (parsed.engine === 'xpath')
        hits = queryXPath(root, parsed.body);
      else if (parsed.engine === 'text' || parsed.engine === 'text:light')
        hits = queryText(root, parsed.body, parsed.engine !== 'text:light');
      else if (parsed.engine === 'role' || parsed.engine === 'internal:role')
        hits = queryRoleAll(root, parsed.body, parsed.engine === 'internal:role');
      else if (parsed.engine === 'id' || parsed.engine === 'data-test' || parsed.engine === 'data-testid' || parsed.engine === 'data-test-id')
        hits = queryCss(root, '[' + parsed.engine + '=' + dq + cssEscapeAttr(parsed.body) + dq + ']');
      else if (parsed.engine === 'css:light' || parsed.engine === 'xpath:light')
        hits = parsed.engine === 'xpath:light' ? queryXPath(root, parsed.body) : queryCssLight(root, parsed.body);
      else if (parsed.engine === 'id:light' || parsed.engine === 'data-test:light' || parsed.engine === 'data-testid:light' || parsed.engine === 'data-test-id:light')
        hits = queryCssLight(root, '[' + parsed.engine.replace(':light', '') + '=' + dq + cssEscapeAttr(parsed.body) + dq + ']');
      else if (customEngines[parsed.engine]) {
        const custom = customEngines[parsed.engine];
        if (custom.queryAll)
          hits = Array.from(custom.queryAll(root, parsed.body) || []);
        else {
          const one = custom.query(root, parsed.body);
          hits = one ? [one] : [];
        }
      }
      else if (hasCustomPseudo(parsed.body))
        hits = queryCustomCss(root, parsed.body);
      else
        hits = queryCss(root, parsed.body);
      for (let i = 0; i < hits.length; i++) {
        const h = hits[i];
        if (h && !seen.has(h)) {
          seen.add(h);
          out.push(h);
        }
      }
    }
    return out;
  };

  const queryAll = (root, selector) => {
    resetTextCache();
    const originalRoot = root;
    const parts = splitChain(selector);
    if (!parts.length) return [];
    const parsed = parts.map(parsePart);
    let captureIndex = -1;
    for (let i = 0; i < parsed.length; i++) {
      if (parsed[i].capture) {
        if (captureIndex >= 0)
          throw new Error('Only one of the selectors can capture using * modifier');
        captureIndex = i;
      }
    }

    let current = [root];
    if (captureIndex >= 0) {
      for (let i = 0; i <= captureIndex; i++)
        current = queryPart(current, parts[i]);
      const rest = parts.slice(captureIndex + 1);
      if (rest.length) {
        current = current.filter((el) => {
          let next = [el];
          for (let i = 0; i < rest.length; i++) {
            next = queryPart(next, rest[i]);
            if (!next.length) return false;
          }
          return true;
        });
      }
      return current;
    }

    for (let i = 0; i < parts.length; i++) {
      const parsed = parsePart(parts[i]);
      if (i === 0 && parsed.engine === 'internal:has')
        throw new Error(dq + 'internal:has' + dq + ' selector cannot be first');
      if (parsed.engine === 'nth') {
        let nth = Number(parsed.body);
        if (nth === -1)
          nth = current.length - 1;
        current = current.slice(nth, nth + 1);
        continue;
      }
      if (parsed.engine === 'visible') {
        const want = parsed.body === 'true';
        current = current.filter((el) => el && el.nodeType === 1 && isElementVisible(el) === want);
        continue;
      }
      if (parsed.engine === 'internal:and') {
        const nested = parseNestedBody('internal:and', parsed.body);
        const hits = new Set(queryAll(originalRoot, nested.selector));
        current = current.filter((el) => hits.has(el));
        continue;
      }
      if (parsed.engine === 'internal:or') {
        const nested = parseNestedBody('internal:or', parsed.body);
        const extra = queryAll(originalRoot, nested.selector);
        const seen = new Set(current);
        const out = current.slice();
        for (let e = 0; e < extra.length; e++) {
          const el = extra[e];
          if (el && !seen.has(el)) {
            seen.add(el);
            out.push(el);
          }
        }
        current = sortInDomOrder(out);
        continue;
      }
      if (parsed.engine === 'internal:has') {
        const nested = parseNestedBody('internal:has', parsed.body);
        current = current.filter((el) => el && el.nodeType === 1 && queryAll(el, nested.selector).length);
        continue;
      }
      if (parsed.engine === 'internal:has-not') {
        const nested = parseNestedBody('internal:has-not', parsed.body);
        current = current.filter((el) => el && el.nodeType === 1 && !queryAll(el, nested.selector).length);
        continue;
      }
      if (parsed.engine === 'internal:chain') {
        const nested = parseNestedBody('internal:chain', parsed.body);
        const out = [];
        const seen = new Set();
        for (let r = 0; r < current.length; r++) {
          const hits = queryAll(current[r], nested.selector);
          for (let h = 0; h < hits.length; h++) {
            if (hits[h] && !seen.has(hits[h])) {
              seen.add(hits[h]);
              out.push(hits[h]);
            }
          }
        }
        current = out;
        continue;
      }
      current = queryPart(current, parts[i]);
    }
    return current;
  };

  function parseNestedBody(name, body) {
    const s = String(body || '').trim();
    try {
      const unescaped = JSON.parse('[' + s + ']');
      if (!Array.isArray(unescaped) || unescaped.length < 1 || unescaped.length > 2 || typeof unescaped[0] !== 'string')
        throw new Error('Malformed selector: ' + name + '=' + s);
      return { selector: unescaped[0], distance: typeof unescaped[1] === 'number' ? unescaped[1] : undefined };
    } catch (e) {
      if (String(e.message || '').indexOf('Malformed selector:') === 0) throw e;
      throw new Error('Malformed selector: ' + name + '=' + s);
    }
  }

  function hasCustomPseudo(sel) {
    return /:(visible|nth-match|right-of|left-of|above|below|near|has|not)(\(|$)|:scope\b|:is\(/.test(String(sel || ''));
  }

  function absolutize(sel) {
    const t = String(sel || '').trim();
    if (!t) return t;
    const c = t.charAt(0);
    if (c === '>' || c === '+' || c === '~') return ':scope ' + t;
    return t;
  }

  function caseInsensitiveIds(sel) {
    let out = '';
    let quote = null;
    for (let i = 0; i < sel.length; i++) {
      const c = sel[i];
      if (quote) {
        out += c;
        if (c === '\\' && i + 1 < sel.length) { out += sel[++i]; continue; }
        if (c === quote) quote = null;
        continue;
      }
      if (c === dq || c === sq) { quote = c; out += c; continue; }
      if (c === '#' && i + 1 < sel.length && /[A-Za-z_]/.test(sel[i + 1])) {
        let j = i + 1;
        while (j < sel.length && /[\\w-]/.test(sel[j])) j++;
        out += '[id=' + dq + cssEscapeAttr(sel.slice(i + 1, j)) + dq + ' i]';
        i = j - 1;
        continue;
      }
      out += c;
    }
    return out;
  }

  function queryCssLight(root, sel) {
    try { return Array.from((root.querySelectorAll && root.querySelectorAll(sel)) || []); }
    catch (e) { return []; }
  }

  function isElementVisible(element) {
    if (!element) return false;
    const view = element.ownerDocument && element.ownerDocument.defaultView;
    const style = view ? view.getComputedStyle(element) : null;
    if (!style) return true;
    if (style.display === 'contents') {
      for (let child = element.firstChild; child; child = child.nextSibling) {
        if (child.nodeType === 1 && isElementVisible(child)) return true;
        if (child.nodeType === 3) {
          const range = element.ownerDocument.createRange();
          range.selectNode(child);
          const rect = range.getBoundingClientRect();
          if (rect.width > 0 && rect.height > 0) return true;
        }
      }
      return false;
    }
    if (style.visibility !== 'visible') return false;
    const rect = element.getBoundingClientRect();
    return rect.width > 0 && rect.height > 0;
  }

  function sortInDomOrder(elements) {
    const list = elements.slice();
    list.sort((a, b) => {
      if (a === b) return 0;
      const pos = a.compareDocumentPosition(b);
      if (pos & 2) return 1;
      if (pos & 4) return -1;
      return 0;
    });
    return list;
  }

  function boxRightOf(box1, box2, maxDistance) {
    const distance = box1.left - box2.right;
    if (distance < 0 || (maxDistance !== undefined && distance > maxDistance)) return;
    return distance + Math.max(box2.bottom - box1.bottom, 0) + Math.max(box1.top - box2.top, 0);
  }
  function boxLeftOf(box1, box2, maxDistance) {
    const distance = box2.left - box1.right;
    if (distance < 0 || (maxDistance !== undefined && distance > maxDistance)) return;
    return distance + Math.max(box2.bottom - box1.bottom, 0) + Math.max(box1.top - box2.top, 0);
  }
  function boxAbove(box1, box2, maxDistance) {
    const distance = box2.top - box1.bottom;
    if (distance < 0 || (maxDistance !== undefined && distance > maxDistance)) return;
    return distance + Math.max(box1.left - box2.left, 0) + Math.max(box2.right - box1.right, 0);
  }
  function boxBelow(box1, box2, maxDistance) {
    const distance = box1.top - box2.bottom;
    if (distance < 0 || (maxDistance !== undefined && distance > maxDistance)) return;
    return distance + Math.max(box1.left - box2.left, 0) + Math.max(box2.right - box1.right, 0);
  }
  function boxNear(box1, box2, maxDistance) {
    const kThreshold = maxDistance === undefined ? 50 : maxDistance;
    let score = 0;
    if (box1.left - box2.right >= 0) score += box1.left - box2.right;
    if (box2.left - box1.right >= 0) score += box2.left - box1.right;
    if (box2.top - box1.bottom >= 0) score += box2.top - box1.bottom;
    if (box1.top - box2.bottom >= 0) score += box1.top - box2.bottom;
    return score > kThreshold ? undefined : score;
  }

  function layoutScore(name, element, inner, maxDistance) {
    const box = element.getBoundingClientRect();
    const scorer = { 'left-of': boxLeftOf, 'right-of': boxRightOf, above: boxAbove, below: boxBelow, near: boxNear }[name];
    let best;
    for (let i = 0; i < inner.length; i++) {
      if (inner[i] === element) continue;
      const score = scorer(box, inner[i].getBoundingClientRect(), maxDistance);
      if (score === undefined) continue;
      if (best === undefined || score < best) best = score;
    }
    return best;
  }

  function splitCommaArgs(s) {
    const out = [];
    let current = '';
    let depth = 0;
    let squares = 0;
    let quote = null;
    for (let i = 0; i < s.length; i++) {
      const c = s[i];
      if (quote) {
        if (c === '\\' && i + 1 < s.length) { current += c + s[++i]; continue; }
        if (c === quote) quote = null;
        current += c;
        continue;
      }
      if (c === dq || c === sq) { quote = c; current += c; continue; }
      if (c === '(') depth++;
      if (c === ')') depth--;
      if (c === '[') squares++;
      if (c === ']') squares--;
      if (c === ',' && depth === 0 && squares === 0) { out.push(current.trim()); current = ''; continue; }
      current += c;
    }
    if (current.trim()) out.push(current.trim());
    return out;
  }

  function unwrapCallFrom(sel, name, from) {
    const prefix = ':' + name + '(';
    let depth = 0;
    let squares = 0;
    let quote = null;
    for (let start = from; start < sel.length; start++) {
      const c = sel[start];
      if (quote) {
        if (c === '\\' && start + 1 < sel.length) { start++; continue; }
        if (c === quote) quote = null;
        continue;
      }
      if (c === dq || c === sq) { quote = c; continue; }
      if (depth === 0 && squares === 0 && sel.slice(start, start + prefix.length) === prefix) {
        let i = start + prefix.length;
        let innerDepth = 1;
        let iq = null;
        while (i < sel.length) {
          const ch = sel[i];
          if (iq) {
            if (ch === '\\' && i + 1 < sel.length) { i += 2; continue; }
            if (ch === iq) iq = null;
            i++;
            continue;
          }
          if (ch === dq || ch === sq) { iq = ch; i++; continue; }
          if (ch === '(') innerDepth++;
          if (ch === ')') {
            innerDepth--;
            if (innerDepth === 0)
              return { start: start, before: sel.slice(0, start), inner: sel.slice(start + prefix.length, i), after: sel.slice(i + 1) };
          }
          i++;
        }
        return null;
      }
      if (c === '(') depth++;
      if (c === ')') depth--;
      if (c === '[') squares++;
      if (c === ']') squares--;
    }
    return null;
  }

  function unwrapCall(sel, name) {
    return unwrapCallFrom(sel, name, 0);
  }

  function unwrapLastCall(sel, name) {
    let found = null;
    let from = 0;
    while (from < sel.length) {
      const call = unwrapCallFrom(sel, name, from);
      if (!call) break;
      found = call;
      from = call.start + 1;
    }
    return found;
  }

  function afterHasCombinator(after) {
    return /[>+~,]/.test(after) || /\s/.test(after);
  }

  function compoundAfterPeel(before, after) {
    const b = String(before || '');
    const a = String(after || '');
    const trimmed = (b + a).trim();
    if (!trimmed) return '*';
    if (/\s$/.test(b) || /[>+~]$/.test(b.trimEnd()))
      return (b.trimEnd() + ' *' + a).trim();
    return trimmed;
  }

  function queryCustomCss(root, body) {
    let sel = absolutize(String(body || '').trim());
    const commaParts = splitCommaArgs(sel);
    if (commaParts.length > 1) {
      const seenC = new Set();
      const unionC = [];
      for (let p = 0; p < commaParts.length; p++) {
        const hits = queryCustomCss(root, commaParts[p]);
        for (let h = 0; h < hits.length; h++) {
          if (hits[h] && !seenC.has(hits[h])) { seenC.add(hits[h]); unionC.push(hits[h]); }
        }
      }
      return sortInDomOrder(unionC);
    }
    if (sel.indexOf(':is(') === 0) {
      const leadIs = unwrapCall(sel, 'is');
      if (leadIs && !leadIs.before && !leadIs.after.trim()) {
        const parts = splitCommaArgs(leadIs.inner);
        const seenI = new Set();
        const outI = [];
        for (let i = 0; i < parts.length; i++) {
          const hits = queryAll(root, parts[i]);
          for (let h = 0; h < hits.length; h++) {
            if (hits[h] && !seenI.has(hits[h])) { seenI.add(hits[h]); outI.push(hits[h]); }
          }
        }
        return sortInDomOrder(outI);
      }
    }
    const hasInners = [];
    let peelHas = true;
    while (peelHas) {
      const call = unwrapCall(sel, 'has');
      if (!call) { peelHas = false; continue; }
      if (afterHasCombinator(call.after)) { peelHas = false; continue; }
      hasInners.push(call.inner);
      sel = compoundAfterPeel(call.before, call.after);
    }
    const isInners = [];
    let peelIs = true;
    while (peelIs) {
      const call = unwrapLastCall(sel, 'is');
      if (!call || afterHasCombinator(call.after)) { peelIs = false; continue; }
      isInners.push(call.inner);
      sel = compoundAfterPeel(call.before, call.after);
    }
    const notInners = [];
    let peelNot = true;
    while (peelNot) {
      const call = unwrapLastCall(sel, 'not');
      if (!call || afterHasCombinator(call.after) || !(/[ >+~,]/.test(call.inner) || call.inner.indexOf(':scope') >= 0)) { peelNot = false; continue; }
      notInners.push(call.inner);
      sel = compoundAfterPeel(call.before, call.after);
    }
    const filterHas = (els) => {
      let out = els || [];
      if (isInners.length) {
        out = out.filter((el) => {
          if (!el) return false;
          return isInners.every((inner) => {
            const parts = splitCommaArgs(inner);
            for (let p = 0; p < parts.length; p++) {
              const hits = queryAll(root, parts[p]);
              for (let h = 0; h < hits.length; h++) {
                if (hits[h] === el) return true;
              }
            }
            return false;
          });
        });
      }
      if (notInners.length) {
        out = out.filter((el) => {
          return notInners.every((inner) => {
            const hits = queryAll(root, inner);
            for (let h = 0; h < hits.length; h++) {
              if (hits[h] === el) return false;
            }
            return true;
          });
        });
      }
      if (!hasInners.length) return out;
      return out.filter((el) => el && el.nodeType === 1 && hasInners.every((inner) => queryAll(el, inner).length));
    };
    if (sel.indexOf(':is(') === 0) {
      const call = unwrapCall(sel, 'is');
      if (call && !call.before && !call.after.trim()) {
        const parts = splitCommaArgs(call.inner);
        const seen = new Set();
        const out = [];
        for (let i = 0; i < parts.length; i++) {
          const hits = queryAll(root, parts[i]);
          for (let h = 0; h < hits.length; h++) {
            if (hits[h] && !seen.has(hits[h])) { seen.add(hits[h]); out.push(hits[h]); }
          }
        }
        return filterHas(sortInDomOrder(out));
      }
    }
    if (sel.indexOf(':nth-match(') === 0 && sel.charAt(sel.length - 1) === ')') {
      const call = unwrapCall(sel, 'nth-match');
      if (call && !call.before && !call.after.trim()) {
        const args = splitCommaArgs(call.inner);
        if (args.length < 2)
          throw new Error(dq + 'nth-match' + dq + ' engine expects non-empty selector list and an index argument');
        const last = args[args.length - 1];
        const index = Number(last);
        if (!isFinite(index) || index < 1)
          throw new Error(dq + 'nth-match' + dq + ' engine expects a one-based index as the last argument');
        const seen = new Set();
        const els = [];
        for (let i = 0; i < args.length - 1; i++) {
          const hits = queryAll(root, args[i]);
          for (let h = 0; h < hits.length; h++) {
            if (hits[h] && !seen.has(hits[h])) { seen.add(hits[h]); els.push(hits[h]); }
          }
        }
        const ordered = sortInDomOrder(els);
        return filterHas(index <= ordered.length ? [ordered[index - 1]] : []);
      }
    }
    if (sel.indexOf(':near(') === 0) {
      const call = unwrapCall(sel, 'near');
      if (call && !call.before) {
        const args = splitCommaArgs(call.inner);
        if (!args.length || (args.length === 1 && isFinite(Number(args[0]))))
          throw new Error(dq + 'near' + dq + ' engine expects a selector list and optional maximum distance in pixels');
      }
    }

    const plus = splitByCombinator(sel, '+');
    if (plus && hasCustomPseudo(plus.left) && hasCustomPseudo(plus.right) && plus.left.indexOf(':scope') < 0 && plus.right.indexOf(':scope') < 0) {
      const leftSet = new Set(queryCustomCss(root, plus.left));
      return filterHas(queryCustomCss(root, plus.right).filter((el) => el.previousElementSibling && leftSet.has(el.previousElementSibling)));
    }

    let visible = false;
    if (sel.indexOf(':visible') >= 0) {
      visible = true;
      sel = sel.replace(/:visible\b/g, '');
    }
    const layouts = [];
    const names = ['right-of', 'left-of', 'above', 'below', 'near'];
    for (let n = 0; n < names.length; n++) {
      let again = true;
      while (again) {
        const call = unwrapCall(sel, names[n]);
        if (!call) { again = false; continue; }
        const args = splitCommaArgs(call.inner);
        if (!args.length || (names[n] === 'near' && args.length === 1 && isFinite(Number(args[0]))))
          throw new Error(dq + names[n] + dq + ' engine expects a selector list and optional maximum distance in pixels');
        let maxDistance;
        let innerSel = args[0];
        if (args.length > 1 && isFinite(Number(args[args.length - 1]))) {
          maxDistance = Number(args[args.length - 1]);
          innerSel = args.slice(0, args.length - 1).join(',');
        }
        layouts.push({ name: names[n], inner: queryAll(root, innerSel), maxDistance: maxDistance });
        sel = (call.before + call.after).trim() || '*';
      }
    }
    const base = queryCss(root, sel || '*');
    const scored = [];
    for (let i = 0; i < base.length; i++) {
      const el = base[i];
      if (visible && !isElementVisible(el)) continue;
      let lastScore;
      let ok = true;
      for (let l = 0; l < layouts.length; l++) {
        const score = layoutScore(layouts[l].name, el, layouts[l].inner, layouts[l].maxDistance);
        if (score === undefined) { ok = false; break; }
        lastScore = score;
      }
      if (ok) scored.push({ el: el, score: lastScore });
    }
    if (layouts.length)
      scored.sort((a, b) => (a.score === undefined ? 0 : a.score) - (b.score === undefined ? 0 : b.score));
    return filterHas(scored.map((s) => s.el));
  }

  function splitByCombinator(sel, combo) {
    let depth = 0;
    let quote = null;
    for (let i = 0; i < sel.length; i++) {
      const c = sel[i];
      if (quote) {
        if (c === '\\' && i + 1 < sel.length) { i++; continue; }
        if (c === quote) quote = null;
        continue;
      }
      if (c === dq || c === sq) { quote = c; continue; }
      if (c === '(') depth++;
      if (c === ')') depth--;
      if (depth === 0 && c === combo && (combo !== ' ' || true)) {
        if (combo === '+' && sel[i] === '+') {
          const left = sel.slice(0, i).trim();
          const right = sel.slice(i + 1).trim();
          if (left && right) return { left: left, right: right };
        }
      }
    }
    return null;
  }

  return {
    query(root, selector) {
      const all = queryAll(root, selector);
      return all.length ? all[0] : null;
    },
    queryAll
  };
})()";

        private static readonly HashSet<string> _officialEngines = new HashSet<string>(StringComparer.Ordinal)
        {
            "css",
            "xpath",
            "text",
            "text:light",
            "id",
            "data-test",
            "data-testid",
            "data-test-id",
            "nth",
            "visible",
            "internal:has",
            "internal:has-not",
            "internal:and",
            "internal:or",
            "internal:chain",
            "id:light",
            "data-test:light",
            "data-testid:light",
            "data-test-id:light",
            "css:light",
            "xpath:light",
            "role",
            "internal:role",
            "aria-ref",
        };

        /// <summary>
        /// Official <c>page.$</c> / <c>page.$$</c> reject a missing selector with
        /// the protocol string-validation message (<c>typeof null === 'object'</c>).
        /// </summary>
        /// <param name="selector">The selector passed to querySelector / $$.</param>
        internal static void EnsureSelector(string selector)
        {
            if (selector == null)
            {
                throw new PlaywrightNativeException("selector: expected string, got object");
            }

            string first = FirstChainPart(selector);
            if (first.StartsWith("internal:has=", StringComparison.Ordinal)
                || string.Equals(first, "internal:has", StringComparison.Ordinal))
            {
                throw new PlaywrightNativeException("\"internal:has\" selector cannot be first");
            }
        }

        /// <summary>
        /// Whether <paramref name="selector"/> uses a capture (<c>*css=</c>) or
        /// a Playwright engine / <c>&gt;&gt;</c> chain that is not plain CSS.
        /// Also routes official auto-detected <c>xpath</c> (<c>//</c>, <c>(//</c>,
        /// <c>..</c>) and quoted <c>text</c> (<c>"…"</c> / <c>'…'</c>).
        /// </summary>
        /// <param name="selector">A locator selector.</param>
        /// <returns><see langword="true"/> when the chain engine should run.</returns>
        internal static bool NeedsChainEngine(string selector)
        {
            if (string.IsNullOrEmpty(selector))
            {
                return false;
            }

            string trimmed = selector.TrimStart();
            if (trimmed == "\"" || trimmed == "'")
            {
                return true;
            }

            if (HasCapture(selector)
                || selector.Contains(">>", StringComparison.Ordinal)
                || (trimmed.Length > 0 && (trimmed[0] == '>' || trimmed[0] == '+' || trimmed[0] == '~'))
                || selector.Contains(":has-text(", StringComparison.Ordinal)
                || selector.Contains(":text(", StringComparison.Ordinal)
                || selector.Contains(":text-is(", StringComparison.Ordinal)
                || selector.Contains(":text-matches(", StringComparison.Ordinal)
                || selector.Contains("internal:", StringComparison.Ordinal)
                || selector.Contains(":light=", StringComparison.Ordinal)
                || selector.Contains(":nth-match(", StringComparison.Ordinal)
                || selector.Contains(":visible", StringComparison.Ordinal)
                || selector.Contains(":right-of(", StringComparison.Ordinal)
                || selector.Contains(":left-of(", StringComparison.Ordinal)
                || selector.Contains(":above(", StringComparison.Ordinal)
                || selector.Contains(":below(", StringComparison.Ordinal)
                || selector.Contains(":near(", StringComparison.Ordinal)
                || selector.Contains(":has(", StringComparison.Ordinal)
                || selector.Contains(":is(", StringComparison.Ordinal)
                || selector.Contains(":scope", StringComparison.Ordinal)
                || selector.Contains("|", StringComparison.Ordinal))
            {
                return true;
            }

            if (IsAutoDetectedXPath(selector) || IsQuotedTextSelector(selector))
            {
                return true;
            }

            if (HasTopLevelComma(selector) || HasStarScopedCombinator(selector))
            {
                return true;
            }

            int equals = selector.IndexOf('=');
            if (equals <= 0)
            {
                return false;
            }

            string name = selector.Substring(0, equals).Trim();
            return _officialEngines.Contains(name);
        }

        /// <summary>
        /// Official auto-detect: <c>//foo</c>, <c>(//foo)[1]</c>, or <c>../span</c>.
        /// </summary>
        /// <param name="selector">A selector or chain part.</param>
        /// <returns><see langword="true"/> when the selector is implicit xpath.</returns>
        internal static bool IsAutoDetectedXPath(string selector)
        {
            if (string.IsNullOrEmpty(selector))
            {
                return false;
            }

            if (selector.StartsWith("..", StringComparison.Ordinal))
            {
                return true;
            }

            int index = 0;
            while (index < selector.Length && selector[index] == '(')
            {
                index++;
            }

            return index + 1 < selector.Length
                && selector[index] == '/'
                && selector[index + 1] == '/';
        }

        /// <summary>
        /// Official auto-detect for a quoted text selector (<c>"test"</c>).
        /// </summary>
        /// <param name="selector">A selector or chain part.</param>
        /// <returns><see langword="true"/> when the whole selector is quoted text.</returns>
        internal static bool IsQuotedTextSelector(string selector)
        {
            if (string.IsNullOrEmpty(selector) || selector.Length < 2)
            {
                return false;
            }

            char quote = selector[0];
            return (quote == '"' || quote == '\'') && selector[selector.Length - 1] == quote;
        }

        /// <summary>
        /// Whether <paramref name="selector"/> captures an intermediate part
        /// (<c>*css=div &gt;&gt; p</c>).
        /// </summary>
        /// <param name="selector">A locator selector.</param>
        /// <returns><see langword="true"/> when nth/first/last must be rejected.</returns>
        internal static bool HasCapture(string selector)
        {
            if (string.IsNullOrEmpty(selector))
            {
                return false;
            }

            // "*" alone and "*#id" / "*:not(...)" are CSS, not *engine= capture.
            int index = 0;
            while (index < selector.Length)
            {
                int next = selector.IndexOf(">>", index, StringComparison.Ordinal);
                string part = (next < 0
                    ? selector.Substring(index)
                    : selector.Substring(index, next - index)).Trim();
                if (IsCapturePart(part))
                {
                    return true;
                }

                if (next < 0)
                {
                    break;
                }

                index = next + 2;
            }

            return false;
        }

        /// <summary>
        /// Applies official <c>has</c> / <c>hasText</c> / <c>hasNot</c> options.
        /// Extra parameters stay at the end of locator factories.
        /// </summary>
        /// <param name="locator">The base locator.</param>
        /// <param name="has">Optional inner locator the match must contain.</param>
        /// <param name="hasText">Optional case-insensitive substring.</param>
        /// <param name="hasTextRegex">Optional text regular expression.</param>
        /// <param name="hasNot">Optional inner locator the match must not contain.</param>
        /// <param name="hasNotText">Optional substring that must not appear.</param>
        /// <param name="hasNotTextRegex">Optional regular expression that must not match.</param>
        /// <returns>The locator with options applied.</returns>
        internal static ILocator ApplyOptions(
            ILocator locator,
            ILocator has = null,
            string hasText = null,
            Regex hasTextRegex = null,
            ILocator hasNot = null,
            string hasNotText = null,
            Regex hasNotTextRegex = null)
        {
            if (locator == null)
            {
                throw new ArgumentNullException(nameof(locator));
            }

            ILocator result = locator;
            if (!string.IsNullOrEmpty(hasText))
            {
                result = result.Filter(hasText);
            }

            if (hasTextRegex != null)
            {
                result = result.Filter(hasTextRegex);
            }

            if (has != null)
            {
                result = result.Has(has);
            }

            if (hasNot != null)
            {
                result = result.HasNot(hasNot);
            }

            if (!string.IsNullOrEmpty(hasNotText))
            {
                result = result.HasNotText(hasNotText);
            }

            if (hasNotTextRegex != null)
            {
                result = result.HasNotText(hasNotTextRegex);
            }

            return result;
        }

        private static string CssEngineBody(string selector)
        {
            int equals = selector.IndexOf('=');
            if (equals > 0)
            {
                string name = selector.Substring(0, equals).Trim();
                if (_officialEngines.Contains(name) || name.EndsWith(":light", StringComparison.Ordinal))
                {
                    return selector.Substring(equals + 1);
                }
            }

            return selector;
        }

        private static bool HasTopLevelComma(string selector)
        {
            return HasTopLevelCssToken(selector, comma: true, combinator: false);
        }

        private static bool HasStarScopedCombinator(string selector)
        {
            if (string.IsNullOrEmpty(selector))
            {
                return false;
            }

            string body = CssEngineBody(selector).TrimStart();
            return body.StartsWith('*') && HasTopLevelCssToken(selector, comma: false, combinator: true);
        }

        private static bool HasTopLevelCssToken(string selector, bool comma, bool combinator)
        {
            if (string.IsNullOrEmpty(selector))
            {
                return false;
            }

            string body = CssEngineBody(selector);
            int depth = 0;
            bool inQuote = false;
            char quote = '\0';
            for (int i = 0; i < body.Length; i++)
            {
                char c = body[i];
                if (inQuote)
                {
                    if (c == '\\')
                    {
                        i++;
                        continue;
                    }

                    if (c == quote)
                    {
                        inQuote = false;
                    }

                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    inQuote = true;
                    quote = c;
                    continue;
                }

                if (c == '(' || c == '[')
                {
                    depth++;
                    continue;
                }

                if (c == ')' || c == ']')
                {
                    depth--;
                    continue;
                }

                if (depth != 0)
                {
                    continue;
                }

                if (comma && c == ',')
                {
                    return true;
                }

                if (!combinator)
                {
                    continue;
                }

                if (c == '>' || c == '+')
                {
                    return true;
                }

                if (c == '~' && (i + 1 >= body.Length || body[i + 1] != '='))
                {
                    return true;
                }

                if (char.IsWhiteSpace(c))
                {
                    int k = i + 1;
                    while (k < body.Length && char.IsWhiteSpace(body[k]))
                    {
                        k++;
                    }

                    if (k < body.Length)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string FirstChainPart(string selector)
        {
            int next = selector.IndexOf(">>", StringComparison.Ordinal);
            return (next < 0 ? selector : selector.Substring(0, next)).Trim();
        }

        private static bool IsCapturePart(string part)
        {
            if (string.IsNullOrEmpty(part) || part[0] != '*' || part.Length < 2)
            {
                return false;
            }

            string rest = part.Substring(1).TrimStart();
            int equals = rest.IndexOf('=');
            if (equals <= 0)
            {
                return false;
            }

            string name = rest.Substring(0, equals).Trim();
            return _officialEngines.Contains(name);
        }
    }
}
