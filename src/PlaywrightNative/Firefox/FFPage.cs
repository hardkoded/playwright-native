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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PlaywrightNative.Firefox
{
    /// <summary>
    /// Represents a Firefox page communicating via the Juggler protocol.
    /// Manages execution contexts, frame lifecycle, input, and network events.
    /// </summary>
    internal class FFPage
    {
        private const string BindingChannelName = "__pw_binding__";
        private const string UtilityWorldName = "__playwright_utility_world__";

        private const string BindingInitScript = @"
(function() {
    const binding = window['" + BindingChannelName + @"'];
    if (!binding) return;
    window['" + BindingChannelName + @"'] = async (name, payload) => {
        const result = await binding(JSON.stringify({name, payload}));
        return JSON.parse(result);
    };
})();";

        private readonly FFSession _session;
        private readonly string _targetId;
        private readonly FFBrowser _browser;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, FFExecutionContext> _contextIdToContext = new();
        private readonly Dictionary<string, (string Name, Func<string, Task<object>> Callback)> _exposedFunctions = new();
        private readonly List<string> _initScripts = new();
        private readonly TaskCompletionSource<bool> _initializedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly object _lifecycleLock = new();
        private readonly HashSet<string> _lifecycleEvents = new();
        private FFExecutionContext _mainContext;
        private FFExecutionContext _utilityContext;
        private int _lifecycleGeneration;
        private string _url = "about:blank";
        private string _mainFrameId;

        /// <summary>
        /// Initializes a new instance of the <see cref="FFPage"/> class.
        /// </summary>
        /// <param name="session">The Juggler page session.</param>
        /// <param name="targetId">The Juggler target ID.</param>
        /// <param name="browser">The owning browser.</param>
        /// <param name="loggerFactory">Optional logger factory.</param>
        public FFPage(FFSession session, string targetId, FFBrowser browser, ILoggerFactory loggerFactory = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _targetId = targetId;
            _browser = browser;
            _logger = loggerFactory?.CreateLogger<FFPage>();

            NetworkManager = new FFNetworkManager(session, this);
            Keyboard = new FFRawKeyboard(session);
            Mouse = new FFRawMouse(session);
            Touchscreen = new FFRawTouchscreen(session);
            FrameManager = new FFFrameManager(this);

            _session.MessageReceived += OnSessionEvent;
        }

        /// <summary>
        /// Occurs when a network request is created.
        /// </summary>
        internal event EventHandler<FFRequest> RequestCreated;

        /// <summary>
        /// Occurs when a network response is received.
        /// </summary>
        internal event EventHandler<FFResponse> ResponseReceived;

        /// <summary>
        /// Occurs when a network request finishes.
        /// </summary>
        internal event EventHandler<FFRequest> RequestFinished;

        /// <summary>
        /// Occurs when a network request fails.
        /// </summary>
        internal event EventHandler<FFRequest> RequestFailed;

        /// <summary>
        /// Occurs when a dialog is opened.
        /// </summary>
        internal event EventHandler<FFDialog> DialogOpened;

        /// <summary>
        /// Occurs when Juggler reports <c>Page.dialogClosed</c>.
        /// </summary>
        internal event EventHandler DialogClosedInBrowser;

        /// <summary>
        /// Occurs when a popup page is opened from this page.
        /// </summary>
        internal event EventHandler<FFPage> PopupOpened;

        /// <summary>
        /// Occurs when the page emits a load event.
        /// </summary>
        internal event EventHandler Load;

        /// <summary>
        /// Occurs when the page emits a DOMContentLoaded event.
        /// </summary>
        internal event EventHandler DOMContentLoaded;

        /// <summary>
        /// Occurs when a lifecycle event is recorded (<c>load</c>, <c>DOMContentLoaded</c>, <c>networkidle</c>).
        /// </summary>
        internal event Action<string> LifecycleChanged;

        /// <summary>
        /// Occurs when the page is closed.
        /// </summary>
        internal event EventHandler Closed;

        /// <summary>
        /// Gets the network manager for this page.
        /// </summary>
        internal FFNetworkManager NetworkManager { get; }

        /// <summary>
        /// Gets the frame manager for this page.
        /// </summary>
        internal FFFrameManager FrameManager { get; }

        /// <summary>
        /// Gets the keyboard input handler.
        /// </summary>
        internal FFRawKeyboard Keyboard { get; }

        /// <summary>
        /// Gets the mouse input handler.
        /// </summary>
        internal FFRawMouse Mouse { get; }

        /// <summary>
        /// Gets the touchscreen input handler.
        /// </summary>
        internal FFRawTouchscreen Touchscreen { get; }

        /// <summary>
        /// Gets the Juggler target ID.
        /// </summary>
        internal string TargetId => _targetId;

        /// <summary>
        /// Gets the underlying Juggler session.
        /// </summary>
        internal FFSession Session => _session;

        /// <summary>
        /// Gets the main execution context for the main frame.
        /// </summary>
        internal FFExecutionContext MainContext => _mainContext;

        /// <summary>
        /// Gets or sets the current main-frame URL.
        /// </summary>
        internal string Url
        {
            get => _url;
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _url = value;
                }
            }
        }

        /// <summary>
        /// Gets a task that completes when the page has been fully initialized.
        /// </summary>
        internal Task InitializedTask => _initializedTcs.Task;

        /// <summary>
        /// Marks the page ready. Juggler has no <c>Page.enable</c> /
        /// <c>Runtime.enable</c> (those are Chromium CDP). Contexts arrive as
        /// <c>Runtime.executionContextCreated</c> after <c>Browser.attachedToTarget</c>.
        /// </summary>
        internal async Task InitializeAsync()
        {
            try
            {
                if (_mainContext == null)
                {
                    using System.Threading.CancellationTokenSource cts = new(30_000);
                    while (_mainContext == null)
                    {
                        cts.Token.ThrowIfCancellationRequested();
                        await Task.Delay(10, cts.Token).ConfigureAwait(false);
                    }
                }

                _initializedTcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                _initializedTcs.TrySetException(ex);
                throw;
            }
        }

        /// <summary>
        /// Navigates the main frame to the given URL.
        /// </summary>
        /// <param name="url">The URL to navigate to.</param>
        /// <param name="waitUntil">The lifecycle event to wait for.</param>
        /// <param name="timeout">Navigation timeout in milliseconds.</param>
        internal async Task<string> NavigateAsync(string url, WaitUntilState waitUntil = WaitUntilState.Load, int timeout = 30_000)
        {
            lock (_lifecycleLock)
            {
                _lifecycleEvents.Clear();
                _lifecycleGeneration++;
                _url = url ?? _url;
            }

            string frameId = await GetMainFrameIdAsync().ConfigureAwait(false);
            JsonElement? response = await _session.SendAsync("Page.navigate", new
            {
                frameId,
                url,
            }).ConfigureAwait(false);

            string navigationId = null;
            if (response.HasValue &&
                response.Value.TryGetProperty("navigationId", out JsonElement navIdEl))
            {
                navigationId = navIdEl.GetString();
            }

            await WaitUntilUrlAsync(url, timeout).ConfigureAwait(false);
            await WaitForLifecycleAsync("load", timeout).ConfigureAwait(false);
            return navigationId;
        }

        /// <summary>
        /// Records the main frame id from <c>Page.frameAttached</c> or
        /// <c>Runtime.executionContextCreated</c> aux data.
        /// </summary>
        /// <param name="frameId">The Juggler frame id.</param>
        /// <param name="worldName">Empty for the default world.</param>
        internal void RememberMainFrameId(string frameId, string worldName)
        {
            if (string.IsNullOrEmpty(frameId) || !string.IsNullOrEmpty(worldName))
            {
                return;
            }

            if (string.IsNullOrEmpty(_mainFrameId))
            {
                _mainFrameId = frameId;
            }
        }

        /// <summary>
        /// Evaluates a JavaScript expression in the main frame's execution context.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the result to.</typeparam>
        /// <param name="expression">The JavaScript expression.</param>
        internal async Task<T> EvaluateAsync<T>(string expression)
        {
            FFExecutionContext context = await GetMainContextAsync().ConfigureAwait(false);
            return await context.EvaluateAsync<T>(expression).ConfigureAwait(false);
        }

        /// <summary>
        /// Evaluates a JavaScript expression in the main frame's execution context.
        /// </summary>
        /// <param name="expression">The JavaScript expression.</param>
        internal async Task<JsonElement?> EvaluateAsync(string expression)
        {
            FFExecutionContext context = await GetMainContextAsync().ConfigureAwait(false);
            return await context.EvaluateAsync(expression).ConfigureAwait(false);
        }

        /// <summary>
        /// Evaluates a JavaScript function with arguments in the main frame.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the result to.</typeparam>
        /// <param name="functionDeclaration">The JavaScript function declaration.</param>
        /// <param name="args">Arguments to pass to the function.</param>
        internal async Task<T> EvaluateFunctionAsync<T>(string functionDeclaration, params object[] args)
        {
            FFExecutionContext context = await GetMainContextAsync().ConfigureAwait(false);
            return await context.EvaluateFunctionAsync<T>(functionDeclaration, args).ConfigureAwait(false);
        }

        /// <summary>
        /// Evaluates a JavaScript function and returns the raw result.
        /// </summary>
        /// <param name="functionDeclaration">The JavaScript function declaration.</param>
        /// <param name="args">Arguments to pass to the function.</param>
        internal async Task<JsonElement?> EvaluateFunctionAsync(string functionDeclaration, params object[] args)
        {
            FFExecutionContext context = await GetMainContextAsync().ConfigureAwait(false);
            return await context.EvaluateFunctionAsync(functionDeclaration, args).ConfigureAwait(false);
        }

        /// <summary>
        /// Queries for a DOM element matching the given selector and returns it as an element handle.
        /// </summary>
        /// <param name="selector">The CSS selector.</param>
        internal async Task<JsonElement?> QuerySelectorAsync(string selector)
        {
            FFExecutionContext context = await GetMainContextAsync().ConfigureAwait(false);
            return await context.EvaluateFunctionHandleAsync(
                "selector => document.querySelector(selector)",
                selector).ConfigureAwait(false);
        }

        /// <summary>
        /// Runs a JavaScript function that returns a DOM node and returns the remote handle.
        /// </summary>
        /// <param name="functionDeclaration">A function declaration returning an Element or null.</param>
        /// <param name="args">Arguments passed to the function.</param>
        /// <returns>The raw remote object, or <see langword="null"/>.</returns>
        internal async Task<JsonElement?> QueryFunctionHandleAsync(string functionDeclaration, params object[] args)
        {
            FFExecutionContext context = await GetMainContextAsync().ConfigureAwait(false);
            return await context.EvaluateFunctionHandleAsync(functionDeclaration, args).ConfigureAwait(false);
        }

        /// <summary>
        /// Evaluates <paramref name="expression"/> and returns a remote-object handle.
        /// </summary>
        /// <param name="expression">A JavaScript expression or function IIFE.</param>
        /// <returns>The raw remote object, or <see langword="null"/>.</returns>
        internal async Task<JsonElement?> EvaluateHandleAsync(string expression)
        {
            FFExecutionContext context = await GetMainContextAsync().ConfigureAwait(false);
            return await context.EvaluateHandleAsync(expression).ConfigureAwait(false);
        }

        /// <summary>
        /// Returns a snapshot of recorded lifecycle event names.
        /// </summary>
        /// <returns>The currently recorded lifecycle events.</returns>
        internal IReadOnlyCollection<string> SnapshotLifecycle()
        {
            lock (_lifecycleLock)
            {
                return new List<string>(_lifecycleEvents);
            }
        }

        /// <summary>
        /// Returns the full HTML content of the page.
        /// </summary>
        internal async Task<string> ContentAsync()
        {
            return await EvaluateFunctionAsync<string>(@"() => {
                let retVal = '';
                if (document.doctype) {
                    retVal = new XMLSerializer().serializeToString(document.doctype);
                }
                if (document.documentElement) {
                    retVal += document.documentElement.outerHTML;
                }
                return retVal;
            }").ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the page HTML content.
        /// </summary>
        /// <param name="html">The HTML content to set.</param>
        internal async Task SetContentAsync(string html)
        {
            await EvaluateFunctionAsync<object>(
                @"html => {
                document.open();
                document.write(html);
                document.close();
            }",
                html).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the viewport size.
        /// </summary>
        /// <param name="width">Viewport width in pixels.</param>
        /// <param name="height">Viewport height in pixels.</param>
        internal Task SetViewportSizeAsync(int width, int height)
            => _session.SendAsync("Page.setViewportSize", new { width, height });

        /// <summary>
        /// Sets extra HTTP headers sent with every request.
        /// </summary>
        /// <param name="headers">Header name/value pairs.</param>
        /// <returns>A task that completes when the protocol command finishes.</returns>
        internal Task SetExtraHttpHeadersAsync(IEnumerable<KeyValuePair<string, string>> headers)
        {
            List<object> list = new List<object>();
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers)
                {
                    list.Add(new { name = header.Key, value = header.Value ?? string.Empty });
                }
            }

            return _session.SendAsync("Network.setExtraHTTPHeaders", new { headers = list });
        }

        /// <summary>
        /// Takes a screenshot of the page.
        /// </summary>
        /// <param name="format">The image format ("png" or "jpeg").</param>
        /// <param name="quality">Quality (0-100, only for jpeg).</param>
        /// <param name="fullPage">Whether to capture the full scrollable page.</param>
        internal async Task<byte[]> ScreenshotAsync(string format = "png", int? quality = null, bool fullPage = false)
        {
            JsonElement? response = await _session.SendAsync("Page.screenshot", new
            {
                mimeType = format == "jpeg" ? "image/jpeg" : "image/png",
                fullPage,
            }).ConfigureAwait(false);

            if (!response.HasValue)
            {
                return [];
            }

            if (!response.Value.TryGetProperty("data", out JsonElement dataEl))
            {
                return [];
            }

            string base64 = dataEl.GetString() ?? string.Empty;
            return Convert.FromBase64String(base64);
        }

        /// <summary>
        /// Registers a route handler for intercepting matching network requests on this page.
        /// </summary>
        /// <param name="pattern">The URL glob pattern.</param>
        /// <param name="handler">The route handler.</param>
        internal async Task RouteAsync(string pattern, Func<FFRoute, Task> handler)
        {
            NetworkManager.AddRoute(pattern, handler);
            await NetworkManager.UpdateInterceptionAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Adds an init script that runs in every new document before page scripts.
        /// </summary>
        /// <param name="script">The JavaScript to run on new documents.</param>
        internal async Task AddInitScriptAsync(string script)
        {
            _initScripts.Add(script);
            await SyncInitScriptsAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Exposes a .NET function as a JavaScript binding on this page.
        /// </summary>
        /// <param name="name">The name to expose the function as in JavaScript.</param>
        /// <param name="callback">The .NET callback invoked when the function is called from JS.</param>
        internal async Task ExposeFunctionAsync(string name, Func<string, Task<object>> callback)
        {
            _exposedFunctions[name] = (name, callback);

            await _session.SendAsync("Page.addBinding", new
            {
                worldName = string.Empty,
                name = BindingChannelName,
            }).ConfigureAwait(false);

            await AddInitScriptAsync($"({BindingInitScript})()").ConfigureAwait(false);
        }

        /// <summary>
        /// Adds a <c>&lt;script&gt;</c> tag to the page.
        /// </summary>
        /// <param name="url">Optional script URL.</param>
        /// <param name="content">Optional inline script content.</param>
        internal async Task<JsonElement?> AddScriptTagAsync(string url = null, string content = null)
        {
            FFExecutionContext context = await GetMainContextAsync().ConfigureAwait(false);

            if (!string.IsNullOrEmpty(url))
            {
                return await context.EvaluateFunctionHandleAsync(
                    "url => new Promise((resolve, reject) => { const s = document.createElement('script'); s.src = url; s.onload = () => resolve(s); s.onerror = reject; document.head.appendChild(s); })",
                    url).ConfigureAwait(false);
            }

            if (!string.IsNullOrEmpty(content))
            {
                return await context.EvaluateFunctionHandleAsync(
                    "content => { const s = document.createElement('script'); s.textContent = content; document.head.appendChild(s); return s; }",
                    content).ConfigureAwait(false);
            }

            throw new ArgumentException("Either url or content must be provided.");
        }

        /// <summary>
        /// Adds a <c>&lt;link rel=stylesheet&gt;</c> or <c>&lt;style&gt;</c> tag to the page.
        /// </summary>
        /// <param name="url">Optional stylesheet URL.</param>
        /// <param name="content">Optional inline CSS content.</param>
        internal async Task<JsonElement?> AddStyleTagAsync(string url = null, string content = null)
        {
            FFExecutionContext context = await GetMainContextAsync().ConfigureAwait(false);

            if (!string.IsNullOrEmpty(url))
            {
                return await context.EvaluateFunctionHandleAsync(
                    "url => new Promise((resolve, reject) => { const l = document.createElement('link'); l.rel='stylesheet'; l.href=url; l.onload=()=>resolve(l); l.onerror=reject; document.head.appendChild(l); })",
                    url).ConfigureAwait(false);
            }

            if (!string.IsNullOrEmpty(content))
            {
                return await context.EvaluateFunctionHandleAsync(
                    "content => { const s = document.createElement('style'); s.textContent=content; document.head.appendChild(s); return s; }",
                    content).ConfigureAwait(false);
            }

            throw new ArgumentException("Either url or content must be provided.");
        }

        /// <summary>
        /// Closes this page.
        /// </summary>
        internal Task ClosePageAsync()
            => _session.SendAsync("Page.close");

        /// <summary>
        /// Called by <see cref="FFBrowser"/> when the page target is detached.
        /// </summary>
        internal void DidClose()
        {
            _session.MessageReceived -= OnSessionEvent;
            Closed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Called by <see cref="FFNetworkManager"/> when a new request is created.
        /// </summary>
        /// <param name="request">The new request.</param>
        internal void OnRequestCreated(FFRequest request)
            => RequestCreated?.Invoke(this, request);

        /// <summary>
        /// Called by <see cref="FFNetworkManager"/> when a response is received.
        /// </summary>
        /// <param name="response">The response.</param>
        internal void OnResponseReceived(FFResponse response)
            => ResponseReceived?.Invoke(this, response);

        /// <summary>
        /// Called by <see cref="FFNetworkManager"/> when a request finishes.
        /// </summary>
        /// <param name="request">The completed request.</param>
        internal void OnRequestFinished(FFRequest request)
            => RequestFinished?.Invoke(this, request);

        /// <summary>
        /// Called by <see cref="FFNetworkManager"/> when a request fails.
        /// </summary>
        /// <param name="request">The failed request.</param>
        internal void OnRequestFailed(FFRequest request)
            => RequestFailed?.Invoke(this, request);

        /// <summary>
        /// Called by <see cref="FFBrowser"/> to notify this page that a popup was opened.
        /// </summary>
        /// <param name="popup">The popup page.</param>
        internal void FirePopupOpened(FFPage popup)
            => PopupOpened?.Invoke(this, popup);

        private void OnSessionEvent(string method, JsonElement? parameters)
        {
            switch (method)
            {
                case "Runtime.executionContextCreated":
                    OnExecutionContextCreated(parameters);
                    break;
                case "Runtime.executionContextDestroyed":
                    OnExecutionContextDestroyed(parameters);
                    break;
                case "Page.eventFired":
                    OnPageEventFired(parameters);
                    break;
                case "Page.dialogOpened":
                    OnDialogOpened(parameters);
                    break;
                case "Page.dialogClosed":
                    DialogClosedInBrowser?.Invoke(this, EventArgs.Empty);
                    break;
                case "Page.bindingCalled":
                    _ = OnBindingCalledAsync(parameters);
                    break;
                case "Page.navigationCommitted":
                    FrameManager.OnNavigationCommitted(parameters);
                    break;
                case "Page.frameAttached":
                    FrameManager.OnFrameAttached(parameters);
                    break;
                case "Page.frameDetached":
                    FrameManager.OnFrameDetached(parameters);
                    break;
            }
        }

        private void OnExecutionContextCreated(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;
            string contextId = payload.TryGetProperty("executionContextId", out JsonElement ctxEl)
                ? ctxEl.GetString() : string.Empty;

            string worldName = string.Empty;
            if (payload.TryGetProperty("auxData", out JsonElement aux))
            {
                if (aux.TryGetProperty("name", out JsonElement nameEl))
                {
                    worldName = nameEl.GetString() ?? string.Empty;
                }

                if (aux.TryGetProperty("frameId", out JsonElement frameEl))
                {
                    RememberMainFrameId(frameEl.GetString(), worldName);
                }
            }
            else if (payload.TryGetProperty("worldName", out JsonElement worldEl))
            {
                worldName = worldEl.GetString() ?? string.Empty;
            }

            FFExecutionContext context = new(_session, contextId);
            _contextIdToContext.TryAdd(contextId, context);

            if (worldName == UtilityWorldName)
            {
                _utilityContext = context;
            }
            else if (string.IsNullOrEmpty(worldName))
            {
                _mainContext = context;
            }
        }

        private void OnExecutionContextDestroyed(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            string contextId = parameters.Value.TryGetProperty("executionContextId", out JsonElement ctxEl)
                ? ctxEl.GetString() : string.Empty;

            _contextIdToContext.TryRemove(contextId, out _);

            if (_mainContext?.ContextId == contextId)
            {
                _mainContext = null;
            }

            if (_utilityContext?.ContextId == contextId)
            {
                _utilityContext = null;
            }
        }

        private void OnPageEventFired(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            string name = parameters.Value.TryGetProperty("name", out JsonElement nameEl)
                ? nameEl.GetString() : string.Empty;

            if (name == "load")
            {
                Load?.Invoke(this, EventArgs.Empty);
                RecordLifecycle("load");
            }
            else if (name == "DOMContentLoaded" || name == "domcontentloaded")
            {
                DOMContentLoaded?.Invoke(this, EventArgs.Empty);
                RecordLifecycle("DOMContentLoaded");
            }
        }

        private void RecordLifecycle(string name)
        {
            bool added;
            lock (_lifecycleLock)
            {
                added = _lifecycleEvents.Add(name);
            }

            if (!added)
            {
                return;
            }

            LifecycleChanged?.Invoke(name);
            if (name == "load")
            {
                int generation = _lifecycleGeneration;
                _ = WaitAndRecordNetworkIdleAsync(generation);
            }
        }

        private async Task WaitAndRecordNetworkIdleAsync(int generation)
        {
            await Task.Delay(500).ConfigureAwait(false);
            lock (_lifecycleLock)
            {
                if (generation != _lifecycleGeneration)
                {
                    return;
                }
            }

            RecordLifecycle("networkidle");
        }

        private void OnDialogOpened(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            FFDialog dialog = new(_session, parameters.Value);
            DialogOpened?.Invoke(this, dialog);
        }

        private async Task OnBindingCalledAsync(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;
            string contextId = payload.TryGetProperty("executionContextId", out JsonElement ctxEl)
                ? ctxEl.GetString() : string.Empty;

            string rawPayload = payload.TryGetProperty("payload", out JsonElement payloadEl)
                ? payloadEl.GetString() : null;

            if (string.IsNullOrEmpty(rawPayload))
            {
                return;
            }

            try
            {
                JsonElement parsed = JsonDocument.Parse(rawPayload).RootElement;
                string name = parsed.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
                string callPayload = parsed.TryGetProperty("payload", out JsonElement callPayloadEl)
                    ? callPayloadEl.GetRawText() : null;

                if (name != null && _exposedFunctions.TryGetValue(name, out (string, Func<string, Task<object>>) fn))
                {
                    object result = await fn.Item2(callPayload).ConfigureAwait(false);
                    string resultJson = result != null ? JsonSerializer.Serialize(result) : "null";

                    await _session.SendAsync("Page.bindingCallResult", new
                    {
                        executionContextId = contextId,
                        result = resultJson,
                    }).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Binding call failed");
            }
        }

        private async Task SyncInitScriptsAsync()
        {
            object[] scripts = new object[_initScripts.Count];
            for (int i = 0; i < _initScripts.Count; i++)
            {
                scripts[i] = new { source = _initScripts[i] };
            }

            await _session.SendAsync("Page.setInitScripts", new { scripts }).ConfigureAwait(false);
        }

        private async Task WaitUntilUrlAsync(string expectedUrl, int timeout)
        {
            using System.Threading.CancellationTokenSource cts = new(timeout);
            while (!string.Equals(_url, expectedUrl, StringComparison.Ordinal))
            {
                cts.Token.ThrowIfCancellationRequested();
                await Task.Delay(10, cts.Token).ConfigureAwait(false);
            }
        }

        private async Task WaitForLifecycleAsync(string name, int timeout)
        {
            using System.Threading.CancellationTokenSource cts = new(timeout);
            while (true)
            {
                lock (_lifecycleLock)
                {
                    if (_lifecycleEvents.Contains(name))
                    {
                        return;
                    }
                }

                cts.Token.ThrowIfCancellationRequested();
                await Task.Delay(10, cts.Token).ConfigureAwait(false);
            }
        }

        private async Task<string> GetMainFrameIdAsync()
        {
            if (!string.IsNullOrEmpty(_mainFrameId))
            {
                return _mainFrameId;
            }

            using System.Threading.CancellationTokenSource cts = new(5_000);
            while (string.IsNullOrEmpty(_mainFrameId))
            {
                cts.Token.ThrowIfCancellationRequested();
                await Task.Delay(10, cts.Token).ConfigureAwait(false);
            }

            return _mainFrameId;
        }

        private async Task<FFExecutionContext> GetMainContextAsync()
        {
            if (_mainContext != null)
            {
                return _mainContext;
            }

            // Wait briefly for the context to be created (up to 5 s).
            using System.Threading.CancellationTokenSource cts = new(5_000);
            while (_mainContext == null)
            {
                cts.Token.ThrowIfCancellationRequested();
                await Task.Delay(10, cts.Token).ConfigureAwait(false);
            }

            return _mainContext;
        }
    }
}
