/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Formats <see cref="Proxy"/> for browser launch / createContext commands.
    /// </summary>
    internal static class ProxySettings
    {
        /// <summary>
        /// Returns <see langword="true"/> when the proxy has a username.
        /// </summary>
        /// <param name="proxy">Proxy options, or <see langword="null"/>.</param>
        /// <returns>Whether credentials should be offered.</returns>
        internal static bool HasCredentials(Proxy proxy)
            => proxy != null && !string.IsNullOrEmpty(proxy.Username);

        /// <summary>
        /// Builds a proxy server URL. Adds an <c>http://</c> scheme when missing.
        /// When <paramref name="includeCredentials"/> is <see langword="true"/> and
        /// the proxy has a username, embeds <c>user:pass@</c> after the scheme.
        /// </summary>
        /// <param name="proxy">Proxy options, or <see langword="null"/>.</param>
        /// <param name="includeCredentials">Whether to embed username/password in the URL.</param>
        /// <returns>The formatted server, or <see langword="null"/>.</returns>
        internal static string FormatServer(Proxy proxy, bool includeCredentials)
        {
            if (proxy == null || string.IsNullOrEmpty(proxy.Server))
            {
                return null;
            }

            string server = proxy.Server;
            if (server.IndexOf("://", StringComparison.Ordinal) < 0)
            {
                server = "http://" + server;
            }

            if (!includeCredentials || !HasCredentials(proxy))
            {
                return server;
            }

            int schemeEnd = server.IndexOf("://", StringComparison.Ordinal);
            string scheme = server.Substring(0, schemeEnd + 3);
            string rest = server.Substring(schemeEnd + 3);
            if (rest.Contains('@', StringComparison.Ordinal))
            {
                return server;
            }

            string user = Uri.EscapeDataString(proxy.Username);
            string password = Uri.EscapeDataString(proxy.Password ?? string.Empty);
            return scheme + user + ":" + password + "@" + rest;
        }

        /// <summary>
        /// Isolated Chromium contexts do not inherit launch
        /// <c>--proxy-bypass-list= &lt;-loopback&gt;</c>. Prefix the token so
        /// localhost / link-local still go through a context proxy, matching
        /// official Playwright.
        /// </summary>
        /// <param name="proxy">Proxy options, or <see langword="null"/>.</param>
        /// <returns>The bypass list, or <see langword="null"/>.</returns>
        internal static string FormatBypassList(Proxy proxy)
        {
            if (proxy == null || string.IsNullOrEmpty(proxy.Server))
            {
                return null;
            }

            string bypass = NormalizeBypass(proxy.Bypass);
            if (string.IsNullOrEmpty(bypass))
            {
                return "<-loopback>";
            }

            if (bypass.Contains("<-loopback>", StringComparison.OrdinalIgnoreCase))
            {
                return bypass;
            }

            return "<-loopback>," + bypass;
        }

        /// <summary>
        /// Official <c>shouldBypassProxy</c>: comma-separated tokens, optional
        /// leading <c>*</c>, and a leading <c>.</c> matches a host suffix.
        /// </summary>
        /// <param name="host">Official <c>URL.host</c> (port only when non-default).</param>
        /// <param name="bypass">Raw bypass list, or <see langword="null"/>.</param>
        /// <returns>Whether the host should skip the proxy.</returns>
        internal static bool ShouldBypass(string host, string bypass)
        {
            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(bypass))
            {
                return false;
            }

            string[] parts = bypass.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string raw in parts)
            {
                string token = raw.StartsWith('*') ? raw.Substring(1) : raw;
                if (token.Length == 0)
                {
                    continue;
                }

                if (token[0] == '.'
                    && (host.EndsWith(token, StringComparison.Ordinal)
                        || string.Equals(host, token.Substring(1), StringComparison.Ordinal)))
                {
                    return true;
                }

                if (string.Equals(host, token, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Official <c>URL.host</c> for a TCP endpoint: omit the port when it
        /// is the default HTTP or HTTPS port.
        /// </summary>
        /// <param name="hostname">DNS or IP host.</param>
        /// <param name="port">Destination port.</param>
        /// <returns>The host token used by <see cref="ShouldBypass"/>.</returns>
        internal static string RequestHost(string hostname, int port)
        {
            if (string.IsNullOrEmpty(hostname) || port == 80 || port == 443)
            {
                return hostname ?? string.Empty;
            }

            return hostname + ":" + port.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Official <c>normalizeProxySettings</c> trims each comma-separated
        /// bypass token.
        /// </summary>
        /// <param name="bypass">Raw bypass list, or <see langword="null"/>.</param>
        /// <returns>Normalized list, or <see langword="null"/>.</returns>
        internal static string NormalizeBypass(string bypass)
        {
            if (string.IsNullOrEmpty(bypass))
            {
                return null;
            }

            string[] parts = bypass.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length == 0 ? null : string.Join(",", parts);
        }

        /// <summary>
        /// Official WebKit <c>authenticateProxyViaHeader</c>: inject
        /// <c>Proxy-Authorization</c> so MiniBrowser sends credentials.
        /// </summary>
        /// <param name="proxy">Context proxy, or <see langword="null"/>.</param>
        /// <param name="extra">Caller extra headers, or <see langword="null"/>.</param>
        /// <returns>Headers including proxy authorization when needed.</returns>
        internal static IEnumerable<KeyValuePair<string, string>> WithProxyAuthorization(
            Proxy proxy,
            IEnumerable<KeyValuePair<string, string>> extra)
        {
            if (!HasCredentials(proxy))
            {
                return extra;
            }

            string token = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(proxy.Username + ":" + (proxy.Password ?? string.Empty)));
            List<KeyValuePair<string, string>> merged = new();
            if (extra != null)
            {
                foreach (KeyValuePair<string, string> header in extra)
                {
                    if (!header.Key.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase))
                    {
                        merged.Add(header);
                    }
                }
            }

            merged.Add(new KeyValuePair<string, string>("Proxy-Authorization", "Basic " + token));
            return merged;
        }
    }
}
