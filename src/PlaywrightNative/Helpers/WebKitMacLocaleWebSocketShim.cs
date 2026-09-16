/*
 * Copyright (c) Microsoft Corporation.
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

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// macOS WebKit (cfNetwork) never sends <c>localhost</c> through an HTTP
    /// proxy, so <see cref="LocaleHandshakeProxy"/> cannot rewrite
    /// <c>Accept-Language</c> on <c>ws://localhost</c> upgrades. Official
    /// client-certificate fixtures already use <c>local.playwright</c> for the
    /// same reason. This init script rewrites loopback WebSocket URLs (and Blob
    /// worker sources that embed them) to distinct fake hosts so the
    /// handshake proxy sees the upgrade; the proxy maps that host back to
    /// the original loopback when connecting. <c>WebSocket.url</c> is restored
    /// to the caller-facing loopback URL. Message <c>origin</c> is left as the
    /// wire origin — wrapping message listeners with <c>Proxy</c> broke
    /// await-in-evaluate Promise resolution for <c>message</c> events.
    /// Network/HAR events see the wire host; <see cref="ToPublicUrl"/> maps
    /// them back for the public API.
    /// </summary>
    internal static class WebKitMacLocaleWebSocketShim
    {
        /// <summary>
        /// Fake host for <c>localhost</c> / <c>*.localhost</c> WebSockets.
        /// </summary>
        internal const string FakeLoopbackHost = "local.playwright";

        /// <summary>
        /// Fake host for <c>127.0.0.1</c> WebSockets (preserves the public URL
        /// when rewriting HAR / <see cref="IWebSocket.Url"/>).
        /// </summary>
        internal const string FakeIpv4LoopbackHost = "local.playwright.ipv4";

        /// <summary>
        /// Fake host for <c>::1</c> WebSockets.
        /// </summary>
        internal const string FakeIpv6LoopbackHost = "local.playwright.ipv6";

        /// <summary>
        /// Page init script that rewrites loopback <c>WebSocket</c> URLs and
        /// Blob parts that open loopback sockets from workers.
        /// </summary>
        internal const string Source =
            @"(() => {
  if (globalThis.__pw_mac_locale_ws_shim__) return;
  globalThis.__pw_mac_locale_ws_shim__ = true;
  const fakeFor = (host) => {
    if (host === '127.0.0.1') return 'local.playwright.ipv4';
    if (host === '[::1]' || host === '::1') return 'local.playwright.ipv6';
    if (host === 'localhost' || (typeof host === 'string' && host.endsWith('.localhost')))
      return 'local.playwright';
    return null;
  };
  const parseWs = (url) => {
    try { return new URL(String(url), location.href); } catch (e) { return null; }
  };
  const rewriteUrl = (url) => {
    const original = parseWs(url);
    if (!original || (original.protocol !== 'ws:' && original.protocol !== 'wss:'))
      return { wire: url, publicUrl: String(url) };
    const fake = fakeFor(original.hostname);
    if (!fake)
      return { wire: url, publicUrl: String(url) };
    const publicUrl = original.toString();
    const wireUrl = new URL(publicUrl);
    wireUrl.hostname = fake;
    return { wire: wireUrl.toString(), publicUrl };
  };
  const rewriteText = (text) => {
    if (typeof text !== 'string') return text;
    return text
      .replace(/ws:\/\/127\.0\.0\.1/gi, 'ws://local.playwright.ipv4')
      .replace(/wss:\/\/127\.0\.0\.1/gi, 'wss://local.playwright.ipv4')
      .replace(/ws:\/\/(\[::1\]|::1)/gi, 'ws://local.playwright.ipv6')
      .replace(/wss:\/\/(\[::1\]|::1)/gi, 'wss://local.playwright.ipv6')
      .replace(/ws:\/\/(localhost|[a-z0-9-]+\.localhost)/gi, 'ws://local.playwright')
      .replace(/wss:\/\/(localhost|[a-z0-9-]+\.localhost)/gi, 'wss://local.playwright');
  };
  const OrigWS = globalThis.WebSocket;
  if (typeof OrigWS === 'function') {
    const Wrapped = function(url, protocols) {
      const rewritten = rewriteUrl(url);
      const ws = protocols === undefined ? new OrigWS(rewritten.wire) : new OrigWS(rewritten.wire, protocols);
      // Restore caller-facing URL only. Do not wrap message listeners — Proxy /
      // rebinding breaks await-in-evaluate Promise resolution for message events
      // (capabilities WebSocketShouldWork / web-socket should work).
      if (rewritten.wire !== rewritten.publicUrl) {
        try {
          Object.defineProperty(ws, 'url', {
            configurable: true,
            enumerable: true,
            get: () => rewritten.publicUrl,
          });
        } catch (e) {}
      }
      return ws;
    };
    Wrapped.prototype = OrigWS.prototype;
    Object.defineProperty(Wrapped, 'name', { value: 'WebSocket' });
    Wrapped.CONNECTING = OrigWS.CONNECTING;
    Wrapped.OPEN = OrigWS.OPEN;
    Wrapped.CLOSING = OrigWS.CLOSING;
    Wrapped.CLOSED = OrigWS.CLOSED;
    try { Object.setPrototypeOf(Wrapped, OrigWS); } catch (e) {}
    globalThis.WebSocket = Wrapped;
  }
  const OrigBlob = globalThis.Blob;
  if (typeof OrigBlob === 'function') {
    const WrappedBlob = function(parts, options) {
      let next = parts;
      if (Array.isArray(parts)) {
        next = parts.map((part) => typeof part === 'string' ? rewriteText(part) : part);
      }
      return new OrigBlob(next, options);
    };
    WrappedBlob.prototype = OrigBlob.prototype;
    try { Object.setPrototypeOf(WrappedBlob, OrigBlob); } catch (e) {}
    globalThis.Blob = WrappedBlob;
  }
})()";

        /// <summary>
        /// Maps a wire host used by the Mac WS shim back to the public loopback
        /// hostname page scripts passed to <c>new WebSocket</c>.
        /// </summary>
        /// <param name="host">Hostname from the network stack (may be fake).</param>
        /// <returns>The public loopback host, or <paramref name="host"/> unchanged.</returns>
        internal static string ToPublicHost(string host)
        {
            if (string.IsNullOrEmpty(host))
            {
                return host;
            }

            if (string.Equals(host, FakeLoopbackHost, StringComparison.OrdinalIgnoreCase))
            {
                return "localhost";
            }

            if (string.Equals(host, FakeIpv4LoopbackHost, StringComparison.OrdinalIgnoreCase))
            {
                return "127.0.0.1";
            }

            if (string.Equals(host, FakeIpv6LoopbackHost, StringComparison.OrdinalIgnoreCase))
            {
                return "::1";
            }

            return host;
        }

        /// <summary>
        /// Maps a wire WebSocket URL (<c>local.playwright*</c>) back to the
        /// caller-facing loopback URL for <see cref="IWebSocket.Url"/> / HAR.
        /// </summary>
        /// <param name="url">URL reported by <c>Network.webSocketCreated</c>.</param>
        /// <returns>The public URL, or <paramref name="url"/> when not rewritten.</returns>
        internal static string ToPublicUrl(string url)
        {
            if (string.IsNullOrEmpty(url)
                || !Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return url;
            }

            string publicHost = ToPublicHost(uri.IdnHost);
            if (string.Equals(publicHost, uri.IdnHost, StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            UriBuilder builder = new UriBuilder(uri)
            {
                Host = publicHost,
            };
            return builder.Uri.AbsoluteUri;
        }

        /// <summary>
        /// Whether <paramref name="host"/> is a Mac WS shim fake loopback host.
        /// </summary>
        /// <param name="host">Hostname without brackets.</param>
        /// <returns><see langword="true"/> for shim fake hosts.</returns>
        internal static bool IsFakeLoopbackHost(string host)
            => string.Equals(host, FakeLoopbackHost, StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, FakeIpv4LoopbackHost, StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, FakeIpv6LoopbackHost, StringComparison.OrdinalIgnoreCase);
    }
}
