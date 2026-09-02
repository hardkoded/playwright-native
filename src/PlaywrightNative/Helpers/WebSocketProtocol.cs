/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Parses Chromium/WebKit <c>Network.webSocketFrame*</c> payloads.
    /// </summary>
    internal static class WebSocketProtocol
    {
        /// <summary>
        /// Reads <c>requestId</c> and <c>url</c> from a created event.
        /// </summary>
        /// <param name="parameters">Event params.</param>
        /// <param name="requestId">The request id.</param>
        /// <param name="url">The socket URL.</param>
        /// <returns><see langword="true"/> when both fields are present.</returns>
        internal static bool TryReadCreated(JsonElement? parameters, out string requestId, out string url)
        {
            requestId = null;
            url = string.Empty;
            if (!parameters.HasValue)
            {
                return false;
            }

            JsonElement payload = parameters.Value;
            if (!payload.TryGetProperty("requestId", out JsonElement idEl)
                || idEl.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            requestId = idEl.GetString();
            if (payload.TryGetProperty("url", out JsonElement urlEl) && urlEl.ValueKind == JsonValueKind.String)
            {
                url = urlEl.GetString();
            }

            return !string.IsNullOrEmpty(requestId);
        }

        /// <summary>
        /// Reads <c>requestId</c> from a socket event.
        /// </summary>
        /// <param name="parameters">Event params.</param>
        /// <returns>The request id, or <see langword="null"/>.</returns>
        internal static string ReadRequestId(JsonElement? parameters)
        {
            if (!parameters.HasValue
                || !parameters.Value.TryGetProperty("requestId", out JsonElement idEl)
                || idEl.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return idEl.GetString();
        }

        /// <summary>
        /// Builds a frame from a <c>response</c> object on sent/received events.
        /// </summary>
        /// <param name="parameters">Event params.</param>
        /// <returns>The frame, or <see langword="null"/>.</returns>
        internal static IWebSocketFrame ReadFrame(JsonElement? parameters)
            => ReadFrame(parameters, out _);

        /// <summary>
        /// Builds a frame and reads the monotonic <c>timestamp</c> in seconds.
        /// </summary>
        /// <param name="parameters">Event params.</param>
        /// <param name="timestampSeconds">Monotonic timestamp, or <c>0</c>.</param>
        /// <returns>The frame, or <see langword="null"/>.</returns>
        internal static IWebSocketFrame ReadFrame(JsonElement? parameters, out double timestampSeconds)
        {
            timestampSeconds = 0;
            if (!parameters.HasValue
                || !parameters.Value.TryGetProperty("response", out JsonElement response)
                || response.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (parameters.Value.TryGetProperty("timestamp", out JsonElement tsEl)
                && tsEl.ValueKind == JsonValueKind.Number)
            {
                timestampSeconds = tsEl.GetDouble();
            }

            int opcode = 1;
            if (response.TryGetProperty("opcode", out JsonElement opcodeEl)
                && opcodeEl.ValueKind == JsonValueKind.Number)
            {
                opcode = opcodeEl.GetInt32();
            }

            string payload = string.Empty;
            if (response.TryGetProperty("payloadData", out JsonElement dataEl)
                && dataEl.ValueKind == JsonValueKind.String)
            {
                payload = dataEl.GetString() ?? string.Empty;
            }

            if (string.IsNullOrEmpty(payload))
            {
                return null;
            }

            if (opcode == 2)
            {
                byte[] binary;
                try
                {
                    binary = Convert.FromBase64String(payload);
                }
                catch (FormatException)
                {
                    binary = Encoding.UTF8.GetBytes(payload);
                }

                return new WebSocketFrame(string.Empty, binary, opcode);
            }

            return new WebSocketFrame(payload, Array.Empty<byte>(), opcode);
        }

        /// <summary>
        /// Reads handshake request headers and clocks from
        /// <c>Network.webSocketWillSendHandshakeRequest</c>.
        /// </summary>
        /// <param name="parameters">Event params.</param>
        /// <param name="headers">Handshake request headers.</param>
        /// <param name="wallTimeMs">Wall clock in milliseconds.</param>
        /// <param name="timestampSeconds">Monotonic timestamp.</param>
        /// <returns><see langword="true"/> when a request object is present.</returns>
        internal static bool TryReadHandshakeRequest(
            JsonElement? parameters,
            out List<KeyValuePair<string, string>> headers,
            out double wallTimeMs,
            out double timestampSeconds)
        {
            headers = new List<KeyValuePair<string, string>>();
            wallTimeMs = 0;
            timestampSeconds = 0;
            if (!parameters.HasValue)
            {
                return false;
            }

            JsonElement payload = parameters.Value;
            if (payload.TryGetProperty("timestamp", out JsonElement tsEl)
                && tsEl.ValueKind == JsonValueKind.Number)
            {
                timestampSeconds = tsEl.GetDouble();
            }

            if (payload.TryGetProperty("wallTime", out JsonElement wallEl)
                && wallEl.ValueKind == JsonValueKind.Number)
            {
                wallTimeMs = wallEl.GetDouble() * 1000;
            }

            if (!payload.TryGetProperty("request", out JsonElement request)
                || request.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            headers.AddRange(ReadHeaders(request));
            return true;
        }

        /// <summary>
        /// Reads handshake response status and headers from
        /// <c>Network.webSocketHandshakeResponseReceived</c>.
        /// </summary>
        /// <param name="parameters">Event params.</param>
        /// <param name="status">HTTP status, or <c>-1</c>.</param>
        /// <param name="statusText">Reason phrase.</param>
        /// <param name="headers">Handshake response headers.</param>
        /// <returns><see langword="true"/> when a response object is present.</returns>
        internal static bool TryReadHandshakeResponse(
            JsonElement? parameters,
            out int status,
            out string statusText,
            out List<KeyValuePair<string, string>> headers)
        {
            status = -1;
            statusText = string.Empty;
            headers = new List<KeyValuePair<string, string>>();
            if (!parameters.HasValue)
            {
                return false;
            }

            JsonElement payload = parameters.Value;
            if (!payload.TryGetProperty("response", out JsonElement response)
                || response.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (response.TryGetProperty("status", out JsonElement statusEl)
                && statusEl.ValueKind == JsonValueKind.Number)
            {
                status = statusEl.GetInt32();
            }

            if (response.TryGetProperty("statusText", out JsonElement textEl)
                && textEl.ValueKind == JsonValueKind.String)
            {
                statusText = textEl.GetString() ?? string.Empty;
            }

            headers.AddRange(ReadHeaders(response));
            return true;
        }

        /// <summary>
        /// Reads a header map or array from a protocol object.
        /// </summary>
        /// <param name="payload">Object that may contain <c>headers</c>.</param>
        /// <returns>Name/value pairs.</returns>
        internal static List<KeyValuePair<string, string>> ReadHeaders(JsonElement payload)
        {
            List<KeyValuePair<string, string>> headers = new();
            if (!payload.TryGetProperty("headers", out JsonElement headersEl))
            {
                return headers;
            }

            if (headersEl.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in headersEl.EnumerateObject())
                {
                    string value = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString() ?? string.Empty
                        : property.Value.ToString();
                    headers.Add(new KeyValuePair<string, string>(property.Name, value));
                }

                return headers;
            }

            if (headersEl.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in headersEl.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    string name = item.TryGetProperty("name", out JsonElement nameEl)
                        && nameEl.ValueKind == JsonValueKind.String
                        ? nameEl.GetString()
                        : string.Empty;
                    string value = item.TryGetProperty("value", out JsonElement valueEl)
                        && valueEl.ValueKind == JsonValueKind.String
                        ? valueEl.GetString()
                        : string.Empty;
                    headers.Add(new KeyValuePair<string, string>(name ?? string.Empty, value ?? string.Empty));
                }
            }

            return headers;
        }

        /// <summary>
        /// Official Chromium <c>socketerror</c> includes <c>: {status}</c> for a
        /// failed handshake. WebKit only reports a generic string; append the
        /// handshake status so <c>web-socket.spec.ts</c> can match.
        /// </summary>
        /// <param name="message">CDP <c>errorMessage</c>.</param>
        /// <param name="handshakeStatus">HTTP status from the handshake, or <c>-1</c>.</param>
        /// <returns>The public socketerror text.</returns>
        internal static string FormatSocketError(string message, int handshakeStatus)
        {
            string text = message ?? string.Empty;
            if (handshakeStatus <= 0 || handshakeStatus == 101)
            {
                return text;
            }

            string marker = ": " + handshakeStatus.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (text.Contains(marker, StringComparison.Ordinal))
            {
                return text;
            }

            if (string.IsNullOrEmpty(text))
            {
                return "Unexpected response code" + marker;
            }

            return text + marker;
        }
    }
}
