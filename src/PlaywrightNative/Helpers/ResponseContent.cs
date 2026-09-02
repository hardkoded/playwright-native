/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Decodes response bodies into text and JSON.
    /// </summary>
    internal static class ResponseContent
    {
        /// <summary>
        /// Reads UTF-8 text from <paramref name="getBytes"/>.
        /// </summary>
        /// <param name="getBytes">Fetches the raw body.</param>
        /// <returns>The decoded text.</returns>
        internal static async Task<string> ReadTextAsync(Func<Task<byte[]>> getBytes)
        {
            if (getBytes == null)
            {
                throw new ArgumentNullException(nameof(getBytes));
            }

            byte[] bytes = await getBytes().ConfigureAwait(false);
            return Encoding.UTF8.GetString(bytes ?? Array.Empty<byte>());
        }

        /// <summary>
        /// Deserializes the body as <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="getBytes">Fetches the raw body.</param>
        /// <returns>The deserialized value.</returns>
        internal static async Task<T> ReadJsonAsync<T>(Func<Task<byte[]>> getBytes)
        {
            string text = await ReadTextAsync(getBytes).ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(text);
        }

        /// <summary>
        /// Parses the body as a <see cref="JsonDocument"/>.
        /// </summary>
        /// <param name="getBytes">Fetches the raw body.</param>
        /// <param name="options">JSON document options.</param>
        /// <returns>The parsed document.</returns>
        internal static async Task<JsonDocument> ReadJsonDocumentAsync(Func<Task<byte[]>> getBytes, JsonDocumentOptions options)
        {
            string text = await ReadTextAsync(getBytes).ConfigureAwait(false);
            return JsonDocument.Parse(text, options);
        }

        /// <summary>
        /// Decodes a CDP/WebKit <c>Network.getResponseBody</c> payload.
        /// </summary>
        /// <param name="result">The protocol result, or <see langword="null"/>.</param>
        /// <returns>The response body bytes.</returns>
        internal static byte[] DecodeProtocolBody(JsonElement? result)
        {
            if (!result.HasValue)
            {
                return Array.Empty<byte>();
            }

            JsonElement resultValue = result.Value;
            string body = resultValue.TryGetProperty("body", out JsonElement bodyElement)
                ? bodyElement.GetString() ?? string.Empty
                : string.Empty;
            bool base64Encoded = resultValue.TryGetProperty("base64Encoded", out JsonElement encodedElement)
                && encodedElement.GetBoolean();
            return base64Encoded ? Convert.FromBase64String(body) : Encoding.UTF8.GetBytes(body);
        }
    }
}
