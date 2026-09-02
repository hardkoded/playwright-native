/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Official <c>packages/injected/src/webSocketMock.ts</c> page-side
    /// <c>WebSocket</c> replacement used by <see cref="WebSocketRouter"/>.
    /// </summary>
    internal static class WebSocketRouteScript
    {
        /// <summary>
        /// Replaces <c>window.WebSocket</c> so matching sockets can be mocked.
        /// Idempotent. Official <c>inject(globalThis)</c>.
        /// </summary>
        internal const string Injector =
            "(() => {" +
            "if (globalThis.__pwWebSocketDispatch) return;" +
            "const NativeWS = globalThis.WebSocket;" +
            "const idToSocket = new Map();" +
            "function generateId() {" +
            "  return Date.now() + '-' + Math.random().toString(16).slice(2);" +
            "}" +
            "function bufferToData(b) {" +
            "  let s = '';" +
            "  for (let i = 0; i < b.length; i++) s += String.fromCharCode(b[i]);" +
            "  return { data: globalThis.btoa(s), isBase64: true };" +
            "}" +
            "function stringToBuffer(s) {" +
            "  s = globalThis.atob(s);" +
            "  const b = new Uint8Array(s.length);" +
            "  for (let i = 0; i < s.length; i++) b[i] = s.charCodeAt(i);" +
            "  return b.buffer;" +
            "}" +
            "function messageToData(message, cb) {" +
            "  if (globalThis.Blob && message instanceof globalThis.Blob)" +
            "    return message.arrayBuffer().then(buffer => cb(bufferToData(new Uint8Array(buffer))));" +
            "  if (typeof message === 'string') return cb({ data: message, isBase64: false });" +
            "  if (ArrayBuffer.isView(message))" +
            "    return cb(bufferToData(new Uint8Array(message.buffer, message.byteOffset, message.byteLength)));" +
            "  if (message instanceof ArrayBuffer) return cb(bufferToData(new Uint8Array(message)));" +
            "  return cb({ data: String(message), isBase64: false });" +
            "}" +
            "function dataToMessage(data, binaryType) {" +
            "  if (!data || !data.isBase64) return data ? data.data : '';" +
            "  const buffer = stringToBuffer(data.data);" +
            "  return binaryType === 'arraybuffer' ? buffer : new Blob([buffer]);" +
            "}" +
            "function normalizeProtocols(protocols) {" +
            "  if (protocols == null) return [];" +
            "  if (typeof protocols === 'string') return [String(protocols)];" +
            "  if (Array.isArray(protocols)) return protocols.map(function(p) { return String(p); });" +
            "  return [];" +
            "}" +
            "function resolveUrl(url) {" +
            "  try {" +
            "    const base = globalThis.document && globalThis.document.baseURI;" +
            "    return new URL(url, base).href.replace(/^http/, 'ws');" +
            "  } catch (e) { return String(url); }" +
            "}" +
            "function originOf(url) {" +
            "  try { return new URL(url).origin; } catch (e) { return ''; }" +
            "}" +
            "function getBinding() { return globalThis.__pwWebSocketRoute; }" +
            "const pwSeenSeq = new Set();" +
            "globalThis.__pwWebSocketDispatch = (request) => {" +
            "  if (!request) return;" +
            "  if (request._seq != null) { if (pwSeenSeq.has(request._seq)) return; pwSeenSeq.add(request._seq); }" +
            "  const ws = idToSocket.get(request.id);" +
            "  if (!ws) return;" +
            "  if (request.type === 'connect') ws._apiConnect();" +
            "  if (request.type === 'passthrough') ws._apiPassThrough();" +
            "  if (request.type === 'ensureOpened') ws._apiEnsureOpened();" +
            "  if (request.type === 'sendToPage') ws._apiSendToPage(dataToMessage(request.data, ws.binaryType));" +
            "  if (request.type === 'closePage') ws._apiClosePage(request.code, request.reason, request.wasClean);" +
            "  if (request.type === 'sendToServer') ws._apiSendToServer(dataToMessage(request.data, ws.binaryType));" +
            "  if (request.type === 'closeServer') ws._apiCloseServer(request.code, request.reason, request.wasClean);" +
            "};" +
            "class PlaywrightWebSocket {" +
            "  constructor(url, protocols) {" +
            "    this.url = resolveUrl(url);" +
            "    this._origin = originOf(this.url);" +
            "    this._protocols = protocols;" +
            "    this.protocol = '';" +
            "    this.extensions = '';" +
            "    this.binaryType = 'blob';" +
            "    this.bufferedAmount = 0;" +
            "    this.readyState = 0;" +
            "    this.onopen = null;" +
            "    this.onmessage = null;" +
            "    this.onerror = null;" +
            "    this.onclose = null;" +
            "    this._listeners = { open: [], message: [], error: [], close: [] };" +
            "    this._ws = null;" +
            "    this._passthrough = false;" +
            "    this._wsBuffered = [];" +
            "    this._id = generateId();" +
            "    idToSocket.set(this._id, this);" +
            "    const self = this;" +
            "    const payload = { type: 'onCreate', op: 'open', id: this._id, url: this.url, protocols: normalizeProtocols(protocols), pageId: globalThis.__pwWebSocketPageId || '', isMain: globalThis.window ? globalThis.window === globalThis.window.top : true };" +
            "    Promise.resolve().then(() => {" +
            "      const b = getBinding();" +
            "      const start = typeof b === 'function' ? Promise.resolve(b(payload)) : Promise.resolve(false);" +
            "      start.then((result) => {" +
            "        if (self.readyState === 3) return;" +
            "        const routed = result === true || (result && result.routed === true);" +
            "        if (routed) {" +
            "          if (result && result.ops) pwWebSocketApplyRaw(result.ops);" +
            "          pwWebSocketPull();" +
            "          setTimeout(function() {" +
            "            if (self.readyState === 0 && !self._ws) self._apiEnsureOpened();" +
            "          }, 20);" +
            "          return;" +
            "        }" +
            "        self._apiPassThrough();" +
            "      }, () => {" +
            "        if (self.readyState === 3) return;" +
            "        self._apiPassThrough();" +
            "      });" +
            "    });" +
            "  }" +
            "  _dispatch(type, event) {" +
            "    const list = this._listeners[type];" +
            "    if (list) for (const fn of list.slice()) { try { fn.call(this, event); } catch (e) {} }" +
            "    const prop = this['on' + type];" +
            "    if (typeof prop === 'function') { try { prop.call(this, event); } catch (e) {} }" +
            "  }" +
            "  _ensureOpened() {" +
            "    if (this.readyState !== 0) return;" +
            "    this.extensions = (this._ws && this._ws.extensions) || '';" +
            "    if (this._ws) this.protocol = this._ws.protocol || '';" +
            "    else if (Array.isArray(this._protocols)) this.protocol = this._protocols[0] || '';" +
            "    else this.protocol = this._protocols || '';" +
            "    this.readyState = 1;" +
            "    this._dispatch('open', new Event('open'));" +
            "  }" +
            "  _apiEnsureOpened() { if (!this._ws) this._ensureOpened(); }" +
            "  _apiSendToPage(message) {" +
            "    this._ensureOpened();" +
            "    if (this.readyState !== 1) return;" +
            "    this._dispatch('message', new MessageEvent('message', { data: message, origin: this._origin, lastEventId: '', cancelable: true }));" +
            "  }" +
            "  _apiSendToServer(message) {" +
            "    if (!this._ws) { this._wsBuffered.push(message); return; }" +
            "    if (this._ws.readyState === 0) this._wsBuffered.push(message);" +
            "    else this._ws.send(message);" +
            "  }" +
            "  _apiConnect() {" +
            "    if (this._ws) return;" +
            "    try { this._ws = new NativeWS(this.url, this._protocols); } catch (e) {" +
            "      this.readyState = 3;" +
            "      this._dispatch('error', new Event('error'));" +
            "      return;" +
            "    }" +
            "    this._ws.binaryType = this.binaryType;" +
            "    const self = this;" +
            "    this._ws.onopen = () => {" +
            "      const pending = self._wsBuffered;" +
            "      self._wsBuffered = [];" +
            "      for (const m of pending) self._ws.send(m);" +
            "      self._ensureOpened();" +
            "    };" +
            "    this._ws.onclose = (event) => self._onWSClose(event.code, event.reason, event.wasClean);" +
            "    this._ws.onmessage = (event) => {" +
            "      if (self._passthrough) self._apiSendToPage(event.data);" +
            "      else if (typeof getBinding() === 'function') messageToData(event.data, data => Promise.resolve(getBinding()({ type: 'onMessageFromServer', id: self._id, data: data })).then(pwWebSocketAfterBinding));" +
            "    };" +
            "    this._ws.onerror = () => self._dispatch('error', new Event('error', { cancelable: true }));" +
            "  }" +
            "  _apiPassThrough() { this._passthrough = true; this._apiConnect(); }" +
            "  _apiCloseServer(code, reason, wasClean) {" +
            "    if (!this._ws) { this._onWSClose(code, reason, wasClean); return; }" +
            "    if (this._ws.readyState === 0 || this._ws.readyState === 1) this._ws.close(code, reason);" +
            "  }" +
            "  _apiClosePage(code, reason, wasClean) {" +
            "    if (code === 1006) wasClean = false;" +
            "    if (this.readyState === 3) return;" +
            "    this.readyState = 3;" +
            "    const ev = typeof CloseEvent === 'function'" +
            "      ? new CloseEvent('close', { code: code || 1000, reason: reason || '', wasClean: !!wasClean, cancelable: true })" +
            "      : new Event('close');" +
            "    this._dispatch('close', ev);" +
            "    this._maybeCleanup();" +
            "    if (this._passthrough) this._apiCloseServer(code, reason, wasClean);" +
            "    else if (typeof getBinding() === 'function') Promise.resolve(getBinding()({ type: 'onClosePage', id: this._id, code: code, reason: reason || '', wasClean: !!wasClean })).then(pwWebSocketAfterBinding);" +
            "  }" +
            "  _onWSClose(code, reason, wasClean) {" +
            "    if (code === 1006) wasClean = false;" +
            "    if (this._passthrough) this._apiClosePage(code, reason, wasClean);" +
            "    else if (typeof getBinding() === 'function') Promise.resolve(getBinding()({ type: 'onCloseServer', id: this._id, code: code, reason: reason || '', wasClean: !!wasClean })).then(pwWebSocketAfterBinding);" +
            "    if (this._ws) {" +
            "      this._ws.onopen = this._ws.onclose = this._ws.onmessage = this._ws.onerror = null;" +
            "      this._ws = null;" +
            "      this._wsBuffered = [];" +
            "    }" +
            "    this._maybeCleanup();" +
            "  }" +
            "  _maybeCleanup() { if (this.readyState === 3 && !this._ws) idToSocket.delete(this._id); }" +
            "  send(message) {" +
            "    if (this.readyState === 0) throw new DOMException(\"Failed to execute 'send' on 'WebSocket': Still in CONNECTING state.\");" +
            "    if (this.readyState !== 1) throw new DOMException('WebSocket is already in CLOSING or CLOSED state.');" +
            "    const self = this;" +
            "    if (this._passthrough) { if (this._ws) this._apiSendToServer(message); return; }" +
            "    if (typeof getBinding() === 'function') messageToData(message, data => Promise.resolve(getBinding()({ type: 'onMessageFromPage', op: 'message', id: self._id, data: data, binary: !!(data && data.isBase64) })).then(pwWebSocketAfterBinding));" +
            "  }" +
            "  close(code, reason) {" +
            "    if (code !== undefined && code !== 1000 && (code < 3000 || code > 4999))" +
            "      throw new DOMException(\"Failed to execute 'close' on 'WebSocket': The close code must be either 1000, or between 3000 and 4999. \" + code + ' is neither.');" +
            "    if (this.readyState === 1 || this.readyState === 0) this.readyState = 2;" +
            "    if (this._passthrough) this._apiCloseServer(code, reason, true);" +
            "    else if (typeof getBinding() === 'function') Promise.resolve(getBinding()({ type: 'onClosePage', op: 'close', id: this._id, code: code, reason: reason || '', wasClean: true })).then(pwWebSocketAfterBinding);" +
            "  }" +
            "  addEventListener(type, fn) { if (!this._listeners[type]) this._listeners[type] = []; this._listeners[type].push(fn); }" +
            "  removeEventListener(type, fn) { const list = this._listeners[type]; if (!list) return; const i = list.indexOf(fn); if (i >= 0) list.splice(i, 1); }" +
            "  dispatchEvent(event) { this._dispatch(event.type, event); return true; }" +
            "}" +
            "PlaywrightWebSocket.CONNECTING = 0;" +
            "PlaywrightWebSocket.OPEN = 1;" +
            "PlaywrightWebSocket.CLOSING = 2;" +
            "PlaywrightWebSocket.CLOSED = 3;" +
            "PlaywrightWebSocket.prototype.CONNECTING = 0;" +
            "PlaywrightWebSocket.prototype.OPEN = 1;" +
            "PlaywrightWebSocket.prototype.CLOSING = 2;" +
            "PlaywrightWebSocket.prototype.CLOSED = 3;" +
            "function pwWebSocketApply(req) {" +
            "  if (!req) return;" +
            "  try { if (globalThis.__pwWebSocketDispatch) globalThis.__pwWebSocketDispatch(req); } catch (e) {}" +
            "  try { const list = globalThis.frames || []; for (let i = 0; i < list.length; i++) { const w = list[i]; if (w && w.__pwWebSocketDispatch) w.__pwWebSocketDispatch(req); } } catch (e) {}" +
            "}" +
            "function pwWebSocketApplyRaw(raw) {" +
            "  let reqs = raw;" +
            "  if (typeof raw === 'string') { try { reqs = JSON.parse(raw); } catch (e) { return; } }" +
            "  if (Array.isArray(reqs)) { for (let i = 0; i < reqs.length; i++) pwWebSocketApply(reqs[i]); }" +
            "  else pwWebSocketApply(reqs);" +
            "}" +
            "function pwWebSocketAfterBinding(raw) {" +
            "  pwWebSocketApplyRaw(raw);" +
            "  return pwWebSocketPull();" +
            "}" +
            "function pwWebSocketPull() {" +
            "  const b = getBinding();" +
            "  if (typeof b !== 'function' || idToSocket.size === 0) return Promise.resolve();" +
            "  return Promise.resolve(b({ type: 'onPull', pageId: globalThis.__pwWebSocketPageId || '' })).then(pwWebSocketApplyRaw, () => {});" +
            "}" +
            "const pwIsMain = globalThis.window ? globalThis.window === globalThis.window.top : true;" +
            "if (pwIsMain && !globalThis.__pwWebSocketPumping) {" +
            "  globalThis.__pwWebSocketPumping = true;" +
            "  setInterval(function() { pwWebSocketPull(); }, 50);" +
            "}" +
            "globalThis.__pwWebSocketHas = (id) => idToSocket.has(String(id));" +
            "globalThis.__pwWebSocketToPage = (id, data, binary) => {" +
            "  const ws = idToSocket.get(String(id));" +
            "  if (!ws) return;" +
            "  ws._apiSendToPage(binary ? dataToMessage({ data: data, isBase64: true }, ws.binaryType) : data);" +
            "};" +
            "globalThis.__pwWebSocketClosePage = (id, code, reason) => {" +
            "  const ws = idToSocket.get(String(id));" +
            "  if (!ws) return;" +
            "  ws._apiClosePage(code, reason, true);" +
            "};" +
            "globalThis.WebSocket = PlaywrightWebSocket;" +
            "})();";
    }
}
