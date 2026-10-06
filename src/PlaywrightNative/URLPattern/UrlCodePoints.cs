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

namespace PlaywrightNative
{
    /// <summary>
    /// Code point helpers shared by the WHATWG URL parser and the URL pattern parser.
    /// </summary>
    internal static class UrlCodePoints
    {
        /// <summary>
        /// Marks the end of input in the URL parser state machine.
        /// </summary>
        public const int Eof = -1;

        public static bool IsAsciiDigit(int c) => c >= '0' && c <= '9';

        public static bool IsAsciiHexDigit(int c) => IsAsciiDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

        public static bool IsAsciiAlpha(int c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        public static bool IsAsciiAlphanumeric(int c) => IsAsciiAlpha(c) || IsAsciiDigit(c);

        public static int ToAsciiLower(int c) => c >= 'A' && c <= 'Z' ? c + 0x20 : c;

        public static string ToAsciiLower(string input)
        {
            var output = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                output.Append((char)ToAsciiLower(c));
            }

            return output.ToString();
        }

        /// <summary>
        /// Converts a string to a USVString (lone surrogates become U+FFFD), as WebIDL does for USVString arguments.
        /// </summary>
        public static string ToUsvString(string input)
        {
            if (input == null)
            {
                return null;
            }

            StringBuilder builder = null;
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                bool lone = false;
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 < input.Length && char.IsLowSurrogate(input[i + 1]))
                    {
                        builder?.Append(c).Append(input[i + 1]);
                        i++;
                        continue;
                    }

                    lone = true;
                }
                else if (char.IsLowSurrogate(c))
                {
                    lone = true;
                }

                if (lone && builder == null)
                {
                    builder = new StringBuilder(input.Length);
                    builder.Append(input, 0, i);
                }

                builder?.Append(lone ? '\uFFFD' : c);
            }

            return builder?.ToString() ?? input;
        }

        public static int[] ToCodePoints(string input)
        {
            var result = new List<int>(input.Length);
            for (int i = 0; i < input.Length; i++)
            {
                if (char.IsHighSurrogate(input[i]) && i + 1 < input.Length && char.IsLowSurrogate(input[i + 1]))
                {
                    result.Add(char.ConvertToUtf32(input[i], input[i + 1]));
                    i++;
                }
                else
                {
                    result.Add(input[i]);
                }
            }

            return result.ToArray();
        }

        public static void AppendCodePoint(StringBuilder output, int codePoint)
        {
            if (codePoint > 0xFFFF)
            {
                output.Append(char.ConvertFromUtf32(codePoint));
            }
            else
            {
                output.Append((char)codePoint);
            }
        }

        public static int CodePointLength(string input)
        {
            int length = 0;
            for (int i = 0; i < input.Length; i++)
            {
                if (char.IsHighSurrogate(input[i]) && i + 1 < input.Length && char.IsLowSurrogate(input[i + 1]))
                {
                    i++;
                }

                length++;
            }

            return length;
        }

        public static bool IsAscii(string input)
        {
            foreach (char c in input)
            {
                if (c > 0x7F)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool InSet(int c, PercentEncodeSet set)
        {
            if (c < 0x20 || c > 0x7E)
            {
                return true;
            }

            return set switch
            {
                PercentEncodeSet.C0Control => false,
                PercentEncodeSet.Fragment => c is ' ' or '"' or '<' or '>' or '`',
                PercentEncodeSet.Query => IsQuery(c),
                PercentEncodeSet.SpecialQuery => IsQuery(c) || c == '\'',
                PercentEncodeSet.Path => IsPath(c),
                PercentEncodeSet.Userinfo => IsPath(c) || c is '/' or ':' or ';' or '=' or '@' or '[' or '\\' or ']' or '|',
                _ => throw new ArgumentOutOfRangeException(nameof(set)),
            };
        }

        /// <summary>
        /// UTF-8 percent-encodes a single code point using <paramref name="set"/> and appends it to <paramref name="output"/>.
        /// </summary>
        public static void AppendPercentEncoded(StringBuilder output, int codePoint, PercentEncodeSet set)
        {
            if (!InSet(codePoint, set))
            {
                output.Append((char)codePoint);
                return;
            }

            foreach (byte b in Encoding.UTF8.GetBytes(char.ConvertFromUtf32(codePoint)))
            {
                output.Append('%').Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        public static string PercentEncode(string input, PercentEncodeSet set)
        {
            var output = new StringBuilder(input.Length);
            foreach (int c in ToCodePoints(input))
            {
                AppendPercentEncoded(output, c, set);
            }

            return output.ToString();
        }

        /// <summary>
        /// Percent-decodes the UTF-8 bytes of <paramref name="input"/> and decodes the result as UTF-8.
        /// </summary>
        public static string PercentDecode(string input)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            var output = new List<byte>(bytes.Length);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == '%' && i + 2 < bytes.Length && IsAsciiHexDigit(bytes[i + 1]) && IsAsciiHexDigit(bytes[i + 2]))
                {
                    output.Add((byte)((HexValue(bytes[i + 1]) << 4) | HexValue(bytes[i + 2])));
                    i += 2;
                }
                else
                {
                    output.Add(bytes[i]);
                }
            }

            return Encoding.UTF8.GetString(output.ToArray());
        }

        public static int HexValue(int c) => c <= '9' ? c - '0' : ToAsciiLower(c) - 'a' + 10;

        private static bool IsQuery(int c) => c is ' ' or '"' or '#' or '<' or '>';

        private static bool IsPath(int c) => IsQuery(c) || c is '?' or '^' or '`' or '{' or '}';
    }
}
