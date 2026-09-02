// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Globalization;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Official Playwright <c>ariaSnapshot({ mode: 'ai' })</c> YAML from a
    /// DOM walk (AI visibility, interactable refs, distillation).
    /// </summary>
    internal static class AriaSnapshotOfficialAi
    {
        private const string DepthToken = "__PW_DEPTH__";
        private const string BoxesToken = "__PW_BOXES__";
        private const string PrefixToken = "__PW_REF_PREFIX__";
        private const string FormatToken = "__PW_FORMAT__";

        private const string CaptureFunction = @"(root) => {
  const maxDepth = __PW_DEPTH__;
  const renderBoxes = __PW_BOXES__;
  const startDepth = __PW_START_DEPTH__;
  const refPrefix = ""__PW_REF_PREFIX__"";
  const visited = new Set();
  const nameSources = new Map();
  const info = new Map();
  const refs = new Map();
  const implicit = {
    A: (e) => e.hasAttribute('href') ? 'link' : '',
    AREA: (e) => e.hasAttribute('href') ? 'link' : '',
    ARTICLE: () => 'article',
    ASIDE: () => 'complementary',
    BUTTON: () => 'button',
    DETAILS: () => 'group',
    DIALOG: () => 'dialog',
    FIELDSET: () => 'group',
    FIGURE: () => 'figure',
    FORM: (e) => (e.hasAttribute('aria-label') || e.hasAttribute('aria-labelledby')) ? 'form' : '',
    H1: () => 'heading', H2: () => 'heading', H3: () => 'heading',
    H4: () => 'heading', H5: () => 'heading', H6: () => 'heading',
    HR: () => 'separator',
    IMG: (e) => (e.getAttribute('alt') === '' && !e.getAttribute('title') && !e.hasAttribute('aria-label')) ? 'presentation' : 'img',
    INPUT: (e) => {
      const t = (e.type || 'text').toLowerCase();
      if (t === 'hidden') return '';
      if (t === 'file') return 'button';
      if (t === 'checkbox') return 'checkbox';
      if (t === 'radio') return 'radio';
      if (t === 'search') return 'searchbox';
      if (t === 'button' || t === 'submit' || t === 'reset' || t === 'image') return 'button';
      if (t === 'range') return 'slider';
      return 'textbox';
    },
    LI: () => 'listitem',
    MAIN: () => 'main',
    MENU: () => 'list',
    METER: () => 'meter',
    NAV: () => 'navigation',
    OL: () => 'list',
    OPTION: () => 'option',
    OUTPUT: () => 'status',
    P: () => 'paragraph',
    PROGRESS: () => 'progressbar',
    SECTION: (e) => (e.hasAttribute('aria-label') || e.hasAttribute('aria-labelledby')) ? 'region' : '',
    SELECT: (e) => (e.multiple || e.size > 1) ? 'listbox' : 'combobox',
    TABLE: () => 'table',
    TBODY: () => 'rowgroup',
    TD: () => 'cell',
    TEXTAREA: () => 'textbox',
    TFOOT: () => 'rowgroup',
    TH: () => 'columnheader',
    THEAD: () => 'rowgroup',
    TR: () => 'row',
    UL: () => 'list'
  };
  const valid = {alert:1,alertdialog:1,application:1,article:1,banner:1,blockquote:1,button:1,caption:1,cell:1,checkbox:1,code:1,columnheader:1,combobox:1,complementary:1,contentinfo:1,definition:1,deletion:1,dialog:1,directory:1,document:1,emphasis:1,feed:1,figure:1,form:1,generic:1,grid:1,gridcell:1,group:1,heading:1,img:1,insertion:1,link:1,list:1,listbox:1,listitem:1,log:1,main:1,mark:1,marquee:1,math:1,meter:1,menu:1,menubar:1,menuitem:1,menuitemcheckbox:1,menuitemradio:1,navigation:1,none:1,note:1,option:1,paragraph:1,presentation:1,progressbar:1,radio:1,radiogroup:1,region:1,row:1,rowgroup:1,rowheader:1,scrollbar:1,search:1,searchbox:1,separator:1,slider:1,spinbutton:1,status:1,strong:1,subscript:1,superscript:1,switch:1,tab:1,table:1,tablist:1,tabpanel:1,term:1,textbox:1,time:1,timer:1,toolbar:1,tooltip:1,tree:1,treegrid:1,treeitem:1};
  const nameFromContent = {button:1,cell:1,checkbox:1,columnheader:1,gridcell:1,heading:1,link:1,menuitem:1,menuitemcheckbox:1,menuitemradio:1,option:1,radio:1,row:1,rowheader:1,switch:1,tab:1,tooltip:1,treeitem:1};
  const nameFromContentDesc = {'':1,caption:1,code:1,contentinfo:1,definition:1,deletion:1,emphasis:1,insertion:1,list:1,listitem:1,mark:1,none:1,paragraph:1,presentation:1,region:1,row:1,rowgroup:1,section:1,strong:1,subscript:1,superscript:1,table:1,term:1,time:1};
  const nameProhibited = {caption:1,code:1,definition:1,deletion:1,emphasis:1,generic:1,insertion:1,mark:1,paragraph:1,presentation:1,strong:1,subscript:1,suggestion:1,superscript:1,term:1,time:1};
  const selectedRoles = {gridcell:1,option:1,row:1,tab:1,rowheader:1,columnheader:1,treeitem:1};
  const checkedRoles = {checkbox:1,menuitemcheckbox:1,option:1,radio:1,switch:1,menuitemradio:1,treeitem:1};
  const disabledRoles = {application:1,button:1,composite:1,gridcell:1,group:1,input:1,link:1,menuitem:1,scrollbar:1,separator:1,tab:1,checkbox:1,columnheader:1,combobox:1,grid:1,listbox:1,menu:1,menubar:1,menuitemcheckbox:1,menuitemradio:1,option:1,radio:1,radiogroup:1,row:1,rowheader:1,searchbox:1,select:1,slider:1,spinbutton:1,switch:1,tablist:1,textbox:1,toolbar:1,tree:1,treegrid:1,treeitem:1};
  const expandedRoles = {application:1,button:1,checkbox:1,combobox:1,gridcell:1,link:1,listbox:1,menuitem:1,row:1,rowheader:1,tab:1,treeitem:1,columnheader:1,menuitemcheckbox:1,menuitemradio:1,switch:1};
  const invalidRoles = {application:1,checkbox:1,combobox:1,gridcell:1,listbox:1,radiogroup:1,slider:1,spinbutton:1,textbox:1,tree:1,columnheader:1,rowheader:1,searchbox:1};
  const levelRoles = {heading:1,listitem:1,row:1,treeitem:1};
  const valueRoles = {listitem:1,paragraph:1,group:1,region:1,cell:1,row:1};

  const tagName = (e) => (e && e.tagName) ? String(e.tagName).toUpperCase() : '';
  const styleOf = (e, pseudo) => {
    try { return e.ownerDocument.defaultView.getComputedStyle(e, pseudo || null); } catch (err) { return null; }
  };
  const hiddenForAria = (e) => {
    if (!e || e.nodeType !== 1) return true;
    const t = tagName(e);
    if (t === 'STYLE' || t === 'SCRIPT' || t === 'NOSCRIPT' || t === 'TEMPLATE' || t === 'HEAD' || t === 'META' || t === 'LINK') return true;
    if (t !== 'SLOT' && typeof e.checkVisibility === 'function') {
      try { if (!e.checkVisibility()) return true; } catch (err) {}
    } else if (t !== 'SLOT') {
      const details = e.closest && e.closest('details');
      if (details && details !== e && !details.open) {
        const summary = e.closest('summary');
        if (!summary || !details.contains(summary)) return true;
      }
    }
    const st = styleOf(e);
    if (st && st.visibility && st.visibility !== 'visible') return true;
    let cur = e;
    while (cur && cur.nodeType === 1) {
      const cs = styleOf(cur);
      if (!cs || cs.display === 'none') return true;
      if (String(cur.getAttribute('aria-hidden') || '').toLowerCase() === 'true') return true;
      if (cur.parentElement && cur.parentElement.shadowRoot && !cur.assignedSlot) return true;
      const rootNode = cur.getRootNode && cur.getRootNode();
      cur = cur.parentElement || (rootNode && rootNode.host) || null;
    }
    return false;
  };
  const isElementVisible = (e) => {
    if (!e || e.nodeType !== 1) return false;
    const st = styleOf(e);
    if (!st) return true;
    if (typeof e.checkVisibility === 'function') {
      try { if (!e.checkVisibility()) return false; } catch (err) {}
    }
    if (st.visibility !== 'visible') return false;
    const r = e.getBoundingClientRect();
    return r.width > 0 && r.height > 0;
  };
  const computeBox = (e) => {
    const st = styleOf(e);
    const r = e.getBoundingClientRect();
    return { visible: !!(r.width > 0 && r.height > 0 && (!st || st.visibility !== 'hidden')), inline: !!(st && st.display === 'inline') };
  };
  const receivesPointerEvents = (e) => {
    const st = styleOf(e);
    return !st || st.pointerEvents !== 'none';
  };
  const hasPointerCursor = (node) => {
    if (!node || !node.el) return false;
    const st = styleOf(node.el);
    return !!(st && st.cursor === 'pointer');
  };
  const explicitRole = (e) => {
    const parts = String(e.getAttribute('role') || '').split(/\s+/);
    for (let i = 0; i < parts.length; i++) {
      if (valid[parts[i]]) return parts[i];
    }
    return '';
  };
  const implicitRole = (e) => {
    const fn = implicit[tagName(e)];
    return fn ? (fn(e) || '') : '';
  };
  const ariaRole = (e) => {
    const ex = explicitRole(e);
    if (!ex) return implicitRole(e);
    if (ex === 'none' || ex === 'presentation') {
      if (e.hasAttribute('aria-label') || e.hasAttribute('tabindex') || e.hasAttribute('aria-labelledby')) return implicitRole(e);
      return ex;
    }
    return ex;
  };
  const idRefs = (e, ref) => {
    if (!ref) return [];
    const scope = (e.getRootNode && e.getRootNode()) || e.ownerDocument;
    const out = [];
    const ids = String(ref).split(/\s+/);
    for (let i = 0; i < ids.length; i++) {
      if (!ids[i]) continue;
      let found = null;
      try { found = scope.getElementById ? scope.getElementById(ids[i]) : scope.querySelector('#' + CSS.escape(ids[i])); } catch (err) {}
      if (found && out.indexOf(found) < 0) out.push(found);
    }
    return out;
  };
  const cssContent = (el, pseudo) => {
    const st = styleOf(el, pseudo);
    if (!st) return '';
    if (st.display === 'none' || st.visibility === 'hidden') return '';
    const raw = String(st.content || '');
    if (!raw || raw === 'none' || raw === 'normal') return '';
    const hex = raw.match(/^[""']?\\([0-9a-fA-F]{1,6})[""']?$/);
    if (hex) return String.fromCharCode(parseInt(hex[1], 16));
    let text = '';
    if ((raw.charAt(0) === '""' && raw.charAt(raw.length - 1) === '""') ||
        (raw.charAt(0) === ""'"" && raw.charAt(raw.length - 1) === ""'"")) {
      try { text = JSON.parse(raw.charAt(0) === ""'"" ? '""' + raw.slice(1, -1).replace(/""/g, '\\""') + '""' : raw); }
      catch (err) { text = raw.slice(1, -1); }
    } else if (raw.indexOf('url(') === 0) {
      return '';
    } else {
      return '';
    }
    if (pseudo && text && st.display && st.display !== 'inline') return ' ' + text + ' ';
    return text || '';
  };
  const flat = (s) => String(s || '').replace(/[\u200b\u00ad]/g, '').split('\u00a0').map((c) => c.replace(/\r\n/g, '\n').replace(/\s\s*/g, ' ')).join('\u00a0').trim();
  const norm = (s) => String(s || '').replace(/[\u200b\u00ad]/g, '').replace(/\s+/g, ' ').trim();
  const truncateDataUrl = (s) => {
    const v = String(s || '');
    if (v.indexOf('data:') !== 0) return v;
    const i = v.indexOf(',');
    if (i < 0) return v;
    return v.slice(0, i + 1) + '\u2026';
  };

  const accName = (element, asDescendant, visitedNames, sources) => {
    if (!element || element.nodeType !== 1) return '';
    if (visitedNames.size > 64) return '';
    if (visitedNames.has(element)) return '';
    if (hiddenForAria(element) && !asDescendant) return '';
    visitedNames.add(element);
    const role = ariaRole(element);
    if (nameProhibited[role] && !asDescendant) return flat(element.getAttribute('title') || '');
    const labelled = idRefs(element, element.getAttribute('aria-labelledby'));
    if (labelled.length) {
      let lab = '';
      for (let i = 0; i < labelled.length; i++) {
        if (sources) sources.add(labelled[i]);
        lab += (lab ? ' ' : '') + accName(labelled[i], true, visitedNames, sources);
      }
      if (flat(lab)) return flat(lab);
    }
    const ariaLabel = element.getAttribute('aria-label') || '';
    if (flat(ariaLabel)) return flat(ariaLabel);
    const t = tagName(element);
    if (asDescendant && (role === 'textbox' || role === 'searchbox' || t === 'INPUT' || t === 'TEXTAREA')) {
      const type = (element.type || 'text').toLowerCase();
      if (t === 'INPUT' && (type === 'checkbox' || type === 'radio' || type === 'file')) { /* skip */ }
      else return flat(String(element.value ?? ''));
    }
    if (t === 'IMG') {
      const alt = element.getAttribute('alt') || '';
      if (flat(alt)) return flat(alt);
    }
    if (t === 'INPUT') {
      const type = (element.type || 'text').toLowerCase();
      if (type === 'file') return 'Choose File';
      if (type === 'submit' && !element.value) return 'Submit';
      if (type === 'reset' && !element.value) return 'Reset';
      if ((type === 'button' || type === 'submit' || type === 'reset') && element.value) return flat(element.value);
    }
    const labels = element.labels;
    if (labels && labels.length) {
      let lab = '';
      for (let i = 0; i < labels.length; i++) {
        if (sources) sources.add(labels[i]);
        lab += (lab ? ' ' : '') + innerText(labels[i], true, visitedNames, sources);
      }
      if (flat(lab)) return flat(lab);
    }
    const allow = nameFromContent[role] || (asDescendant && nameFromContentDesc[role]) || t === 'SUMMARY';
    if (allow) {
      const text = innerText(element, true, visitedNames, sources);
      if (flat(text)) return flat(text);
    }
    if (t === 'TEXTAREA' || t === 'SELECT' || t === 'INPUT') {
      const type = (element.type || 'text').toLowerCase();
      const usePh = t === 'TEXTAREA' || ['text','password','number','search','tel','email','url',''].indexOf(type) >= 0;
      const title = element.getAttribute('title') || '';
      const ph = element.getAttribute('placeholder') || '';
      if (title) return flat(title);
      if (usePh) return flat(ph);
    }
    return flat(element.getAttribute('title') || '');
  };
  const innerText = (element, asDescendant, visitedNames, sources) => {
    const tokens = [];
    const visitName = (node, fromSlot) => {
      if (!node) return;
      if (!fromSlot && node.assignedSlot) return;
      if (node.nodeType === 3) { tokens.push(node.nodeValue || ''); return; }
      if (node.nodeType !== 1) return;
      const display = (styleOf(node) || {}).display || 'inline';
      let token = accName(node, asDescendant, visitedNames, sources);
      if (sources && flat(token)) sources.add(node);
      if (display !== 'inline' || node.nodeName === 'BR') token = ' ' + token + ' ';
      tokens.push(token);
    };
    tokens.push(cssContent(element, '::before'));
    const assigned = element.nodeName === 'SLOT' ? element.assignedNodes() : [];
    if (assigned.length) {
      for (let i = 0; i < assigned.length; i++) visitName(assigned[i], true);
    } else {
      for (let c = element.firstChild; c; c = c.nextSibling) visitName(c, false);
      if (element.shadowRoot) {
        for (let c = element.shadowRoot.firstChild; c; c = c.nextSibling) visitName(c, false);
      }
      const owned = idRefs(element, element.getAttribute('aria-owns'));
      for (let i = 0; i < owned.length; i++) visitName(owned[i], false);
    }
    tokens.push(cssContent(element, '::after'));
    return tokens.join('');
  };

  const win = (root && root.ownerDocument && root.ownerDocument.defaultView) || null;
  if (win && typeof win.__pwAriaLastRef !== 'number') win.__pwAriaLastRef = 0;
  const assignRef = (ariaNode) => {
    if (!ariaNode.box.visible || !ariaNode.receivesPointerEvents) return;
    const element = ariaNode.el;
    let ariaRef = element._ariaRef;
    if (!ariaRef || ariaRef.role !== ariaNode.role || ariaRef.name !== ariaNode.name) {
      const n = ++win.__pwAriaLastRef;
      ariaRef = { role: ariaNode.role, name: ariaNode.name, ref: (refPrefix || '') + 'e' + n };
      element._ariaRef = ariaRef;
    }
    ariaNode.ref = ariaRef.ref;
    refs.set(element, ariaRef.ref);
  };

  const toNode = (element) => {
    const t = tagName(element);
    const active = !!(element.ownerDocument.activeElement === element && element.ownerDocument.hasFocus());
    if (t === 'IFRAME' || t === 'FRAME') {
      const node = { role: 'iframe', name: '', children: [], props: {}, el: element, box: computeBox(element), receivesPointerEvents: true, active: active };
      assignRef(node);
      return node;
    }
    const rawRole = ariaRole(element);
    const role = rawRole || 'generic';
    if (role === 'presentation' || role === 'none') return null;
    const box = computeBox(element);
    if (role === 'generic' && box.inline && element.childNodes.length === 1 && element.childNodes[0].nodeType === 3) return null;
    const sources = new Set();
    const name = nameProhibited[role] ? flat(element.getAttribute('title') || '') : accName(element, false, new Set(), sources);
    const node = { role: role, name: norm(name), children: [], props: {}, el: element, box: box, receivesPointerEvents: receivesPointerEvents(element), active: active };
    nameSources.set(node, sources);
    assignRef(node);
    if (levelRoles[role] && (t.charAt(0) === 'H' || element.hasAttribute('aria-level'))) {
      const lvl = parseInt(t.charAt(0) === 'H' ? t.substring(1) : element.getAttribute('aria-level'), 10);
      if (lvl) node.level = lvl;
    }
    if (checkedRoles[role]) {
      if (element.indeterminate || element.getAttribute('aria-checked') === 'mixed') node.checked = 'mixed';
      else if (element.checked || element.getAttribute('aria-checked') === 'true') node.checked = true;
    }
    if (disabledRoles[role] && (element.disabled || element.getAttribute('aria-disabled') === 'true')) node.disabled = true;
    if (expandedRoles[role] && (element.open || element.getAttribute('aria-expanded') === 'true')) node.expanded = true;
    if (invalidRoles[role]) {
      const inv = element.getAttribute('aria-invalid');
      if (inv && inv !== 'false') node.invalid = inv === 'true' ? true : inv;
    }
    if (role === 'button') {
      const pressed = element.getAttribute('aria-pressed');
      if (pressed === 'mixed') node.pressed = 'mixed';
      else if (pressed === 'true') node.pressed = true;
    }
    if (selectedRoles[role] && (element.selected || element.getAttribute('aria-selected') === 'true')) node.selected = true;
    if ((t === 'INPUT' || t === 'TEXTAREA') && element.type !== 'checkbox' && element.type !== 'radio' && element.type !== 'file') {
      node.children = [String(element.value ?? '')];
    }
    return node;
  };

  const visit = (ariaNode, node, parentVisible, fromSlot) => {
    if (visited.has(node)) return;
    if (!fromSlot && node.assignedSlot) return;
    visited.add(node);
    if (node.nodeType === 3) {
      if (!parentVisible) return;
      if (ariaNode.role !== 'textbox' && node.nodeValue) ariaNode.children.push(node.nodeValue);
      return;
    }
    if (node.nodeType !== 1) return;
    const element = node;
    const hidden = hiddenForAria(element);
    const visible = !hidden || isElementVisible(element);
    const owns = element.hasAttribute('aria-owns') ? idRefs(element, element.getAttribute('aria-owns')) : [];
    const child = visible ? toNode(element) : null;
    if (child && String(element.getAttribute('aria-hidden') || '').toLowerCase() === 'true') child.props['aria-hidden'] = 'true';
    if (child && child.ref) info.set(child.ref, { element: element, nameFromContentRefs: [] });
    if (child) ariaNode.children.push(child);
    process(child || ariaNode, element, owns, visible);
    if (child && child.ref) {
      const sources = nameSources.get(child) || new Set();
      const rec = info.get(child.ref);
      sources.forEach((el) => {
        const r = refs.get(el);
        if (r && r !== child.ref) rec.nameFromContentRefs.push(r);
      });
    }
  };

  const process = (ariaNode, element, owns, parentVisible) => {
    const display = (styleOf(element) || {}).display || 'inline';
    const block = (display !== 'inline' || element.nodeName === 'BR') ? ' ' : '';
    if (block) ariaNode.children.push(block);
    ariaNode.children.push(cssContent(element, '::before'));
    const assigned = element.nodeName === 'SLOT' ? element.assignedNodes() : [];
    if (assigned.length) {
      for (let i = 0; i < assigned.length; i++) visit(ariaNode, assigned[i], parentVisible, true);
    } else {
      for (let c = element.firstChild; c; c = c.nextSibling) {
        if (!c.assignedSlot) visit(ariaNode, c, parentVisible, false);
      }
      if (element.shadowRoot) {
        for (let c = element.shadowRoot.firstChild; c; c = c.nextSibling) visit(ariaNode, c, parentVisible, false);
      }
    }
    for (let i = 0; i < owns.length; i++) visit(ariaNode, owns[i], parentVisible, false);
    ariaNode.children.push(cssContent(element, '::after'));
    if (block) ariaNode.children.push(block);
    if (ariaNode.children.length === 1 && ariaNode.name === ariaNode.children[0]) ariaNode.children = [];
    if (ariaNode.role === 'link' && element.hasAttribute('href')) ariaNode.props.url = truncateDataUrl(element.getAttribute('href') || '');
    if (ariaNode.role === 'textbox' && element.hasAttribute('placeholder') && element.getAttribute('placeholder') !== ariaNode.name) {
      ariaNode.props.placeholder = element.getAttribute('placeholder') || '';
    }
  };

  const isLeafGeneric = (node) => node.role === 'generic' && node.children.every((c) => typeof c === 'string');
  const isClickTargetRoot = (node, ancestors) => !!node.ref && hasPointerCursor(node) && !ancestors.some((a) => !!a.ref && hasPointerCursor(a));

  const distill = (root) => {
    const pending = new Set();
    const ancestors = [];
    const plugins = [
      {
        name: 'mergeStringChildren',
        exit(node) {
          const children = [];
          const buf = [];
          const flush = () => {
            if (!buf.length) return;
            const text = norm(buf.join(''));
            if (text) children.push(text);
            buf.length = 0;
          };
          for (let i = 0; i < node.children.length; i++) {
            const ch = node.children[i];
            if (typeof ch === 'string') buf.push(ch);
            else { flush(); children.push(ch); }
          }
          flush();
          node.children = children;
          if (node.children.length === 1 && node.children[0] === node.name) node.children = [];
        }
      },
      {
        name: 'removeNamelessImages',
        exit(node, ctx) {
          if (node.role === 'img' && !node.name && !node.children.length && !isClickTargetRoot(node, ctx.ancestors)) {
            if (node.ref) pending.add(node.ref);
            return 'remove';
          }
        }
      },
      {
        name: 'removeRedundantNames',
        enter(node, ctx) {
          if (!node.ref) return;
          const rec = info.get(node.ref);
          const nrefs = (rec && rec.nameFromContentRefs) || [];
          for (let i = 0; i < nrefs.length; i++) pending.add(nrefs[i]);
          const beyond = maxDepth != null && ctx.depth > maxDepth;
          if (!beyond && !isLeafGeneric(node)) pending.delete(node.ref);
        },
        exit(node) {
          if (!node.ref) return;
          const rec = info.get(node.ref);
          const nrefs = rec && rec.nameFromContentRefs;
          if (!nrefs || !nrefs.length) return;
          if (nrefs.every((r) => !pending.has(r))) node.name = '';
          else { for (let i = 0; i < nrefs.length; i++) pending.delete(nrefs[i]); }
        }
      },
      {
        name: 'inlineTextIntoGeneric',
        exit(node) {
          if (node.role !== 'generic' || Object.keys(node.props).length || node.children.length !== 1) return;
          const child = node.children[0];
          if (typeof child === 'string') return;
          if (child.role !== 'generic' || child.name || child.active || Object.keys(child.props).length) return;
          if (child.children.length === 1 && typeof child.children[0] === 'string') node.children = [child.children[0]];
        }
      },
      {
        name: 'removeNameRepeatingChild',
        exit(node, ctx) {
          const parent = ctx.ancestors[ctx.ancestors.length - 1];
          if (!parent || !parent.name || node.role !== 'generic' || node.active || Object.keys(node.props).length) return;
          const singleText = node.children.length === 1 && typeof node.children[0] === 'string' ? node.children[0] : undefined;
          const text = node.name ? (node.children.length ? undefined : node.name) : singleText;
          if (text && text === parent.name) {
            if (node.ref) pending.add(node.ref);
            return 'remove';
          }
        }
      },
      {
        name: 'unwrapSingleChildGenerics',
        exit(node, ctx) {
          if (node.role !== 'generic' || node.name || node.children.length > 1 || !node.children.every((c) => typeof c !== 'string' && !!c.ref)) return;
          if (!node.children.length && isClickTargetRoot(node, ctx.ancestors)) return;
          return 'unwrap';
        }
      }
    ];
    const traverse = (node, depth) => {
      const children = [];
      const visitChild = (child) => {
        if (typeof child === 'string') { children.push(child); return; }
        const ctx = { depth: depth + 1, ancestors: ancestors };
        for (let p = 0; p < plugins.length; p++) {
          const result = plugins[p].enter && plugins[p].enter(child, ctx);
          if (result === 'remove') return;
          if (result === 'unwrap') { child.children.forEach(visitChild); return; }
        }
        traverse(child, depth + 1);
        for (let p = 0; p < plugins.length; p++) {
          const result = plugins[p].exit && plugins[p].exit(child, ctx);
          if (result === 'remove') return;
          if (result === 'unwrap') { children.push.apply(children, child.children); return; }
        }
        children.push(child);
      };
      ancestors.push(node);
      node.children.forEach(visitChild);
      ancestors.pop();
      node.children = children;
    };
    const ctx0 = { depth: -1, ancestors: ancestors };
    for (let p = 0; p < plugins.length; p++) { if (plugins[p].enter) plugins[p].enter(root, ctx0); }
    traverse(root, -1);
    for (let p = 0; p < plugins.length; p++) { if (plugins[p].exit) plugins[p].exit(root, ctx0); }
  };

  const jsonString = (value) => '""' + String(value).replace(/\\/g, '\\\\').replace(/""/g, '\\""') + '""';
  const yamlSpecial = (text) => {
    if (!text) return true;
    if (/^(true|false|null|yes|no|on|off|y|n)$/i.test(text)) return true;
    if (text === '-' || /^-?\d+(\.\d+)?$/.test(text)) return true;
    if (/\s/.test(text[0]) || /\s/.test(text[text.length - 1])) return true;
    if (/[:?#[{}\],&*!|>'""`%@]/.test(text[0])) return true;
    if (/:\s|#|[\[\]{}]/.test(text)) return true;
    if (/:$/.test(text)) return true;
    return false;
  };
  const yamlValue = (text) => yamlSpecial(text) ? jsonString(text) : text;
  const nameAsValue = (role) => !!valueRoles[role];

  const write = (node, indent, depth, lines, renderCursor) => {
    if (typeof node === 'string') {
      lines.push('  '.repeat(indent) + '- text: ' + yamlValue(node));
      return;
    }
    const pad = '  '.repeat(indent);
    let key = node.role;
    const hasExtras = !!(node.ref || node.active || node.props['aria-hidden'] || (renderCursor && node.ref && hasPointerCursor(node)));
    const useVal = nameAsValue(node.role) && node.children.length === 0 && !node.props.url && !node.props.placeholder && !hasExtras;
    if (!useVal && node.name) key += ' ' + jsonString(node.name);
    if (node.checked === 'mixed') key += ' [checked=mixed]';
    else if (node.checked) key += ' [checked]';
    if (node.disabled) key += ' [disabled]';
    if (node.expanded) key += ' [expanded]';
    if (node.invalid === 'grammar' || node.invalid === 'spelling') key += ' [invalid=' + node.invalid + ']';
    else if (node.invalid) key += ' [invalid]';
    if (node.level && node.role === 'heading') key += ' [level=' + node.level + ']';
    if (node.pressed === 'mixed') key += ' [pressed=mixed]';
    else if (node.pressed) key += ' [pressed]';
    if (node.selected) key += ' [selected]';
    if (node.props['aria-hidden']) key += ' [aria-hidden]';
    if (node.active) key += ' [active]';
    if (node.ref) key += ' [ref=' + node.ref + ']';
    if (renderCursor && node.ref && hasPointerCursor(node)) key += ' [cursor=pointer]';
    if (renderBoxes && node.el) {
      const r = node.el.getBoundingClientRect();
      key += ' [box=' + Math.round(r.x) + ',' + Math.round(r.y) + ',' + Math.round(r.width) + ',' + Math.round(r.height) + ']';
    }
    const atLimit = maxDepth != null && depth >= maxDepth;
    const kids = [];
    if (node.props.url !== undefined) kids.push({ kind: 'prop', key: '/url', value: node.props.url });
    if (node.props.placeholder !== undefined) kids.push({ kind: 'prop', key: '/placeholder', value: node.props.placeholder });
    const singleText = node.children.length === 1 && typeof node.children[0] === 'string' ? node.children[0] : undefined;
    const inCursor = !!(renderCursor && node.ref && hasPointerCursor(node));
    if (singleText !== undefined && !node.props.url && !node.props.placeholder && !node.name) {
      lines.push(pad + '- ' + key + ': ' + yamlValue(singleText));
      return;
    }
    if (useVal && node.name) {
      lines.push(pad + '- ' + node.role + ': ' + yamlValue(node.name));
      return;
    }
    if (singleText !== undefined && !node.props.url && !node.props.placeholder && node.name && singleText !== node.name) {
      lines.push(pad + '- ' + key + ': ' + yamlValue(singleText));
      return;
    }
    if (!atLimit) {
      for (let i = 0; i < node.children.length; i++) {
        if (typeof node.children[i] === 'string') kids.push({ kind: 'text', value: node.children[i] });
        else kids.push({ kind: 'node', value: node.children[i] });
      }
    } else if (singleText !== undefined) {
      kids.push({ kind: 'text', value: singleText });
    }
    if (!kids.length) {
      lines.push(pad + '- ' + key);
      return;
    }
    if (kids.length === 1 && kids[0].kind === 'text' && !node.props.url && !node.props.placeholder) {
      lines.push(pad + '- ' + key + ': ' + yamlValue(kids[0].value));
      return;
    }
    lines.push(pad + '- ' + key + ':');
    for (let i = 0; i < kids.length; i++) {
      const k = kids[i];
      if (k.kind === 'prop') lines.push('  '.repeat(indent + 1) + '- ' + k.key + ': ' + yamlValue(k.value));
      else if (k.kind === 'text') lines.push('  '.repeat(indent + 1) + '- text: ' + yamlValue(k.value));
      else write(k.value, indent + 1, depth + 1, lines, renderCursor && !inCursor);
    }
  };

  const writeJson = (node, depth, renderCursor) => {
    const out = { role: node.role };
    if (node.name) out.name = node.name;
    if (node.checked === 'mixed' || node.checked === true) out.checked = node.checked;
    if (node.disabled) out.disabled = true;
    if (node.expanded) out.expanded = true;
    if (node.active) out.active = true;
    if (node.invalid) out.invalid = node.invalid;
    if (node.level) out.level = node.level;
    if (node.pressed === 'mixed' || node.pressed === true) out.pressed = node.pressed;
    if (node.selected) out.selected = true;
    if (node.ref) {
      out.ref = node.ref;
      if (renderCursor && hasPointerCursor(node)) out.cursor = 'pointer';
    }
    if (renderBoxes && node.el) {
      const r = node.el.getBoundingClientRect();
      out.box = { x: Math.round(r.x), y: Math.round(r.y), width: Math.round(r.width), height: Math.round(r.height) };
    }
    if (node.props.url !== undefined) out.url = node.props.url;
    if (node.props.placeholder !== undefined) out.placeholder = node.props.placeholder;
    if (node.props['aria-hidden']) out.ariaHidden = true;
    const singleText = node.children.length === 1 && typeof node.children[0] === 'string' ? node.children[0] : undefined;
    const atLimit = maxDepth != null && depth >= maxDepth;
    if (singleText !== undefined) {
      out.text = singleText;
    } else if (!atLimit && node.children.length) {
      const inCursor = !!(renderCursor && node.ref && hasPointerCursor(node));
      out.children = [];
      for (let i = 0; i < node.children.length; i++) {
        const ch = node.children[i];
        if (typeof ch === 'string') out.children.push(ch);
        else out.children.push(writeJson(ch, depth + 1, renderCursor && !inCursor));
      }
    }
    return out;
  };

  const format = ""__PW_FORMAT__"";
  const rootNode = { role: 'fragment', name: '', children: [], props: {}, el: root, box: computeBox(root), receivesPointerEvents: true };
  visit(rootNode, root, true, true);
  distill(rootNode);
  const roots = rootNode.role === 'fragment' ? rootNode.children : [rootNode];
  if (format === 'json') {
    const json = [];
    for (let i = 0; i < roots.length; i++) {
      if (typeof roots[i] === 'string') json.push({ role: 'text', text: roots[i] });
      else json.push(writeJson(roots[i], startDepth, true));
    }
    return JSON.stringify(json);
  }
  const lines = [];
  for (let i = 0; i < roots.length; i++) write(roots[i], 0, startDepth, lines, true);
  return lines.join('\n');
}
";

        /// <summary>
        /// Official AI-mode YAML for <paramref name="root"/>.
        /// </summary>
        /// <param name="root">Snapshot root element.</param>
        /// <param name="depth">Maximum descendant level, or <see langword="null"/>.</param>
        /// <param name="boxes">When <see langword="true"/>, append <c>[box=…]</c>.</param>
        /// <param name="refPrefix">Frame ref prefix, or empty for the first main document.</param>
        /// <param name="startDepth">Depth of the rendered roots in the page tree.</param>
        /// <returns>Playwright AI aria snapshot YAML.</returns>
        internal static Task<string> CaptureYamlAsync(IElementHandle root, int? depth, bool boxes, string refPrefix, int startDepth = 0)
            => CaptureAsync(root, depth, boxes, refPrefix, startDepth, "yaml");

        /// <summary>
        /// Official AI-mode JSON for <paramref name="root"/>.
        /// </summary>
        /// <param name="root">Snapshot root element.</param>
        /// <param name="depth">Maximum descendant level, or <see langword="null"/>.</param>
        /// <param name="boxes">When <see langword="true"/>, include <c>box</c>.</param>
        /// <param name="refPrefix">Frame ref prefix, or empty for the first main document.</param>
        /// <param name="startDepth">Depth of the rendered roots in the page tree.</param>
        /// <returns>Playwright AI aria snapshot JSON.</returns>
        internal static Task<string> CaptureJsonAsync(IElementHandle root, int? depth, bool boxes, string refPrefix, int startDepth = 0)
            => CaptureAsync(root, depth, boxes, refPrefix, startDepth, "json");

        private static async Task<string> CaptureAsync(
            IElementHandle root,
            int? depth,
            bool boxes,
            string refPrefix,
            int startDepth,
            string format)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            string depthJs = depth == null
                ? "undefined"
                : depth.Value.ToString(CultureInfo.InvariantCulture);
            string script = CaptureFunction
                .Replace(DepthToken, depthJs, StringComparison.Ordinal)
                .Replace(BoxesToken, boxes ? "true" : "false", StringComparison.Ordinal)
                .Replace(PrefixToken, refPrefix ?? string.Empty, StringComparison.Ordinal)
                .Replace("__PW_START_DEPTH__", startDepth.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace(FormatToken, format, StringComparison.Ordinal);
            string result = await root.EvaluateAsync<string>(script).ConfigureAwait(false);
            return result ?? (format == "json" ? "[]" : string.Empty);
        }
    }
}
