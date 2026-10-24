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
using System.Globalization;
using System.Text;

namespace PlaywrightNative
{
    /// <summary>
    /// The WHATWG host parser and serializer (https://url.spec.whatwg.org/#host-parsing).
    /// Hosts are kept in their serialized form.
    /// </summary>
    internal static class UrlHostParser
    {
        private static readonly IdnMapping _idn = new() { AllowUnassigned = true, UseStd3AsciiRules = false };

        /// <summary>
        /// Parses <paramref name="input"/> as a host and returns its serialization, or null on failure.
        /// </summary>
        public static string Parse(string input, bool isOpaque)
        {
            if (input.StartsWith('['))
            {
                if (!input.EndsWith(']'))
                {
                    return null;
                }

                ushort[] address = ParseIPv6(UrlCodePoints.ToCodePoints(input[1..^1]));
                return address == null ? null : "[" + SerializeIPv6(address) + "]";
            }

            if (isOpaque)
            {
                return ParseOpaqueHost(input);
            }

            string domain = UrlCodePoints.PercentDecode(input);
            string asciiDomain = DomainToAscii(domain);
            if (asciiDomain == null)
            {
                return null;
            }

            if (EndsInNumber(asciiDomain))
            {
                return ParseIPv4(asciiDomain) is uint ipv4 ? SerializeIPv4(ipv4) : null;
            }

            return asciiDomain;
        }

        public static bool IsForbiddenHostCodePoint(int c)
            => c is 0 or '\t' or '\n' or '\r' or ' ' or '#' or '/' or ':' or '<' or '>' or '?' or '@' or '[' or '\\' or ']' or '^' or '|';

        private static bool IsForbiddenDomainCodePoint(int c)
            => IsForbiddenHostCodePoint(c) || c <= 0x1F || c == '%' || c == 0x7F;

