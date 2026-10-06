/*
 * Copyright (c) 2026 Dario Kondratiuk
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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PlaywrightNative
{
    /// <summary>
    /// Translates a JavaScript regular expression, compiled with the "v" flag as URL patterns require,
    /// into a .NET pattern with the same meaning. It keeps every capture group in the same order,
    /// so group numbers match JavaScript. It throws <see cref="ArgumentException"/> for syntax that
    /// the "v" flag rejects but .NET would accept, and for the few features it does not support
    /// (class string disjunctions, astral code points in classes, Unicode properties other than general categories).
    /// </summary>
    internal sealed class JsRegExpTranslator
    {
        private const string Digit = "0-9";
        private const string NotDigit = @"\u0000-\u002F\u003A-\uFFFF";
        private const string Word = "0-9A-Za-z_";
        private const string NotWord = @"\u0000-\u002F\u003A-\u0040\u005B-\u005E\u0060\u007B-\uFFFF";
        private const string AnyChar = @"[\s\S]";

        private static readonly Regex _quantifier = new(@"\G\{[0-9]+(,[0-9]*)?\}", RegexOptions.CultureInvariant);

        private static readonly HashSet<string> _generalCategories = new(StringComparer.Ordinal)
        {
            "L", "Lu", "Ll", "Lt", "Lm", "Lo", "M", "Mn", "Mc", "Me", "N", "Nd", "Nl", "No",
            "P", "Pc", "Pd", "Ps", "Pe", "Pi", "Pf", "Po", "S", "Sm", "Sc", "Sk", "So",
            "Z", "Zs", "Zl", "Zp", "C", "Cc", "Cf", "Cs", "Co", "Cn",
        };

        private readonly string _source;
        private readonly int _groupOffset;
        private readonly Dictionary<string, int> _groupNames = new(StringComparer.Ordinal);
        private int _captureCount;
        private int _pos;

        private JsRegExpTranslator(string source, int groupOffset)
        {
            _source = source;
            _groupOffset = groupOffset;
        }

        private bool AtEnd => _pos >= _source.Length;

        /// <summary>
        /// Translates <paramref name="source"/>. <paramref name="groupOffset"/> is the number of capture groups
        /// that come before it in the full expression; named backreferences use it to compute group numbers.
        /// </summary>
        public static string Translate(string source, int groupOffset, out int captureCount)
        {
            var translator = new JsRegExpTranslator(source, groupOffset);
            translator.CollectGroups();
            string result = translator.TranslateAlternatives();
            captureCount = translator._captureCount;
            return result;
        }

        private static ArgumentException Error(string message) => new($"Invalid regular expression: {message}");

        private static string Hex4(int codePoint) => @"\u" + codePoint.ToString("X4", CultureInfo.InvariantCulture);

        private static bool IsClassSetSyntaxCharacter(char c) => c is '(' or ')' or '[' or ']' or '{' or '}' or '/' or '-' or '\\' or '|';

        private static bool IsClassSetReservedDoublePunctuator(char c) => "&!#$%*+,.:;<=>?@^`~".Contains(c, StringComparison.Ordinal);

        private char Peek(int offset = 0) => _pos + offset < _source.Length ? _source[_pos + offset] : '\0';

        private void CollectGroups()
        {
            int classDepth = 0;
            for (int i = 0; i < _source.Length; i++)
            {
                char c = _source[i];
                if (c == '\\')
                {
                    i++;
                }
                else if (classDepth > 0)
                {
                    classDepth += c == '[' ? 1 : (c == ']' ? -1 : 0);
                }
                else if (c == '[')
                {
                    classDepth = 1;
                }
                else if (c == '(')
                {
                    if (i + 1 < _source.Length && _source[i + 1] == '?')
                    {
                        if (i + 3 < _source.Length && _source[i + 2] == '<' && _source[i + 3] != '=' && _source[i + 3] != '!')
                        {
                            _captureCount++;
                            int end = _source.IndexOf('>', i + 3);
                            string name = end < 0 ? string.Empty : _source[(i + 3)..end];
                            if (name.Length == 0 || !_groupNames.TryAdd(name, _captureCount))
                            {
                                throw Error($"invalid or duplicate group name in '{_source}'.");
                            }
                        }
                    }
                    else
                    {
                        _captureCount++;
                    }
                }
            }
        }

        private string TranslateAlternatives()
        {
            var result = new StringBuilder();
            while (!AtEnd)
            {
                char c = _source[_pos];
                switch (c)
                {
                    case '\\':
                        result.Append(TranslateEscape(inClass: false).Pattern);
                        break;
                    case '[':
                        _pos++;
                        result.Append(ParseClass().Matcher);
                        break;
                    case '(':
                        result.Append(TranslateGroupOpening());
                        break;
                    case '.':
                        _pos++;
                        result.Append(@"[^\n\r\u2028\u2029]");
                        break;
                    case '$':
                        _pos++;
                        result.Append(@"\z");
                        break;
                    case '{':
                        Match quantifier = _quantifier.Match(_source, _pos);
                        if (!quantifier.Success)
                        {
                            throw Error($"lone quantifier bracket in '{_source}'.");
                        }

                        _pos += quantifier.Length;
                        result.Append(quantifier.Value);
                        break;
                    case '}':
                    case ']':
                        throw Error($"lone '{c}' in '{_source}'.");
                    default:
                        _pos++;
                        result.Append(c);
                        break;
                }
            }

            return result.ToString();
        }

        private string TranslateGroupOpening()
        {
            _pos++;
            if (Peek() != '?')
            {
                return "(";
            }

            char kind = Peek(1);
            if (kind is ':' or '=' or '!')
            {
                _pos += 2;
                return "(?" + kind;
            }

            if (kind == '<' && Peek(2) is '=' or '!')
            {
                char lookbehind = Peek(2);
                _pos += 3;
                return "(?<" + lookbehind;
            }

            if (kind == '<')
            {
                // Named groups become plain groups. .NET numbers named groups after unnamed ones,
                // which would break the positional group numbers URL patterns rely on.
                _pos = _source.IndexOf('>', _pos) + 1;
                return "(";
            }

            // Modifiers such as (?i:...) or (?-i:...).
            Match modifiers = Regex.Match(_source[(_pos + 1)..], @"^[ims]*(-[ims]*)?:", RegexOptions.CultureInvariant);
            if (modifiers.Success && modifiers.Length > 1)
            {
                _pos += 1 + modifiers.Length;
                return "(?" + modifiers.Value;
            }

            throw Error($"invalid group in '{_source}'.");
        }

        /// <summary>
        /// Translates an escape. Returns a stand-alone pattern, the content to use inside a .NET class,
        /// and the code point when the escape stands for a single character (or -1).
        /// </summary>
        private (string Pattern, string ClassContent, int CodePoint) TranslateEscape(bool inClass)
        {
            _pos++;
            if (AtEnd)
            {
                throw Error($"'\\' at end of '{_source}'.");
            }

            char e = _source[_pos++];
            switch (e)
            {
                case 'd':
                    return ("[" + Digit + "]", Digit, -1);
                case 'D':
                    return ("[^" + Digit + "]", NotDigit, -1);
                case 'w':
                    return ("[" + Word + "]", Word, -1);
                case 'W':
                    return ("[^" + Word + "]", NotWord, -1);
                case 's':
                    return (@"\s", @"\s", -1);
                case 'S':
                    return (@"\S", @"\S", -1);
                case 'b' when !inClass:
                    return (@"\b", null, -1);
                case 'B' when !inClass:
                    return (@"\B", null, -1);
                case 'b':
                    return Single(0x08);
                case 'f':
                    return Single(0x0C);
                case 'n':
                    return Single(0x0A);
                case 'r':
                    return Single(0x0D);
                case 't':
                    return Single(0x09);
                case 'v':
                    return Single(0x0B);
                case 'c' when UrlCodePoints.IsAsciiAlpha(Peek()):
                    return Single(_source[_pos++] % 32);
                case '0' when !UrlCodePoints.IsAsciiDigit(Peek()):
                    return Single(0);
                case >= '1' and <= '9' when !inClass:
                    int start = _pos - 1;
                    while (UrlCodePoints.IsAsciiDigit(Peek()))
                    {
                        _pos++;
                    }

                    return ("(?:\\" + _source[start.._pos] + ")", null, -1);
                case 'k' when !inClass && Peek() == '<':
                    int end = _source.IndexOf('>', _pos);
                    string name = end < 0 ? string.Empty : _source[(_pos + 1)..end];
                    if (!_groupNames.TryGetValue(name, out int local))
                    {
                        throw Error($"unknown group name '{name}' in '{_source}'.");
                    }

                    _pos = end + 1;
                    return ("(?:\\" + (_groupOffset + local).ToString(CultureInfo.InvariantCulture) + ")", null, -1);
                case 'x' when UrlCodePoints.IsAsciiHexDigit(Peek()) && UrlCodePoints.IsAsciiHexDigit(Peek(1)):
                    _pos += 2;
                    return Single(int.Parse(_source.AsSpan(_pos - 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                case 'u':
                    return Single(ParseUnicodeEscape());
                case 'p':
                case 'P':
                    string property = ParseUnicodeProperty();
                    string escape = "\\" + e + "{" + property + "}";
                    return (escape, escape, -1);
                case '-' when inClass:
                    return Single('-');
                case '^' or '$' or '\\' or '.' or '*' or '+' or '?' or '(' or ')' or '[' or ']' or '{' or '}' or '|' or '/':
                    return Single(e);
                case '&' or '!' or '#' or '%' or ',' or ':' or ';' or '<' or '=' or '>' or '@' or '`' or '~' when inClass:
                    return Single(e);
                default:
                    throw Error($"invalid escape '\\{e}' in '{_source}'.");
            }

            (string Pattern, string ClassContent, int CodePoint) Single(int codePoint)
            {
                if (codePoint > 0xFFFF)
                {
                    if (inClass)
                    {
                        throw Error($"code points above U+FFFF are not supported in classes: '{_source}'.");
                    }

                    string pair = char.ConvertFromUtf32(codePoint);
                    return ("(?:" + Hex4(pair[0]) + Hex4(pair[1]) + ")", null, codePoint);
                }

                return (Hex4(codePoint), Hex4(codePoint), codePoint);
            }
        }

        private int ParseUnicodeEscape()
        {
            if (Peek() == '{')
            {
                int end = _source.IndexOf('}', _pos);
                string digits = end < 0 ? string.Empty : _source[(_pos + 1)..end];
                if (digits.Length == 0 || !digits.All(c => UrlCodePoints.IsAsciiHexDigit(c))
                    || !int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int codePoint) || codePoint > 0x10FFFF)
                {
                    throw Error($"invalid unicode escape in '{_source}'.");
                }

                _pos = end + 1;
                return codePoint;
            }

            int value = ParseHex4(_pos);
            _pos += 4;
            if (char.IsHighSurrogate((char)value) && Peek() == '\\' && Peek(1) == 'u' && TryParseHex4(_pos + 2, out int low) && char.IsLowSurrogate((char)low))
            {
                _pos += 6;
                return char.ConvertToUtf32((char)value, (char)low);
            }

            return value;
        }

        private int ParseHex4(int index)
            => TryParseHex4(index, out int value) ? value : throw Error($"invalid unicode escape in '{_source}'.");

        private bool TryParseHex4(int index, out int value)
        {
            value = 0;
            if (index + 4 > _source.Length)
            {
                return false;
            }

            for (int i = index; i < index + 4; i++)
            {
                if (!UrlCodePoints.IsAsciiHexDigit(_source[i]))
                {
                    return false;
                }

                value = (value * 16) + UrlCodePoints.HexValue(_source[i]);
            }

            return true;
        }

        private string ParseUnicodeProperty()
        {
            int end = Peek() == '{' ? _source.IndexOf('}', _pos) : -1;
            if (end < 0)
            {
                throw Error($"invalid property escape in '{_source}'.");
            }

            string property = _source[(_pos + 1)..end];
            _pos = end + 1;
            int equals = property.IndexOf('=', StringComparison.Ordinal);
            if (equals >= 0 && property[..equals] is "gc" or "General_Category")
            {
                property = property[(equals + 1)..];
            }

            return _generalCategories.Contains(property)
                ? property
                : throw Error($"unsupported Unicode property '{property}' in '{_source}'.");
        }

        /// <summary>
        /// Parses a "v" flag class after its opening bracket. Returns a pattern that consumes one character.
        /// </summary>
        private ClassOperand ParseClass()
        {
            bool negated = Peek() == '^';
            if (negated)
            {
                _pos++;
            }

            var operands = new List<ClassOperand>();
            string setOperator = null;
            bool lastWasOperand = false;
            while (true)
            {
                if (AtEnd)
                {
                    throw Error($"unterminated class in '{_source}'.");
                }

                if (Peek() == ']')
                {
                    _pos++;
                    break;
                }

                if ((Peek() == '&' && Peek(1) == '&') || (Peek() == '-' && Peek(1) == '-'))
                {
                    string op = _source.Substring(_pos, 2);
                    if (!lastWasOperand || (setOperator ?? op) != op || (setOperator == null && operands.Count > 1))
                    {
                        throw Error($"invalid set operation in '{_source}'.");
                    }

                    setOperator = op;
                    lastWasOperand = false;
                    _pos += 2;
                    continue;
                }

                if (setOperator != null && lastWasOperand)
                {
                    throw Error($"mixed set operations in '{_source}'.");
                }

                operands.Add(ParseClassOperand(allowRange: setOperator == null));
                lastWasOperand = true;
            }

            if (setOperator != null && !lastWasOperand)
            {
                throw Error($"invalid set operation in '{_source}'.");
            }

            string matcher;
            if (setOperator == null && operands.TrueForAll(operand => operand.ClassContent != null))
            {
                string content = string.Concat(operands.Select(operand => operand.ClassContent));
                if (content.Length == 0)
                {
                    return new ClassOperand(negated ? AnyChar : "(?!)", null);
                }

                return new ClassOperand("[" + (negated ? "^" : string.Empty) + content + "]", null);
            }

            if (setOperator == null)
            {
                matcher = "(?:" + string.Join('|', operands.Select(operand => operand.Matcher)) + ")";
            }
            else if (setOperator == "&&")
            {
                matcher = string.Concat(operands.Take(operands.Count - 1).Select(operand => "(?=" + operand.Matcher + ")")) + operands[^1].Matcher;
            }
            else
            {
                matcher = string.Concat(operands.Skip(1).Select(operand => "(?!" + operand.Matcher + ")")) + operands[0].Matcher;
            }

            return new ClassOperand(negated ? "(?!" + matcher + ")" + AnyChar : matcher, null);
        }

        private ClassOperand ParseClassOperand(bool allowRange)
        {
            if (Peek() == '[')
            {
                _pos++;
                return ParseClass();
            }

            int from = ParseClassCharacter(out ClassOperand escapeClass);
            if (escapeClass != null)
            {
                return escapeClass;
            }

            if (allowRange && Peek() == '-' && Peek(1) != '-' && Peek(1) != ']')
            {
                _pos++;
                int to = ParseClassCharacter(out ClassOperand toClass);
                if (toClass != null || to < from)
                {
                    throw Error($"invalid class range in '{_source}'.");
                }

                string range = Hex4(from) + "-" + Hex4(to);
                return new ClassOperand("[" + range + "]", range);
            }

            return new ClassOperand("[" + Hex4(from) + "]", Hex4(from));
        }

        /// <summary>
        /// Parses one class character. When the next item is a class escape such as \d, returns -1 and sets <paramref name="escapeClass"/>.
        /// </summary>
        private int ParseClassCharacter(out ClassOperand escapeClass)
        {
            escapeClass = null;
            char c = Peek();
            if (c == '\\')
            {
                if (Peek(1) == 'q')
                {
                    throw Error($"class string disjunctions are not supported: '{_source}'.");
                }

                (string pattern, string classContent, int codePoint) = TranslateEscape(inClass: true);
                if (codePoint < 0)
                {
                    escapeClass = new ClassOperand(pattern, classContent);
                }

                return codePoint;
            }

            if (IsClassSetSyntaxCharacter(c) || (IsClassSetReservedDoublePunctuator(c) && Peek(1) == c))
            {
                throw Error($"'{c}' must be escaped in a class: '{_source}'.");
            }

            _pos++;
            return c;
        }

        /// <summary>
        /// One operand of a class: a stand-alone .NET pattern that consumes one character, and,
        /// when it can be written inside a plain .NET class, the content to use there.
        /// </summary>
        private sealed record ClassOperand(string Matcher, string ClassContent);
    }
}
