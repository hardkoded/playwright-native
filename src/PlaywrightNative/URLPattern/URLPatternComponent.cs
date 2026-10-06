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
using System.Text;
using System.Text.RegularExpressions;

namespace PlaywrightNative
{
    /// <summary>
    /// A compiled URL pattern component (https://urlpattern.spec.whatwg.org/#component).
    /// </summary>
    internal sealed class URLPatternComponent
    {
        private URLPatternComponent(string patternString, Regex regularExpression, List<string> groupNameList, bool hasRegExpGroups)
        {
            PatternString = patternString;
            RegularExpression = regularExpression;
            GroupNameList = groupNameList;
            HasRegExpGroups = hasRegExpGroups;
        }

        public string PatternString { get; }

        public Regex RegularExpression { get; }

        public IReadOnlyList<string> GroupNameList { get; }

        public bool HasRegExpGroups { get; }

        /// <summary>
        /// Compiles a component (https://urlpattern.spec.whatwg.org/#compile-a-component).
        /// </summary>
        public static URLPatternComponent Compile(string input, Func<string, string> encodingCallback, PatternOptions options)
        {
            List<PatternPart> parts = PatternParser.Parse(input, options, encodingCallback);
            (string regexpString, List<string> nameList) = GenerateRegularExpressionAndNameList(parts, options);
            RegexOptions regexOptions = RegexOptions.CultureInvariant | (options.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
            Regex regex;
            try
            {
                regex = new Regex(regexpString, regexOptions);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"Invalid regular expression in URL pattern '{input}': {ex.Message}", ex);
            }

            return new URLPatternComponent(
                GeneratePatternString(parts, options),
                regex,
                nameList,
                parts.Exists(part => part.Type == PatternPartType.Regexp));
        }

        /// <summary>
        /// Matches <paramref name="input"/> and returns the group values, or null when it does not match.
        /// Groups that did not participate in the match map to null.
        /// </summary>
        public URLPatternComponentResult Exec(string input)
        {
            Match match = RegularExpression.Match(input);
            if (!match.Success)
            {
                return null;
            }

            var groups = new Dictionary<string, string>(GroupNameList.Count);
            for (int i = 0; i < GroupNameList.Count; i++)
            {
                Group group = match.Groups[i + 1];
                groups[GroupNameList[i]] = group.Success ? group.Value : null;
            }

            return new URLPatternComponentResult(input, groups);
        }

        /// <summary>
        /// Generates the regular expression and group name list (https://urlpattern.spec.whatwg.org/#generate-a-regular-expression-and-name-list).
        /// User regular expressions are translated from JavaScript to .NET syntax on the way.
        /// </summary>
        private static (string RegexpString, List<string> NameList) GenerateRegularExpressionAndNameList(List<PatternPart> parts, PatternOptions options)
        {
            var result = new StringBuilder("^");
            var nameList = new List<string>();
            int captureCount = 0;
            foreach (PatternPart part in parts)
            {
                if (part.Type == PatternPartType.FixedText)
                {
                    if (part.Modifier == PatternPartModifier.None)
                    {
                        result.Append(PatternParser.EscapeRegexpString(part.Value));
                    }
                    else
                    {
                        result.Append("(?:").Append(PatternParser.EscapeRegexpString(part.Value)).Append(')')
                            .Append(PatternParser.ModifierToString(part.Modifier));
                    }

                    continue;
                }

                nameList.Add(part.Name);
                string regexpValue = part.Type switch
                {
                    PatternPartType.SegmentWildcard => PatternParser.GenerateSegmentWildcardRegexp(options),
                    PatternPartType.FullWildcard => PatternParser.FullWildcardRegexpValue,
                    _ => part.Value,
                };

                // JavaScript numbers capture groups by the position of their opening parenthesis.
                // The translator keeps every capture group, so the numbering stays the same in .NET.
                captureCount++;
                string translated = JsRegExpTranslator.Translate(regexpValue, captureCount, out int innerCaptures);
                captureCount += innerCaptures;
                string modifier = PatternParser.ModifierToString(part.Modifier);
                bool repeating = part.Modifier is PatternPartModifier.ZeroOrMore or PatternPartModifier.OneOrMore;

                if (part.Prefix.Length == 0 && part.Suffix.Length == 0)
                {
                    if (!repeating)
                    {
                        result.Append('(').Append(translated).Append(')').Append(modifier);
                    }
                    else
                    {
                        result.Append("((?:").Append(translated).Append(')').Append(modifier).Append(')');
                    }

                    continue;
                }

                string prefix = PatternParser.EscapeRegexpString(part.Prefix);
                string suffix = PatternParser.EscapeRegexpString(part.Suffix);
                if (!repeating)
                {
                    result.Append("(?:").Append(prefix).Append('(').Append(translated).Append(')').Append(suffix).Append(')').Append(modifier);
                    continue;
                }

                string repeated = JsRegExpTranslator.Translate(regexpValue, captureCount, out innerCaptures);
                captureCount += innerCaptures;
                result.Append("(?:").Append(prefix).Append("((?:").Append(translated).Append(")(?:").Append(suffix).Append(prefix)
                    .Append("(?:").Append(repeated).Append("))*)").Append(suffix).Append(')');
                if (part.Modifier == PatternPartModifier.ZeroOrMore)
                {
                    result.Append('?');
                }
            }

            result.Append(@"\z");
            return (result.ToString(), nameList);
        }