        private static string DomainToAscii(string domain)
        {
            string result;
            if (UrlCodePoints.IsAscii(domain))
            {
                result = UrlCodePoints.ToAsciiLower(domain);
            }
            else
            {
                try
                {
                    result = _idn.GetAscii(domain);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }

            if (result.Length == 0)
            {
                return null;
            }

            foreach (char c in result)
            {
                if (IsForbiddenDomainCodePoint(c))
                {
                    return null;
                }
            }

            return result;
        }

        private static string ParseOpaqueHost(string input)
        {
            foreach (int c in UrlCodePoints.ToCodePoints(input))
            {
                if (IsForbiddenHostCodePoint(c))
                {
                    return null;
                }
            }

            return UrlCodePoints.PercentEncode(input, PercentEncodeSet.C0Control);
        }

        private static bool EndsInNumber(string input)
        {
            string[] parts = input.Split('.');
            string last = parts[^1];
            if (last.Length == 0)
            {
                if (parts.Length == 1)
                {
                    return false;
                }

                last = parts[^2];
            }

            if (last.Length > 0 && IsAllDigits(last))
            {
                return true;
            }

            return ParseIPv4Number(last).HasValue;
        }

        private static bool IsAllDigits(string value)
        {
            foreach (char c in value)
            {
                if (!UrlCodePoints.IsAsciiDigit(c))
                {
                    return false;
                }
            }

            return true;
        }

        private static uint? ParseIPv4(string input)
        {
            string[] parts = input.Split('.');
            int count = parts.Length;
            if (parts[^1].Length == 0 && count > 1)
            {
                count--;
            }

            if (count > 4)
            {
                return null;
            }

            ulong[] numbers = new ulong[count];
            for (int i = 0; i < count; i++)
            {
                ulong? number = ParseIPv4Number(parts[i]);
                if (!number.HasValue)
                {
                    return null;
                }

                numbers[i] = number.Value;
            }

            for (int i = 0; i < count - 1; i++)
            {
                if (numbers[i] > 255)
                {
                    return null;
                }
            }

            if (numbers[count - 1] >= 1UL << (8 * (5 - count)))
            {
                return null;
            }

            ulong ipv4 = numbers[count - 1];
            for (int i = 0; i < count - 1; i++)
            {
                ipv4 += numbers[i] << (8 * (3 - i));
            }

            return (uint)ipv4;
        }

        /// <summary>
        /// Parses an IPv4 number. Values that do not fit in 40 bits saturate, which still fails the range checks.
        /// </summary>
        private static ulong? ParseIPv4Number(string input)
        {
            if (input.Length == 0)
            {
                return null;
            }

            int radix = 10;
            if (input.Length >= 2 && (input.StartsWith("0x", StringComparison.Ordinal) || input.StartsWith("0X", StringComparison.Ordinal)))
            {
                input = input[2..];
                radix = 16;
            }
            else if (input.Length >= 2 && input[0] == '0')
            {
                input = input[1..];
                radix = 8;
            }

            ulong value = 0;
            foreach (char c in input)
            {
                int digit = radix == 16 && UrlCodePoints.IsAsciiHexDigit(c) ? UrlCodePoints.HexValue(c) : (UrlCodePoints.IsAsciiDigit(c) ? c - '0' : -1);
                if (digit < 0 || digit >= radix)
                {
                    return null;
                }

                value = Math.Min((value * (ulong)radix) + (ulong)digit, 1UL << 40);
            }

            return value;
        }

        private static string SerializeIPv4(uint address)
            => string.Join('.', address >> 24, (address >> 16) & 0xFF, (address >> 8) & 0xFF, address & 0xFF);

        private static ushort[] ParseIPv6(int[] input)
        {
            ushort[] address = new ushort[8];
            int pieceIndex = 0;
            int? compress = null;
            int pointer = 0;
            int At(int i) => i < input.Length ? input[i] : UrlCodePoints.Eof;

            if (At(pointer) == ':')
            {
                if (At(pointer + 1) != ':')
                {
                    return null;
                }

                pointer += 2;
                pieceIndex++;
                compress = pieceIndex;
            }

            while (At(pointer) != UrlCodePoints.Eof)
            {
                if (pieceIndex == 8)
                {
                    return null;
                }

                if (At(pointer) == ':')
                {
                    if (compress.HasValue)
                    {
                        return null;
                    }

                    pointer++;
                    pieceIndex++;
                    compress = pieceIndex;
                    continue;
                }

                int value = 0;
                int length = 0;
                while (length < 4 && UrlCodePoints.IsAsciiHexDigit(At(pointer)))
                {
                    value = (value * 0x10) + UrlCodePoints.HexValue(At(pointer));
                    pointer++;
                    length++;
                }

                if (At(pointer) == '.')
                {
                    if (length == 0)
                    {
                        return null;
                    }

                    pointer -= length;
                    if (pieceIndex > 6)
                    {
                        return null;
                    }

                    int numbersSeen = 0;
                    while (At(pointer) != UrlCodePoints.Eof)
                    {
                        int? ipv4Piece = null;
                        if (numbersSeen > 0)
                        {
                            if (At(pointer) == '.' && numbersSeen < 4)
                            {
                                pointer++;
                            }
                            else
                            {
                                return null;
                            }
                        }

                        if (!UrlCodePoints.IsAsciiDigit(At(pointer)))
                        {
                            return null;
                        }

                        while (UrlCodePoints.IsAsciiDigit(At(pointer)))
                        {
                            int number = At(pointer) - '0';
                            if (ipv4Piece == null)
                            {
                                ipv4Piece = number;
                            }
                            else if (ipv4Piece == 0)
                            {
                                return null;
                            }
                            else
                            {
                                ipv4Piece = (ipv4Piece * 10) + number;
                            }

                            if (ipv4Piece > 255)
                            {
                                return null;
                            }

                            pointer++;
                        }

                        address[pieceIndex] = (ushort)((address[pieceIndex] * 0x100) + ipv4Piece.Value);
                        numbersSeen++;
                        if (numbersSeen is 2 or 4)
                        {
                            pieceIndex++;
                        }
                    }

                    if (numbersSeen != 4)
                    {
                        return null;
                    }

                    break;
                }
                else if (At(pointer) == ':')
                {
                    pointer++;
                    if (At(pointer) == UrlCodePoints.Eof)
                    {
                        return null;
                    }
                }
                else if (At(pointer) != UrlCodePoints.Eof)
                {
                    return null;
                }

                address[pieceIndex] = (ushort)value;
                pieceIndex++;
            }

            if (compress.HasValue)
            {
                int swaps = pieceIndex - compress.Value;
                pieceIndex = 7;
                while (pieceIndex != 0 && swaps > 0)
                {
                    (address[pieceIndex], address[compress.Value + swaps - 1]) = (address[compress.Value + swaps - 1], address[pieceIndex]);
                    pieceIndex--;
                    swaps--;
                }
            }
            else if (pieceIndex != 8)
            {
                return null;
            }

            return address;
        }

        private static string SerializeIPv6(ushort[] address)
        {
            // Find the first longest run of two or more zero pieces.
            int compress = -1;
            int longest = 1;
            for (int i = 0; i < 8;)
            {
                int start = i;
                while (i < 8 && address[i] == 0)
                {
                    i++;
                }

                if (i - start > longest)
                {
                    longest = i - start;
                    compress = start;
                }

                if (i == start)
                {
                    i++;
                }
            }

            var output = new StringBuilder();
            bool ignore0 = false;
            for (int i = 0; i < 8; i++)
            {
                if (ignore0 && address[i] == 0)
                {
                    continue;
                }

                ignore0 = false;
                if (compress == i)
                {
                    output.Append(i == 0 ? "::" : ":");
                    ignore0 = true;
                    continue;
                }

                output.Append(address[i].ToString("x", CultureInfo.InvariantCulture));
                if (i != 7)
                {
                    output.Append(':');
                }
            }

            return output.ToString();
        }
    }
}
