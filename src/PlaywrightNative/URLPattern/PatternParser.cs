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

namespace PlaywrightNative
{
    /// <summary>
    /// The pattern string parser (https://urlpattern.spec.whatwg.org/#parsing-pattern-strings).
    /// </summary>
    internal sealed class PatternParser
    {
        public const string FullWildcardRegexpValue = ".*";

        private readonly List<PatternToken> _tokens;
        private readonly Func<string, string> _encodingCallback;
        private readonly string _segmentWildcardRegexp;
        private readonly List<PatternPart> _parts = [];
        private string _pendingFixedValue = string.Empty;
        private int _index;
        private int _nextNumericName;

        private PatternParser(string input, PatternOptions options, Func<string, string> encodingCallback)
        {
            _tokens = PatternTokenizer.Tokenize(input, lenient: false);
            _encodingCallback = encodingCallback;
            _segmentWildcardRegexp = GenerateSegmentWildcardRegexp(options);
        }

        /// <summary>
        /// Parses a pattern string into a part list. Throws <see cref="ArgumentException"/> when the pattern is not well formed.
        /// </summary>
        public static List<PatternPart> Parse(string input, PatternOptions options, Func<string, string> encodingCallback)
        {
            var parser = new PatternParser(input, options, encodingCallback);
            parser.Run(options);
            return parser._parts;
        }

        public static string GenerateSegmentWildcardRegexp(PatternOptions options)
            => "[^" + EscapeRegexpString(options.DelimiterCodePoint) + "]+?";

        public static string EscapeRegexpString(string input)
        {
            var result = new System.Text.StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (c is '.' or '+' or '*' or '?' or '^' or '$' or '{' or '}' or '(' or ')' or '[' or ']' or '|' or '/' or '\\')
                {
                    result.Append('\\');
                }

                result.Append(c);
            }

            return result.ToString();
        }

        public static string EscapePatternString(string input)
        {
            var result = new System.Text.StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (c is '+' or '*' or '?' or ':' or '{' or '}' or '(' or ')' or '\\')
                {
                    result.Append('\\');
                }

                result.Append(c);
            }

