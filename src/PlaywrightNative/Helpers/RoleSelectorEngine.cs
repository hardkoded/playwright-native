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
    /// Official <c>role=</c> / <c>internal:role=</c> engine injected into
    /// <see cref="SelectorQuery.ChainEngineScript"/>.
    /// </summary>
    internal static class RoleSelectorEngine
    {
        /// <summary>
        /// JS helpers used by the selector-chain <c>queryPart</c> role branch.
        /// </summary>
        internal const string Functions = @"
  const kAriaSelectedRoles = ['gridcell', 'option', 'row', 'tab', 'rowheader', 'columnheader', 'treeitem'];
  const kAriaCheckedRoles = ['checkbox', 'menuitemcheckbox', 'option', 'radio', 'switch', 'menuitemradio', 'treeitem'];
  const kAriaPressedRoles = ['button'];
  const kAriaExpandedRoles = ['application', 'button', 'checkbox', 'combobox', 'gridcell', 'link', 'listbox', 'menuitem', 'row', 'rowheader', 'tab', 'treeitem', 'columnheader', 'menuitemcheckbox', 'menuitemradio', 'switch'];
  const kAriaLevelRoles = ['heading', 'listitem', 'row', 'treeitem'];
  const kAriaDisabledRoles = ['application', 'button', 'composite', 'gridcell', 'group', 'input', 'link', 'menuitem', 'scrollbar', 'separator', 'tab', 'checkbox', 'columnheader', 'combobox', 'grid', 'listbox', 'menu', 'menubar', 'menuitemcheckbox', 'menuitemradio', 'option', 'radio', 'radiogroup', 'row', 'rowheader', 'searchbox', 'select', 'slider', 'spinbutton', 'switch', 'tablist', 'textbox', 'toolbar', 'tree', 'treegrid', 'treeitem'];
  const kSupportedAttributes = ['checked', 'description', 'disabled', 'expanded', 'include-hidden', 'level', 'name', 'pressed', 'selected'];

  const parentElementOrShadowHost = (element) => {
    if (element.parentElement) return element.parentElement;
    if (element.parentNode && element.parentNode.nodeType === 11 && element.parentNode.host)
      return element.parentNode.host;
    return null;
  };

  const elementSafeTagName = (el) => el && el.tagName ? el.tagName : '';

  const getExplicitAriaRole = (element) => {
    const roles = String(element.getAttribute('role') || '').split(' ');
    for (let i = 0; i < roles.length; i++) {
      const r = roles[i].trim();
      if (r) return r;
    }
    return null;
  };

  const getImplicitAriaRole = (element) => {
    const tag = elementSafeTagName(element);
    if (tag === 'BUTTON') return 'button';
    if (tag === 'DETAILS') return 'group';
    if (tag === 'OPTION') return 'option';
    if (tag === 'OUTPUT') return 'status';
    if (tag === 'LI') return 'listitem';
    if (tag === 'TEXTAREA') return 'textbox';
    if (tag === 'A' && element.hasAttribute('href')) return 'link';
    if (tag === 'IMG') return 'img';
    if (tag === 'H1' || tag === 'H2' || tag === 'H3' || tag === 'H4' || tag === 'H5' || tag === 'H6') return 'heading';
    if (tag === 'SELECT') {
      const size = Number(element.size);
      if (element.hasAttribute('multiple') || size > 1) return 'listbox';
      return 'combobox';
    }
    if (tag === 'INPUT') {
      const type = String(element.type || '').toLowerCase();
      if (type === 'hidden') return null;
      if (type === 'checkbox') return 'checkbox';
      if (type === 'radio') return 'radio';
      if (type === 'button' || type === 'submit' || type === 'reset' || type === 'image' || type === 'file') return 'button';
      if (type === 'number') return 'spinbutton';
      if (type === 'range') return 'slider';
      if (type === 'search') return 'searchbox';
      return 'textbox';
    }
    return null;
  };

  const getAriaRole = (element) => getExplicitAriaRole(element) || getImplicitAriaRole(element);

  const getAriaBoolean = (attr) => attr === null ? undefined : String(attr).toLowerCase() === 'true';

  const getComputedStyleSafe = (element) => {
    const view = element.ownerDocument && element.ownerDocument.defaultView;
    return view ? view.getComputedStyle(element) : null;
  };

  const isElementIgnoredForAria = (element) => {
    const tag = elementSafeTagName(element);
    return tag === 'STYLE' || tag === 'SCRIPT' || tag === 'NOSCRIPT' || tag === 'TEMPLATE';
  };

  const isElementStyleVisibilityVisible = (element, style) => {
    const detailsOrSummary = element.closest && element.closest('details,summary');
    if (detailsOrSummary && detailsOrSummary !== element && detailsOrSummary.nodeName === 'DETAILS' && !detailsOrSummary.open)
      return false;
    style = style || getComputedStyleSafe(element);
    if (!style) return true;
    return style.visibility === 'visible';
  };

  const belongsToDisplayNoneOrAriaHiddenOrNonSlotted = (element) => {
    if (element.parentElement && element.parentElement.shadowRoot && !element.assignedSlot)
      return true;
    const style = getComputedStyleSafe(element);
    if (!style || style.display === 'none' || getAriaBoolean(element.getAttribute('aria-hidden')) === true)
      return true;
    const parent = parentElementOrShadowHost(element);
    return parent ? belongsToDisplayNoneOrAriaHiddenOrNonSlotted(parent) : false;
  };

  const isElementHiddenForAria = (element) => {
    if (isElementIgnoredForAria(element)) return true;
    const style = getComputedStyleSafe(element);
    const isSlot = element.nodeName === 'SLOT';
    const isOptionInsideSelect = element.nodeName === 'OPTION' && !!element.closest('select');
    if (!isOptionInsideSelect && !isSlot && !isElementStyleVisibilityVisible(element, style))
      return true;
    return belongsToDisplayNoneOrAriaHiddenOrNonSlotted(element);
  };

  const getAriaSelected = (element) => {
    if (elementSafeTagName(element) === 'OPTION') return !!element.selected;
    if (kAriaSelectedRoles.indexOf(getAriaRole(element) || '') >= 0)
      return getAriaBoolean(element.getAttribute('aria-selected')) === true;
    return false;
  };

  const getChecked = (element) => {
    const tag = elementSafeTagName(element);
    if (tag === 'INPUT' && element.indeterminate) return 'mixed';
    if (tag === 'INPUT' && (element.type === 'checkbox' || element.type === 'radio')) return !!element.checked;
    if (kAriaCheckedRoles.indexOf(getAriaRole(element) || '') >= 0) {
      const checked = element.getAttribute('aria-checked');
      if (checked === 'true') return true;
      if (checked === 'mixed') return 'mixed';
      return false;
    }
    return false;
  };

  const getAriaPressed = (element) => {
    if (kAriaPressedRoles.indexOf(getAriaRole(element) || '') >= 0) {
      const pressed = element.getAttribute('aria-pressed');
      if (pressed === 'true') return true;
      if (pressed === 'mixed') return 'mixed';
    }
    return false;
  };

  const getAriaExpanded = (element) => {
    if (elementSafeTagName(element) === 'DETAILS') return !!element.open;
    if (kAriaExpandedRoles.indexOf(getAriaRole(element) || '') >= 0) {
      const expanded = element.getAttribute('aria-expanded');
      if (expanded === null) return undefined;
      return expanded === 'true';
    }
    return undefined;
  };

  const getAriaLevel = (element) => {
    const native = { H1: 1, H2: 2, H3: 3, H4: 4, H5: 5, H6: 6 }[elementSafeTagName(element)];
    if (native) return native;
    if (kAriaLevelRoles.indexOf(getAriaRole(element) || '') >= 0) {
      const attr = element.getAttribute('aria-level');
      const value = attr === null ? NaN : Number(attr);
      if (Number.isInteger(value) && value >= 1) return value;
    }
    return 0;
  };

  const belongsToDisabledOptGroup = (element) =>
    elementSafeTagName(element) === 'OPTION' && !!element.closest('OPTGROUP[DISABLED]');

  const belongsToDisabledFieldSet = (element) => {
    const fieldSetElement = element && element.closest && element.closest('FIELDSET[DISABLED]');
    if (!fieldSetElement) return false;
    const legendElement = fieldSetElement.querySelector(':scope > LEGEND');
    return !legendElement || !legendElement.contains(element);
  };

  const isNativelyDisabled = (element) => {
    const tag = elementSafeTagName(element);
    const isNative = tag === 'BUTTON' || tag === 'INPUT' || tag === 'SELECT' || tag === 'TEXTAREA' || tag === 'OPTION' || tag === 'OPTGROUP';
    return isNative && (element.hasAttribute('disabled') || belongsToDisabledOptGroup(element) || belongsToDisabledFieldSet(element));
  };

  const hasAriaDisabledInChain = (element) => {
    const attribute = String(element.getAttribute('aria-disabled') || '').toLowerCase();
    if (attribute === 'true') return true;
    if (attribute === 'false') return false;
    const parent = parentElementOrShadowHost(element);
    return parent ? hasAriaDisabledInChain(parent) : false;
  };

  const getAriaDisabled = (element) => {
    if (isNativelyDisabled(element)) return true;
    if (kAriaDisabledRoles.indexOf(getAriaRole(element) || '') < 0) return false;
    return hasAriaDisabledInChain(element);
  };

  const normalizeWhiteSpace = (text) => String(text || '').replace(/[\u200b\u00ad]/g, '').replace(/\s+/g, ' ').trim();

  const textExcluding = (root, skip) => {
    let out = '';
    const walk = (node) => {
      if (!node || node === skip) return;
      if (node.nodeType === 3) { out += node.nodeValue || ''; return; }
      if (node.nodeType !== 1) return;
      const kids = node.childNodes || [];
      for (let i = 0; i < kids.length; i++) walk(kids[i]);
    };
    walk(root);
    return out;
  };

  const getIdRefs = (element, ref) => {
    if (!ref) return [];
    let root = element;
    while (root.parentNode) root = root.parentNode;
    const ids = String(ref).split(' ').filter((id) => !!id);
    const result = [];
    for (let i = 0; i < ids.length; i++) {
      try {
        const first = root.querySelector && root.querySelector('#' + CSS.escape(ids[i]));
        if (first && result.indexOf(first) < 0) result.push(first);
      } catch (e) { }
    }
    return result;
  };

  const getElementAccessibleNameText = (element) => {
    const labelled = element.getAttribute('aria-labelledby');
    if (labelled) {
      const refs = getIdRefs(element, labelled);
      const parts = [];
      for (let i = 0; i < refs.length; i++)
        parts.push(refs[i].textContent || '');
      const joined = parts.join(' ');
      if (normalizeWhiteSpace(joined)) return joined;
    }
    const ariaLabel = element.getAttribute('aria-label');
    if (ariaLabel && String(ariaLabel).trim()) return ariaLabel;
    if (element.labels && element.labels.length) {
      let t = '';
      for (let i = 0; i < element.labels.length; i++)
        t += textExcluding(element.labels[i], element) + ' ';
      if (normalizeWhiteSpace(t)) return t;
    }
    const tag = elementSafeTagName(element);
    if (tag === 'IMG') return element.getAttribute('alt') || '';
    if (tag === 'INPUT') {
      const type = String(element.type || '').toLowerCase();
      if (type === 'submit' || type === 'button' || type === 'reset') return element.value || '';
    }
    return element.textContent || '';
  };

  const getElementAccessibleDescription = (element) => {
    if (element.hasAttribute('aria-describedby')) {
      const refs = getIdRefs(element, element.getAttribute('aria-describedby'));
      const parts = [];
      for (let i = 0; i < refs.length; i++)
        parts.push(refs[i].textContent || '');
      return parts.join(' ');
    }
    if (element.hasAttribute('aria-description'))
      return element.getAttribute('aria-description') || '';
    return element.getAttribute('title') || '';
  };

  const matchesAttributePart = (value, attr) => {
    const objValue = typeof value === 'string' && !attr.caseSensitive ? value.toUpperCase() : value;
    const attrValue = typeof attr.value === 'string' && !attr.caseSensitive ? attr.value.toUpperCase() : attr.value;
    if (attr.op === '<truthy>') return !!objValue;
    if (attr.op === '=') {
      if (attrValue instanceof RegExp)
        return typeof objValue === 'string' && !!objValue.match(attrValue);
      return objValue === attrValue;
    }
    if (typeof objValue !== 'string' || typeof attrValue !== 'string') return false;
    if (attr.op === '*=') return objValue.indexOf(attrValue) >= 0;
    if (attr.op === '^=') return objValue.indexOf(attrValue) === 0;
    if (attr.op === '$=') return objValue.length >= attrValue.length && objValue.slice(-attrValue.length) === attrValue;
    if (attr.op === '|=') return objValue === attrValue || objValue.indexOf(attrValue + '-') === 0;
    if (attr.op === '~=') return objValue.split(' ').indexOf(attrValue) >= 0;
    return false;
  };

  const parseAttributeSelector = (selector, allowUnquotedStrings) => {
    let wp = 0;
    let EOL = selector.length === 0;
    const next = () => selector[wp] || '';
    const eat1 = () => {
      const result = next();
      ++wp;
      EOL = wp >= selector.length;
      return result;
    };
    const syntaxError = (stage) => {
      if (EOL)
        throw new Error('Unexpected end of selector while parsing selector `' + selector + '`');
      throw new Error('Error while parsing selector `' + selector + '` - unexpected symbol ""' + next() + '"" at position ' + wp + (stage ? ' during ' + stage : ''));
    };
    const skipSpaces = () => { while (!EOL && /\s/.test(next())) eat1(); };
    const isCSSNameChar = (char) =>
      (char >= '\u0080') ||
      (char >= '0' && char <= '9') ||
      (char >= 'A' && char <= 'Z') ||
      (char >= 'a' && char <= 'z') ||
      char === '_' || char === '-';
    const readIdentifier = () => {
      let result = '';
      skipSpaces();
      while (!EOL && isCSSNameChar(next())) result += eat1();
      return result;
    };
    const readQuotedString = (quote) => {
      let result = eat1();
      if (result !== quote) syntaxError('parsing quoted string');
      while (!EOL && next() !== quote) {
        if (next() === '\\') eat1();
        result += eat1();
      }
      if (next() !== quote) syntaxError('parsing quoted string');
      result += eat1();
      return result;
    };
    const readRegularExpression = () => {
      if (eat1() !== '/') syntaxError('parsing regular expression');
      let source = '';
      let inClass = false;
      while (!EOL) {
        if (next() === '\\') {
          source += eat1();
          if (EOL) syntaxError('parsing regular expression');
        } else if (inClass && next() === ']') {
          inClass = false;
        } else if (!inClass && next() === '[') {
          inClass = true;
        } else if (!inClass && next() === '/') {
          break;
        }
        source += eat1();
      }
      if (eat1() !== '/') syntaxError('parsing regular expression');
      let flags = '';
      while (!EOL && /[dgimsuy]/.test(next())) flags += eat1();
      try { return new RegExp(source, flags); }
      catch (e) { throw new Error('Error while parsing selector `' + selector + '`: ' + (e && e.message ? e.message : e)); }
    };
    const readAttributeToken = () => {
      let token = '';
      skipSpaces();
      if (next() === dq || next() === sq)
        token = readQuotedString(next()).slice(1, -1);
      else
        token = readIdentifier();
      if (!token) syntaxError('parsing property path');
      return token;
    };
    const readOperator = () => {
      skipSpaces();
      let op = '';
      if (!EOL) op += eat1();
      if (!EOL && op !== '=') op += eat1();
      if (['=', '*=', '^=', '$=', '|=', '~='].indexOf(op) < 0) syntaxError('parsing operator');
      return op;
    };
    const readAttribute = () => {
      eat1();
      const jsonPath = [];
      jsonPath.push(readAttributeToken());
      skipSpaces();
      while (next() === '.') {
        eat1();
        jsonPath.push(readAttributeToken());
        skipSpaces();
      }
      if (next() === ']') {
        eat1();
        return { name: jsonPath.join('.'), jsonPath: jsonPath, op: '<truthy>', value: true, caseSensitive: false };
      }
      const operator = readOperator();
      let value = undefined;
      let caseSensitive = true;
      skipSpaces();
      if (next() === '/') {
        if (operator !== '=')
          throw new Error('Error while parsing selector `' + selector + '` - cannot use ' + operator + ' in attribute with regular expression');
        value = readRegularExpression();
      } else if (next() === dq || next() === sq) {
        value = readQuotedString(next()).slice(1, -1);
        skipSpaces();
        if (next() === 'i' || next() === 'I') { caseSensitive = false; eat1(); }
        else if (next() === 's' || next() === 'S') { caseSensitive = true; eat1(); }
      } else {
        value = '';
        while (!EOL && (isCSSNameChar(next()) || next() === '+' || next() === '.'))
          value += eat1();
        if (value === 'true') value = true;
        else if (value === 'false') value = false;
        else if (!allowUnquotedStrings) {
          value = +value;
          if (Number.isNaN(value)) syntaxError('parsing attribute value');
        }
      }
      skipSpaces();
      if (next() !== ']') syntaxError('parsing attribute value');
      eat1();
      if (operator !== '=' && typeof value !== 'string')
        throw new Error('Error while parsing selector `' + selector + '` - cannot use ' + operator + ' in attribute with non-string matching value - ' + value);
      return { name: jsonPath.join('.'), jsonPath: jsonPath, op: operator, value: value, caseSensitive: caseSensitive };
    };
    const result = { name: '', attributes: [] };
    result.name = readIdentifier();
    skipSpaces();
    while (next() === '[') {
      result.attributes.push(readAttribute());
      skipSpaces();
    }
    if (!EOL) syntaxError(undefined);
    if (!result.name && !result.attributes.length)
      throw new Error('Error while parsing selector `' + selector + '` - selector cannot be empty');
    return result;
  };

  const validateSupportedRole = (attr, roles, role) => {
    if (roles.indexOf(role) < 0)
      throw new Error(dq + attr + dq + ' attribute is only supported for roles: ' + roles.slice().sort().map((r) => dq + r + dq).join(', '));
  };

  const validateSupportedValues = (attr, values) => {
    if (attr.op !== '<truthy>' && values.indexOf(attr.value) < 0)
      throw new Error(dq + attr.name + dq + ' must be one of ' + values.map((v) => JSON.stringify(v)).join(', '));
  };

  const validateSupportedOp = (attr, ops) => {
    if (ops.indexOf(attr.op) < 0)
      throw new Error(dq + attr.name + dq + ' does not support ' + dq + attr.op + dq + ' matcher');
  };

  const validateAttributes = (attrs, role) => {
    const options = { role: role };
    for (let i = 0; i < attrs.length; i++) {
      const attr = attrs[i];
      switch (attr.name) {
        case 'checked':
          validateSupportedRole(attr.name, kAriaCheckedRoles, role);
          validateSupportedValues(attr, [true, false, 'mixed']);
          validateSupportedOp(attr, ['<truthy>', '=']);
          options.checked = attr.op === '<truthy>' ? true : attr.value;
          break;
        case 'pressed':
          validateSupportedRole(attr.name, kAriaPressedRoles, role);
          validateSupportedValues(attr, [true, false, 'mixed']);
          validateSupportedOp(attr, ['<truthy>', '=']);
          options.pressed = attr.op === '<truthy>' ? true : attr.value;
          break;
        case 'selected':
          validateSupportedRole(attr.name, kAriaSelectedRoles, role);
          validateSupportedValues(attr, [true, false]);
          validateSupportedOp(attr, ['<truthy>', '=']);
          options.selected = attr.op === '<truthy>' ? true : attr.value;
          break;
        case 'expanded':
          validateSupportedRole(attr.name, kAriaExpandedRoles, role);
          validateSupportedValues(attr, [true, false]);
          validateSupportedOp(attr, ['<truthy>', '=']);
          options.expanded = attr.op === '<truthy>' ? true : attr.value;
          break;
        case 'level':
          validateSupportedRole(attr.name, kAriaLevelRoles, role);
          if (typeof attr.value === 'string') attr.value = +attr.value;
          if (attr.op !== '=' || typeof attr.value !== 'number' || Number.isNaN(attr.value))
            throw new Error(dq + 'level' + dq + ' attribute must be compared to a number');
          options.level = attr.value;
          break;
        case 'disabled':
          validateSupportedValues(attr, [true, false]);
          validateSupportedOp(attr, ['<truthy>', '=']);
          options.disabled = attr.op === '<truthy>' ? true : attr.value;
          break;
        case 'name':
          if (attr.op === '<truthy>')
            throw new Error(dq + 'name' + dq + ' attribute must have a value');
          if (typeof attr.value !== 'string' && !(attr.value instanceof RegExp))
            throw new Error(dq + 'name' + dq + ' attribute must be a string or a regular expression');
          options.name = attr.value;
          options.nameOp = attr.op;
          options.nameExact = attr.caseSensitive;
          break;
        case 'description':
          if (attr.op === '<truthy>')
            throw new Error(dq + 'description' + dq + ' attribute must have a value');
          if (typeof attr.value !== 'string' && !(attr.value instanceof RegExp))
            throw new Error(dq + 'description' + dq + ' attribute must be a string or a regular expression');
          options.description = attr.value;
          options.descriptionOp = attr.op;
          options.descriptionExact = attr.caseSensitive;
          break;
        case 'include-hidden':
          validateSupportedValues(attr, [true, false]);
          validateSupportedOp(attr, ['<truthy>', '=']);
          options.includeHidden = attr.op === '<truthy>' ? true : attr.value;
          break;
        default:
          throw new Error('Unknown attribute ' + dq + attr.name + dq + ', must be one of ' + kSupportedAttributes.map((a) => dq + a + dq).join(', ') + '.');
      }
    }
    return options;
  };

  const queryRoleAll = (scope, selector, internalRole) => {
    const parsed = parseAttributeSelector(selector, true);
    const role = String(parsed.name || '').toLowerCase();
    if (!role) throw new Error('Role must not be empty');
    const options = validateAttributes(parsed.attributes, role);
    const result = [];
    const match = (element) => {
      if (getAriaRole(element) !== options.role) return;
      if (options.selected !== undefined && getAriaSelected(element) !== options.selected) return;
      if (options.checked !== undefined && getChecked(element) !== options.checked) return;
      if (options.pressed !== undefined && getAriaPressed(element) !== options.pressed) return;
      if (options.expanded !== undefined && getAriaExpanded(element) !== options.expanded) return;
      if (options.level !== undefined && getAriaLevel(element) !== options.level) return;
      if (options.disabled !== undefined && getAriaDisabled(element) !== options.disabled) return;
      if (!options.includeHidden && isElementHiddenForAria(element)) return;
      if (options.name !== undefined) {
        let accessibleName = normalizeWhiteSpace(getElementAccessibleNameText(element));
        let name = options.name;
        if (typeof name === 'string') name = normalizeWhiteSpace(name);
        let nameOp = options.nameOp || '=';
        if (internalRole && !options.nameExact && nameOp === '=') nameOp = '*=';
        if (!matchesAttributePart(accessibleName, { name: '', jsonPath: [], op: nameOp, value: name, caseSensitive: !!options.nameExact }))
          return;
      }
      if (options.description !== undefined) {
        let accessibleDescription = normalizeWhiteSpace(getElementAccessibleDescription(element));
        let description = options.description;
        if (typeof description === 'string') description = normalizeWhiteSpace(description);
        let descriptionOp = options.descriptionOp || '=';
        if (internalRole && !options.descriptionExact && descriptionOp === '=') descriptionOp = '*=';
        if (!matchesAttributePart(accessibleDescription, { name: '', jsonPath: [], op: descriptionOp, value: description, caseSensitive: !!options.descriptionExact }))
          return;
      }
      result.push(element);
    };
    const query = (root) => {
      const shadows = [];
      if (root && root.shadowRoot) shadows.push(root.shadowRoot);
      const all = (root && root.querySelectorAll) ? root.querySelectorAll('*') : [];
      for (let i = 0; i < all.length; i++) {
        match(all[i]);
        if (all[i].shadowRoot) shadows.push(all[i].shadowRoot);
      }
      for (let s = 0; s < shadows.length; s++) query(shadows[s]);
    };
    query(scope);
    return result;
  };

";
    }
}
