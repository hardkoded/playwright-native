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

namespace PlaywrightNative
{
    /// <summary>
    /// Processes a URLPatternInit (https://urlpattern.spec.whatwg.org/#process-a-urlpatterninit).
    /// For patterns, values pass through and the base URL is escaped. For URLs, every value is canonicalized.
    /// </summary>
    internal static class URLPatternInitProcessor
    {
        /// <summary>
        /// Processes <paramref name="init"/>. <paramref name="isPattern"/> selects the "pattern" type over the "url" type.
        /// <paramref name="defaults"/> holds the starting values; its null members stay absent.
        /// Throws <see cref="ArgumentException"/> when a value or the base URL is invalid.
        /// </summary>
        public static URLPatternFields Process(URLPatternFields init, bool isPattern, URLPatternFields defaults)
        {
            var result = new URLPatternFields
            {
                Protocol = defaults.Protocol,
                Username = defaults.Username,
                Password = defaults.Password,
                Hostname = defaults.Hostname,
                Port = defaults.Port,
                Pathname = defaults.Pathname,
                Search = defaults.Search,
                Hash = defaults.Hash,
            };

            UrlRecord baseUrl = null;
            if (init.BaseURL != null)
            {
                baseUrl = UrlParser.Parse(init.BaseURL) ?? throw new ArgumentException($"Invalid base URL '{init.BaseURL}'.");
                bool hasProtocol = init.Protocol != null;
                bool hasHostname = hasProtocol || init.Hostname != null;
                bool hasPort = hasHostname || init.Port != null;
                bool hasPathname = hasPort || init.Pathname != null;
                bool hasSearch = hasPathname || init.Search != null;
                bool hasUsername = hasPort || init.Username != null;

                if (!hasProtocol)
                {
                    result.Protocol = ProcessBaseURLString(baseUrl.Scheme, isPattern);
                }

                if (!isPattern && !hasUsername)
                {
                    result.Username = ProcessBaseURLString(baseUrl.Username, isPattern);
                }

                if (!isPattern && !hasUsername && init.Password == null)
                {
                    result.Password = ProcessBaseURLString(baseUrl.Password, isPattern);
                }

                if (!hasHostname)
                {
                    result.Hostname = ProcessBaseURLString(baseUrl.Host ?? string.Empty, isPattern);
                }

                if (!hasPort)
                {
                    result.Port = baseUrl.Port?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                }

                if (!hasPathname)
                {
                    result.Pathname = ProcessBaseURLString(baseUrl.SerializePath(), isPattern);
                }

                if (!hasSearch)
                {
                    result.Search = ProcessBaseURLString(baseUrl.Query ?? string.Empty, isPattern);
                }

                if (!hasSearch && init.Hash == null)
                {
                    result.Hash = ProcessBaseURLString(baseUrl.Fragment ?? string.Empty, isPattern);
                }
            }

            if (init.Protocol != null)
            {
                string stripped = init.Protocol.EndsWith(':') ? init.Protocol[..^1] : init.Protocol;
                result.Protocol = isPattern ? stripped : URLPatternCanonicalizer.Protocol(stripped);
            }

            if (init.Username != null)
            {
                result.Username = isPattern ? init.Username : URLPatternCanonicalizer.Username(init.Username);
            }

            if (init.Password != null)
            {
                result.Password = isPattern ? init.Password : URLPatternCanonicalizer.Password(init.Password);
            }

            if (init.Hostname != null)
            {
                result.Hostname = isPattern ? init.Hostname : URLPatternCanonicalizer.Hostname(init.Hostname);
            }

            string resultProtocol = result.Protocol ?? string.Empty;
            if (init.Port != null)
            {
                result.Port = isPattern ? init.Port : URLPatternCanonicalizer.Port(init.Port, resultProtocol);
            }

            if (init.Pathname != null)
            {
                result.Pathname = init.Pathname;
                if (baseUrl != null && !baseUrl.HasOpaquePath && !IsAbsolutePathname(result.Pathname, isPattern))
                {
                    string baseUrlPath = ProcessBaseURLString(baseUrl.SerializePath(), isPattern);
                    int slashIndex = baseUrlPath.LastIndexOf('/');
                    if (slashIndex >= 0)
                    {
                        result.Pathname = baseUrlPath[..(slashIndex + 1)] + result.Pathname;
                    }
                }

                result.Pathname = ProcessPathname(result.Pathname, resultProtocol, isPattern);
            }

            if (init.Search != null)
            {
                string stripped = init.Search.StartsWith('?') ? init.Search[1..] : init.Search;
                result.Search = isPattern ? stripped : URLPatternCanonicalizer.Search(stripped);
            }

            if (init.Hash != null)
            {
                string stripped = init.Hash.StartsWith('#') ? init.Hash[1..] : init.Hash;
                result.Hash = isPattern ? stripped : URLPatternCanonicalizer.Hash(stripped);
            }

            return result;
        }

        private static string ProcessBaseURLString(string input, bool isPattern)
            => isPattern ? PatternParser.EscapePatternString(input) : input;

        private static bool IsAbsolutePathname(string input, bool isPattern)
        {
            if (input.Length == 0)
            {
                return false;
            }

            if (input[0] == '/')
            {
                return true;
            }

            if (!isPattern || input.Length < 2)
            {
                return false;
            }

            return (input[0] == '\\' || input[0] == '{') && input[1] == '/';
        }

        private static string ProcessPathname(string pathname, string protocol, bool isPattern)
        {
            if (isPattern)
            {
                return pathname;
            }

            return protocol.Length == 0 || UrlRecord.IsSpecialScheme(protocol)
                ? URLPatternCanonicalizer.Pathname(pathname)
                : URLPatternCanonicalizer.OpaquePathname(pathname);
        }
    }
}
