/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Maps Playwright cookie models to Chromium Storage / WebKit Playwright protocol payloads.
    /// </summary>
    internal static class ContextCookies
    {
        /// <summary>
        /// Official <c>kMaxCookieExpiresDateInSeconds</c> (Fri, 31 Dec 9999 23:59:59 UTC).
        /// </summary>
        internal const double MaxCookieExpiresDateInSeconds = 253402300799d;

        /// <summary>
        /// Converts public <see cref="Cookie"/> values into protocol cookie objects.
        /// Matches official <c>rewriteCookies</c> plus Chromium / WebKit protocol shaping.
        /// </summary>
        /// <param name="cookies">Cookies to add.</param>
        /// <param name="webKit">
        /// When <see langword="true"/>, emit WebKit <c>session</c> and convert expires to milliseconds.
        /// </param>
        /// <returns>Protocol cookie dictionaries.</returns>
        internal static object[] ToProtocol(IEnumerable<Cookie> cookies, bool webKit = false)
        {
            if (cookies == null)
            {
                throw new ArgumentNullException(nameof(cookies));
            }

            List<object> payload = new();
            foreach (Cookie cookie in cookies)
            {
                if (cookie == null)
                {
                    continue;
                }

                Cookie rewritten = Rewrite(cookie);
                Dictionary<string, object> item = new()
                {
                    ["name"] = rewritten.Name ?? string.Empty,
                    ["value"] = rewritten.Value ?? string.Empty,
                };

                // Official toChromiumCookie keeps url after rewriteCookies.
                if (!string.IsNullOrEmpty(rewritten.Url))
                {
                    item["url"] = rewritten.Url;
                }

                if (!string.IsNullOrEmpty(rewritten.Domain))
                {
                    item["domain"] = rewritten.Domain;
                }

                if (!string.IsNullOrEmpty(rewritten.Path))
                {
                    item["path"] = rewritten.Path;
                }

                bool session = !rewritten.Expires.HasValue || rewritten.Expires.Value == -1;
                if (rewritten.Expires.HasValue)
                {
                    double expires = rewritten.Expires.Value;
                    item["expires"] = webKit && expires != -1 ? expires * 1000d : expires;
                }

                if (rewritten.HttpOnly.HasValue)
                {
                    item["httpOnly"] = rewritten.HttpOnly.Value;
                }

                if (rewritten.Secure.HasValue)
                {
                    item["secure"] = rewritten.Secure.Value;
                }

                if (rewritten.SameSite == Microsoft.Playwright.SameSiteAttribute.Lax
                    || rewritten.SameSite == Microsoft.Playwright.SameSiteAttribute.Strict
                    || rewritten.SameSite == Microsoft.Playwright.SameSiteAttribute.None)
                {
                    item["sameSite"] = rewritten.SameSite.ToString();
                }
                else
                {
                    // Chromium Storage.setCookies drops cookies that omit sameSite.
                    // Official cookies() then reports sameSite ?? 'Lax' (None on
                    // Windows WebKit), which is defaultSameSiteCookieValue.
                    item["sameSite"] = webKit && OperatingSystem.IsWindows()
                        ? nameof(Microsoft.Playwright.SameSiteAttribute.None)
                        : nameof(Microsoft.Playwright.SameSiteAttribute.Lax);
                }

                if (webKit)
                {
                    item["session"] = session;
                }

                if (!string.IsNullOrEmpty(rewritten.PartitionKey))
                {
                    item["partitionKey"] = webKit
                        ? rewritten.PartitionKey
                        : (object)new
                        {
                            topLevelSite = rewritten.PartitionKey,

                            // Official toChromiumCookie: _crHasCrossSiteAncestor ?? true.
                            hasCrossSiteAncestor = CookieExtras.GetHasCrossSiteAncestor(rewritten) ?? true,
                        };
                }

                payload.Add(item);
            }

            return payload.ToArray();
        }

        /// <summary>
        /// Reads a protocol <c>{ cookies: [...] }</c> result.
        /// </summary>
        /// <param name="result">CDP / Playwright command result.</param>
        /// <param name="webKit">
        /// When <see langword="true"/>, convert WebKit millisecond expires to seconds.
        /// </param>
        /// <returns>Public cookie results.</returns>
        internal static IReadOnlyList<BrowserContextCookiesResult> FromProtocol(
            JsonElement? result,
            bool webKit = false)
        {
            List<BrowserContextCookiesResult> cookies = new();
            if (!result.HasValue
                || !result.Value.TryGetProperty("cookies", out JsonElement array)
                || array.ValueKind != JsonValueKind.Array)
            {
                return cookies;
            }

            foreach (JsonElement item in array.EnumerateArray())
            {
                BrowserContextCookiesResult cookie = new BrowserContextCookiesResult
                {
                    Name = ReadString(item, "name"),
                    Value = ReadString(item, "value"),
                    Domain = ReadString(item, "domain"),
                    Path = ReadString(item, "path"),
                    Expires = (float)ReadExpires(item, webKit),
                    HttpOnly = ReadBool(item, "httpOnly"),
                    Secure = ReadBool(item, "secure"),
                    SameSite = ReadSameSite(item, webKit),
                    PartitionKey = ReadPartitionKey(item),
                };
                BrowserContextCookiesResultExtras.SetHasCrossSiteAncestor(cookie, ReadHasCrossSiteAncestor(item));
                cookies.Add(cookie);
            }

            return cookies;
        }

        /// <summary>
        /// Keeps cookies that would be sent to any of <paramref name="urls"/>.
        /// When <paramref name="urls"/> is null or empty, returns <paramref name="cookies"/> unchanged.
        /// Matches official <c>filterCookies</c>.
        /// </summary>
        /// <param name="cookies">Cookies from the browser.</param>
        /// <param name="urls">Optional URL filter.</param>
        /// <returns>Matching cookies.</returns>
        internal static IReadOnlyList<BrowserContextCookiesResult> FilterByUrls(
            IReadOnlyList<BrowserContextCookiesResult> cookies,
            IEnumerable<string> urls)
        {
            if (cookies == null)
            {
                return Array.Empty<BrowserContextCookiesResult>();
            }

            if (urls == null)
            {
                return cookies;
            }

            List<Uri> parsed = new();
            foreach (string url in urls)
            {
                if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                {
                    parsed.Add(uri);
                }
            }

            if (parsed.Count == 0)
            {
                return cookies;
            }

            List<BrowserContextCookiesResult> filtered = new();
            foreach (BrowserContextCookiesResult cookie in cookies)
            {
                foreach (Uri uri in parsed)
                {
                    if (MatchesUrl(cookie, uri))
                    {
                        filtered.Add(cookie);
                        break;
                    }
                }
            }

            return filtered;
        }

        /// <summary>
        /// Returns whether <paramref name="cookie"/> would be sent to <paramref name="url"/>.
        /// </summary>
        /// <param name="cookie">A stored cookie.</param>
        /// <param name="url">An absolute URL, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> when the cookie matches the URL.</returns>
        internal static bool MatchesUrl(BrowserContextCookiesResult cookie, string url)
        {
            if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return false;
            }

            return MatchesUrl(cookie, uri);
        }

        /// <summary>
        /// Official <c>rewriteCookies</c>: validate and expand <see cref="Cookie.Url"/>.
        /// </summary>
        /// <param name="cookie">A public cookie to add.</param>
        /// <returns>A cookie with domain / path / secure filled from the URL.</returns>
        internal static Cookie Rewrite(Cookie cookie)
        {
            if (cookie == null)
            {
                throw new ArgumentNullException(nameof(cookie));
            }

            bool hasUrl = !string.IsNullOrEmpty(cookie.Url);
            bool hasDomain = !string.IsNullOrEmpty(cookie.Domain);
            bool hasPath = !string.IsNullOrEmpty(cookie.Path);
            if (!hasUrl && !(hasDomain && hasPath))
            {
                throw new PlaywrightSharpException("Cookie should have a url or a domain/path pair");
            }

            if (hasUrl && hasDomain)
            {
                throw new PlaywrightSharpException("Cookie should have either url or domain");
            }

            if (hasUrl && hasPath)
            {
                throw new PlaywrightSharpException("Cookie should have either url or path");
            }

            if (cookie.Expires.HasValue
                && cookie.Expires.Value < 0
                && cookie.Expires.Value != -1)
            {
                throw new PlaywrightSharpException(
                    "Cookie should have a valid expires, only -1 or a positive number for the unix timestamp in seconds is allowed");
            }

            if (cookie.Expires.HasValue
                && cookie.Expires.Value > 0
                && cookie.Expires.Value > MaxCookieExpiresDateInSeconds)
            {
                throw new PlaywrightSharpException(
                    "Cookie should have a valid expires, only -1 or a positive number for the unix timestamp in seconds is allowed");
            }

            if (!hasUrl)
            {
                return cookie;
            }

            if (string.Equals(cookie.Url, "about:blank", StringComparison.Ordinal))
            {
                throw new PlaywrightSharpException(
                    "Blank page can not have cookie \"" + (cookie.Name ?? string.Empty) + "\"");
            }

            if (cookie.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                throw new PlaywrightSharpException(
                    "Data URL page can not have cookie \"" + (cookie.Name ?? string.Empty) + "\"");
            }

            if (!Uri.TryCreate(cookie.Url, UriKind.Absolute, out Uri uri))
            {
                throw new PlaywrightSharpException("Cookie should have a url or a domain/path pair");
            }

            string pathname = uri.AbsolutePath;
            int slash = pathname.LastIndexOf('/');
            Cookie result = new Cookie
            {
                Name = cookie.Name,
                Value = cookie.Value,
                Url = cookie.Url,
                Domain = uri.Host,
                Path = slash >= 0 ? pathname.Substring(0, slash + 1) : "/",
                Expires = cookie.Expires,
                HttpOnly = cookie.HttpOnly,
                Secure = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase),
                SameSite = cookie.SameSite,
                PartitionKey = cookie.PartitionKey,
            };
            CookieExtras.SetHasCrossSiteAncestor(result, CookieExtras.GetHasCrossSiteAncestor(cookie));
            return result;
        }

        private static bool MatchesUrl(BrowserContextCookiesResult cookie, Uri uri)
        {
            if (cookie == null || uri == null)
            {
                return false;
            }

            string domain = cookie.Domain ?? string.Empty;
            if (!domain.StartsWith('.'))
            {
                domain = "." + domain;
            }

            if (!("." + uri.Host).EndsWith(domain, StringComparison.Ordinal))
            {
                return false;
            }

            string path = cookie.Path ?? string.Empty;
            if (!uri.AbsolutePath.StartsWith(path, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !IsLocalHostname(uri.Host)
                && cookie.Secure)
            {
                return false;
            }

            return true;
        }

        private static bool IsLocalHostname(string hostname)
            => string.Equals(hostname, "localhost", StringComparison.OrdinalIgnoreCase)
                || hostname.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);

        private static string ReadPartitionKey(JsonElement item)
        {
            if (!item.TryGetProperty("partitionKey", out JsonElement value))
            {
                return string.Empty;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? string.Empty;
            }

            if (value.ValueKind == JsonValueKind.Object
                && value.TryGetProperty("topLevelSite", out JsonElement site)
                && site.ValueKind == JsonValueKind.String)
            {
                return site.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        private static bool? ReadHasCrossSiteAncestor(JsonElement item)
        {
            if (!item.TryGetProperty("partitionKey", out JsonElement value)
                || value.ValueKind != JsonValueKind.Object
                || !value.TryGetProperty("hasCrossSiteAncestor", out JsonElement flag))
            {
                return null;
            }

            if (flag.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (flag.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            return null;
        }

        private static string ReadString(JsonElement item, string name)
            => item.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;

        private static bool ReadBool(JsonElement item, string name)
            => item.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;

        private static double ReadExpires(JsonElement item, bool webKit)
        {
            if (ReadBool(item, "session"))
            {
                return -1;
            }

            if (!item.TryGetProperty("expires", out JsonElement value)
                || value.ValueKind != JsonValueKind.Number)
            {
                return -1;
            }

            double expires = value.GetDouble();
            if (expires == -1)
            {
                return -1;
            }

            if (webKit)
            {
                expires /= 1000d;
            }

            return expires;
        }

        private static Microsoft.Playwright.SameSiteAttribute ReadSameSite(JsonElement item, bool webKit)
        {
            string raw = ReadString(item, "sameSite");
            if (string.Equals(raw, "Strict", StringComparison.OrdinalIgnoreCase))
            {
                return Microsoft.Playwright.SameSiteAttribute.Strict;
            }

            if (string.Equals(raw, "Lax", StringComparison.OrdinalIgnoreCase))
            {
                return Microsoft.Playwright.SameSiteAttribute.Lax;
            }

            if (string.Equals(raw, "None", StringComparison.OrdinalIgnoreCase))
            {
                return Microsoft.Playwright.SameSiteAttribute.None;
            }

            // Official Chromium: sameSite ?? 'Lax'. WebKit reports the engine value.
            return webKit ? default : Microsoft.Playwright.SameSiteAttribute.Lax;
        }
    }
}
