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
using System.IO;
using System.Reflection;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>role=</c> / <c>internal:role=</c> engine injected into
    /// <see cref="SelectorQuery.ChainEngineScript"/>.
    /// </summary>
    internal static class RoleSelectorEngine
    {
        /// <summary>
        /// Upstream <c>packages/injected/src/roleUtils.ts</c>, bundled into
        /// <c>roleUtilsSource.js</c>. Declares <c>var roleUtils</c> holding the
        /// upstream exports (<c>getAriaRole</c>, <c>getElementAccessibleNameText</c>, ...).
        /// </summary>
        internal static readonly string RoleUtilsSource = LoadRoleUtilsSource();

        /// <summary>
        /// JS helpers used by the selector-chain <c>queryPart</c> role branch.
        /// </summary>
        internal static readonly string Functions = RoleUtilsSource + @"
  const kAriaSelectedRoles = roleUtils.kAriaSelectedRoles;
  const kAriaCheckedRoles = roleUtils.kAriaCheckedRoles;
  const kAriaPressedRoles = roleUtils.kAriaPressedRoles;
  const kAriaExpandedRoles = roleUtils.kAriaExpandedRoles;
  const kAriaLevelRoles = roleUtils.kAriaLevelRoles;
  const kSupportedAttributes = ['checked', 'description', 'disabled', 'expanded', 'include-hidden', 'level', 'name', 'pressed', 'selected'];

  const normalizeWhiteSpace = (text) => text.replace(/[​­]/g, '').trim().replace(/\s+/g, ' ');

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
      if (roleUtils.getAriaRole(element) !== options.role) return;
      if (options.selected !== undefined && roleUtils.getAriaSelected(element) !== options.selected) return;
      if (options.checked !== undefined && roleUtils.getAriaChecked(element) !== options.checked) return;
      if (options.pressed !== undefined && roleUtils.getAriaPressed(element) !== options.pressed) return;
      if (options.expanded !== undefined && roleUtils.getAriaExpanded(element) !== options.expanded) return;
      if (options.level !== undefined && roleUtils.getAriaLevel(element) !== options.level) return;
      if (options.disabled !== undefined && roleUtils.getAriaDisabled(element) !== options.disabled) return;
      if (!options.includeHidden && roleUtils.isElementHiddenForAria(element)) return;
      if (options.name !== undefined) {
        let accessibleName = normalizeWhiteSpace(roleUtils.getElementAccessibleNameText(element, !!options.includeHidden));
        let name = options.name;
        if (typeof name === 'string') name = normalizeWhiteSpace(name);
        let nameOp = options.nameOp || '=';
        if (internalRole && !options.nameExact && nameOp === '=') nameOp = '*=';
        if (!matchesAttributePart(accessibleName, { name: '', jsonPath: [], op: nameOp, value: name, caseSensitive: !!options.nameExact }))
          return;
      }
      if (options.description !== undefined) {
        let accessibleDescription = normalizeWhiteSpace(roleUtils.getElementAccessibleDescription(element, !!options.includeHidden).text);
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
    roleUtils.beginAriaCaches();
    try {
      query(scope);
    } finally {
      roleUtils.endAriaCaches();
    }
    return result;
  };

";

        private static string LoadRoleUtilsSource()
        {
            Assembly assembly = typeof(RoleSelectorEngine).Assembly;
            using Stream stream = assembly.GetManifestResourceStream("PlaywrightNative.Helpers.roleUtilsSource.js")
                ?? throw new PlaywrightException("Bundled Playwright roleUtils source is missing.");
            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
