/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Parses CDP / WebKit post data into the official Playwright
    /// <c>postData</c> / <c>postDataBuffer</c> / <c>postDataJSON</c> shapes.
    /// </summary>
    internal static class RequestPostData
    {
        /// <summary>
        /// Reads <c>postDataEntries</c> (base64 bytes) or the <c>postData</c> string
        /// from a protocol request payload.
        /// </summary>
        /// <param name="requestPayload">The nested <c>request</c> object.</param>
        /// <returns>The raw body, or <see langword="null"/>.</returns>
        internal static byte[] FromProtocol(JsonElement requestPayload)
        {
            if (requestPayload.TryGetProperty("postDataEntries", out JsonElement entries)
                && entries.ValueKind == JsonValueKind.Array)
            {
                List<byte> combined = new();
                foreach (JsonElement entry in entries.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object
                        || !entry.TryGetProperty("bytes", out JsonElement bytesEl)
                        || bytesEl.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    string encoded = bytesEl.GetString();
                    if (string.IsNullOrEmpty(encoded))
                    {
                        continue;
                    }

                    try
                    {
                        combined.AddRange(Convert.FromBase64String(encoded));
                    }
                    catch (FormatException)
                    {
                    }
                }

                if (combined.Count > 0)
                {
                    return combined.ToArray();
                }
            }

            if (requestPayload.TryGetProperty("postData", out JsonElement postDataEl)
                && postDataEl.ValueKind == JsonValueKind.String)
            {
                string postData = postDataEl.GetString();
                return string.IsNullOrEmpty(postData) ? null : Encoding.UTF8.GetBytes(postData);
            }

            return null;
        }

        /// <summary>
        /// Decodes WebKit's base64 <c>postData</c> field into raw bytes.
        /// </summary>
        /// <param name="postData">The protocol post-data string.</param>
        /// <returns>The raw body, or <see langword="null"/>.</returns>
        internal static byte[] FromWebKitBase64(string postData)
        {
            if (string.IsNullOrEmpty(postData))
            {
                return null;
            }

            if ((postData.Length % 4) == 0)
            {
                try
                {
                    return Convert.FromBase64String(postData);
                }
                catch (FormatException)
                {
                }
            }

            return Encoding.UTF8.GetBytes(postData);
        }

        /// <summary>
        /// UTF-8 text view of <paramref name="buffer"/>, matching official
        /// <c>request.postData()</c>.
        /// </summary>
        /// <param name="buffer">The raw body.</param>
        /// <returns>The UTF-8 string, or <see langword="null"/>.</returns>
        internal static string ToUtf8String(byte[] buffer)
            => buffer == null ? null : Encoding.UTF8.GetString(buffer);

        /// <summary>
        /// Parses JSON or <c>application/x-www-form-urlencoded</c> post data.
        /// </summary>
        /// <param name="postData">The UTF-8 body text.</param>
        /// <param name="headers">Request headers (for content-type).</param>
        /// <param name="documentOptions">JSON document options.</param>
        /// <returns>The parsed document, or <see langword="null"/>.</returns>
        internal static JsonDocument ParseJson(
            string postData,
            IEnumerable<KeyValuePair<string, string>> headers,
            JsonDocumentOptions documentOptions = default)
        {
            if (string.IsNullOrEmpty(postData))
            {
                return null;
            }

            string contentType = HeaderMap.Value(headers, "content-type");
            if (contentType != null
                && contentType.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                Dictionary<string, string> entries = new(StringComparer.Ordinal);
                string[] pairs = postData.Split('&', StringSplitOptions.None);
                for (int i = 0; i < pairs.Length; i++)
                {
                    string pair = pairs[i];
                    if (string.IsNullOrEmpty(pair))
                    {
                        continue;
                    }

                    int eq = pair.IndexOf('=', StringComparison.Ordinal);
                    string rawKey = eq < 0 ? pair : pair.Substring(0, eq);
                    string rawValue = eq < 0 ? string.Empty : pair.Substring(eq + 1);
                    string key = DecodeFormValue(rawKey);
                    string value = DecodeFormValue(rawValue);
                    entries[key] = value;
                }

                return JsonDocument.Parse(JsonSerializer.Serialize(entries), documentOptions);
            }

            try
            {
                return JsonDocument.Parse(postData, documentOptions);
            }
            catch (JsonException ex)
            {
                throw new PlaywrightNativeException("POST data is not a valid JSON object: " + postData, ex);
            }
        }

        private static string DecodeFormValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }
    }
}
