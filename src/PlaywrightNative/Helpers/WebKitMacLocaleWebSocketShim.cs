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
    /// localhost when connecting. <c>WebSocket.url</c> and message
    /// <c>origin</c> are restored to the caller-facing loopback URL so page
    /// scripts still observe <c>ws://localhost</c>.
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
  const parseWs = (url) => {
    try {
      return new URL(String(url), location.href);
    } catch (e) {
      return null;
    }
  };
  const rewriteUrl = (url) => {
    const original = parseWs(url);
    if (!original || (original.protocol !== 'ws:' && original.protocol !== 'wss:') || !isLoopback(original.hostname))
      return { wire: url, publicUrl: String(url), publicOrigin: null, fakeOrigin: null };
    const publicOrigin = original.protocol + '//' + original.host;
    const publicUrl = original.toString();
    const wireUrl = new URL(publicUrl);
    wireUrl.hostname = fake;
    return {
      wire: wireUrl.toString(),
      publicUrl,
      publicOrigin,
      fakeOrigin: wireUrl.protocol + '//' + wireUrl.host,
    };
  };
  const rewriteText = (text) => {
    if (typeof text !== 'string') return text;
    return text
      .replace(/ws:\/\/(localhost|127\.0\.0\.1|\[::1\]|::1)/gi, 'ws://' + fake)
      .replace(/wss:\/\/(localhost|127\.0\.0\.1|\[::1\]|::1)/gi, 'wss://' + fake);
  };
  const wrapMessageEvent = (event, publicOrigin, fakeOrigin) => {
    if (!publicOrigin || !fakeOrigin || event.origin !== fakeOrigin)
      return event;
    try {
      return new Proxy(event, {
        get(target, prop, receiver) {
          if (prop === 'origin') return publicOrigin;
          const value = Reflect.get(target, prop, receiver);
          return typeof value === 'function' ? value.bind(target) : value;
        }
      });
    } catch (e) {
      return event;
    }
  };
  const OrigWS = globalThis.WebSocket;
  if (typeof OrigWS === 'function') {
    const Wrapped = function(url, protocols) {
      const rewritten = rewriteUrl(url);
      const ws = protocols === undefined ? new OrigWS(rewritten.wire) : new OrigWS(rewritten.wire, protocols);
      if (!rewritten.publicOrigin || rewritten.wire === rewritten.publicUrl)
        return ws;
      try {
        Object.defineProperty(ws, 'url', {
          configurable: true,
          enumerable: true,
          get: () => rewritten.publicUrl,
        });
      } catch (e) {}
      const wrapListener = (listener) => {
        if (typeof listener !== 'function') return listener;
        const wrapped = function(event) {
          return listener.call(this, wrapMessageEvent(event, rewritten.publicOrigin, rewritten.fakeOrigin));
        };
        wrapped.__pw_orig_listener = listener;
        listener.__pw_wrapped_listener = wrapped;
        return wrapped;
      };
      const origAdd = ws.addEventListener.bind(ws);
      ws.addEventListener = function(type, listener, options) {
        if (type === 'message')
          return origAdd(type, wrapListener(listener), options);
        return origAdd(type, listener, options);
      };
      const origRemove = ws.removeEventListener.bind(ws);
      ws.removeEventListener = function(type, listener, options) {
        if (type === 'message' && listener && listener.__pw_wrapped_listener)
          return origRemove(type, listener.__pw_wrapped_listener, options);
        return origRemove(type, listener, options);
      };
      let userOnMessage = null;
      let wrappedOnMessage = null;
      try {
        Object.defineProperty(ws, 'onmessage', {
          configurable: true,
          enumerable: true,
          get: () => userOnMessage,
          set: (listener) => {
            if (wrappedOnMessage)
              origRemove('message', wrappedOnMessage);
            userOnMessage = listener;
            wrappedOnMessage = typeof listener === 'function' ? wrapListener(listener) : null;
            if (wrappedOnMessage)
              origAdd('message', wrappedOnMessage);
          }
        });
      } catch (e) {}
      return ws;
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
