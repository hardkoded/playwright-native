/*
 * Copyright (c) 2020 Darío Kondratiuk
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
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Firefox
{
    /// <summary>
    /// Public <see cref="IBrowserContext"/> wrapping <see cref="FFBrowserContext"/>.
    /// </summary>
    internal sealed partial class FirefoxBrowserContext : IBrowserContext, IHasStrictSelectors, IHasDefaultTimeouts, IHasBrowserContextExtras, IHasExtraHttpHeaders, IHasIgnoreHttpsErrors, IHasClientCertificates, IDialogHost
    {
        private readonly FFBrowserContext _ctx;
        private readonly IBrowser _browser;
        private readonly List<string> _initScripts = new();
        private float _defaultTimeout = 30_000;
        private float _defaultNavigationTimeout = 30_000;
        private Dictionary<string, string> _extraHttpHeaders;
        private bool _closed;
        private IReadOnlyList<ClientCertificate> _clientCertificates;

        internal FirefoxBrowserContext(FFBrowserContext ctx, IBrowser browser)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _browser = browser ?? throw new ArgumentNullException(nameof(browser));
            Clock = new Clock(this);
            Credentials = new ContextCredentials(this);
            Tracing = new EmptyTracing(this);
        }

        /// <inheritdoc/>
        public event EventHandler<IPage> Page;

        /// <inheritdoc/>
        public event EventHandler<IBrowserContext> Close;

        /// <inheritdoc/>
        public event EventHandler<IRequest> Request;

        /// <inheritdoc/>
        public event EventHandler<IResponse> Response;

        /// <inheritdoc/>
        public event EventHandler<IRequest> RequestFailed;

        /// <inheritdoc/>
        public event EventHandler<IRequest> RequestFinished;

        /// <inheritdoc/>
#pragma warning disable CS0067 // Service workers are Chromium-only.
        public event EventHandler<IWorker> ServiceWorker;
#pragma warning restore CS0067

        /// <inheritdoc/>
#pragma warning disable CS0067 // Console forwarding is implemented on Chromium and WebKit.
        public event EventHandler<IConsoleMessage> Console;
#pragma warning restore CS0067

        /// <inheritdoc/>
        public event EventHandler<IDownload> Download;

        /// <inheritdoc/>
        public event EventHandler<IDialog> Dialog;

        /// <inheritdoc/>
        public event EventHandler<IDialog> DialogClosed;

        /// <inheritdoc/>
        public event EventHandler<IPage> PageClose;

        /// <inheritdoc/>
        public event EventHandler<IPage> PageLoad;

        /// <inheritdoc/>
        public event EventHandler<IFrame> FrameAttached;

        /// <inheritdoc/>
        public event EventHandler<IFrame> FrameDetached;

        /// <inheritdoc/>
        public event EventHandler<IFrame> FrameNavigated;

        /// <inheritdoc/>
        public event EventHandler<IWebError> WebError;

        /// <inheritdoc/>
        public event EventHandler<IPage> BackgroundPage;

        /// <inheritdoc/>
        public IBrowser Browser => _browser;

        /// <inheritdoc/>
        public bool IsClosed => _closed;

        /// <inheritdoc/>
        public bool StrictSelectors { get; internal set; }

        /// <inheritdoc/>
        public ITracing Tracing { get; }

        /// <inheritdoc/>
        public IClock Clock { get; }

        /// <inheritdoc/>
        public ICredentials Credentials { get; }

        /// <inheritdoc/>
        public IAPIRequestContext APIRequest => APIRequestContext.For(this);

        /// <inheritdoc/>
        public IDebugger Debugger { get; } = new EmptyDebugger();

        /// <inheritdoc/>
        public IReadOnlyCollection<IWorker> ServiceWorkers { get; } = Array.Empty<IWorker>();

        /// <inheritdoc/>
        public IReadOnlyList<IPage> BackgroundPages { get; } = Array.Empty<IPage>();

        /// <inheritdoc/>
        public IReadOnlyList<IPage> Pages
        {
            get
            {
                List<IPage> result = new();
                foreach (FFPage p in _ctx.Pages)
                {
                    result.Add(new FirefoxPage(p, this));
                }

                return result;
            }
        }

        /// <inheritdoc/>
        public float DefaultNavigationTimeout
        {
            get => _defaultNavigationTimeout;
            set => _defaultNavigationTimeout = value;
        }

        /// <inheritdoc/>
        public float DefaultTimeout
        {
            get => _defaultTimeout;
            set => _defaultTimeout = value;
        }

        /// <inheritdoc/>
        IReadOnlyDictionary<string, string> IHasExtraHttpHeaders.ExtraHttpHeaders => _extraHttpHeaders;

        /// <inheritdoc/>
        bool IHasIgnoreHttpsErrors.IgnoreHttpsErrors => false;

        /// <inheritdoc/>
        IReadOnlyList<ClientCertificate> IHasClientCertificates.ClientCertificates => _clientCertificates;

        /// <summary>
        /// Official <c>browser.newPage()</c> marks the context so a second
        /// <see cref="NewPageAsync"/> throws.
        /// </summary>
        internal bool OwnedByBrowserNewPage { get; set; }

        /// <inheritdoc/>
        public bool HasDialogListeners() => Dialog != null;

        /// <inheritdoc/>
        public void RaiseDialog(IDialog dialog) => Dialog?.Invoke(this, dialog);

        public async Task<IAsyncDisposable> AddInitScriptAsync(string script = null, string scriptPath = null, object arg = default)
        {
            if (!string.IsNullOrEmpty(scriptPath))
            {
                script = PathIo.ReadText(scriptPath);
            }

            if (arg != null && !string.IsNullOrEmpty(script))
            {
                script = EvaluateWithArg.Wrap(script, arg);
            }

            if (string.IsNullOrEmpty(script))
            {
                throw new ArgumentException("Either script or scriptPath must be provided.", nameof(script));
            }

            _initScripts.Add(script);
            foreach (IPage page in Pages)
            {
                await page.AddInitScriptAsync(script).ConfigureAwait(false);
            }

            return AddInitScriptHelper.CreateDisposable(() => Task.CompletedTask);
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> AddInitScriptAsync(string script, object arg, bool exposeFunctions)
            => throw NotImplementedHelper.ForMethod(nameof(AddInitScriptAsync));

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeBindingAsync(string name, Action callback, bool? handle = default)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeBindingAsync));

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeBindingAsync(string name, Func<BindingSource, IJSHandle, object> callback)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeBindingAsync) + " with handle");

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeBindingAsync<TResult>(string name, Func<TResult> callback)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeBindingAsync));

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeBindingAsync<T1, T2, TResult>(string name, Func<BindingSource, T1, T2, TResult> callback)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeBindingAsync));

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync(string name, Action callback)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeFunctionAsync));

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync<T>(string name, Action<T> callback)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeFunctionAsync));

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync<TResult>(string name, Func<TResult> callback)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeFunctionAsync));

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync<T, TResult>(string name, Func<T, TResult> callback)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeFunctionAsync));

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync<T1, T2, TResult>(string name, Func<T1, T2, TResult> callback)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeFunctionAsync));

        /// <inheritdoc/>
        public Task AddCookiesAsync(IEnumerable<Cookie> cookies)
            => throw NotImplementedHelper.ForMethod(nameof(AddCookiesAsync));

        /// <inheritdoc/>
        public async Task CloseAsync(string reason = default)
        {
            _ = reason;
            if (_closed)
            {
                return;
            }

            await HarRecorder.FlushAsync(this).ConfigureAwait(false);
            await VideoRecorder.FlushAsync(this).ConfigureAwait(false);
            _closed = true;
            await _ctx.CloseAsync().ConfigureAwait(false);
            Close?.Invoke(this, this);
        }

        /// <inheritdoc/>
        public Task ClearCookiesAsync()
            => throw NotImplementedHelper.ForMethod(nameof(ClearCookiesAsync));

        /// <inheritdoc/>
        public Task<IReadOnlyList<BrowserContextCookiesResult>> GetCookiesAsync(IEnumerable<string> urls = default)
            => throw NotImplementedHelper.ForMethod(nameof(GetCookiesAsync));

        /// <inheritdoc/>
        public Task<IReadOnlyList<BrowserContextCookiesResult>> CookiesAsync()
            => GetCookiesAsync();

        /// <inheritdoc/>
        public Task<IReadOnlyList<BrowserContextCookiesResult>> CookiesAsync(IEnumerable<string> urls)
            => GetCookiesAsync(urls);

        /// <inheritdoc/>
        public Task<string> StorageStateAsync(string path = default, bool? indexedDB = default, bool? credentials = default)
            => throw NotImplementedHelper.ForMethod(nameof(StorageStateAsync));

        /// <inheritdoc/>
        public Task GrantPermissionsAsync(IEnumerable<string> permissions, string origin = default)
            => throw NotImplementedHelper.ForMethod(nameof(GrantPermissionsAsync));

        /// <inheritdoc/>
        public Task ClearPermissionsAsync()
            => throw NotImplementedHelper.ForMethod(nameof(ClearPermissionsAsync));

        /// <inheritdoc/>
        public Task SetGeolocationAsync(Geolocation geolocation)
            => throw NotImplementedHelper.ForMethod(nameof(SetGeolocationAsync));

        /// <inheritdoc/>
        public Task SetOfflineAsync(bool offline)
            => throw NotImplementedHelper.ForMethod(nameof(SetOfflineAsync));

        /// <inheritdoc/>
        public Task SetHttpCredentialsAsync(IEnumerable<HttpCredentials> httpCredentials)
            => throw NotImplementedHelper.ForMethod(nameof(SetHttpCredentialsAsync));

        /// <inheritdoc/>
        public async Task<IPage> NewPageAsync()
        {
            BrowserNewPageOwner.ThrowIfOwned(OwnedByBrowserNewPage);

            FFPage page = await _ctx.NewPageAsync().ConfigureAwait(false);
            await page.InitializedTask.ConfigureAwait(false);
            FirefoxPage instance = new FirefoxPage(page, this);
            await ApplyContextChromeAsync(instance).ConfigureAwait(false);
            AttachPageNetwork(instance);
            Page?.Invoke(this, instance);
            return instance;
        }

        /// <inheritdoc/>
        public Task<ICDPSession> NewCDPSessionAsync(IPage page)
            => throw new PlaywrightNativeException("CDP sessions are only supported in Chromium.");

        /// <inheritdoc/>
        public Task<T> WaitForEventAsync<T>(PlaywrightEvent<T> contextEvent, Func<T, bool> predicate = null, float? timeout = null)
            => ContextWaitForEventHelper.WaitAsync(this, contextEvent, predicate, timeout);

        /// <inheritdoc/>
        public Task RouteAsync(string urlString, Action<IRoute> handler, int? times = default)
            => throw NotImplementedHelper.ForMethod(nameof(RouteAsync));

        /// <inheritdoc/>
        public Task RouteAsync(string urlString, Func<IRoute, Task> handler, int? times = default)
            => throw NotImplementedHelper.ForMethod(nameof(RouteAsync));

        /// <inheritdoc/>
        public Task RouteAsync(Regex urlRegex, Action<IRoute> handler, int? times = default)
            => throw NotImplementedHelper.ForMethod(nameof(RouteAsync));

        /// <inheritdoc/>
        public Task RouteAsync(Regex urlRegex, Func<IRoute, Task> handler, int? times = default)
            => throw NotImplementedHelper.ForMethod(nameof(RouteAsync));

        /// <inheritdoc/>
        public Task RouteAsync(Func<string, bool> urlFunc, Action<IRoute> handler, int? times = default)
            => throw NotImplementedHelper.ForMethod(nameof(RouteAsync));

        /// <inheritdoc/>
        public Task RouteAsync(Func<string, bool> urlFunc, Func<IRoute, Task> handler, int? times = default)
            => throw NotImplementedHelper.ForMethod(nameof(RouteAsync));

        /// <inheritdoc/>
        public Task RouteAsync(string urlString, Regex urlRegex, Func<string, bool> urlFunc, Action<IRoute> handler, int? times = default)
            => throw NotImplementedHelper.ForMethod(nameof(RouteAsync));

        /// <summary>Not yet implemented.</summary>
        public Task SetDefaultNavigationTimeoutAsync(float timeout)
        {
            DefaultNavigationTimeout = timeout;
            return Task.CompletedTask;
        }

        /// <summary>Sets <see cref="DefaultTimeout"/>.</summary>
        public Task SetDefaultTimeoutAsync(float timeout)
        {
            DefaultTimeout = timeout;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task SetExtraHttpHeadersAsync(IEnumerable<KeyValuePair<string, string>> headers)
        {
            _extraHttpHeaders = ExtraHttpHeaders.ToMap(headers);
            foreach (IPage page in Pages)
            {
                await page.SetExtraHttpHeadersAsync(_extraHttpHeaders).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public Task UnrouteAsync(string urlString, Action<IRoute> handler = null, UnrouteBehavior behavior = default)
            => throw NotImplementedHelper.ForMethod(nameof(UnrouteAsync));

        /// <inheritdoc/>
        public Task UnrouteAsync(string urlString, Func<IRoute, Task> handler, UnrouteBehavior behavior = default)
            => throw NotImplementedHelper.ForMethod(nameof(UnrouteAsync));

        /// <inheritdoc/>
        public Task UnrouteAsync(Regex urlRegex, Action<IRoute> handler = null, UnrouteBehavior behavior = default)
            => throw NotImplementedHelper.ForMethod(nameof(UnrouteAsync));

        /// <inheritdoc/>
        public Task UnrouteAsync(Regex urlRegex, Func<IRoute, Task> handler, UnrouteBehavior behavior = default)
            => throw NotImplementedHelper.ForMethod(nameof(UnrouteAsync));

        /// <inheritdoc/>
        public Task UnrouteAsync(Func<string, bool> urlFunc, Action<IRoute> handler = null, UnrouteBehavior behavior = default)
            => throw NotImplementedHelper.ForMethod(nameof(UnrouteAsync));

        /// <inheritdoc/>
        public Task UnrouteAsync(Func<string, bool> urlFunc, Func<IRoute, Task> handler, UnrouteBehavior behavior = default)
            => throw NotImplementedHelper.ForMethod(nameof(UnrouteAsync));

        /// <inheritdoc/>
        public Task UnrouteAsync(string urlString, Regex urlRegex, Func<string, bool> urlFunc, Action<IRoute> handler = default, UnrouteBehavior behavior = default)
            => throw NotImplementedHelper.ForMethod(nameof(UnrouteAsync));

        /// <inheritdoc/>
        public Task UnrouteAllAsync(UnrouteBehavior behavior = default)
            => throw NotImplementedHelper.ForMethod(nameof(UnrouteAllAsync));

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            await CloseAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Stores official <c>clientCertificates</c> for APIRequest.
        /// </summary>
        /// <param name="certificates">Configured certificates, or <see langword="null"/>.</param>
        internal void AttachClientCertificates(IEnumerable<ClientCertificate> certificates)
        {
            _clientCertificates = ClientCertificateHelper.Snapshot(certificates);
        }

        private void AttachPageNetwork(IPage page)
        {
            page.Request += (_, request) => Request?.Invoke(this, request);
            page.Response += (_, response) => Response?.Invoke(this, response);
            page.RequestFailed += (_, request) => RequestFailed?.Invoke(this, request);
            page.RequestFinished += (_, request) => RequestFinished?.Invoke(this, request);
            page.Download += (_, download) => Download?.Invoke(this, download);
            if (page is IHasPageExtras extras)
            {
                extras.DialogClosed += (_, dialog) => DialogClosed?.Invoke(this, dialog);
            }

            page.Close += (_, closed) => PageClose?.Invoke(this, closed);
            page.Load += (_, loaded) => PageLoad?.Invoke(this, loaded);
            page.FrameAttached += (_, frame) => FrameAttached?.Invoke(this, frame);
            page.FrameDetached += (_, frame) => FrameDetached?.Invoke(this, frame);
            page.FrameNavigated += (_, frame) => FrameNavigated?.Invoke(this, frame);
            page.PageError += (_, error) => WebError?.Invoke(
                this,
                new WebError(page, error.ToString(), (page as IHasLastPageErrorLocation)?.LastPageErrorLocation));
        }

        private async Task ApplyContextChromeAsync(IPage page)
        {
            foreach (string script in _initScripts)
            {
                await page.AddInitScriptAsync(script).ConfigureAwait(false);
            }

            if (_extraHttpHeaders != null)
            {
                await page.SetExtraHttpHeadersAsync(_extraHttpHeaders).ConfigureAwait(false);
            }

            if (Credentials is ContextCredentials credentials)
            {
                await credentials.AttachIfInstalledAsync(page).ConfigureAwait(false);
            }
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task<IAsyncDisposable> IBrowserContext.AddInitScriptAsync(string script, string scriptPath) => AddInitScriptAsync(script, scriptPath);

        Task IBrowserContext.ClearCookiesAsync(BrowserContextClearCookiesOptions options) => CookieClearFilter.ClearAsync(this, options, ClearCookiesAsync);

        Task IBrowserContext.CloseAsync(BrowserContextCloseOptions options) => CloseAsync(options?.Reason);

        Task<IReadOnlyList<BrowserContextCookiesResult>> IBrowserContext.CookiesAsync(string urls) => string.IsNullOrEmpty(urls) ? CookiesAsync() : CookiesAsync(new[] { urls });

        Task<IAsyncDisposable> IBrowserContext.ExposeBindingAsync(string name, Action callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IBrowserContext.ExposeBindingAsync(string name, Action<BindingSource> callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IBrowserContext.ExposeBindingAsync<T>(string name, Action<BindingSource, T> callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IBrowserContext.ExposeBindingAsync<TResult>(string name, Func<BindingSource, TResult> callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IBrowserContext.ExposeBindingAsync<T, TResult>(string name, Func<BindingSource, T, TResult> callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IBrowserContext.ExposeBindingAsync<T1, T2, T3, TResult>(string name, Func<BindingSource, T1, T2, T3, TResult> callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IBrowserContext.ExposeBindingAsync<T1, T2, T3, T4, TResult>(string name, Func<BindingSource, T1, T2, T3, T4, TResult> callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IBrowserContext.ExposeFunctionAsync<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, TResult> callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IBrowserContext.ExposeFunctionAsync<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, TResult> callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task IBrowserContext.GrantPermissionsAsync(IEnumerable<string> permissions, BrowserContextGrantPermissionsOptions options) => GrantPermissionsAsync(permissions, options?.Origin);

        Task<ICDPSession> IBrowserContext.NewCDPSessionAsync(IFrame page) => Task.FromResult<ICDPSession>(default!);

        async Task<IAsyncDisposable> IBrowserContext.RouteAsync(string url, Action<IRoute> handler, BrowserContextRouteOptions options)
        {
            await RouteAsync(url, handler, options?.Times).ConfigureAwait(false);
            return NoopContextDisposable.Instance;
        }

        async Task<IAsyncDisposable> IBrowserContext.RouteAsync(Regex url, Action<IRoute> handler, BrowserContextRouteOptions options)
        {
            await RouteAsync(url, handler, options?.Times).ConfigureAwait(false);
            return NoopContextDisposable.Instance;
        }

        async Task<IAsyncDisposable> IBrowserContext.RouteAsync(Func<string, bool> url, Action<IRoute> handler, BrowserContextRouteOptions options)
        {
            await RouteAsync(url, handler, options?.Times).ConfigureAwait(false);
            return NoopContextDisposable.Instance;
        }

        async Task<IAsyncDisposable> IBrowserContext.RouteAsync(string url, Func<IRoute, Task> handler, BrowserContextRouteOptions options)
        {
            await RouteAsync(url, handler, options?.Times).ConfigureAwait(false);
            return NoopContextDisposable.Instance;
        }

        async Task<IAsyncDisposable> IBrowserContext.RouteAsync(Regex url, Func<IRoute, Task> handler, BrowserContextRouteOptions options)
        {
            await RouteAsync(url, handler, options?.Times).ConfigureAwait(false);
            return NoopContextDisposable.Instance;
        }

        async Task<IAsyncDisposable> IBrowserContext.RouteAsync(Func<string, bool> url, Func<IRoute, Task> handler, BrowserContextRouteOptions options)
        {
            await RouteAsync(url, handler, options?.Times).ConfigureAwait(false);
            return NoopContextDisposable.Instance;
        }

        Task IBrowserContext.RouteFromHARAsync(string har, BrowserContextRouteFromHAROptions options) => Task.CompletedTask;

        Task IBrowserContext.RouteWebSocketAsync(string url, Action<IWebSocketRoute> handler) => Task.CompletedTask;

        Task IBrowserContext.RouteWebSocketAsync(Regex url, Action<IWebSocketRoute> handler) => Task.CompletedTask;

        Task IBrowserContext.RouteWebSocketAsync(Func<string, bool> url, Action<IWebSocketRoute> handler) => Task.CompletedTask;

        Task<IConsoleMessage> IBrowserContext.RunAndWaitForConsoleMessageAsync(Func<Task> action, BrowserContextRunAndWaitForConsoleMessageOptions options)
            => RunAndWaitInternalAsync(
                action,
                WaitForEventAsync(BrowserContextEvent.Console, options?.Predicate, options?.Timeout));

        Task<IPage> IBrowserContext.RunAndWaitForPageAsync(Func<Task> action, BrowserContextRunAndWaitForPageOptions options)
            => RunAndWaitInternalAsync(
                action,
                WaitForEventAsync(BrowserContextEvent.Page, options?.Predicate, options?.Timeout));

        void IBrowserContext.SetDefaultNavigationTimeout(float timeout) => _ = SetDefaultNavigationTimeoutAsync(timeout);

        void IBrowserContext.SetDefaultTimeout(float timeout) => _ = SetDefaultTimeoutAsync(timeout);

        Task IBrowserContext.SetExtraHTTPHeadersAsync(IEnumerable<KeyValuePair<string, string>> headers) => SetExtraHttpHeadersAsync(headers);

        Task IBrowserContext.SetStorageStateAsync(string storageStatePath)
        {
            string value = storageStatePath;
            bool inlineJson = !string.IsNullOrEmpty(value)
                && value.TrimStart().StartsWith('{');
            return StorageStateHelper.ApplyAsync(
                this,
                inlineJson ? value : null,
                inlineJson ? null : value,
                replaceExisting: true);
        }

        Task<string> IBrowserContext.StorageStateAsync(BrowserContextStorageStateOptions options) => StorageStateAsync(options?.Path, options?.IndexedDB, options?.Credentials);

        Task IBrowserContext.UnrouteAllAsync(BrowserContextUnrouteAllOptions options) => Task.CompletedTask;

        Task IBrowserContext.UnrouteAsync(string url, Action<IRoute> handler) => Task.CompletedTask;

        Task IBrowserContext.UnrouteAsync(Regex url, Action<IRoute> handler) => Task.CompletedTask;

        Task IBrowserContext.UnrouteAsync(Func<string, bool> url, Action<IRoute> handler) => Task.CompletedTask;

        Task IBrowserContext.UnrouteAsync(string url, Func<IRoute, Task> handler) => Task.CompletedTask;

        Task IBrowserContext.UnrouteAsync(Regex url, Func<IRoute, Task> handler) => Task.CompletedTask;

        Task IBrowserContext.UnrouteAsync(Func<string, bool> url, Func<IRoute, Task> handler) => Task.CompletedTask;

        Task<IConsoleMessage> IBrowserContext.WaitForConsoleMessageAsync(BrowserContextWaitForConsoleMessageOptions options)
            => WaitForEventAsync(BrowserContextEvent.Console, options?.Predicate, options?.Timeout);

        Task<IPage> IBrowserContext.WaitForPageAsync(BrowserContextWaitForPageOptions options)
            => WaitForEventAsync(BrowserContextEvent.Page, options?.Predicate, options?.Timeout);

        private static async Task<T> RunAndWaitInternalAsync<T>(Func<Task> action, Task<T> waitTask)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            Task actionTask = action();
            T result = await waitTask.ConfigureAwait(false);
            await actionTask.ConfigureAwait(false);
            return result;
        }

        private sealed class NoopContextDisposable : IAsyncDisposable
        {
            internal static readonly NoopContextDisposable Instance = new();

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
