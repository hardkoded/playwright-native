/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>route.fulfill</c> helpers: <c>splitSetCookieHeader</c>,
    /// <c>_responseBodyOverride</c> for non-2xx/3xx, and header-map joins
    /// that keep multiple <c>Set-Cookie</c> values.
    /// </summary>
    internal static class RouteFulfill
    {
        /// <summary>
        /// Official Playwright stores the fulfilled body on the request when
        /// Chrome's <c>Network.getResponseBody</c> cannot return it (1xx and
        /// 4xx/5xx).
        /// </summary>
        /// <param name="status">Fulfilled status code.</param>
        /// <returns><see langword="true"/> when the body must be stored locally.</returns>
        internal static bool ShouldOverrideBody(int status)
            => status < 200 || status >= 400;

        /// <summary>
        /// Splits a newline-joined <c>Set-Cookie</c> value into one header
        /// entry per cookie (official <c>splitSetCookieHeader</c>).
        /// </summary>
        /// <param name="headers">Fulfill headers.</param>
        /// <returns>A new list. Never <see langword="null"/>.</returns>
        internal static List<KeyValuePair<string, string>> SplitSetCookie(
            IEnumerable<KeyValuePair<string, string>> headers)
        {
            List<KeyValuePair<string, string>> result = new();
            if (headers == null)
            {
                return result;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.IsNullOrEmpty(header.Key))
                {
                    continue;
                }

                string value = header.Value ?? string.Empty;
                if (string.Equals(header.Key, "set-cookie", StringComparison.OrdinalIgnoreCase)
                    && value.Contains('\n', StringComparison.Ordinal))
                {
                    foreach (string part in value.Split('\n'))
                    {
                        result.Add(new KeyValuePair<string, string>(header.Key, part));
                    }
                }
                else
                {
                    result.Add(new KeyValuePair<string, string>(header.Key, value));
                }
            }

            return result;
        }

        /// <summary>
        /// Builds a case-insensitive header map. Duplicate <c>Set-Cookie</c>
        /// values are joined with newlines; other names last-win.
        /// </summary>
        /// <param name="headers">Header sequence, or <see langword="null"/>.</param>
        /// <returns>The map, or <see langword="null"/> when <paramref name="headers"/> is <see langword="null"/>.</returns>
        internal static IDictionary<string, string> ToHeaderMap(
            IEnumerable<KeyValuePair<string, string>> headers)
        {
            if (headers == null)
            {
                return null;
            }

            Dictionary<string, string> dict = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.IsNullOrEmpty(header.Key))
                {
                    continue;
                }

                string value = header.Value ?? string.Empty;
                if (string.Equals(header.Key, "set-cookie", StringComparison.OrdinalIgnoreCase)
                    && dict.TryGetValue(header.Key, out string existing))
                {
                    dict[header.Key] = existing + "\n" + value;
                }
                else
                {
                    dict[header.Key] = value;
                }
            }

            return dict;
        }

        /// <summary>
        /// Joins HTTP header values the way Playwright header maps do.
        /// </summary>
        /// <param name="name">Header name.</param>
        /// <param name="values">Header values.</param>
        /// <returns>The joined value.</returns>
        internal static string JoinValues(string name, IEnumerable<string> values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            if (string.Equals(name, "set-cookie", StringComparison.OrdinalIgnoreCase))
            {
                return string.Join("\n", values);
            }

            return string.Join(", ", values);
        }

        /// <summary>
        /// Official API fetch returns a decompressed body while leaving
        /// <c>content-encoding</c> on the header map. Chromium then ignores
        /// that header on <c>Fetch.fulfillRequest</c> and renders the raw body.
        /// </summary>
        /// <param name="body">The network body, possibly compressed.</param>
        /// <param name="headers">Response headers that may include <c>content-encoding</c>.</param>
        /// <returns>The decompressed body, or <paramref name="body"/> when uncompressed.</returns>
        internal static byte[] DecodeEncodedBody(
            byte[] body,
            IEnumerable<KeyValuePair<string, string>> headers)
        {
            if (body == null || body.Length == 0 || headers == null)
            {
                return body ?? Array.Empty<byte>();
            }

            string encoding = HeaderMap.Value(headers, "content-encoding");
            if (string.IsNullOrEmpty(encoding))
            {
                return body;
            }

            try
            {
                byte[] current = body;
                string[] parts = encoding.Split(',');
                for (int i = parts.Length - 1; i >= 0; i--)
                {
                    string token = parts[i].Trim();
                    if (string.Equals(token, "gzip", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(token, "x-gzip", StringComparison.OrdinalIgnoreCase))
                    {
                        current = Inflate(current, gzip: true);
                    }
                    else if (string.Equals(token, "deflate", StringComparison.OrdinalIgnoreCase))
                    {
                        current = Inflate(current, gzip: false);
                    }
                    else if (string.Equals(token, "br", StringComparison.OrdinalIgnoreCase))
                    {
                        current = InflateBrotli(current);
                    }
                }

                return current;
            }
            catch (Exception ex) when (ex is InvalidDataException || ex is IOException || ex is InvalidOperationException)
            {
                throw new PlaywrightNativeException(
                    "failed to decompress '" + encoding.Trim() + "' encoding",
                    ex);
            }
        }

        private static byte[] Inflate(byte[] body, bool gzip)
        {
            if (!gzip)
            {
                try
                {
                    return InflateZlib(body);
                }
                catch (InvalidDataException)
                {
                    // Node zlib.deflate is zlib-wrapped; some servers send raw deflate.
                }
            }

            using MemoryStream input = new(body);
            using Stream decoder = gzip
                ? new GZipStream(input, CompressionMode.Decompress)
                : new DeflateStream(input, CompressionMode.Decompress);
            using MemoryStream output = new();
            decoder.CopyTo(output);
            return output.ToArray();
        }

        private static byte[] InflateZlib(byte[] body)
        {
            using MemoryStream input = new(body);
            using ZLibStream decoder = new(input, CompressionMode.Decompress);
            using MemoryStream output = new();
            decoder.CopyTo(output);
            return output.ToArray();
        }

        private static byte[] InflateBrotli(byte[] body)
        {
            using MemoryStream input = new(body);
            using BrotliStream decoder = new(input, CompressionMode.Decompress);
            using MemoryStream output = new();
            decoder.CopyTo(output);
            return output.ToArray();
        }
    }
}
