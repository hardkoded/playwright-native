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
    /// A URL pattern, as defined by the WHATWG URL Pattern standard (https://urlpattern.spec.whatwg.org/).
    /// It matches URLs component by component, for example <c>new URLPattern("https://*.example.com/books/:id")</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This type gives parity with the JavaScript <c>URLPattern</c> that Playwright for Node.js accepts.
    /// It is a PlaywrightNative extension: playwright-dotnet has no such type. If a future
    /// <c>Microsoft.Playwright</c> release adds its own <c>URLPattern</c>, code that imports both namespaces
    /// can pick one with a using alias, for example <c>using URLPattern = PlaywrightNative.URLPattern;</c>.
    /// </para>
    /// <para>
    /// Custom regular expression groups use JavaScript syntax with the "v" flag. They run on .NET
    /// <see cref="System.Text.RegularExpressions.Regex"/> after translation. Class string disjunctions (<c>\q{...}</c>),
    /// and Unicode properties other than general categories, are not supported and throw.
    /// </para>
    /// </remarks>
    public sealed class URLPattern
    {
        private static readonly string[] _specialSchemes = ["ftp", "file", "http", "https", "ws", "wss"];

        private readonly URLPatternComponent _protocol;
        private readonly URLPatternComponent _username;
        private readonly URLPatternComponent _password;
        private readonly URLPatternComponent _hostname;
        private readonly URLPatternComponent _port;
        private readonly URLPatternComponent _pathname;
        private readonly URLPatternComponent _search;
        private readonly URLPatternComponent _hash;

        /// <summary>
        /// Initializes a new instance of the <see cref="URLPattern"/> class from a pattern string.
        /// The string must be absolute (start with a protocol).
        /// </summary>
        /// <param name="input">The pattern string, for example <c>https://example.com/books/:id</c>.</param>
        /// <param name="options">Matching options.</param>
        /// <exception cref="ArgumentException">The pattern is not valid.</exception>
        public URLPattern(string input, URLPatternOptions options = null)
            : this(input, null, options)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="URLPattern"/> class from a pattern string resolved against a base URL.
        /// </summary>
        /// <param name="input">The pattern string. It can be relative when <paramref name="baseURL"/> is given.</param>
        /// <param name="baseURL">The base URL, or null.</param>
        /// <param name="options">Matching options.</param>
        /// <exception cref="ArgumentException">The pattern or the base URL is not valid.</exception>
        public URLPattern(string input, string baseURL, URLPatternOptions options = null)
        {
            ArgumentNullException.ThrowIfNull(input);
            URLPatternFields init = ConstructorStringParser.Parse(UrlCodePoints.ToUsvString(input));
            if (baseURL == null && init.Protocol == null)
            {
                throw new ArgumentException($"Relative URL pattern '{input}' needs a base URL.", nameof(input));
            }

            if (baseURL != null)
            {
                init.BaseURL = UrlCodePoints.ToUsvString(baseURL);
            }

            (_protocol, _username, _password, _hostname, _port, _pathname, _search, _hash) = Create(init, options);
            Predicate = url => Test(url);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="URLPattern"/> class from separate components.
        /// Components that are not given match anything, unless <see cref="URLPatternInit.BaseURL"/> supplies them.
        /// </summary>
        /// <param name="init">The component patterns.</param>
        /// <param name="options">Matching options.</param>
        /// <exception cref="ArgumentException">The pattern is not valid.</exception>
        public URLPattern(URLPatternInit init, URLPatternOptions options = null)
        {
            ArgumentNullException.ThrowIfNull(init);
            (_protocol, _username, _password, _hostname, _port, _pathname, _search, _hash) = Create(URLPatternFields.From(init), options);
            Predicate = url => Test(url);
        }

        /// <summary>
        /// Gets the normalized protocol pattern.
        /// </summary>
        public string Protocol => _protocol.PatternString;

        /// <summary>
        /// Gets the normalized username pattern.
        /// </summary>
        public string Username => _username.PatternString;

        /// <summary>
        /// Gets the normalized password pattern.
        /// </summary>
        public string Password => _password.PatternString;

        /// <summary>
        /// Gets the normalized hostname pattern.
        /// </summary>
        public string Hostname => _hostname.PatternString;

        /// <summary>
        /// Gets the normalized port pattern.
        /// </summary>
        public string Port => _port.PatternString;

        /// <summary>
        /// Gets the normalized pathname pattern.
        /// </summary>
        public string Pathname => _pathname.PatternString;

        /// <summary>
        /// Gets the normalized search pattern.
        /// </summary>
        public string Search => _search.PatternString;

        /// <summary>
        /// Gets the normalized hash pattern.
        /// </summary>
        public string Hash => _hash.PatternString;

        /// <summary>
        /// Gets a value indicating whether any component uses a custom regular expression group.
        /// </summary>
        public bool HasRegExpGroups => _protocol.HasRegExpGroups || _username.HasRegExpGroups || _password.HasRegExpGroups
            || _hostname.HasRegExpGroups || _port.HasRegExpGroups || _pathname.HasRegExpGroups
            || _search.HasRegExpGroups || _hash.HasRegExpGroups;

        /// <summary>
        /// Gets a URL predicate for this pattern. It is created once, so callers can compare it by reference.
        /// A URL that cannot be parsed does not match.
        /// </summary>
        internal Func<string, bool> Predicate { get; }

        /// <summary>
        /// Tests whether a URL matches this pattern.
        /// </summary>
        /// <param name="input">The URL. It can be relative when <paramref name="baseURL"/> is given.</param>
        /// <param name="baseURL">The base URL, or null.</param>
        /// <returns>Whether the URL matches. A URL that cannot be parsed does not match.</returns>
        public bool Test(string input, string baseURL = null) => Exec(input, baseURL) != null;

        /// <summary>
        /// Tests whether separate URL components match this pattern. Components that are not given are empty,
        /// unless <see cref="URLPatternInit.BaseURL"/> supplies them.
        /// </summary>
        /// <param name="input">The URL components.</param>
        /// <returns>Whether the components match. Components that cannot be canonicalized do not match.</returns>
        public bool Test(URLPatternInit input) => Exec(input) != null;

        /// <summary>
        /// Matches a URL against this pattern and returns the matched groups.
        /// </summary>
        /// <param name="input">The URL. It can be relative when <paramref name="baseURL"/> is given.</param>
        /// <param name="baseURL">The base URL, or null.</param>
        /// <returns>The match, or null when the URL does not match or cannot be parsed.</returns>
        public URLPatternResult Exec(string input, string baseURL = null)
        {
            ArgumentNullException.ThrowIfNull(input);
            UrlRecord baseUrl = null;
            if (baseURL != null)
            {
                baseUrl = UrlParser.Parse(UrlCodePoints.ToUsvString(baseURL));
                if (baseUrl == null)
                {
                    return null;
                }
            }

            UrlRecord url = UrlParser.Parse(UrlCodePoints.ToUsvString(input), baseUrl);
            if (url == null)
            {
                return null;
            }

            return Match(
                url.Scheme,
                url.Username,
                url.Password,
                url.Host ?? string.Empty,
                url.Port?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                url.SerializePath(),
                url.Query ?? string.Empty,
                url.Fragment ?? string.Empty);
        }

        /// <summary>
        /// Matches separate URL components against this pattern and returns the matched groups.
        /// </summary>
        /// <param name="input">The URL components.</param>
        /// <returns>The match, or null when the components do not match or cannot be canonicalized.</returns>
        public URLPatternResult Exec(URLPatternInit input)
        {
            ArgumentNullException.ThrowIfNull(input);
            URLPatternFields values;
            try
            {
                var empty = new URLPatternFields
                {
                    Protocol = string.Empty,
                    Username = string.Empty,
                    Password = string.Empty,
                    Hostname = string.Empty,
                    Port = string.Empty,
                    Pathname = string.Empty,
                    Search = string.Empty,
                    Hash = string.Empty,
                };
                values = URLPatternInitProcessor.Process(URLPatternFields.From(input), isPattern: false, empty);
            }
            catch (ArgumentException)
            {
                return null;
            }

            return Match(values.Protocol, values.Username, values.Password, values.Hostname, values.Port, values.Pathname, values.Search, values.Hash);
        }

        /// <summary>
        /// Returns whether a compiled protocol component matches any special scheme.
        /// </summary>
        internal static bool ProtocolComponentMatchesSpecialScheme(URLPatternComponent protocol)
            => Array.Exists(_specialSchemes, scheme => protocol.RegularExpression.IsMatch(scheme));

        private static (URLPatternComponent Protocol, URLPatternComponent Username, URLPatternComponent Password, URLPatternComponent Hostname,
            URLPatternComponent Port, URLPatternComponent Pathname, URLPatternComponent Search, URLPatternComponent Hash) Create(URLPatternFields init, URLPatternOptions options)
        {
            URLPatternFields processed = URLPatternInitProcessor.Process(init, isPattern: true, new URLPatternFields());
            processed.Protocol ??= "*";
            processed.Username ??= "*";
            processed.Password ??= "*";
            processed.Hostname ??= "*";
            processed.Port ??= "*";
            processed.Pathname ??= "*";
            processed.Search ??= "*";
            processed.Hash ??= "*";
            if (UrlRecord.IsSpecialScheme(processed.Protocol)
                && processed.Port == UrlRecord.DefaultPort(processed.Protocol)?.ToString(CultureInfo.InvariantCulture))
            {
                processed.Port = string.Empty;
            }

            bool ignoreCase = options?.IgnoreCase ?? false;
            URLPatternComponent protocol = URLPatternComponent.Compile(processed.Protocol, URLPatternCanonicalizer.Protocol, PatternOptions.Default);
            URLPatternComponent username = URLPatternComponent.Compile(processed.Username, URLPatternCanonicalizer.Username, PatternOptions.Default);
            URLPatternComponent password = URLPatternComponent.Compile(processed.Password, URLPatternCanonicalizer.Password, PatternOptions.Default);
            URLPatternComponent hostname = URLPatternComponent.Compile(
                processed.Hostname,
                IsIPv6HostnamePattern(processed.Hostname) ? URLPatternCanonicalizer.IPv6Hostname : URLPatternCanonicalizer.Hostname,
                PatternOptions.Hostname);
            URLPatternComponent port = URLPatternComponent.Compile(processed.Port, URLPatternCanonicalizer.Port, PatternOptions.Default);
            PatternOptions compileOptions = PatternOptions.Default with { IgnoreCase = ignoreCase };
            URLPatternComponent pathname = ProtocolComponentMatchesSpecialScheme(protocol)
                ? URLPatternComponent.Compile(processed.Pathname, URLPatternCanonicalizer.Pathname, PatternOptions.Pathname with { IgnoreCase = ignoreCase })
                : URLPatternComponent.Compile(processed.Pathname, URLPatternCanonicalizer.OpaquePathname, compileOptions);
            URLPatternComponent search = URLPatternComponent.Compile(processed.Search, URLPatternCanonicalizer.Search, compileOptions);
            URLPatternComponent hash = URLPatternComponent.Compile(processed.Hash, URLPatternCanonicalizer.Hash, compileOptions);
            return (protocol, username, password, hostname, port, pathname, search, hash);
        }

        private static bool IsIPv6HostnamePattern(string input)
            => input.Length >= 2 && (input[0] == '[' || ((input[0] is '{' or '\\') && input[1] == '['));

        private URLPatternResult Match(string protocol, string username, string password, string hostname, string port, string pathname, string search, string hash)
        {
            URLPatternComponentResult protocolResult = _protocol.Exec(protocol);
            URLPatternComponentResult usernameResult = protocolResult == null ? null : _username.Exec(username);
            URLPatternComponentResult passwordResult = usernameResult == null ? null : _password.Exec(password);
            URLPatternComponentResult hostnameResult = passwordResult == null ? null : _hostname.Exec(hostname);
            URLPatternComponentResult portResult = hostnameResult == null ? null : _port.Exec(port);
            URLPatternComponentResult pathnameResult = portResult == null ? null : _pathname.Exec(pathname);
            URLPatternComponentResult searchResult = pathnameResult == null ? null : _search.Exec(search);
            URLPatternComponentResult hashResult = searchResult == null ? null : _hash.Exec(hash);
            return hashResult == null
                ? null
                : new URLPatternResult(protocolResult, usernameResult, passwordResult, hostnameResult, portResult, pathnameResult, searchResult, hashResult);
        }
    }
}
