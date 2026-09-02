/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Builds an HTTP Basic <c>Authorization</c> header from <see cref="HttpCredentials"/>.
    /// </summary>
    internal static class HttpBasicAuth
    {
        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="credentials"/> has a username.
        /// </summary>
        /// <param name="credentials">Credentials, or <see langword="null"/>.</param>
        /// <returns>Whether credentials should be sent.</returns>
        internal static bool HasCredentials(HttpCredentials credentials)
            => credentials != null && !string.IsNullOrEmpty(credentials.Username);

        /// <summary>
        /// Returns <see langword="true"/> when any entry has a username.
        /// </summary>
        /// <param name="credentials">Credentials, or <see langword="null"/>.</param>
        /// <returns>Whether any credential should be sent.</returns>
        internal static bool HasCredentials(IEnumerable<HttpCredentials> credentials)
        {
            if (credentials == null)
            {
                return false;
            }

            foreach (HttpCredentials item in credentials)
            {
                if (HasCredentials(item))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the first credential whose origin matches <paramref name="requestUrl"/>
        /// (official list order). Entries without an origin match every URL.
        /// </summary>
        /// <param name="credentials">Configured credentials, or <see langword="null"/>.</param>
        /// <param name="requestUrl">Absolute request URL, or <see langword="null"/>.</param>
        /// <returns>The first matching credential, or <see langword="null"/>.</returns>
        internal static HttpCredentials Pick(IEnumerable<HttpCredentials> credentials, string requestUrl)
        {
            if (credentials == null)
            {
                return null;
            }

            foreach (HttpCredentials item in credentials)
            {
                if (HasCredentials(item) && MatchesOrigin(item, requestUrl))
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// Snapshot used by context/network managers.
        /// </summary>
        /// <param name="credentials">A single credential, a list, or <see langword="null"/>.</param>
        /// <returns>A list that is never <see langword="null"/>.</returns>
        internal static IReadOnlyList<HttpCredentials> Snapshot(HttpCredentials credentials)
            => credentials == null
                ? Array.Empty<HttpCredentials>()
                : new HttpCredentials[] { credentials };

        /// <summary>
        /// Snapshot used by context/network managers.
        /// </summary>
        /// <param name="credentials">A list, or <see langword="null"/>.</param>
        /// <returns>A list that is never <see langword="null"/>.</returns>
        internal static IReadOnlyList<HttpCredentials> Snapshot(IEnumerable<HttpCredentials> credentials)
        {
            if (credentials == null)
            {
                return Array.Empty<HttpCredentials>();
            }

            List<HttpCredentials> list = new List<HttpCredentials>();
            foreach (HttpCredentials item in credentials)
            {
                if (item != null)
                {
                    list.Add(item);
                }
            }

            return list;
        }

        /// <summary>
        /// Returns <see langword="true"/> when credentials should be sent before a 401.
        /// </summary>
        /// <param name="credentials">Credentials, or <see langword="null"/>.</param>
        /// <param name="defaultAlways">
        /// Value used when <see cref="HttpCredentials.Send"/> is
        /// <see cref="HttpCredentialsSend.Undefined"/>. Chromium challenges
        /// default to <see langword="false"/>; WebKit and APIRequest default to
        /// <see langword="true"/>.
        /// </param>
        /// <returns>Whether a preemptive <c>Authorization</c> header should be sent.</returns>
        internal static bool ShouldSendPreemptively(IEnumerable<HttpCredentials> credentials, bool defaultAlways)
            => ShouldSendPreemptively(Pick(credentials, requestUrl: null), defaultAlways);

        internal static bool ShouldSendPreemptively(HttpCredentials credentials, bool defaultAlways)
        {
            if (!HasCredentials(credentials))
            {
                return false;
            }

            if (credentials.Send == HttpCredentialsSend.Always)
            {
                return true;
            }

            if (credentials.Send == HttpCredentialsSend.Unauthorized)
            {
                return false;
            }

            return defaultAlways;
        }

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="credentials"/> has no
        /// origin restriction, or when <paramref name="requestUrl"/> matches that origin.
        /// </summary>
        /// <param name="credentials">Credentials, or <see langword="null"/>.</param>
        /// <param name="requestUrl">Absolute request URL, or <see langword="null"/>.</param>
        /// <returns>Whether credentials may be used for this URL.</returns>
        internal static bool MatchesOrigin(HttpCredentials credentials, string requestUrl)
        {
            if (credentials == null || string.IsNullOrEmpty(credentials.Origin))
            {
                return true;
            }

            if (string.IsNullOrEmpty(requestUrl)
                || !Uri.TryCreate(requestUrl, UriKind.Absolute, out Uri request)
                || !Uri.TryCreate(credentials.Origin, UriKind.Absolute, out Uri origin))
            {
                return false;
            }

            return string.Equals(request.Scheme, origin.Scheme, StringComparison.OrdinalIgnoreCase)
                && string.Equals(request.Host, origin.Host, StringComparison.OrdinalIgnoreCase)
                && EffectivePort(request) == EffectivePort(origin);
        }

        /// <summary>
        /// Adds a Basic <c>Authorization</c> header unless one is already present.
        /// </summary>
        /// <param name="headers">Header map to update.</param>
        /// <param name="credentials">Credentials, or <see langword="null"/>.</param>
        /// <param name="requestUrl">
        /// Absolute request URL used to honor <see cref="HttpCredentials.Origin"/>.
        /// When omitted, the header is applied whenever credentials are present.
        /// </param>
        internal static void ApplyTo(Dictionary<string, string> headers, IEnumerable<HttpCredentials> credentials, string requestUrl = null)
            => ApplyTo(headers, Pick(credentials, requestUrl), requestUrl);

        internal static void ApplyTo(Dictionary<string, string> headers, HttpCredentials credentials, string requestUrl = null)
        {
            if (headers == null || !HasCredentials(credentials) || headers.ContainsKey("Authorization"))
            {
                return;
            }

            if (!string.IsNullOrEmpty(requestUrl) && !MatchesOrigin(credentials, requestUrl))
            {
                return;
            }

            string raw = credentials.Username + ":" + (credentials.Password ?? string.Empty);
            headers["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.GetEncoding("iso-8859-1").GetBytes(raw));
        }

        private static int EffectivePort(Uri uri)
        {
            if (uri.IsDefaultPort)
            {
                return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80;
            }

            return uri.Port;
        }
    }
}
