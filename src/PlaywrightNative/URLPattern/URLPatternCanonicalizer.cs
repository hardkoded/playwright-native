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
    /// The URL pattern encoding callbacks (https://urlpattern.spec.whatwg.org/#canon-encoding-callbacks).
    /// Each one runs the WHATWG URL parser on a dummy URL and throws <see cref="ArgumentException"/> on failure.
    /// </summary>
    internal static class URLPatternCanonicalizer
    {
        public static string Protocol(string value)
        {
            if (value.Length == 0)
            {
                return value;
            }

            UrlRecord url = UrlParser.Parse(value + "://dummy.invalid/");
            return url?.Scheme ?? throw Invalid("protocol", value);
        }

        public static string Username(string value) => UrlCodePoints.PercentEncode(value, PercentEncodeSet.Userinfo);

        public static string Password(string value) => UrlCodePoints.PercentEncode(value, PercentEncodeSet.Userinfo);

        public static string Hostname(string value)
        {
            if (value.Length == 0)
            {
                return value;
            }

            UrlRecord url = UrlRecord.CreateDummy();
            if (!UrlParser.ParseInto(value, url, UrlParserState.Hostname))
            {
                throw Invalid("hostname", value);
            }

            return url.Host ?? string.Empty;
        }

        public static string IPv6Hostname(string value)
        {
            var result = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (!UrlCodePoints.IsAsciiHexDigit(c) && c != '[' && c != ']' && c != ':')
                {
                    throw Invalid("hostname", value);
                }

                result.Append((char)UrlCodePoints.ToAsciiLower(c));
            }

            return result.ToString();
        }

        public static string Port(string value) => Port(value, null);

        public static string Port(string portValue, string protocolValue)
        {
            if (portValue.Length == 0)
            {
                return portValue;
            }

            UrlRecord url = UrlRecord.CreateDummy();
            url.Scheme = protocolValue ?? string.Empty;
            if (!UrlParser.ParseInto(portValue, url, UrlParserState.Port))
            {
                throw Invalid("port", portValue);
            }

            return url.Port?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        }

        public static string Pathname(string value)
        {
            if (value.Length == 0)
            {
                return value;
            }

            bool leadingSlash = value[0] == '/';
            UrlRecord url = UrlRecord.CreateDummy();
            url.Path.Clear();
            UrlParser.ParseInto(leadingSlash ? value : "/-" + value, url, UrlParserState.PathStart);
            string result = url.SerializePath();
            return leadingSlash ? result : result[2..];
        }

        public static string OpaquePathname(string value)
        {
            if (value.Length == 0)
            {
                return value;
            }

            UrlRecord url = UrlRecord.CreateDummy();
            url.OpaquePath = string.Empty;
            if (!UrlParser.ParseInto(value, url, UrlParserState.OpaquePath))
            {
                throw Invalid("pathname", value);
            }

            return url.SerializePath();
        }

        public static string Search(string value)
        {
            if (value.Length == 0)
            {
                return value;
            }

            UrlRecord url = UrlRecord.CreateDummy();
            url.Query = string.Empty;
            UrlParser.ParseInto(value, url, UrlParserState.Query);
            return url.Query;
        }

        public static string Hash(string value)
        {
            if (value.Length == 0)
            {
                return value;
            }

            UrlRecord url = UrlRecord.CreateDummy();
            url.Fragment = string.Empty;
            UrlParser.ParseInto(value, url, UrlParserState.Fragment);
            return url.Fragment;
        }

        private static ArgumentException Invalid(string component, string value)
            => new($"Invalid {component} '{value}' in URL pattern.");
    }
}
