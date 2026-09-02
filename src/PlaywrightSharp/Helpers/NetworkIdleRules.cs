/*
 * MIT License
 *
 * Copyright (c) 2020 Darío Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Shared rules for <c>networkidle</c> inflight tracking. Mirrors upstream
    /// <c>FrameManager._isExcludedFromNetworkIdle</c> (favicon + EventSource / issue 37226).
    /// </summary>
    internal static class NetworkIdleRules
    {
        /// <summary>
        /// Quiet-period threshold after the last inflight request, in milliseconds.
        /// </summary>
        internal const int QuietPeriodMs = 500;

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="resourceType"/> is an EventSource.
        /// </summary>
        /// <param name="resourceType">The protocol resource type (any casing).</param>
        /// <returns><see langword="true"/> for EventSource / SSE connections.</returns>
        internal static bool IsEventSource(string resourceType)
        {
            return !string.IsNullOrEmpty(resourceType)
                && string.Equals(resourceType, "eventsource", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="resourceType"/> is a WebSocket.
        /// Handshake requests can stay open for the socket lifetime and must not
        /// block <c>networkidle</c> (same class of hang as EventSource / issue 37226).
        /// </summary>
        /// <param name="resourceType">The protocol resource type (any casing).</param>
        /// <returns><see langword="true"/> for WebSocket connections.</returns>
        internal static bool IsWebSocket(string resourceType)
        {
            return !string.IsNullOrEmpty(resourceType)
                && string.Equals(resourceType, "websocket", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns <see langword="true"/> when the request must not keep <c>networkidle</c>
        /// from firing (favicon housekeeping, EventSource, or WebSocket).
        /// </summary>
        /// <param name="url">The request URL.</param>
        /// <param name="resourceType">The protocol resource type.</param>
        /// <returns><see langword="true"/> when the request is ignored for idle.</returns>
        internal static bool IsExcluded(string url, string resourceType)
        {
            if (IsIgnoredUrl(url))
            {
                return true;
            }

            return IsEventSource(resourceType) || IsWebSocket(resourceType);
        }

        /// <summary>
        /// Favicon, <c>ws:</c>/<c>wss:</c> URLs, and the test-server echo path
        /// <c>/ws</c> (Chromium reports the handshake as <c>http(s)://…/ws</c>).
        /// </summary>
        /// <param name="url">The request URL.</param>
        /// <returns><see langword="true"/> when the URL is ignored for idle.</returns>
        internal static bool IsIgnoredUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            if (url.Contains("/favicon.ico", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (url.StartsWith("ws:", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("wss:", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return HasPath(url, "/ws");
        }

        private static bool HasPath(string url, string path)
        {
            int scheme = url.IndexOf("://", StringComparison.Ordinal);
            string rest = scheme >= 0 ? url.Substring(scheme + 3) : url;
            int slash = rest.IndexOf('/');
            string urlPath = slash >= 0 ? rest.Substring(slash) : "/";
            int query = urlPath.IndexOf('?', StringComparison.Ordinal);
            int hash = urlPath.IndexOf('#', StringComparison.Ordinal);
            int cut = query;
            if (hash >= 0 && (cut < 0 || hash < cut))
            {
                cut = hash;
            }

            if (cut >= 0)
            {
                urlPath = urlPath.Substring(0, cut);
            }

            return string.Equals(urlPath, path, StringComparison.OrdinalIgnoreCase);
        }
    }
}
