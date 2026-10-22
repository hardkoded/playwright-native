// Copyright (c) Microsoft Corporation.
// Modifications copyright (c) Microsoft Corporation.
// Licensed under the Apache License, Version 2.0.
//
// Generated from microsoft/playwright@7ad3fba1a packages/injected/src/roleUtils.ts
// (with the domUtils.ts and isomorphic/cssTokenizer.ts code it imports; the
// tokenizer is CC0, from https://github.com/tabatkins/parse-css) by:
//   esbuild entry.ts --bundle --format=iife --global-name=roleUtils --target=ES2019
// where entry.ts is `export * from 'packages/injected/src/roleUtils';`.
// Do not edit by hand. Regenerate it when porting upstream roleUtils changes.
var roleUtils = (() => {
  var __defProp = Object.defineProperty;
  var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
  var __getOwnPropNames = Object.getOwnPropertyNames;
  var __hasOwnProp = Object.prototype.hasOwnProperty;
  var __export = (target, all) => {
    for (var name in all)
      __defProp(target, name, { get: all[name], enumerable: true });
  };
  var __copyProps = (to, from, except, desc) => {
    if (from && typeof from === "object" || typeof from === "function") {
      for (let key of __getOwnPropNames(from))
        if (!__hasOwnProp.call(to, key) && key !== except)
          __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
    }
    return to;
  };
  var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);

  // entry.ts
  var entry_exports = {};
  __export(entry_exports, {
    beginAriaCaches: () => beginAriaCaches,
    endAriaCaches: () => endAriaCaches,
    getAriaChecked: () => getAriaChecked,
    getAriaDisabled: () => getAriaDisabled,
    getAriaExpanded: () => getAriaExpanded,
    getAriaInvalid: () => getAriaInvalid,
    getAriaLabelledByElements: () => getAriaLabelledByElements,
    getAriaLevel: () => getAriaLevel,
    getAriaPressed: () => getAriaPressed,
    getAriaRole: () => getAriaRole,
    getAriaSelected: () => getAriaSelected,
    getCSSContent: () => getCSSContent,
    getCheckedAllowMixed: () => getCheckedAllowMixed,
    getCheckedWithoutMixed: () => getCheckedWithoutMixed,
    getElementAccessibleDescription: () => getElementAccessibleDescription,
    getElementAccessibleErrorMessage: () => getElementAccessibleErrorMessage,
    getElementAccessibleName: () => getElementAccessibleName,
    getElementAccessibleNameText: () => getElementAccessibleNameText,
    getReadonly: () => getReadonly,
    isElementHiddenForAria: () => isElementHiddenForAria,
    isElementIgnoredForAria: () => isElementIgnoredForAria,
    kAriaCheckedRoles: () => kAriaCheckedRoles,
    kAriaDisabledRoles: () => kAriaDisabledRoles,
    kAriaExpandedRoles: () => kAriaExpandedRoles,
    kAriaInvalidRoles: () => kAriaInvalidRoles,
    kAriaLevelRoles: () => kAriaLevelRoles,
    kAriaPressedRoles: () => kAriaPressedRoles,
    kAriaSelectedRoles: () => kAriaSelectedRoles,
    receivesPointerEvents: () => receivesPointerEvents
  });

  // packages/isomorphic/cssTokenizer.ts
  var between = function(num, first, last) {
    return num >= first && num <= last;
  };
  function digit(code) {
    return between(code, 48, 57);
  }
  function hexdigit(code) {
    return digit(code) || between(code, 65, 70) || between(code, 97, 102);
  }
  function uppercaseletter(code) {
    return between(code, 65, 90);
  }
  function lowercaseletter(code) {
    return between(code, 97, 122);
  }
  function letter(code) {
    return uppercaseletter(code) || lowercaseletter(code);
  }
  function nonascii(code) {
    return code >= 128;
  }
  function namestartchar(code) {
    return letter(code) || nonascii(code) || code === 95;
  }
  function namechar(code) {
    return namestartchar(code) || digit(code) || code === 45;
  }
  function nonprintable(code) {
    return between(code, 0, 8) || code === 11 || between(code, 14, 31) || code === 127;
  }
  function newline(code) {
    return code === 10;
  }
  function whitespace(code) {
    return newline(code) || code === 9 || code === 32;
  }
  var maximumallowedcodepoint = 1114111;
  var InvalidCharacterError = class extends Error {
    constructor(message) {
      super(message);
      this.name = "InvalidCharacterError";
    }
  };
  function preprocess(str) {
    const codepoints = [];
    for (let i = 0; i < str.length; i++) {
      let code = str.charCodeAt(i);
      if (code === 13 && str.charCodeAt(i + 1) === 10) {
        code = 10;
        i++;
      }
      if (code === 13 || code === 12)
        code = 10;
      if (code === 0)
        code = 65533;
      if (between(code, 55296, 56319) && between(str.charCodeAt(i + 1), 56320, 57343)) {
        const lead = code - 55296;
        const trail = str.charCodeAt(i + 1) - 56320;
        code = Math.pow(2, 16) + lead * Math.pow(2, 10) + trail;
        i++;
      }
      codepoints.push(code);
    }
    return codepoints;
  }
  function stringFromCode(code) {
    if (code <= 65535)
      return String.fromCharCode(code);
    code -= Math.pow(2, 16);
    const lead = Math.floor(code / Math.pow(2, 10)) + 55296;
    const trail = code % Math.pow(2, 10) + 56320;
    return String.fromCharCode(lead) + String.fromCharCode(trail);
  }
  function tokenize(str1) {
    const str = preprocess(str1);
    let i = -1;
    const tokens = [];
    let code;
    let line = 0;
    let column = 0;
    let lastLineLength = 0;
    const incrLineno = function() {
      line += 1;
      lastLineLength = column;
      column = 0;
    };
    const locStart = { line, column };
    const codepoint = function(i2) {
      if (i2 >= str.length)
        return -1;
      return str[i2];
    };
    const next = function(num) {
      if (num === void 0)
        num = 1;
      if (num > 3)
        throw "Spec Error: no more than three codepoints of lookahead.";
      return codepoint(i + num);
    };
    const consume = function(num) {
      if (num === void 0)
        num = 1;
      i += num;
      code = codepoint(i);
      if (newline(code))
        incrLineno();
      else
        column += num;
      return true;
    };
    const reconsume = function() {
      i -= 1;
      if (newline(code)) {
        line -= 1;
        column = lastLineLength;
      } else {
        column -= 1;
      }
      locStart.line = line;
      locStart.column = column;
      return true;
    };
    const eof = function(codepoint2) {
      if (codepoint2 === void 0)
        codepoint2 = code;
      return codepoint2 === -1;
    };
    const donothing = function() {
    };
    const parseerror = function() {
    };
    const consumeAToken = function() {
      consumeComments();
      consume();
      if (whitespace(code)) {
        while (whitespace(next()))
          consume();
        return new WhitespaceToken();
      } else if (code === 34) {
        return consumeAStringToken();
      } else if (code === 35) {
        if (namechar(next()) || areAValidEscape(next(1), next(2))) {
          const token = new HashToken("");
          if (wouldStartAnIdentifier(next(1), next(2), next(3)))
            token.type = "id";
          token.value = consumeAName();
          return token;
        } else {
          return new DelimToken(code);
        }
      } else if (code === 36) {
        if (next() === 61) {
          consume();
          return new SuffixMatchToken();
        } else {
          return new DelimToken(code);
        }
      } else if (code === 39) {
        return consumeAStringToken();
      } else if (code === 40) {
        return new OpenParenToken();
      } else if (code === 41) {
        return new CloseParenToken();
      } else if (code === 42) {
        if (next() === 61) {
          consume();
          return new SubstringMatchToken();
        } else {
          return new DelimToken(code);
        }
      } else if (code === 43) {
        if (startsWithANumber()) {
          reconsume();
          return consumeANumericToken();
        } else {
          return new DelimToken(code);
        }
      } else if (code === 44) {
        return new CommaToken();
      } else if (code === 45) {
        if (startsWithANumber()) {
          reconsume();
          return consumeANumericToken();
        } else if (next(1) === 45 && next(2) === 62) {
          consume(2);
          return new CDCToken();
        } else if (startsWithAnIdentifier()) {
          reconsume();
          return consumeAnIdentlikeToken();
        } else {
          return new DelimToken(code);
        }
      } else if (code === 46) {
        if (startsWithANumber()) {
          reconsume();
          return consumeANumericToken();
        } else {
          return new DelimToken(code);
        }
      } else if (code === 58) {
        return new ColonToken();
      } else if (code === 59) {
        return new SemicolonToken();
      } else if (code === 60) {
        if (next(1) === 33 && next(2) === 45 && next(3) === 45) {
          consume(3);
          return new CDOToken();
        } else {
          return new DelimToken(code);
        }
      } else if (code === 64) {
        if (wouldStartAnIdentifier(next(1), next(2), next(3)))
          return new AtKeywordToken(consumeAName());
        else
          return new DelimToken(code);
      } else if (code === 91) {
        return new OpenSquareToken();
      } else if (code === 92) {
        if (startsWithAValidEscape()) {
          reconsume();
          return consumeAnIdentlikeToken();
        } else {
          parseerror();
          return new DelimToken(code);
        }
      } else if (code === 93) {
        return new CloseSquareToken();
      } else if (code === 94) {
        if (next() === 61) {
          consume();
          return new PrefixMatchToken();
        } else {
          return new DelimToken(code);
        }
      } else if (code === 123) {
        return new OpenCurlyToken();
      } else if (code === 124) {
        if (next() === 61) {
          consume();
          return new DashMatchToken();
        } else if (next() === 124) {
          consume();
          return new ColumnToken();
        } else {
          return new DelimToken(code);
        }
      } else if (code === 125) {
        return new CloseCurlyToken();
      } else if (code === 126) {
        if (next() === 61) {
          consume();
          return new IncludeMatchToken();
        } else {
          return new DelimToken(code);
        }
      } else if (digit(code)) {
        reconsume();
        return consumeANumericToken();
      } else if (namestartchar(code)) {
        reconsume();
        return consumeAnIdentlikeToken();
      } else if (eof()) {
        return new EOFToken();
      } else {
        return new DelimToken(code);
      }
    };
    const consumeComments = function() {
      while (next(1) === 47 && next(2) === 42) {
        consume(2);
        while (true) {
          consume();
          if (code === 42 && next() === 47) {
            consume();
            break;
          } else if (eof()) {
            parseerror();
            return;
          }
        }
      }
    };
    const consumeANumericToken = function() {
      const num = consumeANumber();
      if (wouldStartAnIdentifier(next(1), next(2), next(3))) {
        const token = new DimensionToken();
        token.value = num.value;
        token.repr = num.repr;
        token.type = num.type;
        token.unit = consumeAName();
        return token;
      } else if (next() === 37) {
        consume();
        const token = new PercentageToken();
        token.value = num.value;
        token.repr = num.repr;
        return token;
      } else {
        const token = new NumberToken();
        token.value = num.value;
        token.repr = num.repr;
        token.type = num.type;
        return token;
      }
    };
    const consumeAnIdentlikeToken = function() {
      const str2 = consumeAName();
      if (str2.toLowerCase() === "url" && next() === 40) {
        consume();
        while (whitespace(next(1)) && whitespace(next(2)))
          consume();
        if (next() === 34 || next() === 39)
          return new FunctionToken(str2);
        else if (whitespace(next()) && (next(2) === 34 || next(2) === 39))
          return new FunctionToken(str2);
        else
          return consumeAURLToken();
      } else if (next() === 40) {
        consume();
        return new FunctionToken(str2);
      } else {
        return new IdentToken(str2);
      }
    };
    const consumeAStringToken = function(endingCodePoint) {
      if (endingCodePoint === void 0)
        endingCodePoint = code;
      let string = "";
      while (consume()) {
        if (code === endingCodePoint || eof()) {
          return new StringToken(string);
        } else if (newline(code)) {
          parseerror();
          reconsume();
          return new BadStringToken();
        } else if (code === 92) {
          if (eof(next()))
            donothing();
          else if (newline(next()))
            consume();
          else
            string += stringFromCode(consumeEscape());
        } else {
          string += stringFromCode(code);
        }
      }
      throw new Error("Internal error");
    };
    const consumeAURLToken = function() {
      const token = new URLToken("");
      while (whitespace(next()))
        consume();
      if (eof(next()))
        return token;
      while (consume()) {
        if (code === 41 || eof()) {
          return token;
        } else if (whitespace(code)) {
          while (whitespace(next()))
            consume();
          if (next() === 41 || eof(next())) {
            consume();
            return token;
          } else {
            consumeTheRemnantsOfABadURL();
            return new BadURLToken();
          }
        } else if (code === 34 || code === 39 || code === 40 || nonprintable(code)) {
          parseerror();
          consumeTheRemnantsOfABadURL();
          return new BadURLToken();
        } else if (code === 92) {
          if (startsWithAValidEscape()) {
            token.value += stringFromCode(consumeEscape());
          } else {
            parseerror();
            consumeTheRemnantsOfABadURL();
            return new BadURLToken();
          }
        } else {
          token.value += stringFromCode(code);
        }
      }
      throw new Error("Internal error");
    };
    const consumeEscape = function() {
      consume();
      if (hexdigit(code)) {
        const digits = [code];
        for (let total = 0; total < 5; total++) {
          if (hexdigit(next())) {
            consume();
            digits.push(code);
          } else {
            break;
          }
        }
        if (whitespace(next()))
          consume();
        let value = parseInt(digits.map(function(x) {
          return String.fromCharCode(x);
        }).join(""), 16);
        if (value > maximumallowedcodepoint)
          value = 65533;
        return value;
      } else if (eof()) {
        return 65533;
      } else {
        return code;
      }
    };
    const areAValidEscape = function(c1, c2) {
      if (c1 !== 92)
        return false;
      if (newline(c2))
        return false;
      return true;
    };
    const startsWithAValidEscape = function() {
      return areAValidEscape(code, next());
    };
    const wouldStartAnIdentifier = function(c1, c2, c3) {
      if (c1 === 45)
        return namestartchar(c2) || c2 === 45 || areAValidEscape(c2, c3);
      else if (namestartchar(c1))
        return true;
      else if (c1 === 92)
        return areAValidEscape(c1, c2);
      else
        return false;
    };
    const startsWithAnIdentifier = function() {
      return wouldStartAnIdentifier(code, next(1), next(2));
    };
    const wouldStartANumber = function(c1, c2, c3) {
      if (c1 === 43 || c1 === 45) {
        if (digit(c2))
          return true;
        if (c2 === 46 && digit(c3))
          return true;
        return false;
      } else if (c1 === 46) {
        if (digit(c2))
          return true;
        return false;
      } else if (digit(c1)) {
        return true;
      } else {
        return false;
      }
    };
    const startsWithANumber = function() {
      return wouldStartANumber(code, next(1), next(2));
    };
    const consumeAName = function() {
      let result = "";
      while (consume()) {
        if (namechar(code)) {
          result += stringFromCode(code);
        } else if (startsWithAValidEscape()) {
          result += stringFromCode(consumeEscape());
        } else {
          reconsume();
          return result;
        }
      }
      throw new Error("Internal parse error");
    };
    const consumeANumber = function() {
      let repr = "";
      let type = "integer";
      if (next() === 43 || next() === 45) {
        consume();
        repr += stringFromCode(code);
      }
      while (digit(next())) {
        consume();
        repr += stringFromCode(code);
      }
      if (next(1) === 46 && digit(next(2))) {
        consume();
        repr += stringFromCode(code);
        consume();
        repr += stringFromCode(code);
        type = "number";
        while (digit(next())) {
          consume();
          repr += stringFromCode(code);
        }
      }
      const c1 = next(1);
      const c2 = next(2);
      const c3 = next(3);
      if ((c1 === 69 || c1 === 101) && digit(c2)) {
        consume();
        repr += stringFromCode(code);
        consume();
        repr += stringFromCode(code);
        type = "number";
        while (digit(next())) {
          consume();
          repr += stringFromCode(code);
        }
      } else if ((c1 === 69 || c1 === 101) && (c2 === 43 || c2 === 45) && digit(c3)) {
        consume();
        repr += stringFromCode(code);
        consume();
        repr += stringFromCode(code);
        consume();
        repr += stringFromCode(code);
        type = "number";
        while (digit(next())) {
          consume();
          repr += stringFromCode(code);
        }
      }
      const value = convertAStringToANumber(repr);
      return { type, value, repr };
    };
    const convertAStringToANumber = function(string) {
      return +string;
    };
    const consumeTheRemnantsOfABadURL = function() {
      while (consume()) {
        if (code === 41 || eof()) {
          return;
        } else if (startsWithAValidEscape()) {
          consumeEscape();
          donothing();
        } else {
          donothing();
        }
      }
    };
    let iterationCount = 0;
    while (!eof(next())) {
      tokens.push(consumeAToken());
      iterationCount++;
      if (iterationCount > str.length * 2)
        throw new Error("I'm infinite-looping!");
    }
    return tokens;
  }
  var CSSParserToken = class {
    constructor() {
      this.tokenType = "";
    }
    toJSON() {
      return { token: this.tokenType };
    }
    toString() {
      return this.tokenType;
    }
    toSource() {
      return "" + this;
    }
  };
  var BadStringToken = class extends CSSParserToken {
    constructor() {
      super(...arguments);
      this.tokenType = "BADSTRING";
    }
  };
  var BadURLToken = class extends CSSParserToken {
    constructor() {
      super(...arguments);
      this.tokenType = "BADURL";
    }
  };
  var WhitespaceToken = class extends CSSParserToken {
    constructor() {
      super(...arguments);
      this.tokenType = "WHITESPACE";
    }
    toString() {
      return "WS";
    }
    toSource() {
      return " ";
    }
  };
  var CDOToken = class extends CSSParserToken {
    constructor() {
      super(...arguments);
      this.tokenType = "CDO";
    }
    toSource() {
      return "<!--";
    }
  };
  var CDCToken = class extends CSSParserToken {
    constructor() {
      super(...arguments);
      this.tokenType = "CDC";
    }
    toSource() {
      return "-->";
    }
  };
  var ColonToken = class extends CSSParserToken {
    constructor() {
      super(...arguments);
      this.tokenType = ":";
    }
  };
  var SemicolonToken = class extends CSSParserToken {
    constructor() {
      super(...arguments);
      this.tokenType = ";";
    }
  };
  var CommaToken = class extends CSSParserToken {
    constructor() {
      super(...arguments);
      this.tokenType = ",";
    }
  };
  var GroupingToken = class extends CSSParserToken {
    constructor() {
      super(...arguments);
      this.value = "";
      this.mirror = "";
    }
  };
  var OpenCurlyToken = class extends GroupingToken {
    constructor() {
      super();
      this.tokenType = "{";
      this.value = "{";
      this.mirror = "}";
    }
  };
  var CloseCurlyToken = class extends GroupingToken {
    constructor() {
      super();
      this.tokenType = "}";
      this.value = "}";
      this.mirror = "{";
    }
  };
  var OpenSquareToken = class extends GroupingToken {
    constructor() {
      super();
      this.tokenType = "[";
      this.value = "[";
      this.mirror = "]";
    }
  };
  var CloseSquareToken = class extends GroupingToken {
    constructor() {
      super();
      this.tokenType = "]";
      this.value = "]";
      this.mirror = "[";
    }
  };
  var OpenParenToken = class extends GroupingToken {
    constructor() {
      super();
      this.tokenType = "(";
      this.value = "(";
      this.mirror = ")";
    }
  };
  var CloseParenToken = class extends GroupingToken {
    constructor() {
      super();
      this.tokenType = ")";
      this.value = ")";
      this.mirror = "(";
    }
  };
  var IncludeMatchToken = class extends CSSParserToken {
    constructor() {
      super(...arguments);
      this.tokenType = "~=";
    }
  };
  var DashMatchToken = class extends CSSParserToken {
    constructor() {
      super(...arguments);
      this.tokenType = "|=";
    }
  };
  var PrefixMatchToken = class extends CSSParserToken {
    constructor() {
      super(...arguments);
      this.tokenType = "^=";
    }
  };
  var SuffixMatchToken = class extends CSSParserToken {
    constructor() {
      super(...arguments);
      this.tokenType = "$=";
    }
  };
  var SubstringMatchToken = class extends CSSParserToken {
    constructor() {
      super(...arguments);
      this.tokenType = "*=";
    }
  };
  var ColumnToken = class extends CSSParserToken {
    constructor() {
      super(...arguments);
      this.tokenType = "||";
    }
  };
  var EOFToken = class extends CSSParserToken {
    constructor() {
      super(...arguments);
      this.tokenType = "EOF";
    }
    toSource() {
      return "";
    }
  };
  var DelimToken = class extends CSSParserToken {
    constructor(code) {
      super();
      this.tokenType = "DELIM";
      this.value = "";
      this.value = stringFromCode(code);
    }
    toString() {
      return "DELIM(" + this.value + ")";
    }
    toJSON() {
      const json = this.constructor.prototype.constructor.prototype.toJSON.call(this);
      json.value = this.value;
      return json;
    }
    toSource() {
      if (this.value === "\\")
        return "\\\n";
      else
        return this.value;
    }
  };
  var StringValuedToken = class extends CSSParserToken {
    constructor() {
      super(...arguments);
      this.value = "";
    }
    ASCIIMatch(str) {
      return this.value.toLowerCase() === str.toLowerCase();
    }
    toJSON() {
      const json = this.constructor.prototype.constructor.prototype.toJSON.call(this);
      json.value = this.value;
      return json;
    }
  };
  var IdentToken = class extends StringValuedToken {
    constructor(val) {
      super();
      this.tokenType = "IDENT";
      this.value = val;
    }
    toString() {
      return "IDENT(" + this.value + ")";
    }
    toSource() {
      return escapeIdent(this.value);
    }
  };
  var FunctionToken = class extends StringValuedToken {
    constructor(val) {
      super();
      this.tokenType = "FUNCTION";
      this.value = val;
      this.mirror = ")";
    }
    toString() {
      return "FUNCTION(" + this.value + ")";
    }
    toSource() {
      return escapeIdent(this.value) + "(";
    }
  };
  var AtKeywordToken = class extends StringValuedToken {
    constructor(val) {
      super();
      this.tokenType = "AT-KEYWORD";
      this.value = val;
    }
    toString() {
      return "AT(" + this.value + ")";
    }
    toSource() {
      return "@" + escapeIdent(this.value);
    }
  };
  var HashToken = class extends StringValuedToken {
    constructor(val) {
      super();
      this.tokenType = "HASH";
      this.value = val;
      this.type = "unrestricted";
    }
    toString() {
      return "HASH(" + this.value + ")";
    }
    toJSON() {
      const json = this.constructor.prototype.constructor.prototype.toJSON.call(this);
      json.value = this.value;
      json.type = this.type;
      return json;
    }
    toSource() {
      if (this.type === "id")
        return "#" + escapeIdent(this.value);
      else
        return "#" + escapeHash(this.value);
    }
  };
  var StringToken = class extends StringValuedToken {
    constructor(val) {
      super();
      this.tokenType = "STRING";
      this.value = val;
    }
    toString() {
      return '"' + escapeString(this.value) + '"';
    }
  };
  var URLToken = class extends StringValuedToken {
    constructor(val) {
      super();
      this.tokenType = "URL";
      this.value = val;
    }
    toString() {
      return "URL(" + this.value + ")";
    }
    toSource() {
      return 'url("' + escapeString(this.value) + '")';
    }
  };
  var NumberToken = class extends CSSParserToken {
    constructor() {
      super();
      this.tokenType = "NUMBER";
      this.type = "integer";
      this.repr = "";
    }
    toString() {
      if (this.type === "integer")
        return "INT(" + this.value + ")";
      return "NUMBER(" + this.value + ")";
    }
    toJSON() {
      const json = super.toJSON();
      json.value = this.value;
      json.type = this.type;
      json.repr = this.repr;
      return json;
    }
    toSource() {
      return this.repr;
    }
  };
  var PercentageToken = class extends CSSParserToken {
    constructor() {
      super();
      this.tokenType = "PERCENTAGE";
      this.repr = "";
    }
    toString() {
      return "PERCENTAGE(" + this.value + ")";
    }
    toJSON() {
      const json = this.constructor.prototype.constructor.prototype.toJSON.call(this);
      json.value = this.value;
      json.repr = this.repr;
      return json;
    }
    toSource() {
      return this.repr + "%";
    }
  };
  var DimensionToken = class extends CSSParserToken {
    constructor() {
      super();
      this.tokenType = "DIMENSION";
      this.type = "integer";
      this.repr = "";
      this.unit = "";
    }
    toString() {
      return "DIM(" + this.value + "," + this.unit + ")";
    }
    toJSON() {
      const json = this.constructor.prototype.constructor.prototype.toJSON.call(this);
      json.value = this.value;
      json.type = this.type;
      json.repr = this.repr;
      json.unit = this.unit;
      return json;
    }
    toSource() {
      const source = this.repr;
      let unit = escapeIdent(this.unit);
      if (unit[0].toLowerCase() === "e" && (unit[1] === "-" || between(unit.charCodeAt(1), 48, 57))) {
        unit = "\\65 " + unit.slice(1, unit.length);
      }
      return source + unit;
    }
  };
  function escapeIdent(string) {
    string = "" + string;
    let result = "";
    const firstcode = string.charCodeAt(0);
    for (let i = 0; i < string.length; i++) {
      const code = string.charCodeAt(i);
      if (code === 0)
        throw new InvalidCharacterError("Invalid character: the input contains U+0000.");
      if (between(code, 1, 31) || code === 127 || i === 0 && between(code, 48, 57) || i === 1 && between(code, 48, 57) && firstcode === 45)
        result += "\\" + code.toString(16) + " ";
      else if (code >= 128 || code === 45 || code === 95 || between(code, 48, 57) || between(code, 65, 90) || between(code, 97, 122))
        result += string[i];
      else
        result += "\\" + string[i];
    }
    return result;
  }
  function escapeHash(string) {
    string = "" + string;
    let result = "";
    for (let i = 0; i < string.length; i++) {
      const code = string.charCodeAt(i);
      if (code === 0)
        throw new InvalidCharacterError("Invalid character: the input contains U+0000.");
      if (code >= 128 || code === 45 || code === 95 || between(code, 48, 57) || between(code, 65, 90) || between(code, 97, 122))
        result += string[i];
      else
        result += "\\" + code.toString(16) + " ";
    }
    return result;
  }
  function escapeString(string) {
    string = "" + string;
    let result = "";
    for (let i = 0; i < string.length; i++) {
      const code = string.charCodeAt(i);
      if (code === 0)
        throw new InvalidCharacterError("Invalid character: the input contains U+0000.");
      if (between(code, 1, 31) || code === 127)
        result += "\\" + code.toString(16) + " ";
      else if (code === 34 || code === 92)
        result += "\\" + string[i];
      else
        result += string[i];
    }
    return result;
  }

  // packages/injected/src/domUtils.ts
  var globalOptions = {};
  function parentElementOrShadowHost(element) {
    if (element.parentElement)
      return element.parentElement;
    if (!element.parentNode)
      return;
    if (element.parentNode.nodeType === 11 && element.parentNode.host)
      return element.parentNode.host;
  }
  function enclosingShadowRootOrDocument(element) {
    let node = element;
    while (node.parentNode)
      node = node.parentNode;
    if (node.nodeType === 11 || node.nodeType === 9)
      return node;
  }
  function enclosingShadowHost(element) {
    while (element.parentElement)
      element = element.parentElement;
    return parentElementOrShadowHost(element);
  }
  function closestCrossShadow(element, css, scope) {
    while (element) {
      const closest = element.closest(css);
      if (scope && closest !== scope && (closest == null ? void 0 : closest.contains(scope)))
        return;
      if (closest)
        return closest;
      element = enclosingShadowHost(element);
    }
  }
  function getElementComputedStyle(element, pseudo) {
    const cache = pseudo === "::before" ? cacheStyleBefore : pseudo === "::after" ? cacheStyleAfter : cacheStyle;
    if (cache && cache.has(element))
      return cache.get(element);
    const style = element.ownerDocument && element.ownerDocument.defaultView ? element.ownerDocument.defaultView.getComputedStyle(element, pseudo) : void 0;
    cache == null ? void 0 : cache.set(element, style);
    return style;
  }
  function isElementStyleVisibilityVisible(element, style) {
    const cached = cacheStyleVisibility == null ? void 0 : cacheStyleVisibility.get(element);
    if (cached !== void 0)
      return cached;
    const result = computeElementStyleVisibilityVisible(element, style);
    cacheStyleVisibility == null ? void 0 : cacheStyleVisibility.set(element, result);
    return result;
  }
  function computeElementStyleVisibilityVisible(element, style) {
    style = style != null ? style : getElementComputedStyle(element);
    if (!style)
      return true;
    if (!element.checkVisibility() && !isWebKitListBoxOptionVisible(element, style))
      return false;
    if (style.visibility !== "visible")
      return false;
    return true;
  }
  function isListBoxSelect(select) {
    return select.multiple || select.size > 1;
  }
  function isWebKitListBoxOptionVisible(element, style) {
    var _a;
    if (globalOptions.browserNameForWorkarounds !== "webkit" || element.nodeName !== "OPTION" || style.display === "none")
      return false;
    const select = element.closest("select");
    if (!select || !isListBoxSelect(select))
      return false;
    for (let e = element.parentElement; e && e !== select; e = e.parentElement) {
      if (((_a = getElementComputedStyle(e)) == null ? void 0 : _a.display) === "none")
        return false;
    }
    return isElementStyleVisibilityVisible(select);
  }
  var kTextNodeRange = /* @__PURE__ */ Symbol("playwrightTextNodeRange");
  function isVisibleTextNode(node) {
    var _a;
    const document = node.ownerDocument;
    const range = (_a = document[kTextNodeRange]) != null ? _a : document[kTextNodeRange] = document.createRange();
    range.selectNode(node);
    const rect = range.getBoundingClientRect();
    const result = rect.width > 0 && rect.height > 0;
    range.setStart(document, 0);
    range.collapse(true);
    return result;
  }
  function elementSafeTagName(element) {
    const tagName = element.tagName;
    if (typeof tagName === "string") {
      const firstCharCode = tagName.charCodeAt(0);
      if (firstCharCode >= 97 && firstCharCode <= 122)
        return tagName.toUpperCase();
      return tagName;
    }
    if (element instanceof HTMLFormElement)
      return "FORM";
    return element.tagName.toUpperCase();
  }
  var cacheStyle;
  var cacheStyleBefore;
  var cacheStyleAfter;
  var cacheStyleVisibility;
  var cachesCounter = 0;
  function beginDOMCaches() {
    ++cachesCounter;
    cacheStyle != null ? cacheStyle : cacheStyle = /* @__PURE__ */ new Map();
    cacheStyleBefore != null ? cacheStyleBefore : cacheStyleBefore = /* @__PURE__ */ new Map();
    cacheStyleAfter != null ? cacheStyleAfter : cacheStyleAfter = /* @__PURE__ */ new Map();
    cacheStyleVisibility != null ? cacheStyleVisibility : cacheStyleVisibility = /* @__PURE__ */ new Map();
  }
  function endDOMCaches() {
    if (!--cachesCounter) {
      cacheStyle = void 0;
      cacheStyleBefore = void 0;
      cacheStyleAfter = void 0;
      cacheStyleVisibility = void 0;
    }
  }

  // packages/injected/src/roleUtils.ts
  function hasExplicitAccessibleName(e) {
    return e.hasAttribute("aria-label") || e.hasAttribute("aria-labelledby");
  }
  var kAncestorPreventingLandmark = "article:not([role]), aside:not([role]), main:not([role]), nav:not([role]), section:not([role]), [role=article], [role=complementary], [role=main], [role=navigation], [role=region]";
  var kGlobalAriaAttributes = [
    ["aria-atomic", void 0],
    ["aria-busy", void 0],
    ["aria-controls", void 0],
    ["aria-current", void 0],
    ["aria-describedby", void 0],
    ["aria-details", void 0],
    // Global use deprecated in ARIA 1.2
    // ['aria-disabled', undefined],
    ["aria-dropeffect", void 0],
    // Global use deprecated in ARIA 1.2
    // ['aria-errormessage', undefined],
    ["aria-flowto", void 0],
    ["aria-grabbed", void 0],
    // Global use deprecated in ARIA 1.2
    // ['aria-haspopup', undefined],
    ["aria-hidden", void 0],
    // Global use deprecated in ARIA 1.2
    // ['aria-invalid', undefined],
    ["aria-keyshortcuts", void 0],
    ["aria-label", ["caption", "code", "deletion", "emphasis", "generic", "insertion", "paragraph", "presentation", "strong", "subscript", "superscript"]],
    ["aria-labelledby", ["caption", "code", "deletion", "emphasis", "generic", "insertion", "paragraph", "presentation", "strong", "subscript", "superscript"]],
    ["aria-live", void 0],
    ["aria-owns", void 0],
    ["aria-relevant", void 0],
    ["aria-roledescription", ["generic"]]
  ];
  function hasGlobalAriaAttribute(element, forRole) {
    return kGlobalAriaAttributes.some(([attr, prohibited]) => {
      return !(prohibited == null ? void 0 : prohibited.includes(forRole || "")) && element.hasAttribute(attr);
    });
  }
  function hasTabIndex(element) {
    return !Number.isNaN(Number(String(element.getAttribute("tabindex"))));
  }
  function isFocusable(element) {
    return !isNativelyDisabled(element) && (isNativelyFocusable(element) || hasTabIndex(element));
  }
  function isNativelyFocusable(element) {
    const tagName = elementSafeTagName(element);
    if (["BUTTON", "DETAILS", "SELECT", "TEXTAREA"].includes(tagName))
      return true;
    if (tagName === "A" || tagName === "AREA")
      return element.hasAttribute("href");
    if (tagName === "INPUT")
      return !element.hidden;
    return false;
  }
  var kImplicitRoleByTagName = {
    "A": (e) => {
      return e.hasAttribute("href") ? "link" : null;
    },
    "AREA": (e) => {
      return e.hasAttribute("href") ? "link" : null;
    },
    "ARTICLE": () => "article",
    "ASIDE": () => "complementary",
    "BLOCKQUOTE": () => "blockquote",
    "BUTTON": () => "button",
    "CAPTION": () => "caption",
    "CODE": () => "code",
    "DATALIST": () => "listbox",
    "DD": () => "definition",
    "DEL": () => "deletion",
    "DETAILS": () => "group",
    "DFN": () => "term",
    "DIALOG": () => "dialog",
    "DT": () => "term",
    "EM": () => "emphasis",
    "FIELDSET": () => "group",
    "FIGURE": () => "figure",
    "FOOTER": (e) => closestCrossShadow(e, kAncestorPreventingLandmark) ? null : "contentinfo",
    "FORM": (e) => hasExplicitAccessibleName(e) ? "form" : null,
    "H1": () => "heading",
    "H2": () => "heading",
    "H3": () => "heading",
    "H4": () => "heading",
    "H5": () => "heading",
    "H6": () => "heading",
    "HEADER": (e) => closestCrossShadow(e, kAncestorPreventingLandmark) ? null : "banner",
    "HR": () => "separator",
    "HTML": () => "document",
    "IMG": (e) => e.getAttribute("alt") === "" && !e.getAttribute("title") && !hasGlobalAriaAttribute(e) && !hasTabIndex(e) ? "presentation" : "img",
    "INPUT": (e) => {
      const type = e.type.toLowerCase();
      if (["email", "search", "tel", "text", "url", ""].includes(type)) {
        const list = getIdRefs(e, e.getAttribute("list"))[0];
        if (list && elementSafeTagName(list) === "DATALIST")
          return "combobox";
        return type === "search" ? "searchbox" : "textbox";
      }
      if (type === "hidden")
        return null;
      if (type === "file")
        return "button";
      return inputTypeToRole[type] || "textbox";
    },
    "INS": () => "insertion",
    "LI": () => "listitem",
    "MAIN": () => "main",
    "MARK": () => "mark",
    "MATH": () => "math",
    "MENU": () => "list",
    "METER": () => "meter",
    "NAV": () => "navigation",
    "OL": () => "list",
    "OPTGROUP": () => "group",
    "OPTION": () => "option",
    "OUTPUT": () => "status",
    "P": () => "paragraph",
    "PROGRESS": () => "progressbar",
    "SEARCH": () => "search",
    "SECTION": (e) => hasExplicitAccessibleName(e) ? "region" : null,
    "SELECT": (e) => isListBoxSelect(e) ? "listbox" : "combobox",
    "STRONG": () => "strong",
    "SUB": () => "subscript",
    "SUP": () => "superscript",
    // For <svg> we default to Chrome behavior:
    // - Chrome reports 'img'.
    // - Firefox reports 'diagram' that is not in official ARIA spec yet.
    // - Safari reports 'no role', but still computes accessible name.
    "SVG": () => "img",
    "TABLE": () => "table",
    "TBODY": () => "rowgroup",
    "TD": (e) => {
      const table = closestCrossShadow(e, "table");
      const role = table ? getExplicitAriaRole(table) : "";
      return role === "grid" || role === "treegrid" ? "gridcell" : "cell";
    },
    "TEXTAREA": () => "textbox",
    "TFOOT": () => "rowgroup",
    "TH": (e) => {
      const scope = e.getAttribute("scope");
      if (scope === "col" || scope === "colgroup")
        return "columnheader";
      if (scope === "row" || scope === "rowgroup")
        return "rowheader";
      const nextSibling = e.nextElementSibling;
      const prevSibling = e.previousElementSibling;
      const row = !!e.parentElement && elementSafeTagName(e.parentElement) === "TR" ? e.parentElement : void 0;
      if (!nextSibling && !prevSibling) {
        if (row) {
          const table = closestCrossShadow(row, "table");
          if (table && table.rows.length <= 1)
            return null;
        }
        return "columnheader";
      }
      if (isHeaderCell(nextSibling) && isHeaderCell(prevSibling))
        return "columnheader";
      if (isNonEmptyDataCell(nextSibling) || isNonEmptyDataCell(prevSibling))
        return "rowheader";
      return "columnheader";
    },
    "THEAD": () => "rowgroup",
    "TIME": () => "time",
    "TR": () => "row",
    "UL": () => "list"
  };
  function isHeaderCell(element) {
    return !!element && elementSafeTagName(element) === "TH";
  }
  function isNonEmptyDataCell(element) {
    var _a;
    if (!element || elementSafeTagName(element) !== "TD")
      return false;
    return !!(((_a = element.textContent) == null ? void 0 : _a.trim()) || element.children.length > 0);
  }
  var kPresentationInheritanceParents = {
    "DD": ["DL", "DIV"],
    "DIV": ["DL"],
    "DT": ["DL", "DIV"],
    "LI": ["OL", "UL"],
    "TBODY": ["TABLE"],
    "TD": ["TR"],
    "TFOOT": ["TABLE"],
    "TH": ["TR"],
    "THEAD": ["TABLE"],
    "TR": ["THEAD", "TBODY", "TFOOT", "TABLE"]
  };
  function getImplicitAriaRole(element) {
    var _a;
    const implicitRole = ((_a = kImplicitRoleByTagName[elementSafeTagName(element)]) == null ? void 0 : _a.call(kImplicitRoleByTagName, element)) || "";
    if (!implicitRole)
      return null;
    let ancestor = element;
    while (ancestor) {
      const parent = parentElementOrShadowHost(ancestor);
      const parents = kPresentationInheritanceParents[elementSafeTagName(ancestor)];
      if (!parents || !parent || !parents.includes(elementSafeTagName(parent)))
        break;
      const parentExplicitRole = getExplicitAriaRole(parent);
      if ((parentExplicitRole === "none" || parentExplicitRole === "presentation") && !hasPresentationConflictResolution(parent, parentExplicitRole))
        return parentExplicitRole;
      ancestor = parent;
    }
    return implicitRole;
  }
  var validRoles = [
    "alert",
    "alertdialog",
    "application",
    "article",
    "banner",
    "blockquote",
    "button",
    "caption",
    "cell",
    "checkbox",
    "code",
    "columnheader",
    "combobox",
    "complementary",
    "contentinfo",
    "definition",
    "deletion",
    "dialog",
    "directory",
    "document",
    "emphasis",
    "feed",
    "figure",
    "form",
    "generic",
    "grid",
    "gridcell",
    "group",
    "heading",
    "img",
    "insertion",
    "link",
    "list",
    "listbox",
    "listitem",
    "log",
    "main",
    "mark",
    "marquee",
    "math",
    "meter",
    "menu",
    "menubar",
    "menuitem",
    "menuitemcheckbox",
    "menuitemradio",
    "navigation",
    "none",
    "note",
    "option",
    "paragraph",
    "presentation",
    "progressbar",
    "radio",
    "radiogroup",
    "region",
    "row",
    "rowgroup",
    "rowheader",
    "scrollbar",
    "search",
    "searchbox",
    "separator",
    "slider",
    "spinbutton",
    "status",
    "strong",
    "subscript",
    "superscript",
    "switch",
    "tab",
    "table",
    "tablist",
    "tabpanel",
    "term",
    "textbox",
    "time",
    "timer",
    "toolbar",
    "tooltip",
    "tree",
    "treegrid",
    "treeitem"
  ];
  function getExplicitAriaRole(element) {
    const roles = (element.getAttribute("role") || "").split(" ").map((role) => role.trim());
    return roles.find((role) => validRoles.includes(role)) || null;
  }
  function hasPresentationConflictResolution(element, role) {
    return hasGlobalAriaAttribute(element, role) || isFocusable(element);
  }
  function getAriaRole(element) {
    const cached = cacheAriaRole == null ? void 0 : cacheAriaRole.get(element);
    if (cached !== void 0)
      return cached;
    const role = computeAriaRole(element);
    cacheAriaRole == null ? void 0 : cacheAriaRole.set(element, role);
    return role;
  }
  function computeAriaRole(element) {
    const explicitRole = getExplicitAriaRole(element);
    if (!explicitRole)
      return getImplicitAriaRole(element);
    if (explicitRole === "none" || explicitRole === "presentation") {
      const implicitRole = getImplicitAriaRole(element);
      if (hasPresentationConflictResolution(element, implicitRole))
        return implicitRole;
    }
    return explicitRole;
  }
  function getAriaBoolean(attr) {
    return attr === null ? void 0 : attr.toLowerCase() === "true";
  }
  function isElementIgnoredForAria(element) {
    return ["STYLE", "SCRIPT", "NOSCRIPT", "TEMPLATE"].includes(elementSafeTagName(element));
  }
  function isElementHiddenForAria(element) {
    if (isElementIgnoredForAria(element))
      return true;
    const style = getElementComputedStyle(element);
    const isSlot = element.nodeName === "SLOT";
    if ((style == null ? void 0 : style.display) === "contents" && !isSlot) {
      for (let child = element.firstChild; child; child = child.nextSibling) {
        if (child.nodeType === 1 && !isElementHiddenForAria(child))
          return false;
        if (child.nodeType === 3 && isVisibleTextNode(child))
          return false;
      }
      return true;
    }
    const isOptionInsideSelect = element.nodeName === "OPTION" && !!element.closest("select");
    if (!isOptionInsideSelect && !isSlot && !isElementStyleVisibilityVisible(element, style))
      return true;
    return belongsToDisplayNoneOrAriaHiddenOrNonSlotted(element);
  }
  function belongsToDisplayNoneOrAriaHiddenOrNonSlotted(element) {
    let hidden = cacheIsHidden == null ? void 0 : cacheIsHidden.get(element);
    if (hidden === void 0) {
      hidden = false;
      if (element.parentElement && element.parentElement.shadowRoot && !element.assignedSlot)
        hidden = true;
      if (!hidden) {
        const style = getElementComputedStyle(element);
        hidden = !style || style.display === "none" || getAriaBoolean(element.getAttribute("aria-hidden")) === true;
      }
      if (!hidden) {
        const parent = parentElementOrShadowHost(element);
        if (parent)
          hidden = belongsToDisplayNoneOrAriaHiddenOrNonSlotted(parent);
      }
      cacheIsHidden == null ? void 0 : cacheIsHidden.set(element, hidden);
    }
    return hidden;
  }
  function getIdRefs(element, ref) {
    if (!ref)
      return [];
    const root = enclosingShadowRootOrDocument(element);
    if (!root)
      return [];
    try {
      const ids = ref.split(" ").filter((id) => !!id);
      const result = [];
      for (const id of ids) {
        const firstElement = root.querySelector("#" + CSS.escape(id));
        if (firstElement && !result.includes(firstElement))
          result.push(firstElement);
      }
      return result;
    } catch (e) {
      return [];
    }
  }
  function trimFlatString(s) {
    return s.trim();
  }
  function asFlatString(s) {
    return s.split("\xA0").map((chunk) => chunk.replace(/\r\n/g, "\n").replace(/[\u200b\u00ad]/g, "").replace(/\s\s*/g, " ")).join("\xA0").trim();
  }
  function queryInAriaOwned(element, selector) {
    const result = [...element.querySelectorAll(selector)];
    for (const owned of getIdRefs(element, element.getAttribute("aria-owns"))) {
      if (owned.matches(selector))
        result.push(owned);
      result.push(...owned.querySelectorAll(selector));
    }
    return result;
  }
  function getCSSContent(element, pseudo) {
    const cache = pseudo === "::before" ? cachePseudoContentBefore : pseudo === "::after" ? cachePseudoContentAfter : cachePseudoContent;
    if (cache == null ? void 0 : cache.has(element))
      return cache == null ? void 0 : cache.get(element);
    const style = getElementComputedStyle(element, pseudo);
    let content;
    if (style) {
      const contentValue = style.content;
      if (contentValue && contentValue !== "none" && contentValue !== "normal") {
        if (style.display !== "none" && style.visibility !== "hidden") {
          content = parseCSSContentPropertyAsString(element, contentValue, !!pseudo);
        }
      }
    }
    if (pseudo && content !== void 0) {
      const display = (style == null ? void 0 : style.display) || "inline";
      if (display !== "inline")
        content = " " + content + " ";
    }
    if (cache)
      cache.set(element, content);
    return content;
  }
  function parseCSSContentPropertyAsString(element, content, isPseudo) {
    if (!content || content === "none" || content === "normal") {
      return;
    }
    try {
      let tokens = tokenize(content).filter((token) => !(token instanceof WhitespaceToken));
      const delimIndex = tokens.findIndex((token) => token instanceof DelimToken && token.value === "/");
      if (delimIndex !== -1) {
        tokens = tokens.slice(delimIndex + 1);
      } else if (!isPseudo) {
        return;
      }
      const accumulated = [];
      let index = 0;
      while (index < tokens.length) {
        if (tokens[index] instanceof StringToken) {
          accumulated.push(tokens[index].value);
          index++;
        } else if (index + 2 < tokens.length && tokens[index] instanceof FunctionToken && tokens[index].value === "attr" && tokens[index + 1] instanceof IdentToken && tokens[index + 2] instanceof CloseParenToken) {
          const attrName = tokens[index + 1].value;
          accumulated.push(element.getAttribute(attrName) || "");
          index += 3;
        } else {
          return;
        }
      }
      return accumulated.join("");
    } catch {
    }
  }
  function getAriaLabelledByElements(element) {
    const ref = element.getAttribute("aria-labelledby");
    if (ref === null)
      return null;
    const refs = getIdRefs(element, ref);
    return refs.length ? refs : null;
  }
  function allowsNameFromContent(role, targetDescendant) {
    const alwaysAllowsNameFromContent = ["button", "cell", "checkbox", "columnheader", "gridcell", "heading", "link", "menuitem", "menuitemcheckbox", "menuitemradio", "option", "radio", "row", "rowheader", "switch", "tab", "tooltip", "treeitem"].includes(role);
    const descendantAllowsNameFromContent = targetDescendant && ["", "caption", "code", "contentinfo", "definition", "deletion", "emphasis", "insertion", "list", "listitem", "mark", "none", "paragraph", "presentation", "region", "row", "rowgroup", "section", "strong", "subscript", "superscript", "table", "term", "time"].includes(role);
    return alwaysAllowsNameFromContent || descendantAllowsNameFromContent;
  }
  function computeAccessibleNameComposite(element, includeHidden, collectElements) {
    const elementProhibitsNaming = ["caption", "code", "definition", "deletion", "emphasis", "generic", "insertion", "mark", "paragraph", "presentation", "strong", "subscript", "suggestion", "superscript", "term", "time"].includes(getAriaRole(element) || "");
    if (elementProhibitsNaming)
      return { ...emptyCompositeString(), derivedFromContent: false };
    const outDerivedFromContent = { value: false };
    const result = getTextAlternativeInternal(element, {
      includeHidden,
      collectElements,
      outDerivedFromContent,
      visitedElements: /* @__PURE__ */ new Set(),
      embeddedInTargetElement: "self"
    });
    return { text: asFlatString(result.text), elements: result.elements, derivedFromContent: outDerivedFromContent.value };
  }
  function getElementAccessibleName(element, includeHidden) {
    const cache = includeHidden ? cacheAccessibleNameHidden : cacheAccessibleName;
    let accessibleName = cache == null ? void 0 : cache.get(element);
    if (accessibleName === void 0) {
      accessibleName = computeAccessibleNameComposite(
        element,
        includeHidden,
        true
        /* collectElements */
      );
      cache == null ? void 0 : cache.set(element, accessibleName);
    }
    return accessibleName;
  }
  function getElementAccessibleNameText(element, includeHidden) {
    var _a;
    const composite = (_a = includeHidden ? cacheAccessibleNameHidden : cacheAccessibleName) == null ? void 0 : _a.get(element);
    if (composite !== void 0)
      return composite.text;
    const cache = includeHidden ? cacheAccessibleNameTextHidden : cacheAccessibleNameText;
    let text = cache == null ? void 0 : cache.get(element);
    if (text === void 0) {
      text = computeAccessibleNameComposite(
        element,
        includeHidden,
        false
        /* collectElements */
      ).text;
      cache == null ? void 0 : cache.set(element, text);
    }
    return text;
  }
  function getElementAccessibleDescription(element, includeHidden) {
    const cache = includeHidden ? cacheAccessibleDescriptionHidden : cacheAccessibleDescription;
    let accessibleDescription = cache == null ? void 0 : cache.get(element);
    if (accessibleDescription === void 0) {
      accessibleDescription = { text: "", derivedFromContent: false };
      if (element.hasAttribute("aria-describedby")) {
        const describedBy = getIdRefs(element, element.getAttribute("aria-describedby"));
        accessibleDescription.text = asFlatString(describedBy.map((ref) => getTextAlternativeInternal(ref, {
          includeHidden,
          visitedElements: /* @__PURE__ */ new Set(),
          embeddedInDescribedBy: { element: ref, hidden: isElementHiddenForAria(ref) }
        }).text).join(" "));
        accessibleDescription.derivedFromContent = describedBy.some((ref) => ref === element || element.contains(ref));
      } else if (element.hasAttribute("aria-description")) {
        accessibleDescription.text = asFlatString(element.getAttribute("aria-description") || "");
      } else {
        accessibleDescription.text = asFlatString(element.getAttribute("title") || "");
      }
      cache == null ? void 0 : cache.set(element, accessibleDescription);
    }
    return accessibleDescription;
  }
  var kAriaInvalidRoles = [
    "application",
    "checkbox",
    "columnheader",
    "combobox",
    "gridcell",
    "listbox",
    "radiogroup",
    "rowheader",
    "searchbox",
    "slider",
    "spinbutton",
    "switch",
    "textbox",
    "tree"
  ];
  function getAriaInvalid(element) {
    const ariaInvalid = element.getAttribute("aria-invalid");
    if (!ariaInvalid || ariaInvalid.trim() === "" || ariaInvalid.toLocaleLowerCase() === "false")
      return "false";
    if (ariaInvalid === "true" || ariaInvalid === "grammar" || ariaInvalid === "spelling")
      return ariaInvalid;
    return "true";
  }
  function getValidityInvalid(element) {
    if ("validity" in element) {
      const validity = element.validity;
      return (validity == null ? void 0 : validity.valid) === false;
    }
    return false;
  }
  function getElementAccessibleErrorMessage(element) {
    const cache = cacheAccessibleErrorMessage;
    let accessibleErrorMessage = cacheAccessibleErrorMessage == null ? void 0 : cacheAccessibleErrorMessage.get(element);
    if (accessibleErrorMessage === void 0) {
      accessibleErrorMessage = "";
      const isAriaInvalid = getAriaInvalid(element) !== "false";
      const isValidityInvalid = getValidityInvalid(element);
      if (isAriaInvalid || isValidityInvalid) {
        const errorMessageId = element.getAttribute("aria-errormessage");
        const errorMessages = getIdRefs(element, errorMessageId);
        const parts = errorMessages.map((errorMessage) => asFlatString(
          getTextAlternativeInternal(errorMessage, {
            visitedElements: /* @__PURE__ */ new Set(),
            embeddedInDescribedBy: { element: errorMessage, hidden: isElementHiddenForAria(errorMessage) }
          }).text
        ));
        accessibleErrorMessage = parts.join(" ").trim();
      }
      cache == null ? void 0 : cache.set(element, accessibleErrorMessage);
    }
    return accessibleErrorMessage;
  }
  function insideTargetElement(options) {
    return options.embeddedInTargetElement === "self" || options.embeddedInTargetElement === "descendant";
  }
  function getTextAlternativeInternal(element, options) {
    var _a, _b, _c, _d, _e;
    if (options.visitedElements.has(element))
      return emptyCompositeString();
    const childOptions = {
      ...options,
      embeddedInTargetElement: options.embeddedInTargetElement === "self" ? "descendant" : options.embeddedInTargetElement
    };
    if (!options.includeHidden) {
      const isEmbeddedInHiddenReferenceTraversal = !!((_a = options.embeddedInLabelledBy) == null ? void 0 : _a.hidden) || !!((_b = options.embeddedInDescribedBy) == null ? void 0 : _b.hidden) || !!((_c = options.embeddedInNativeTextAlternative) == null ? void 0 : _c.hidden) || !!((_d = options.embeddedInLabel) == null ? void 0 : _d.hidden);
      if (isElementIgnoredForAria(element) || !isEmbeddedInHiddenReferenceTraversal && isElementHiddenForAria(element)) {
        options.visitedElements.add(element);
        return emptyCompositeString();
      }
    }
    const labelledBy = getAriaLabelledByElements(element);
    if (!options.embeddedInLabelledBy) {
      const accessibleName = joinCompositeString((labelledBy || []).map((ref) => getTextAlternativeInternal(ref, {
        ...options,
        embeddedInLabelledBy: { element: ref, hidden: isElementHiddenForAria(ref) },
        embeddedInDescribedBy: void 0,
        embeddedInTargetElement: void 0,
        embeddedInLabel: void 0,
        embeddedInNativeTextAlternative: void 0
      })), " ", options.collectElements);
      if (accessibleName.text) {
        if (options.outDerivedFromContent && insideTargetElement(options) && (labelledBy || []).some((ref) => ref === element || element.contains(ref)))
          options.outDerivedFromContent.value = true;
        return accessibleName;
      }
    }
    const role = getAriaRole(element) || "";
    const tagName = elementSafeTagName(element);
    if (!!options.embeddedInLabel || !!options.embeddedInLabelledBy || options.embeddedInTargetElement === "descendant") {
      const isOwnLabel = [...element.labels || []].includes(element);
      const isOwnLabelledBy = (labelledBy || []).includes(element);
      if (!isOwnLabel && !isOwnLabelledBy) {
        if (role === "textbox" || role === "searchbox") {
          options.visitedElements.add(element);
          if (tagName === "INPUT" || tagName === "TEXTAREA")
            return compositeString(element.value, element, options.collectElements);
          return compositeString(element.textContent, element, options.collectElements);
        }
        if (["combobox", "listbox"].includes(role)) {
          options.visitedElements.add(element);
          let selectedOptions;
          if (tagName === "SELECT") {
            selectedOptions = [...element.selectedOptions];
            if (!selectedOptions.length && element.options.length)
              selectedOptions.push(element.options[0]);
          } else {
            const listbox = role === "combobox" ? queryInAriaOwned(element, "*").find((e) => getAriaRole(e) === "listbox") : element;
            selectedOptions = listbox ? queryInAriaOwned(listbox, '[aria-selected="true"]').filter((e) => getAriaRole(e) === "option") : [];
          }
          if (!selectedOptions.length && tagName === "INPUT") {
            return compositeString(element.value, element, options.collectElements);
          }
          return joinCompositeString(selectedOptions.map((option) => getTextAlternativeInternal(option, childOptions)), " ", options.collectElements);
        }
        if (["progressbar", "scrollbar", "slider", "spinbutton", "meter"].includes(role)) {
          options.visitedElements.add(element);
          if (element.hasAttribute("aria-valuetext"))
            return compositeString(element.getAttribute("aria-valuetext"), element, options.collectElements);
          if (element.hasAttribute("aria-valuenow"))
            return compositeString(element.getAttribute("aria-valuenow"), element, options.collectElements);
          return compositeString(element.getAttribute("value"), element, options.collectElements);
        }
        if (["menu"].includes(role)) {
          options.visitedElements.add(element);
          return emptyCompositeString();
        }
      }
    }
    const ariaLabel = element.getAttribute("aria-label") || "";
    if (trimFlatString(ariaLabel)) {
      options.visitedElements.add(element);
      return compositeString(ariaLabel, element, options.collectElements);
    }
    if (!["presentation", "none"].includes(role)) {
      if (tagName === "INPUT" && ["button", "submit", "reset"].includes(element.type)) {
        options.visitedElements.add(element);
        const value = element.value || "";
        if (trimFlatString(value))
          return compositeString(value, element, options.collectElements);
        if (element.type === "submit")
          return compositeString("Submit", element, options.collectElements);
        if (element.type === "reset")
          return compositeString("Reset", element, options.collectElements);
        const title = element.getAttribute("title") || "";
        return compositeString(title, element, options.collectElements);
      }
      if (tagName === "INPUT" && element.type === "file") {
        options.visitedElements.add(element);
        const labels = element.labels || [];
        if (labels.length && !options.embeddedInLabelledBy)
          return getAccessibleNameFromAssociatedLabels(labels, options);
        return compositeString("Choose File", element, options.collectElements);
      }
      if (tagName === "INPUT" && element.type === "image") {
        options.visitedElements.add(element);
        const labels = element.labels || [];
        if (labels.length && !options.embeddedInLabelledBy)
          return getAccessibleNameFromAssociatedLabels(labels, options);
        const alt = element.getAttribute("alt") || "";
        if (trimFlatString(alt))
          return compositeString(alt, element, options.collectElements);
        const title = element.getAttribute("title") || "";
        if (trimFlatString(title))
          return compositeString(title, element, options.collectElements);
        return compositeString("Submit", element, options.collectElements);
      }
      if (!labelledBy && tagName === "BUTTON") {
        options.visitedElements.add(element);
        const labels = element.labels || [];
        if (labels.length)
          return getAccessibleNameFromAssociatedLabels(labels, options);
      }
      if (!labelledBy && tagName === "OUTPUT") {
        options.visitedElements.add(element);
        const labels = element.labels || [];
        if (labels.length)
          return getAccessibleNameFromAssociatedLabels(labels, options);
        return compositeString(element.getAttribute("title") || "", element, options.collectElements);
      }
      if (!labelledBy && (tagName === "TEXTAREA" || tagName === "SELECT" || tagName === "INPUT" || tagName === "METER" || tagName === "PROGRESS")) {
        options.visitedElements.add(element);
        const labels = element.labels || [];
        if (labels.length)
          return getAccessibleNameFromAssociatedLabels(labels, options);
        const usePlaceholder = tagName === "INPUT" && ["text", "password", "number", "search", "tel", "email", "url"].includes(element.type) || tagName === "TEXTAREA";
        const placeholder = element.getAttribute("placeholder") || "";
        const title = element.getAttribute("title") || "";
        if (!usePlaceholder || title)
          return compositeString(title, element, options.collectElements);
        return compositeString(placeholder, element, options.collectElements);
      }
      if (!labelledBy && tagName === "FIELDSET") {
        options.visitedElements.add(element);
        for (let child = element.firstElementChild; child; child = child.nextElementSibling) {
          if (elementSafeTagName(child) === "LEGEND") {
            return getTextAlternativeInternal(child, {
              ...childOptions,
              embeddedInNativeTextAlternative: { element: child, hidden: isElementHiddenForAria(child) }
            });
          }
        }
        const title = element.getAttribute("title") || "";
        return compositeString(title, element, options.collectElements);
      }
      if (!labelledBy && tagName === "FIGURE") {
        options.visitedElements.add(element);
        for (let child = element.firstElementChild; child; child = child.nextElementSibling) {
          if (elementSafeTagName(child) === "FIGCAPTION") {
            return getTextAlternativeInternal(child, {
              ...childOptions,
              embeddedInNativeTextAlternative: { element: child, hidden: isElementHiddenForAria(child) }
            });
          }
        }
        const title = element.getAttribute("title") || "";
        return compositeString(title, element, options.collectElements);
      }
      if (tagName === "IMG") {
        options.visitedElements.add(element);
        const alt = element.getAttribute("alt") || "";
        if (trimFlatString(alt))
          return compositeString(alt, element, options.collectElements);
        const title = element.getAttribute("title") || "";
        return compositeString(title, element, options.collectElements);
      }
      if (tagName === "TABLE") {
        options.visitedElements.add(element);
        for (let child = element.firstElementChild; child; child = child.nextElementSibling) {
          if (elementSafeTagName(child) === "CAPTION") {
            return getTextAlternativeInternal(child, {
              ...childOptions,
              embeddedInNativeTextAlternative: { element: child, hidden: isElementHiddenForAria(child) }
            });
          }
        }
        const summary = element.getAttribute("summary") || "";
        if (summary)
          return compositeString(summary, element, options.collectElements);
      }
      if (tagName === "AREA") {
        options.visitedElements.add(element);
        const alt = element.getAttribute("alt") || "";
        if (trimFlatString(alt))
          return compositeString(alt, element, options.collectElements);
        const title = element.getAttribute("title") || "";
        return compositeString(title, element, options.collectElements);
      }
      if (tagName === "SVG" || element.ownerSVGElement) {
        options.visitedElements.add(element);
        for (let child = element.firstElementChild; child; child = child.nextElementSibling) {
          if (elementSafeTagName(child) === "TITLE" && child.ownerSVGElement) {
            return getTextAlternativeInternal(child, {
              ...childOptions,
              embeddedInLabelledBy: { element: child, hidden: isElementHiddenForAria(child) }
            });
          }
        }
      }
      if (element.ownerSVGElement && tagName === "A") {
        const title = element.getAttribute("xlink:title") || "";
        if (trimFlatString(title)) {
          options.visitedElements.add(element);
          return compositeString(title, element, options.collectElements);
        }
      }
    }
    const shouldNameFromContentForSummary = tagName === "SUMMARY" && !["presentation", "none"].includes(role);
    if (allowsNameFromContent(role, options.embeddedInTargetElement === "descendant") || shouldNameFromContentForSummary || !!options.embeddedInLabelledBy || !!options.embeddedInDescribedBy || !!options.embeddedInLabel || !!options.embeddedInNativeTextAlternative) {
      options.visitedElements.add(element);
      const accessibleName = innerAccumulatedElementText(element, childOptions);
      const maybeTrimmedAccessibleName = options.embeddedInTargetElement === "self" ? trimFlatString(accessibleName.text) : accessibleName.text;
      if (maybeTrimmedAccessibleName) {
        if (options.outDerivedFromContent && insideTargetElement(options) && trimFlatString(accessibleName.text))
          options.outDerivedFromContent.value = true;
        (_e = accessibleName.elements) == null ? void 0 : _e.add(element);
        return accessibleName;
      }
    }
    if (!["presentation", "none"].includes(role) || tagName === "IFRAME" || tagName === "FRAME") {
      options.visitedElements.add(element);
      const title = element.getAttribute("title") || "";
      if (trimFlatString(title))
        return compositeString(title, element, options.collectElements);
    }
    options.visitedElements.add(element);
    return emptyCompositeString();
  }
  function innerAccumulatedElementText(element, options) {
    const tokens = [];
    const elements = options.collectElements ? /* @__PURE__ */ new Set() : void 0;
    const visit = (node, skipSlotted) => {
      var _a;
      if (skipSlotted && node.assignedSlot)
        return;
      if (node.nodeType === 1) {
        const display = ((_a = getElementComputedStyle(node)) == null ? void 0 : _a.display) || "inline";
        const childComposite = getTextAlternativeInternal(node, options);
        let token = childComposite.text;
        for (const contributor of childComposite.elements || [])
          elements == null ? void 0 : elements.add(contributor);
        if (display !== "inline" || node.nodeName === "BR")
          token = " " + token + " ";
        tokens.push(token);
      } else if (node.nodeType === 3) {
        tokens.push(node.textContent || "");
      }
    };
    tokens.push(getCSSContent(element, "::before") || "");
    const content = getCSSContent(element);
    if (content !== void 0) {
      tokens.push(content);
    } else {
      const assignedNodes = element.nodeName === "SLOT" ? element.assignedNodes() : [];
      if (assignedNodes.length) {
        for (const child of assignedNodes)
          visit(child, false);
      } else {
        for (let child = element.firstChild; child; child = child.nextSibling)
          visit(child, true);
        if (element.shadowRoot) {
          for (let child = element.shadowRoot.firstChild; child; child = child.nextSibling)
            visit(child, true);
        }
        for (const owned of getIdRefs(element, element.getAttribute("aria-owns")))
          visit(owned, true);
      }
    }
    tokens.push(getCSSContent(element, "::after") || "");
    return { text: tokens.join(""), elements };
  }
  var kAriaSelectedRoles = ["gridcell", "option", "row", "tab", "rowheader", "columnheader", "treeitem"];
  function getAriaSelected(element) {
    if (elementSafeTagName(element) === "OPTION")
      return element.selected;
    if (kAriaSelectedRoles.includes(getAriaRole(element) || ""))
      return getAriaBoolean(element.getAttribute("aria-selected")) === true;
    return false;
  }
  var kAriaCheckedRoles = ["checkbox", "menuitemcheckbox", "option", "radio", "switch", "menuitemradio", "treeitem"];
  function getAriaChecked(element) {
    const result = getChecked(element, true);
    return result === "error" ? false : result;
  }
  function getCheckedAllowMixed(element) {
    return getChecked(element, true);
  }
  function getCheckedWithoutMixed(element) {
    const result = getChecked(element, false);
    return result;
  }
  function getChecked(element, allowMixed) {
    const tagName = elementSafeTagName(element);
    if (allowMixed && tagName === "INPUT" && element.indeterminate)
      return "mixed";
    if (tagName === "INPUT" && ["checkbox", "radio"].includes(element.type))
      return element.checked;
    if (kAriaCheckedRoles.includes(getAriaRole(element) || "")) {
      const checked = element.getAttribute("aria-checked");
      if (checked === "true")
        return true;
      if (allowMixed && checked === "mixed")
        return "mixed";
      return false;
    }
    return "error";
  }
  var kAriaReadonlyRoles = ["checkbox", "combobox", "grid", "gridcell", "listbox", "radiogroup", "slider", "spinbutton", "textbox", "columnheader", "rowheader", "searchbox", "switch", "treegrid"];
  function getReadonly(element) {
    const tagName = elementSafeTagName(element);
    if (["INPUT", "TEXTAREA", "SELECT"].includes(tagName))
      return element.hasAttribute("readonly");
    if (kAriaReadonlyRoles.includes(getAriaRole(element) || ""))
      return element.getAttribute("aria-readonly") === "true";
    if (element.isContentEditable)
      return false;
    return "error";
  }
  var kAriaPressedRoles = ["button"];
  function getAriaPressed(element) {
    if (kAriaPressedRoles.includes(getAriaRole(element) || "")) {
      const pressed = element.getAttribute("aria-pressed");
      if (pressed === "true")
        return true;
      if (pressed === "mixed")
        return "mixed";
    }
    return false;
  }
  var kAriaExpandedRoles = ["application", "button", "checkbox", "combobox", "gridcell", "link", "listbox", "menuitem", "row", "rowheader", "tab", "treeitem", "columnheader", "menuitemcheckbox", "menuitemradio", "rowheader", "switch"];
  function getAriaExpanded(element) {
    if (elementSafeTagName(element) === "DETAILS")
      return element.open;
    if (kAriaExpandedRoles.includes(getAriaRole(element) || "")) {
      const expanded = element.getAttribute("aria-expanded");
      if (expanded === null)
        return void 0;
      if (expanded === "true")
        return true;
      return false;
    }
    return void 0;
  }
  var kAriaLevelRoles = ["heading", "listitem", "row", "treeitem"];
  function getAriaLevel(element) {
    const native = { "H1": 1, "H2": 2, "H3": 3, "H4": 4, "H5": 5, "H6": 6 }[elementSafeTagName(element)];
    if (native)
      return native;
    if (kAriaLevelRoles.includes(getAriaRole(element) || "")) {
      const attr = element.getAttribute("aria-level");
      const value = attr === null ? Number.NaN : Number(attr);
      if (Number.isInteger(value) && value >= 1)
        return value;
    }
    return 0;
  }
  var kAriaDisabledRoles = ["application", "button", "composite", "gridcell", "group", "input", "link", "menuitem", "scrollbar", "separator", "tab", "checkbox", "columnheader", "combobox", "grid", "listbox", "menu", "menubar", "menuitemcheckbox", "menuitemradio", "option", "radio", "radiogroup", "row", "rowheader", "searchbox", "select", "slider", "spinbutton", "switch", "tablist", "textbox", "toolbar", "tree", "treegrid", "treeitem"];
  function getAriaDisabled(element) {
    return isNativelyDisabled(element) || hasExplicitAriaDisabled(element);
  }
  function isNativelyDisabled(element) {
    const isNativeFormControl = ["BUTTON", "INPUT", "SELECT", "TEXTAREA", "OPTION", "OPTGROUP"].includes(elementSafeTagName(element));
    return isNativeFormControl && (element.hasAttribute("disabled") || belongsToDisabledOptGroup(element) || belongsToDisabledFieldSet(element));
  }
  function belongsToDisabledOptGroup(element) {
    return elementSafeTagName(element) === "OPTION" && !!element.closest("OPTGROUP[DISABLED]");
  }
  function belongsToDisabledFieldSet(element) {
    const fieldSetElement = element == null ? void 0 : element.closest("FIELDSET[DISABLED]");
    if (!fieldSetElement)
      return false;
    const legendElement = fieldSetElement.querySelector(":scope > LEGEND");
    return !legendElement || !legendElement.contains(element);
  }
  function hasExplicitAriaDisabled(element) {
    if (!kAriaDisabledRoles.includes(getAriaRole(element) || ""))
      return false;
    return hasAriaDisabledInChain(element);
  }
  function hasAriaDisabledInChain(element) {
    let result = cacheAriaDisabled == null ? void 0 : cacheAriaDisabled.get(element);
    if (result === void 0) {
      const attribute = (element.getAttribute("aria-disabled") || "").toLowerCase();
      if (attribute === "true") {
        result = true;
      } else if (attribute === "false") {
        result = false;
      } else {
        const parent = parentElementOrShadowHost(element);
        result = parent ? hasAriaDisabledInChain(parent) : false;
      }
      cacheAriaDisabled == null ? void 0 : cacheAriaDisabled.set(element, result);
    }
    return result;
  }
  function getAccessibleNameFromAssociatedLabels(labels, options) {
    return joinCompositeString([...labels].map((label) => getTextAlternativeInternal(label, {
      ...options,
      embeddedInLabel: { element: label, hidden: isElementHiddenForAria(label) },
      embeddedInNativeTextAlternative: void 0,
      embeddedInLabelledBy: void 0,
      embeddedInDescribedBy: void 0,
      embeddedInTargetElement: void 0
    })).filter((accessibleName) => !!accessibleName.text), " ", options.collectElements);
  }
  function receivesPointerEvents(element) {
    const cache = cachePointerEvents;
    let e = element;
    let result;
    const parents = [];
    for (; e; e = parentElementOrShadowHost(e)) {
      const cached = cache.get(e);
      if (cached !== void 0) {
        result = cached;
        break;
      }
      parents.push(e);
      const style = getElementComputedStyle(e);
      if (!style) {
        result = true;
        break;
      }
      const value = style.pointerEvents;
      if (value) {
        result = value !== "none";
        break;
      }
    }
    if (result === void 0)
      result = true;
    for (const parent of parents)
      cache.set(parent, result);
    return result;
  }
  var cacheAccessibleName;
  var cacheAccessibleNameHidden;
  var cacheAccessibleNameText;
  var cacheAccessibleNameTextHidden;
  var cacheAccessibleDescription;
  var cacheAccessibleDescriptionHidden;
  var cacheAccessibleErrorMessage;
  var cacheIsHidden;
  var cachePseudoContent;
  var cachePseudoContentBefore;
  var cachePseudoContentAfter;
  var cachePointerEvents;
  var cacheAriaRole;
  var cacheAriaDisabled;
  var cachesCounter2 = 0;
  function beginAriaCaches() {
    beginDOMCaches();
    ++cachesCounter2;
    cacheAriaRole != null ? cacheAriaRole : cacheAriaRole = /* @__PURE__ */ new Map();
    cacheAriaDisabled != null ? cacheAriaDisabled : cacheAriaDisabled = /* @__PURE__ */ new Map();
    cacheAccessibleName != null ? cacheAccessibleName : cacheAccessibleName = /* @__PURE__ */ new Map();
    cacheAccessibleNameHidden != null ? cacheAccessibleNameHidden : cacheAccessibleNameHidden = /* @__PURE__ */ new Map();
    cacheAccessibleNameText != null ? cacheAccessibleNameText : cacheAccessibleNameText = /* @__PURE__ */ new Map();
    cacheAccessibleNameTextHidden != null ? cacheAccessibleNameTextHidden : cacheAccessibleNameTextHidden = /* @__PURE__ */ new Map();
    cacheAccessibleDescription != null ? cacheAccessibleDescription : cacheAccessibleDescription = /* @__PURE__ */ new Map();
    cacheAccessibleDescriptionHidden != null ? cacheAccessibleDescriptionHidden : cacheAccessibleDescriptionHidden = /* @__PURE__ */ new Map();
    cacheAccessibleErrorMessage != null ? cacheAccessibleErrorMessage : cacheAccessibleErrorMessage = /* @__PURE__ */ new Map();
    cacheIsHidden != null ? cacheIsHidden : cacheIsHidden = /* @__PURE__ */ new Map();
    cachePseudoContent != null ? cachePseudoContent : cachePseudoContent = /* @__PURE__ */ new Map();
    cachePseudoContentBefore != null ? cachePseudoContentBefore : cachePseudoContentBefore = /* @__PURE__ */ new Map();
    cachePseudoContentAfter != null ? cachePseudoContentAfter : cachePseudoContentAfter = /* @__PURE__ */ new Map();
    cachePointerEvents != null ? cachePointerEvents : cachePointerEvents = /* @__PURE__ */ new Map();
  }
  function endAriaCaches() {
    if (!--cachesCounter2) {
      cacheAccessibleName = void 0;
      cacheAccessibleNameHidden = void 0;
      cacheAccessibleNameText = void 0;
      cacheAccessibleNameTextHidden = void 0;
      cacheAccessibleDescription = void 0;
      cacheAccessibleDescriptionHidden = void 0;
      cacheAccessibleErrorMessage = void 0;
      cacheIsHidden = void 0;
      cachePseudoContent = void 0;
      cachePseudoContentBefore = void 0;
      cachePseudoContentAfter = void 0;
      cachePointerEvents = void 0;
      cacheAriaRole = void 0;
      cacheAriaDisabled = void 0;
    }
    endDOMCaches();
  }
  var inputTypeToRole = {
    "button": "button",
    "checkbox": "checkbox",
    "image": "button",
    "number": "spinbutton",
    "radio": "radio",
    "range": "slider",
    "reset": "button",
    "submit": "button"
  };
  function emptyCompositeString() {
    return { text: "" };
  }
  function compositeString(text, element, collectElements) {
    const elements = text && collectElements ? /* @__PURE__ */ new Set([element]) : void 0;
    return { text: text || "", elements };
  }
  function joinCompositeString(parts, separator, collectElements) {
    let elements;
    if (collectElements) {
      elements = /* @__PURE__ */ new Set();
      for (const part of parts) {
        for (const element of part.elements || [])
          elements.add(element);
      }
    }
    return { text: parts.map((part) => part.text).join(separator), elements };
  }
  return __toCommonJS(entry_exports);
})();
