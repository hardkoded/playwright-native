/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Matches a page URL against a glob string, a <see cref="Regex"/>, or a predicate.
    /// Mirrors Playwright's <c>urlMatches</c> / <c>globToRegexPattern</c>.
    /// </summary>
    internal static class UrlMatcher
    {
        // https://developer.mozilla.org/en-docs/Web/JavaScript/Guide/Regular_expressions#escaping
        private static readonly HashSet<char> _escapedGlobChars = new()
        {
            '$', '^', '+', '.', '*', '(', ')', '|', '\\', '?', '{', '}', '[', ']',
        };

        private static readonly HashSet<string> _specialSchemes = new(StringComparer.OrdinalIgnoreCase)
        {
            "http",
            "https",
            "ws",
            "wss",
            "ftp",
            "file",
        };

        private static readonly char[] _globMetaChars = new[] { '*', '?', '{', '}', '\\' };

        /// <summary>
        /// Returns whether <paramref name="url"/> matches the first non-null matcher.
        /// Predicate wins, then regex, then glob string.
        /// </summary>
        /// <param name="url">The current page URL.</param>
        /// <param name="urlString">A glob pattern, or an exact URL.</param>
        /// <param name="urlRegex">A regular expression.</param>
        /// <param name="urlFunc">A predicate receiving the URL.</param>
        /// <param name="baseUrl">Optional context <c>baseURL</c> for relative globs.</param>
        /// <returns><see langword="true"/> when the URL matches.</returns>
        internal static bool Matches(string url, string urlString, Regex urlRegex, Func<string, bool> urlFunc, string baseUrl = null)
        {
            string value = url ?? string.Empty;

            if (urlFunc != null)
            {
                return urlFunc(value);
            }

            if (urlRegex != null)
            {
                return urlRegex.IsMatch(value);
            }

            if (!string.IsNullOrEmpty(urlString))
            {
                return UrlMatches(baseUrl, value, urlString);
            }

            return false;
        }

        /// <summary>
        /// Official Playwright glob match, including URL normalization through
        /// <see cref="Uri"/> (default ports, percent-encoding, IDN).
        /// </summary>
        /// <param name="url">The URL to test.</param>
        /// <param name="pattern">The glob pattern.</param>
        /// <returns><see langword="true"/> if the URL matches.</returns>
        internal static bool MatchesGlob(string url, string pattern)
        {
            if (string.IsNullOrEmpty(pattern) || pattern == "**/*" || pattern == "**")
            {
                return true;
            }

            return UrlMatches(null, url, pattern);
        }

        /// <summary>
        /// Official <c>urlMatches(baseURL, url, glob)</c>.
        /// </summary>
        /// <param name="baseUrl">Optional base URL used to resolve relative globs.</param>
        /// <param name="url">The request or page URL.</param>
        /// <param name="glob">The glob pattern.</param>
        /// <returns><see langword="true"/> when the URL matches.</returns>
        internal static bool UrlMatches(string baseUrl, string url, string glob)
            => UrlMatches(baseUrl, url, glob, webSocketUrl: false);

        /// <summary>
        /// Official <c>urlMatches(baseURL, url, glob, webSocketUrl)</c>.
        /// </summary>
        /// <param name="baseUrl">Optional base URL used to resolve relative globs.</param>
        /// <param name="url">The request or page URL.</param>
        /// <param name="glob">The glob pattern.</param>
        /// <param name="webSocketUrl">
        /// When <see langword="true"/>, an HTTP(S) <paramref name="baseUrl"/> is
        /// converted to <c>ws</c>/<c>wss</c> like official WebSocket routing.
        /// </param>
        /// <returns><see langword="true"/> when the URL matches.</returns>
        internal static bool UrlMatches(string baseUrl, string url, string glob, bool webSocketUrl)
        {
            if (string.IsNullOrEmpty(glob))
            {
                return true;
            }

            string resolvedBase = webSocketUrl ? ToWebSocketBaseUrl(baseUrl) : baseUrl;
            string pattern = ResolveGlobToRegexPattern(resolvedBase, glob);
            return pattern != null && new Regex(pattern).IsMatch(url ?? string.Empty);
        }

        /// <summary>
        /// Official <c>globToRegexPattern</c>. Throws on unbalanced or nested braces.
        /// </summary>
        /// <param name="glob">The glob pattern.</param>
        /// <returns>A regex source string, or <see langword="null"/> when <paramref name="glob"/> is empty.</returns>
        internal static string GlobToRegexPattern(string glob)
        {
            if (string.IsNullOrEmpty(glob))
            {
                return null;
            }

            List<string> tokens = new() { "^" };
            bool inGroup = false;

            for (int i = 0; i < glob.Length; ++i)
            {
                char c = glob[i];
                if (c == '\\' && i + 1 < glob.Length)
                {
                    char escaped = glob[++i];
                    tokens.Add(_escapedGlobChars.Contains(escaped) ? "\\" + escaped : escaped.ToString());
                    continue;
                }

                if (c == '*')
                {
                    char charBefore = i > 0 ? glob[i - 1] : '\0';
                    int starCount = 1;
                    while (i < glob.Length - 1 && glob[i + 1] == '*')
                    {
                        starCount++;
                        i++;
                    }

                    if (starCount > 1)
                    {
                        char charAfter = i + 1 < glob.Length ? glob[i + 1] : '\0';
                        if (charAfter == '/')
                        {
                            tokens.Add(charBefore == '/' ? "((.+/)|)" : "(.*/)");
                            i++;
                        }
                        else
                        {
                            tokens.Add("(.*)");
                        }
                    }
                    else
                    {
                        tokens.Add("([^/]*)");
                    }

                    continue;
                }

                switch (c)
                {
                    case '{':
                        if (inGroup)
                        {
                            throw new PlaywrightSharpException(
                                "Invalid glob pattern " + JsonSerializer.Serialize(glob) + ": nested '{' is not supported");
                        }

                        inGroup = true;
                        tokens.Add("(");
                        break;
                    case '}':
                        if (!inGroup)
                        {
                            throw new PlaywrightSharpException(
                                "Invalid glob pattern " + JsonSerializer.Serialize(glob) + ": unmatched '}'");
                        }

                        inGroup = false;
                        tokens.Add(")");
                        break;
                    case ',':
                        tokens.Add(inGroup ? "|" : "\\,");
                        break;
                    default:
                        tokens.Add(_escapedGlobChars.Contains(c) ? "\\" + c : c.ToString());
                        break;
                }
            }

            if (inGroup)
            {
                throw new PlaywrightSharpException(
                    "Invalid glob pattern " + JsonSerializer.Serialize(glob) + ": unmatched '{'");
            }

            tokens.Add("$");
            return string.Concat(tokens);
        }

        /// <summary>
        /// Validates a route glob at registration time so unbalanced braces throw
        /// instead of aborting a later request.
        /// </summary>
        /// <param name="glob">The glob passed to <c>RouteAsync</c>.</param>
        internal static void ValidateGlob(string glob)
        {
            if (string.IsNullOrEmpty(glob))
            {
                return;
            }

            _ = GlobToRegexPattern(glob);
        }

        /// <summary>
        /// Official unroute compares regexes by pattern and options, not instance.
        /// </summary>
        /// <param name="left">The registered regex.</param>
        /// <param name="right">The unroute regex.</param>
        /// <returns><see langword="true"/> when both describe the same matcher.</returns>
        internal static bool SameRegex(Regex left, Regex right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(left.ToString(), right.ToString(), StringComparison.Ordinal)
                && left.Options == right.Options;
        }

        /// <summary>
        /// Official unroute compares handlers by method and target so a method-group
        /// conversion to <see cref="Action{T}"/> still matches the registration.
        /// </summary>
        /// <param name="left">The registered handler identity.</param>
        /// <param name="right">The unroute handler identity.</param>
        /// <returns><see langword="true"/> when both refer to the same method.</returns>
        internal static bool SameHandler(object left, object right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            if (ReferenceEquals(left, right))
            {
                return true;
            }

            return left is Delegate leftDelegate
                && right is Delegate rightDelegate
                && leftDelegate.Equals(rightDelegate);
        }

        internal static string ResolveGlobToRegexPattern(string baseUrl, string glob)
        {
            return GlobToRegexPattern(ResolveGlobBase(baseUrl, glob));
        }

        private static string ResolveGlobBase(string baseUrl, string match)
        {
            if (match.StartsWith('*'))
            {
                return match;
            }

            Dictionary<string, string> tokenMap = new(StringComparer.Ordinal);

            string MapToken(string original, string replacement)
            {
                if (string.IsNullOrEmpty(original))
                {
                    return string.Empty;
                }

                tokenMap[replacement] = original;
                return replacement;
            }

            // Escaped `\\?` behaves the same as `?` in official glob patterns.
            match = match.Replace("\\\\?", "?", StringComparison.Ordinal);

            if (match.StartsWith("about:", StringComparison.Ordinal)
                || match.StartsWith("data:", StringComparison.Ordinal)
                || match.StartsWith("chrome:", StringComparison.Ordinal)
                || match.StartsWith("edge:", StringComparison.Ordinal)
                || match.StartsWith("file:", StringComparison.Ordinal))
            {
                return match;
            }

            string[] parts = match.Split('/');
            for (int index = 0; index < parts.Length; index++)
            {
                string token = parts[index];
                if (token == "." || token == ".." || token.Length == 0)
                {
                    continue;
                }

                if (index == 0 && token.EndsWith(':'))
                {
                    if (token.Contains('*') || token.Contains('{'))
                    {
                        parts[index] = MapToken(token, "http:");
                    }

                    continue;
                }

                if (token.IndexOfAny(_globMetaChars) < 0)
                {
                    continue;
                }

                int questionIndex = token.IndexOf('?');
                if (questionIndex == -1)
                {
                    parts[index] = MapToken(token, "playwright-pw-" + index.ToString() + "-pw-playwright");
                    continue;
                }

                string newPrefix = MapToken(
                    token.Substring(0, questionIndex),
                    "playwright-pw-" + index.ToString() + "-pw-playwright");
                string newSuffix = MapToken(
                    token.Substring(questionIndex),
                    "?playwright-pw2-" + index.ToString() + "-pw2-playwright");
                parts[index] = newPrefix + newSuffix;
            }

            string relativePath = string.Join("/", parts);
            (string resolved, string caseInsensitivePart) = ResolveBaseURL(baseUrl, relativePath);
            foreach (KeyValuePair<string, string> entry in tokenMap)
            {
                bool normalize = caseInsensitivePart != null
                    && caseInsensitivePart.Contains(entry.Key, StringComparison.Ordinal);
#pragma warning disable CA1308 // Official urlMatches lowercases scheme and host.
                string replacement = normalize ? entry.Value.ToLowerInvariant() : entry.Value;
#pragma warning restore CA1308
                resolved = resolved.Replace(entry.Key, replacement, StringComparison.Ordinal);
            }

            return resolved;
        }

        private static (string Resolved, string CaseInsensitivePart) ResolveBaseURL(string baseUrl, string url)
        {
            try
            {
                Uri uri;
                string resolved;
                if (string.IsNullOrEmpty(baseUrl))
                {
                    uri = WithIdnHost(FixupTrailingSlash(new Uri(url, UriKind.Absolute)));
                    resolved = IsSpecialScheme(uri.Scheme) ? AbsoluteHref(uri) : url;
                }
                else
                {
                    Uri baseUri = FixupTrailingSlash(new Uri(baseUrl, UriKind.RelativeOrAbsolute));
                    uri = WithIdnHost(FixupTrailingSlash(new Uri(baseUri, url)));
                    resolved = IsSpecialScheme(uri.Scheme) ? AbsoluteHref(uri) : url;
                }

                string caseInsensitivePrefix = uri.GetLeftPart(UriPartial.Authority);
                return (resolved, caseInsensitivePrefix);
            }
            catch (UriFormatException)
            {
                return (url, null);
            }
            catch (ArgumentException)
            {
                return (url, null);
            }
        }

        private static string ToWebSocketBaseUrl(string baseUrl)
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                return baseUrl;
            }

            try
            {
                Uri uri = new Uri(baseUrl, UriKind.Absolute);
                string scheme = uri.Scheme;
                if (string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase))
                {
                    scheme = "ws";
                }
                else if (string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase))
                {
                    scheme = "wss";
                }
                else
                {
                    return baseUrl;
                }

                UriBuilder builder = new UriBuilder(uri)
                {
                    Scheme = scheme,
                };
                return builder.Uri.AbsoluteUri;
            }
            catch (UriFormatException)
            {
                return baseUrl;
            }
            catch (ArgumentException)
            {
                return baseUrl;
            }
        }

        private static Uri FixupTrailingSlash(Uri uri)
        {
            if (uri == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(uri.AbsolutePath))
            {
                return uri;
            }

            UriBuilder builder = new(uri)
            {
                Path = "/",
            };
            return builder.Uri;
        }

        private static Uri WithIdnHost(Uri uri)
        {
            if (uri == null || string.IsNullOrEmpty(uri.Host)
                || string.Equals(uri.Host, uri.IdnHost, StringComparison.Ordinal))
            {
                return uri;
            }

            UriBuilder builder = new(uri)
            {
                Host = uri.IdnHost,
            };
            return builder.Uri;
        }

        private static bool IsSpecialScheme(string scheme)
            => !string.IsNullOrEmpty(scheme) && _specialSchemes.Contains(scheme);

        private static string AbsoluteHref(Uri uri)
        {
            // AbsoluteUri keeps percent-encoding (JS URL.toString()), unlike Uri.ToString().
            string href = uri.AbsoluteUri;
            if (href.EndsWith('/'))
            {
                return href;
            }

            if (string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
                && uri.AbsolutePath == "/")
            {
                return href + "/";
            }

            return href;
        }
    }
}