            return result.ToString();
        }

        public static string ModifierToString(PatternPartModifier modifier) => modifier switch
        {
            PatternPartModifier.ZeroOrMore => "*",
            PatternPartModifier.Optional => "?",
            PatternPartModifier.OneOrMore => "+",
            _ => string.Empty,
        };

        private void Run(PatternOptions options)
        {
            while (_index < _tokens.Count)
            {
                PatternToken charToken = TryConsumeToken(PatternTokenType.Char);
                PatternToken nameToken = TryConsumeToken(PatternTokenType.Name);
                PatternToken regexpOrWildcardToken = TryConsumeRegexpOrWildcardToken(nameToken);
                if (nameToken != null || regexpOrWildcardToken != null)
                {
                    string prefix = charToken?.Value ?? string.Empty;
                    if (prefix.Length > 0 && prefix != options.PrefixCodePoint)
                    {
                        _pendingFixedValue += prefix;
                        prefix = string.Empty;
                    }

                    MaybeAddPartFromPendingFixedValue();
                    PatternToken modifierToken = TryConsumeModifierToken();
                    AddPart(prefix, nameToken, regexpOrWildcardToken, string.Empty, modifierToken);
                    continue;
                }

                PatternToken fixedToken = charToken ?? TryConsumeToken(PatternTokenType.EscapedChar);
                if (fixedToken != null)
                {
                    _pendingFixedValue += fixedToken.Value;
                    continue;
                }

                PatternToken openToken = TryConsumeToken(PatternTokenType.Open);
                if (openToken != null)
                {
                    string prefix = ConsumeText();
                    nameToken = TryConsumeToken(PatternTokenType.Name);
                    regexpOrWildcardToken = TryConsumeRegexpOrWildcardToken(nameToken);
                    string suffix = ConsumeText();
                    ConsumeRequiredToken(PatternTokenType.Close);
                    PatternToken modifierToken = TryConsumeModifierToken();
                    AddPart(prefix, nameToken, regexpOrWildcardToken, suffix, modifierToken);
                    continue;
                }

                MaybeAddPartFromPendingFixedValue();
                ConsumeRequiredToken(PatternTokenType.End);
            }
        }

        private PatternToken TryConsumeToken(PatternTokenType type)
        {
            PatternToken next = _tokens[_index];
            if (next.Type != type)
            {
                return null;
            }

            _index++;
            return next;
        }

        private PatternToken TryConsumeModifierToken()
            => TryConsumeToken(PatternTokenType.OtherModifier) ?? TryConsumeToken(PatternTokenType.Asterisk);

        private PatternToken TryConsumeRegexpOrWildcardToken(PatternToken nameToken)
        {
            PatternToken token = TryConsumeToken(PatternTokenType.Regexp);
            if (nameToken == null && token == null)
            {
                token = TryConsumeToken(PatternTokenType.Asterisk);
            }

            return token;
        }

        private void ConsumeRequiredToken(PatternTokenType type)
        {
            if (TryConsumeToken(type) == null)
            {
                throw new ArgumentException($"Invalid pattern: unexpected '{_tokens[_index].Value}' at index {_tokens[_index].Index}.");
            }
        }

        private string ConsumeText()
        {
            string result = string.Empty;
            while (true)
            {
                PatternToken token = TryConsumeToken(PatternTokenType.Char) ?? TryConsumeToken(PatternTokenType.EscapedChar);
                if (token == null)
                {
                    return result;
                }

                result += token.Value;
            }
        }

        private void MaybeAddPartFromPendingFixedValue()
        {
            if (_pendingFixedValue.Length == 0)
            {
                return;
            }

            string encodedValue = _encodingCallback(_pendingFixedValue);
            _pendingFixedValue = string.Empty;
            _parts.Add(new PatternPart(PatternPartType.FixedText, encodedValue, PatternPartModifier.None));
        }

        private void AddPart(string prefix, PatternToken nameToken, PatternToken regexpOrWildcardToken, string suffix, PatternToken modifierToken)
        {
            PatternPartModifier modifier = modifierToken?.Value switch
            {
                "?" => PatternPartModifier.Optional,
                "*" => PatternPartModifier.ZeroOrMore,
                "+" => PatternPartModifier.OneOrMore,
                _ => PatternPartModifier.None,
            };

            if (nameToken == null && regexpOrWildcardToken == null && modifier == PatternPartModifier.None)
            {
                _pendingFixedValue += prefix;
                return;
            }

            MaybeAddPartFromPendingFixedValue();
            if (nameToken == null && regexpOrWildcardToken == null)
            {
                if (prefix.Length == 0)
                {
                    return;
                }

                _parts.Add(new PatternPart(PatternPartType.FixedText, _encodingCallback(prefix), modifier));
                return;
            }

            string regexpValue = regexpOrWildcardToken == null
                ? _segmentWildcardRegexp
                : (regexpOrWildcardToken.Type == PatternTokenType.Asterisk ? FullWildcardRegexpValue : regexpOrWildcardToken.Value);

            PatternPartType type = PatternPartType.Regexp;
            if (regexpValue == _segmentWildcardRegexp)
            {
                type = PatternPartType.SegmentWildcard;
                regexpValue = string.Empty;
            }
            else if (regexpValue == FullWildcardRegexpValue)
            {
                type = PatternPartType.FullWildcard;
                regexpValue = string.Empty;
            }

            string name = string.Empty;
            if (nameToken != null)
            {
                name = nameToken.Value;
            }
            else if (regexpOrWildcardToken != null)
            {
                name = _nextNumericName.ToString(CultureInfo.InvariantCulture);
                _nextNumericName++;
            }

            if (_parts.Exists(part => part.Name == name))
            {
                throw new ArgumentException($"Invalid pattern: duplicate group name '{name}'.");
            }

            _parts.Add(new PatternPart(type, regexpValue, modifier, name, _encodingCallback(prefix), _encodingCallback(suffix)));
        }
    }
}
