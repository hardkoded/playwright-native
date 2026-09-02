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

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Handshake and timing captured for one page WebSocket HAR entry.
    /// </summary>
    internal sealed class HarWebSocketState
    {
        private readonly object _gate = new();
        private readonly List<KeyValuePair<string, string>> _requestHeaders = new();
        private readonly List<KeyValuePair<string, string>> _responseHeaders = new();

        internal DateTimeOffset Started { get; private set; } = DateTimeOffset.UtcNow;

        internal double WallTimeMs { get; private set; }

        internal double TimestampBaselineMs { get; private set; }

        internal int Status { get; private set; } = -1;

        internal string StatusText { get; private set; } = string.Empty;

        internal string FailureText { get; private set; } = string.Empty;

        internal IReadOnlyList<KeyValuePair<string, string>> RequestHeaders
        {
            get
            {
                lock (_gate)
                {
                    return _requestHeaders.ToArray();
                }
            }
        }

        internal IReadOnlyList<KeyValuePair<string, string>> ResponseHeaders
        {
            get
            {
                lock (_gate)
                {
                    return _responseHeaders.ToArray();
                }
            }
        }

        internal void ApplyHandshakeRequest(
            IEnumerable<KeyValuePair<string, string>> headers,
            double wallTimeMs,
            double timestampSeconds)
        {
            lock (_gate)
            {
                _requestHeaders.Clear();
                AddHeaders(_requestHeaders, headers);
                if (wallTimeMs > 0)
                {
                    WallTimeMs = wallTimeMs;
                    TimestampBaselineMs = wallTimeMs - (timestampSeconds * 1000);
                    Started = DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(wallTimeMs));
                }
            }
        }

        internal void ApplyHandshakeResponse(
            int status,
            string statusText,
            IEnumerable<KeyValuePair<string, string>> headers,
            bool overwrite = true)
        {
            lock (_gate)
            {
                if (!overwrite && Status >= 0)
                {
                    if (Status != 404 || status <= 0 || status == 404)
                    {
                        return;
                    }
                }

                if (status <= 0 && Status > 0)
                {
                    return;
                }

                Status = status;
                StatusText = statusText ?? string.Empty;
                _responseHeaders.Clear();
                AddHeaders(_responseHeaders, headers);
            }
        }

        internal void ApplyFailure(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            lock (_gate)
            {
                if (string.IsNullOrEmpty(FailureText))
                {
                    FailureText = message;
                }
            }
        }

        internal double FrameWallTimeMs(double timestampSeconds)
        {
            lock (_gate)
            {
                if (TimestampBaselineMs != 0 || WallTimeMs != 0)
                {
                    return TimestampBaselineMs + (timestampSeconds * 1000);
                }
            }

            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private static void AddHeaders(
            List<KeyValuePair<string, string>> target,
            IEnumerable<KeyValuePair<string, string>> headers)
        {
            if (headers == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                string value = header.Value ?? string.Empty;
                if (value.IndexOf('\n') < 0)
                {
                    target.Add(new KeyValuePair<string, string>(header.Key ?? string.Empty, value));
                    continue;
                }

                foreach (string part in value.Split('\n'))
                {
                    if (!string.IsNullOrEmpty(part))
                    {
                        target.Add(new KeyValuePair<string, string>(header.Key ?? string.Empty, part));
                    }
                }
            }
        }
    }
}
