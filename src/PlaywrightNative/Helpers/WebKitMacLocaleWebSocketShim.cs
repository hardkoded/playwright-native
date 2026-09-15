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
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// macOS WebKit (cfNetwork) never sends <c>localhost</c> through an HTTP
    /// proxy, so <see cref="LocaleHandshakeProxy"/> cannot rewrite
    /// <c>Accept-Language</c> on <c>ws://localhost</c> upgrades. Official
    /// client-certificate fixtures already use <c>local.playwright</c> for the
    /// same reason. This init script rewrites loopback WebSocket URLs (and Blob
    /// worker sources that embed them) to <c>local.playwright</c> so the
    /// handshake proxy sees the upgrade; the proxy maps that host back to
    /// localhost when connecting.
    /// </summary>
    internal static class WebKitMacLocaleWebSocketShim
    {
        /// <summary>
        /// Host used for loopback WebSockets so macOS WebKit routes them via
        /// <see cref="LocaleHandshakeProxy"/>.
        /// </summary>
        internal const string FakeLoopbackHost = "local.playwright";

        /// <summary>
        /// Page init script that rewrites loopback <c>WebSocket</c> URLs and
        /// Blob parts that open loopback sockets from workers.
        /// </summary>
        internal const string Source =
            @"(() => {
  if (globalThis.__pw_mac_locale_ws_shim__) return;
  globalThis.__pw_mac_locale_ws_shim__ = true;
  const fake = 'local.playwright';
  const isLoopback = (host) => host === 'localhost' || host === '127.0.0.1' || host === '[::1]' || host === '::1';
  const rewriteUrl = (url) => {
    try {
      const u = new URL(String(url), location.href);
      if ((u.protocol === 'ws:' || u.protocol === 'wss:') && isLoopback(u.hostname)) {
        u.hostname = fake;
        return u.toString();
      }
    } catch (e) {}
    return url;
  };
  const rewriteText = (text) => {
    if (typeof text !== 'string') return text;
    return text
      .replace(/ws:\/\/(localhost|127\.0\.0\.1|\[::1\]|::1)/gi, 'ws://' + fake)
      .replace(/wss:\/\/(localhost|127\.0\.0\.1|\[::1\]|::1)/gi, 'wss://' + fake);
  };
  const OrigWS = globalThis.WebSocket;
  if (typeof OrigWS === 'function') {
    const Wrapped = function(url, protocols) {
      const next = rewriteUrl(url);
      return protocols === undefined ? new OrigWS(next) : new OrigWS(next, protocols);
    };
    Wrapped.prototype = OrigWS.prototype;
    Object.defineProperty(Wrapped, 'name', { value: 'WebSocket' });
    Wrapped.CONNECTING = OrigWS.CONNECTING;
    Wrapped.OPEN = OrigWS.OPEN;
    Wrapped.CLOSING = OrigWS.CLOSING;
    Wrapped.CLOSED = OrigWS.CLOSED;
    try {
      Object.setPrototypeOf(Wrapped, OrigWS);
    } catch (e) {}
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
    try {
      Object.setPrototypeOf(WrappedBlob, OrigBlob);
    } catch (e) {}
    globalThis.Blob = WrappedBlob;
  }
})()";
    }
}
