/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>route.continue</c> header and URL rules from Playwright
    /// <c>network.ts</c> (<c>applyHeadersOverrides</c>, forbidden headers,
    /// same-protocol URL overrides).
    /// </summary>
    internal static class RouteContinue
    {
        private static readonly HashSet<string> ForbiddenHeaderNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "accept-charset",
            "accept-encoding",
            "access-control-request-headers",
            "access-control-request-method",
            "connection",
            "content-length",
            "cookie",
            "date",
            "dnt",
            "expect",
            "host",
            "keep-alive",
            "origin",
            "referer",
            "set-cookie",
            "te",
            "trailer",
            "transfer-encoding",
            "upgrade",
            "via",
        };

        private static readonly HashSet<string> ForbiddenMethods = new(StringComparer.OrdinalIgnoreCase)
        {
            "CONNECT",
            "TRACE",
            "TRACK",
        };

        /// <summary>
        /// Throws when <paramref name="overrideUrl"/> changes the scheme of
        /// <paramref name="originalUrl"/>.
        /// </summary>
        /// <param name="originalUrl">The intercepted request URL.</param>
        /// <param name="overrideUrl">The continue URL override, or <see langword="null"/>.</param>
        internal static void EnsureSameProtocol(string originalUrl, string overrideUrl)
        {
            if (string.IsNullOrEmpty(overrideUrl))
            {
                return;
            }

            if (!Uri.TryCreate(originalUrl, UriKind.Absolute, out Uri oldUri)
                || !Uri.TryCreate(overrideUrl, UriKind.Absolute, out Uri newUri)
                || !string.Equals(oldUri.Scheme, newUri.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                throw new PlaywrightNativeException("New URL must have same protocol as overridden URL");
            }
        }

        /// <summary>
        /// Merges continue header overrides with the original request: forbidden
        /// names stay on the original values; other names come from the override
        /// (omitting a name deletes it).
        /// </summary>
        /// <param name="original">Headers captured at interception.</param>
        /// <param name="overrides">Headers passed to continue.</param>
        /// <returns>The merged map.</returns>
        internal static Dictionary<string, string> ApplyHeadersOverrides(
            IEnumerable<KeyValuePair<string, string>> original,
            IEnumerable<KeyValuePair<string, string>> overrides)
        {
            Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
            if (overrides != null)
            {
                foreach (KeyValuePair<string, string> header in overrides)
                {
                    if (string.IsNullOrEmpty(header.Key)
                        || header.Value == null
                        || IsForbiddenHeader(header.Key, header.Value))
                    {
                        continue;
                    }

                    result[header.Key] = header.Value;
                }
            }

            if (original != null)
            {
                foreach (KeyValuePair<string, string> header in original)
                {
                    if (string.IsNullOrEmpty(header.Key) || !IsForbiddenHeader(header.Key, header.Value))
                    {
                        continue;
                    }

                    result[header.Key] = header.Value ?? string.Empty;
                }
            }

            return result;
        }

        /// <summary>
        /// Returns a copy without <c>Cookie</c> so the browser cookie jar wins.
        /// </summary>
        /// <param name="headers">Merged continue headers.</param>
        /// <returns>A cookie-stripped copy.</returns>
        internal static Dictionary<string, string> RemoveCookie(IEnumerable<KeyValuePair<string, string>> headers)
        {
            Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
            if (headers == null)
            {
                return result;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.IsNullOrEmpty(header.Key)
                    || string.Equals(header.Key, "cookie", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result[header.Key] = header.Value ?? string.Empty;
            }

            return result;
        }

        /// <summary>
        /// Official forbidden-request-header check (MDN + <c>proxy-</c> /
        /// <c>sec-</c> prefixes).
        /// </summary>
        /// <param name="name">Header name.</param>
        /// <param name="value">Header value, used for method-override names.</param>
        /// <returns><see langword="true"/> when the header cannot be overridden.</returns>
        internal static bool IsForbiddenHeader(string name, string value)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (ForbiddenHeaderNames.Contains(name)
                || name.StartsWith("proxy-", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("sec-", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(name, "x-http-method", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "x-http-method-override", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "x-method-override", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrEmpty(value) && ForbiddenMethods.Contains(value);
            }

            return false;
        }
    }
}