        /// <summary>
        /// Generates the normalized pattern string (https://urlpattern.spec.whatwg.org/#generate-a-pattern-string).
        /// </summary>
        private static string GeneratePatternString(List<PatternPart> parts, PatternOptions options)
        {
            var result = new StringBuilder();
            for (int index = 0; index < parts.Count; index++)
            {
                PatternPart part = parts[index];
                PatternPart previousPart = index > 0 ? parts[index - 1] : null;
                PatternPart nextPart = index < parts.Count - 1 ? parts[index + 1] : null;
                if (part.Type == PatternPartType.FixedText)
                {
                    if (part.Modifier == PatternPartModifier.None)
                    {
                        result.Append(PatternParser.EscapePatternString(part.Value));
                    }
                    else
                    {
                        result.Append('{').Append(PatternParser.EscapePatternString(part.Value)).Append('}')
                            .Append(PatternParser.ModifierToString(part.Modifier));
                    }

                    continue;
                }

                bool customName = !UrlCodePoints.IsAsciiDigit(part.Name[0]);
                bool needsGrouping = part.Suffix.Length > 0 || (part.Prefix.Length > 0 && part.Prefix != options.PrefixCodePoint);
                if (!needsGrouping
                    && customName
                    && part.Type == PatternPartType.SegmentWildcard
                    && part.Modifier == PatternPartModifier.None
                    && nextPart != null
                    && nextPart.Prefix.Length == 0
                    && nextPart.Suffix.Length == 0)
                {
                    needsGrouping = nextPart.Type == PatternPartType.FixedText
                        ? nextPart.Value.Length > 0 && PatternTokenizer.IsValidNameCodePoint(char.ConvertToUtf32(nextPart.Value, 0), false)
                        : UrlCodePoints.IsAsciiDigit(nextPart.Name[0]);
                }

                if (!needsGrouping
                    && part.Prefix.Length == 0
                    && previousPart != null
                    && previousPart.Type == PatternPartType.FixedText
                    && options.PrefixCodePoint.Length > 0
                    && previousPart.Value.EndsWith(options.PrefixCodePoint, StringComparison.Ordinal))
                {
                    needsGrouping = true;
                }

                if (needsGrouping)
                {
                    result.Append('{');
                }

                result.Append(PatternParser.EscapePatternString(part.Prefix));
                if (customName)
                {
                    result.Append(':').Append(part.Name);
                }

                if (part.Type == PatternPartType.Regexp)
                {
                    result.Append('(').Append(part.Value).Append(')');
                }
                else if (part.Type == PatternPartType.SegmentWildcard && !customName)
                {
                    result.Append('(').Append(PatternParser.GenerateSegmentWildcardRegexp(options)).Append(')');
                }
                else if (part.Type == PatternPartType.FullWildcard)
                {
                    if (!customName
                        && (previousPart == null
                            || previousPart.Type == PatternPartType.FixedText
                            || previousPart.Modifier != PatternPartModifier.None
                            || needsGrouping
                            || part.Prefix.Length > 0))
                    {
                        result.Append('*');
                    }
                    else
                    {
                        result.Append('(').Append(PatternParser.FullWildcardRegexpValue).Append(')');
                    }
                }

                if (part.Type == PatternPartType.SegmentWildcard
                    && customName
                    && part.Suffix.Length > 0
                    && PatternTokenizer.IsValidNameCodePoint(char.ConvertToUtf32(part.Suffix, 0), false))
                {
                    result.Append('\\');
                }

                result.Append(PatternParser.EscapePatternString(part.Suffix));
                if (needsGrouping)
                {
                    result.Append('}');
                }

                result.Append(PatternParser.ModifierToString(part.Modifier));
            }

            return result.ToString();
        }
    }
}
