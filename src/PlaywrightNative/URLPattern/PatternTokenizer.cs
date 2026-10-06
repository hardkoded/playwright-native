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
    /// The pattern string tokenizer (https://urlpattern.spec.whatwg.org/#tokenizing).
    /// It walks code points, but keeps UTF-16 offsets so substrings line up with the input.
    /// </summary>
    internal sealed class PatternTokenizer
    {
        private readonly string _input;
        private readonly bool _lenient;
        private readonly List<PatternToken> _tokens = [];
        private int _index;
        private int _nextIndex;
        private int _codePoint;

        private PatternTokenizer(string input, bool lenient)
        {
            _input = input;
            _lenient = lenient;
        }

        /// <summary>
        /// Tokenizes <paramref name="input"/>. The strict policy throws <see cref="ArgumentException"/> on invalid input;
        /// the lenient policy emits "invalid-char" tokens instead.
        /// </summary>
        public static List<PatternToken> Tokenize(string input, bool lenient)
        {
            var tokenizer = new PatternTokenizer(input, lenient);
            tokenizer.Run();
            return tokenizer._tokens;
        }

        /// <summary>
        /// Returns whether <paramref name="codePoint"/> can appear in a group name (JavaScript IdentifierStart / IdentifierPart).
        /// </summary>
        public static bool IsValidNameCodePoint(int codePoint, bool first)
        {
            if (codePoint == '$' || codePoint == '_')
            {
                return true;
            }

            if (!first && (codePoint == 0x200C || codePoint == 0x200D))
            {
                return true;
            }

            // Other_ID_Start and Other_ID_Continue code points from Unicode PropList.txt.
            if (codePoint is 0x1885 or 0x1886 or 0x2118 or 0x212E or 0x309B or 0x309C)
            {
                return true;
            }

            if (!first && codePoint is 0x00B7 or 0x0387 or (>= 0x1369 and <= 0x1371) or 0x19DA or 0x30FB or 0xFF65)
            {
                return true;
            }

            // Pattern_Syntax code points that are otherwise letters.
            if (codePoint is 0x2E2F)
            {
                return false;
            }

            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(codePoint);
            switch (category)
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.LetterNumber:
                    return true;
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark:
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.ConnectorPunctuation:
                    return !first;
                default:
                    return false;
            }
        }

        private static bool IsAscii(int codePoint) => codePoint <= 0x7F;

        private void Run()
        {
            while (_index < _input.Length)
            {
                SeekAndGetNextCodePoint(_index);
                switch (_codePoint)
                {
                    case '*':
                        AddTokenWithDefaultPositionAndLength(PatternTokenType.Asterisk);
                        continue;
                    case '+':
                    case '?':
                        AddTokenWithDefaultPositionAndLength(PatternTokenType.OtherModifier);
                        continue;
                    case '\\':
                        if (_nextIndex >= _input.Length)
                        {
                            ProcessTokenizingError(_nextIndex, _index);
                            continue;
                        }

                        int escapedIndex = _nextIndex;
                        GetNextCodePoint();
                        AddTokenWithDefaultLength(PatternTokenType.EscapedChar, _nextIndex, escapedIndex);
                        continue;
                    case '{':
                        AddTokenWithDefaultPositionAndLength(PatternTokenType.Open);
                        continue;
                    case '}':
                        AddTokenWithDefaultPositionAndLength(PatternTokenType.Close);
                        continue;
                    case ':':
                        TokenizeName();
                        continue;
                    case '(':
                        TokenizeRegexp();
                        continue;
                    default:
                        AddTokenWithDefaultPositionAndLength(PatternTokenType.Char);
                        continue;
                }
            }

            AddTokenWithDefaultLength(PatternTokenType.End, _index, _index);
        }

        private void TokenizeName()
        {
            int namePosition = _nextIndex;
            int nameStart = namePosition;
            while (namePosition < _input.Length)
            {
                SeekAndGetNextCodePoint(namePosition);
                if (!IsValidNameCodePoint(_codePoint, namePosition == nameStart))
                {
                    break;
                }

                namePosition = _nextIndex;
            }

            if (namePosition <= nameStart)
            {
                ProcessTokenizingError(nameStart, _index);
                return;
            }

            AddTokenWithDefaultLength(PatternTokenType.Name, namePosition, nameStart);
        }

        private void TokenizeRegexp()
        {
            int depth = 1;
            int regexpPosition = _nextIndex;
            int regexpStart = regexpPosition;
            while (regexpPosition < _input.Length)
            {
                SeekAndGetNextCodePoint(regexpPosition);
                if (!IsAscii(_codePoint) || (regexpPosition == regexpStart && _codePoint == '?'))
                {
                    ProcessTokenizingError(regexpStart, _index);
                    return;
                }

                if (_codePoint == '\\')
                {
                    if (_nextIndex >= _input.Length)
                    {
                        ProcessTokenizingError(regexpStart, _index);
                        return;
                    }

                    GetNextCodePoint();
                    if (!IsAscii(_codePoint))
                    {
                        ProcessTokenizingError(regexpStart, _index);
                        return;
                    }

                    regexpPosition = _nextIndex;
                    continue;
                }

                if (_codePoint == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        regexpPosition = _nextIndex;
                        break;
                    }
                }
                else if (_codePoint == '(')
                {
                    depth++;
                    if (_nextIndex >= _input.Length)
                    {
                        ProcessTokenizingError(regexpStart, _index);
                        return;
                    }

                    int temporaryPosition = _nextIndex;
                    GetNextCodePoint();
                    if (_codePoint != '?')
                    {
                        ProcessTokenizingError(regexpStart, _index);
                        return;
                    }

                    _nextIndex = temporaryPosition;
                }

                regexpPosition = _nextIndex;
            }

            if (depth != 0)
            {
                ProcessTokenizingError(regexpStart, _index);
                return;
            }

            int regexpLength = regexpPosition - regexpStart - 1;
            if (regexpLength == 0)
            {
                ProcessTokenizingError(regexpStart, _index);
                return;
            }

            AddToken(PatternTokenType.Regexp, regexpPosition, regexpStart, regexpLength);
        }

        private void GetNextCodePoint()
        {
            char c = _input[_nextIndex];
            if (char.IsHighSurrogate(c) && _nextIndex + 1 < _input.Length && char.IsLowSurrogate(_input[_nextIndex + 1]))
            {
                _codePoint = char.ConvertToUtf32(c, _input[_nextIndex + 1]);
                _nextIndex += 2;
            }
            else
            {
                _codePoint = c;
                _nextIndex++;
            }
        }

        private void SeekAndGetNextCodePoint(int index)
        {
            _nextIndex = index;
            GetNextCodePoint();
        }

        private void AddToken(PatternTokenType type, int nextPosition, int valuePosition, int valueLength)
        {
            _tokens.Add(new PatternToken(type, _index, _input.Substring(valuePosition, valueLength)));
            _index = nextPosition;
        }

        private void AddTokenWithDefaultLength(PatternTokenType type, int nextPosition, int valuePosition)
            => AddToken(type, nextPosition, valuePosition, nextPosition - valuePosition);

        private void AddTokenWithDefaultPositionAndLength(PatternTokenType type)
            => AddTokenWithDefaultLength(type, _nextIndex, _index);

        private void ProcessTokenizingError(int nextPosition, int valuePosition)
        {
            if (!_lenient)
            {
                throw new ArgumentException($"Invalid pattern '{_input}' at index {valuePosition}.");
            }

            AddTokenWithDefaultLength(PatternTokenType.InvalidChar, nextPosition, valuePosition);
        }
    }
}
