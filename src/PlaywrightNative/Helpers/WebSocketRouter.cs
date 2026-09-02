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
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Installs <see cref="IPage.RouteWebSocketAsync(string, Action{IWebSocketRoute})"/>
    /// handlers and dispatches page-side socket events.
    /// </summary>
    internal static class WebSocketRouter
    {
        private const string BindingName = "__pwWebSocketRoute";

        private static readonly ConditionalWeakTable<IPage, PageState> _pages = new ConditionalWeakTable<IPage, PageState>();
        private static readonly ConditionalWeakTable<IBrowserContext, ContextState> _contexts = new ConditionalWeakTable<IBrowserContext, ContextState>();

        /// <summary>
        /// Registers a page-level WebSocket route.
        /// </summary>
        /// <param name="page">The page to intercept.</param>
        /// <param name="url">Glob or path pattern. Empty matches every socket.</param>
        /// <param name="handler">Route handler.</param>
        /// <returns>A task that completes when interception is installed.</returns>
        internal static Task InstallAsync(IPage page, string url, Action<IWebSocketRoute> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            return InstallAsync(
                page,
                url,
                ws =>
                {
                    handler(ws);
                    return Task.CompletedTask;
                },
                handler);
        }

        /// <summary>
        /// Registers a page-level WebSocket route with an async handler.
        /// </summary>
        /// <param name="page">The page to intercept.</param>
        /// <param name="url">Glob or path pattern. Empty matches every socket.</param>
        /// <param name="handler">Async route handler.</param>
        /// <param name="identity">Optional handler identity used when unrouting.</param>
        /// <returns>A task that completes when interception is installed.</returns>
        internal static async Task InstallAsync(IPage page, string url, Func<IWebSocketRoute, Task> handler, object identity = null)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            PageState state = GetPage(page);
            lock (state.Lock)
            {
                state.Handlers.Add(new RouteEntry(url ?? string.Empty, handler, identity ?? handler));
            }

            IBrowserContext context = page.Context;
            if (context != null)
            {
                await EnsureContextBindingAsync(context).ConfigureAwait(false);
                await EnsurePageTaggedAsync(page).ConfigureAwait(false);
                return;
            }

            await EnsurePageBindingAsync(page).ConfigureAwait(false);
        }

        /// <summary>
        /// Registers a page-level WebSocket route whose URL matches <paramref name="url"/>.
        /// </summary>
        /// <param name="page">The page to intercept.</param>
        /// <param name="url">A regular expression to match the socket URL.</param>
        /// <param name="handler">Route handler.</param>
        /// <returns>A task that completes when interception is installed.</returns>
        internal static Task InstallAsync(IPage page, Regex url, Action<IWebSocketRoute> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            return InstallAsync(
                page,
                url,
                ws =>
                {
                    handler(ws);
                    return Task.CompletedTask;
                },
                handler);
        }

        /// <summary>
        /// Registers a page-level WebSocket route whose URL matches <paramref name="url"/>.
        /// </summary>
        /// <param name="page">The page to intercept.</param>
        /// <param name="url">A regular expression to match the socket URL.</param>
        /// <param name="handler">Async route handler.</param>
        /// <param name="identity">Optional handler identity used when unrouting.</param>
        /// <returns>A task that completes when interception is installed.</returns>
        internal static async Task InstallAsync(IPage page, Regex url, Func<IWebSocketRoute, Task> handler, object identity = null)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            PageState state = GetPage(page);
            lock (state.Lock)
            {
                state.Handlers.Add(new RouteEntry(url, handler, identity ?? handler));
            }

            IBrowserContext context = page.Context;
            if (context != null)
            {
                await EnsureContextBindingAsync(context).ConfigureAwait(false);
                await EnsurePageTaggedAsync(page).ConfigureAwait(false);
                return;
            }

            await EnsurePageBindingAsync(page).ConfigureAwait(false);
        }

        /// <summary>
        /// Registers a page-level WebSocket route whose URL matches <paramref name="url"/>.
        /// </summary>
        /// <param name="page">The page to intercept.</param>
        /// <param name="url">A predicate receiving the socket URL.</param>
        /// <param name="handler">Route handler.</param>
        /// <returns>A task that completes when interception is installed.</returns>
        internal static Task InstallAsync(IPage page, Func<string, bool> url, Action<IWebSocketRoute> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            return InstallAsync(
                page,
                url,
                ws =>
                {
                    handler(ws);
                    return Task.CompletedTask;
                },
                handler);
        }

        /// <summary>
        /// Registers a page-level WebSocket route whose URL matches <paramref name="url"/>.
        /// </summary>
        /// <param name="page">The page to intercept.</param>
        /// <param name="url">A predicate receiving the socket URL.</param>
        /// <param name="handler">Async route handler.</param>
        /// <param name="identity">Optional handler identity used when unrouting.</param>
        /// <returns>A task that completes when interception is installed.</returns>
        internal static async Task InstallAsync(IPage page, Func<string, bool> url, Func<IWebSocketRoute, Task> handler, object identity = null)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            PageState state = GetPage(page);
            lock (state.Lock)
            {
                state.Handlers.Add(new RouteEntry(url, handler, identity ?? handler));
            }

            IBrowserContext context = page.Context;
            if (context != null)
            {
                await EnsureContextBindingAsync(context).ConfigureAwait(false);
                await EnsurePageTaggedAsync(page).ConfigureAwait(false);
                return;
            }

            await EnsurePageBindingAsync(page).ConfigureAwait(false);
        }

        /// <summary>
        /// Registers a context-level WebSocket route.
        /// </summary>
        /// <param name="context">The context to intercept.</param>
        /// <param name="url">Glob or path pattern. Empty matches every socket.</param>
        /// <param name="handler">Route handler.</param>
        /// <returns>A task that completes when interception is installed.</returns>
        internal static Task InstallAsync(IBrowserContext context, string url, Action<IWebSocketRoute> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            return InstallAsync(
                context,
                url,
                ws =>
                {
                    handler(ws);
                    return Task.CompletedTask;
                },
                handler);
        }

        /// <summary>
        /// Registers a context-level WebSocket route with an async handler.
        /// </summary>
        /// <param name="context">The context to intercept.</param>
        /// <param name="url">Glob or path pattern. Empty matches every socket.</param>
        /// <param name="handler">Async route handler.</param>
        /// <param name="identity">Optional handler identity used when unrouting.</param>
        /// <returns>A task that completes when interception is installed.</returns>
        internal static async Task InstallAsync(IBrowserContext context, string url, Func<IWebSocketRoute, Task> handler, object identity = null)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            ContextState state = GetContext(context);
            lock (state.Lock)
            {
                state.Handlers.Add(new RouteEntry(url ?? string.Empty, handler, identity ?? handler));
            }

            await EnsureContextBindingAsync(context).ConfigureAwait(false);
            foreach (IPage page in context.Pages)
            {
                await EnsurePageTaggedAsync(page).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Registers a context-level WebSocket route whose URL matches <paramref name="url"/>.
        /// </summary>
        /// <param name="context">The context to intercept.</param>
        /// <param name="url">A regular expression to match the socket URL.</param>
        /// <param name="handler">Route handler.</param>
        /// <returns>A task that completes when interception is installed.</returns>
        internal static Task InstallAsync(IBrowserContext context, Regex url, Action<IWebSocketRoute> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            return InstallAsync(
                context,
                url,
                ws =>
                {
                    handler(ws);
                    return Task.CompletedTask;
                },
                handler);
        }

        /// <summary>
        /// Registers a context-level WebSocket route whose URL matches <paramref name="url"/>.
        /// </summary>
        /// <param name="context">The context to intercept.</param>
        /// <param name="url">A regular expression to match the socket URL.</param>
        /// <param name="handler">Async route handler.</param>
        /// <param name="identity">Optional handler identity used when unrouting.</param>
        /// <returns>A task that completes when interception is installed.</returns>
        internal static async Task InstallAsync(IBrowserContext context, Regex url, Func<IWebSocketRoute, Task> handler, object identity = null)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            ContextState state = GetContext(context);
            lock (state.Lock)
            {
                state.Handlers.Add(new RouteEntry(url, handler, identity ?? handler));
            }

            await EnsureContextBindingAsync(context).ConfigureAwait(false);
            foreach (IPage page in context.Pages)
            {
                await EnsurePageTaggedAsync(page).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Registers a context-level WebSocket route whose URL matches <paramref name="url"/>.
        /// </summary>
        /// <param name="context">The context to intercept.</param>
        /// <param name="url">A predicate receiving the socket URL.</param>
        /// <param name="handler">Route handler.</param>
        /// <returns>A task that completes when interception is installed.</returns>
        internal static Task InstallAsync(IBrowserContext context, Func<string, bool> url, Action<IWebSocketRoute> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            return InstallAsync(
                context,
                url,
                ws =>
                {
                    handler(ws);
                    return Task.CompletedTask;
                },
                handler);
        }

        /// <summary>
        /// Registers a context-level WebSocket route whose URL matches <paramref name="url"/>.
        /// </summary>
        /// <param name="context">The context to intercept.</param>
        /// <param name="url">A predicate receiving the socket URL.</param>
        /// <param name="handler">Async route handler.</param>
        /// <param name="identity">Optional handler identity used when unrouting.</param>
        /// <returns>A task that completes when interception is installed.</returns>
        internal static async Task InstallAsync(IBrowserContext context, Func<string, bool> url, Func<IWebSocketRoute, Task> handler, object identity = null)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            ContextState state = GetContext(context);
            lock (state.Lock)
            {
                state.Handlers.Add(new RouteEntry(url, handler, identity ?? handler));
            }

            await EnsureContextBindingAsync(context).ConfigureAwait(false);
            foreach (IPage page in context.Pages)
            {
                await EnsurePageTaggedAsync(page).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Removes a page-level WebSocket route.
        /// </summary>
        /// <param name="page">The page that owns the route.</param>
        /// <param name="url">Pattern passed to <see cref="InstallAsync(IPage, string, Action{IWebSocketRoute})"/>.</param>
        /// <param name="handler">
        /// Optional handler identity. When omitted, the last matching pattern is removed.
        /// </param>
        /// <returns>A completed task.</returns>
        internal static Task UnrouteAsync(IPage page, string url, Action<IWebSocketRoute> handler)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            PageState state = GetPage(page);
            lock (state.Lock)
            {
                RemoveHandler(state.Handlers, url, handler);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Removes a page-level WebSocket route registered with a regex.
        /// </summary>
        /// <param name="page">The page that owns the route.</param>
        /// <param name="url">Pattern passed to <see cref="InstallAsync(IPage, Regex, Action{IWebSocketRoute})"/>.</param>
        /// <param name="handler">
        /// Optional handler identity. When omitted, the last matching pattern is removed.
        /// </param>
        /// <returns>A completed task.</returns>
        internal static Task UnrouteAsync(IPage page, Regex url, Action<IWebSocketRoute> handler)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            PageState state = GetPage(page);
            lock (state.Lock)
            {
                RemoveHandler(state.Handlers, url, handler);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Removes a page-level WebSocket route registered with an async handler.
        /// </summary>
        /// <param name="page">The page that owns the route.</param>
        /// <param name="url">Pattern passed to <see cref="InstallAsync(IPage, string, Func{IWebSocketRoute, Task}, object)"/>.</param>
        /// <param name="handler">Handler identity. When null, the last matching pattern is removed.</param>
        /// <returns>A completed task.</returns>
        internal static Task UnrouteAsync(IPage page, string url, Func<IWebSocketRoute, Task> handler)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            PageState state = GetPage(page);
            lock (state.Lock)
            {
                RemoveHandler(state.Handlers, url, handler);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Removes a page-level WebSocket route registered with a regex and an async handler.
        /// </summary>
        /// <param name="page">The page that owns the route.</param>
        /// <param name="url">Pattern passed to <see cref="InstallAsync(IPage, Regex, Func{IWebSocketRoute, Task}, object)"/>.</param>
        /// <param name="handler">Handler identity. When null, the last matching pattern is removed.</param>
        /// <returns>A completed task.</returns>
        internal static Task UnrouteAsync(IPage page, Regex url, Func<IWebSocketRoute, Task> handler)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            PageState state = GetPage(page);
            lock (state.Lock)
            {
                RemoveHandler(state.Handlers, url, handler);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Removes a page-level WebSocket route registered with a predicate.
        /// </summary>
        /// <param name="page">The page that owns the route.</param>
        /// <param name="url">Predicate passed to <see cref="InstallAsync(IPage, Func{string, bool}, Action{IWebSocketRoute})"/>.</param>
        /// <param name="handler">
        /// Optional handler identity. When omitted, the last matching predicate is removed.
        /// </param>
        /// <returns>A completed task.</returns>
        internal static Task UnrouteAsync(IPage page, Func<string, bool> url, Action<IWebSocketRoute> handler)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            PageState state = GetPage(page);
            lock (state.Lock)
            {
                RemoveHandler(state.Handlers, url, handler);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Removes a page-level WebSocket route registered with a predicate and an async handler.
        /// </summary>
        /// <param name="page">The page that owns the route.</param>
        /// <param name="url">Predicate passed to <see cref="InstallAsync(IPage, Func{string, bool}, Func{IWebSocketRoute, Task}, object)"/>.</param>
        /// <param name="handler">Handler identity. When null, the last matching predicate is removed.</param>
        /// <returns>A completed task.</returns>
        internal static Task UnrouteAsync(IPage page, Func<string, bool> url, Func<IWebSocketRoute, Task> handler)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            PageState state = GetPage(page);
            lock (state.Lock)
            {
                RemoveHandler(state.Handlers, url, handler);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Removes a context-level WebSocket route.
        /// </summary>
        /// <param name="context">The context that owns the route.</param>
        /// <param name="url">Pattern passed to <see cref="InstallAsync(IBrowserContext, string, Action{IWebSocketRoute})"/>.</param>
        /// <param name="handler">
        /// Optional handler identity. When omitted, the last matching pattern is removed.
        /// </param>
        /// <returns>A completed task.</returns>
        internal static Task UnrouteAsync(IBrowserContext context, string url, Action<IWebSocketRoute> handler)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ContextState state = GetContext(context);
            lock (state.Lock)
            {
                RemoveHandler(state.Handlers, url, handler);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Removes a context-level WebSocket route registered with an async handler.
        /// </summary>
        /// <param name="context">The context that owns the route.</param>
        /// <param name="url">Pattern passed to <see cref="InstallAsync(IBrowserContext, string, Func{IWebSocketRoute, Task}, object)"/>.</param>
        /// <param name="handler">Handler identity. When null, the last matching pattern is removed.</param>
        /// <returns>A completed task.</returns>
        internal static Task UnrouteAsync(IBrowserContext context, string url, Func<IWebSocketRoute, Task> handler)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ContextState state = GetContext(context);
            lock (state.Lock)
            {
                RemoveHandler(state.Handlers, url, handler);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Removes a context-level WebSocket route registered with a regex.
        /// </summary>
        /// <param name="context">The context that owns the route.</param>
        /// <param name="url">Pattern passed to <see cref="InstallAsync(IBrowserContext, Regex, Action{IWebSocketRoute})"/>.</param>
        /// <param name="handler">
        /// Optional handler identity. When omitted, the last matching pattern is removed.
        /// </param>
        /// <returns>A completed task.</returns>
        internal static Task UnrouteAsync(IBrowserContext context, Regex url, Action<IWebSocketRoute> handler)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ContextState state = GetContext(context);
            lock (state.Lock)
            {
                RemoveHandler(state.Handlers, url, handler);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Removes a context-level WebSocket route registered with a regex and an async handler.
        /// </summary>
        /// <param name="context">The context that owns the route.</param>
        /// <param name="url">Pattern passed to <see cref="InstallAsync(IBrowserContext, Regex, Func{IWebSocketRoute, Task}, object)"/>.</param>
        /// <param name="handler">Handler identity. When null, the last matching pattern is removed.</param>
        /// <returns>A completed task.</returns>
        internal static Task UnrouteAsync(IBrowserContext context, Regex url, Func<IWebSocketRoute, Task> handler)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ContextState state = GetContext(context);
            lock (state.Lock)
            {
                RemoveHandler(state.Handlers, url, handler);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Removes a context-level WebSocket route registered with a predicate.
        /// </summary>
        /// <param name="context">The context that owns the route.</param>
        /// <param name="url">Predicate passed to <see cref="InstallAsync(IBrowserContext, Func{string, bool}, Action{IWebSocketRoute})"/>.</param>
        /// <param name="handler">
        /// Optional handler identity. When omitted, the last matching predicate is removed.
        /// </param>
        /// <returns>A completed task.</returns>
        internal static Task UnrouteAsync(IBrowserContext context, Func<string, bool> url, Action<IWebSocketRoute> handler)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ContextState state = GetContext(context);
            lock (state.Lock)
            {
                RemoveHandler(state.Handlers, url, handler);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Removes a context-level WebSocket route registered with a predicate and an async handler.
        /// </summary>
        /// <param name="context">The context that owns the route.</param>
        /// <param name="url">Predicate passed to <see cref="InstallAsync(IBrowserContext, Func{string, bool}, Func{IWebSocketRoute, Task}, object)"/>.</param>
        /// <param name="handler">Handler identity. When null, the last matching predicate is removed.</param>
        /// <returns>A completed task.</returns>
        internal static Task UnrouteAsync(IBrowserContext context, Func<string, bool> url, Func<IWebSocketRoute, Task> handler)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ContextState state = GetContext(context);
            lock (state.Lock)
            {
                RemoveHandler(state.Handlers, url, handler);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Removes every page-level WebSocket route.
        /// </summary>
        /// <param name="page">The page that owns the routes.</param>
        /// <returns>A completed task.</returns>
        internal static Task UnrouteAllAsync(IPage page)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            PageState state = GetPage(page);
            lock (state.Lock)
            {
                state.Handlers.Clear();
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Removes every context-level WebSocket route.
        /// </summary>
        /// <param name="context">The context that owns the routes.</param>
        /// <returns>A completed task.</returns>
        internal static Task UnrouteAllAsync(IBrowserContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ContextState state = GetContext(context);
            lock (state.Lock)
            {
                state.Handlers.Clear();
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Queues an injected dispatch so the page can pull it without a nested
        /// <c>evaluate</c> (official uses evaluateExpression; a promise-returning
        /// page.evaluate would otherwise deadlock). Completes immediately; the
        /// page applies the payload from a later binding return or evaluate.
        /// </summary>
        internal static Task EnqueueDispatchAsync(IPage page, Dictionary<string, object> request)
        {
            if (page == null || request == null)
            {
                return Task.CompletedTask;
            }

            TaskCompletionSource<bool> done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            done.TrySetResult(true);
            PageState state = GetPage(page);
            lock (state.Lock)
            {
                state.DispatchSeq++;
                request["_seq"] = state.DispatchSeq;
                state.PendingDispatch.Add((request, done));
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Official <c>frame.evaluateExpression(__pwWebSocketDispatch)</c>. Fire
        /// this after <see cref="EnqueueDispatchAsync"/>; <c>_seq</c> drops duplicates
        /// when a binding return already applied the payload.
        /// </summary>
        internal static Task EvaluateDispatchAsync(IPage page, Dictionary<string, object> request)
        {
            if (page == null || request == null)
            {
                return Task.CompletedTask;
            }

            string json = JsonSerializer.Serialize(request);
            string script = "try{if(typeof globalThis.__pwWebSocketDispatch==='function')globalThis.__pwWebSocketDispatch(" + json + ")}catch(e){}";
            return EvaluateWithoutAwaitingPromiseAsync(page, script);
        }

        private static Task EvaluateWithoutAwaitingPromiseAsync(IPage page, string script)
        {
            if (page is Page chromium)
            {
                return chromium.CrPage.EvaluateWithoutAwaitingPromiseAsync(script);
            }

            if (page is WebKit.WKPage webkit)
            {
                return webkit.EvaluateWithoutAwaitingPromiseAsync(script);
            }

            foreach (IFrame frame in page.Frames)
            {
                if (frame != null && !frame.IsDetached)
                {
                    _ = EvaluateOnFrameAsync(frame, script);
                }
            }

            return Task.CompletedTask;
        }

        private static async Task EvaluateOnFrameAsync(IFrame frame, string script)
        {
            if (frame == null || frame.IsDetached)
            {
                return;
            }

            try
            {
                await frame.EvaluateAsync(script).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static async Task EnsureContextBindingAsync(IBrowserContext context)
        {
            ContextState state = GetContext(context);
            bool install = false;
            lock (state.Lock)
            {
                if (!state.BindingInstalled)
                {
                    state.BindingInstalled = true;
                    install = true;
                }

                if (!state.Subscribed)
                {
                    state.Subscribed = true;
                    context.Page += (_, page) =>
                    {
                        _ = TagNewPageAsync(page);
                    };
                }
            }

            if (!install)
            {
                return;
            }

            await context.ExposeFunctionAsync<JsonElement, Task<object>>(BindingName, payload => OnBindingAsync(context, payload)).ConfigureAwait(false);
            await context.AddInitScriptAsync(WebSocketRouteScript.Injector).ConfigureAwait(false);
        }

        private static async Task TagNewPageAsync(IPage page)
        {
            try
            {
                await EnsurePageTaggedAsync(page).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }
        }

        private static async Task EnsurePageTaggedAsync(IPage page)
        {
            PageState pageState = GetPage(page);
            IBrowserContext context = page.Context;
            string pageId = null;
            lock (pageState.Lock)
            {
                if (string.IsNullOrEmpty(pageState.PageId))
                {
                    pageId = Guid.NewGuid().ToString("N");
                    pageState.PageId = pageId;
                }
            }

            if (pageId != null && context != null)
            {
                ContextState contextState = GetContext(context);
                lock (contextState.Lock)
                {
                    contextState.PagesById[pageId] = page;
                }

                string assign = "globalThis.__pwWebSocketPageId='" + pageId + "';";
                await page.AddInitScriptAsync(assign).ConfigureAwait(false);
                try
                {
                    await page.EvaluateAsync(assign).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }
            }

            await page.AddInitScriptAsync(WebSocketRouteScript.Injector).ConfigureAwait(false);
            try
            {
                await page.EvaluateAsync(WebSocketRouteScript.Injector).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }

            foreach (IFrame frame in page.Frames)
            {
                if (frame == null || frame.IsDetached)
                {
                    continue;
                }

                try
                {
                    await frame.EvaluateAsync(WebSocketRouteScript.Injector).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }
            }

            page.FrameAttached += (_, frame) =>
            {
                _ = InjectFrameAsync(frame);
            };
        }

        private static async Task InjectFrameAsync(IFrame frame)
        {
            if (frame == null || frame.IsDetached)
            {
                return;
            }

            try
            {
                await frame.EvaluateAsync(WebSocketRouteScript.Injector).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }
        }

        private static async Task EnsurePageBindingAsync(IPage page)
        {
            PageState state = GetPage(page);
            bool install = false;
            lock (state.Lock)
            {
                if (!state.BindingInstalled)
                {
                    state.BindingInstalled = true;
                    install = true;
                }
            }

            if (install)
            {
                await page.ExposeFunctionAsync<JsonElement, Task<object>>(BindingName, payload => OnBindingAsync(page.Context, payload, page)).ConfigureAwait(false);
            }

            await page.AddInitScriptAsync(WebSocketRouteScript.Injector).ConfigureAwait(false);
            try
            {
                await page.EvaluateAsync(WebSocketRouteScript.Injector).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }
        }

        private static Task<object> OnBindingAsync(IBrowserContext context, JsonElement payload)
            => OnBindingAsync(context, payload, null);

        private static List<Dictionary<string, object>> DrainPending(IPage page)
        {
            if (page == null)
            {
                return new List<Dictionary<string, object>>();
            }

            List<(Dictionary<string, object> Request, TaskCompletionSource<bool> Done)> pending;
            PageState state = GetPage(page);
            lock (state.Lock)
            {
                pending = new List<(Dictionary<string, object>, TaskCompletionSource<bool>)>(state.PendingDispatch);
                state.PendingDispatch.Clear();
            }

            List<Dictionary<string, object>> requests = new List<Dictionary<string, object>>(pending.Count);
            foreach ((Dictionary<string, object> request, TaskCompletionSource<bool> done) in pending)
            {
                if (request != null)
                {
                    requests.Add(request);
                }

                done?.TrySetResult(true);
            }

            return requests;
        }

        private static async Task<object> OnBindingAsync(IBrowserContext context, JsonElement payload, IPage knownPage)
        {
            if (payload.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            string type = ReadString(payload, "type");
            IPage page = knownPage ?? ResolvePage(context, ReadString(payload, "pageId"));
            if (string.Equals(type, "onPull", StringComparison.Ordinal))
            {
                return DrainPending(page);
            }

            string op = ReadString(payload, "op");
            string id = ReadString(payload, "id");
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            if (string.Equals(type, "onCreate", StringComparison.Ordinal)
                || string.Equals(op, "open", StringComparison.Ordinal))
            {
                return await HandleOpenAsync(
                    page,
                    context,
                    id,
                    ReadString(payload, "url"),
                    ReadStringArray(payload, "protocols"),
                    ReadBool(payload, "isMain")).ConfigureAwait(false);
            }

            WebSocketRoute route = FindRoute(page, context, id);
            if (string.Equals(type, "onMessageFromPage", StringComparison.Ordinal)
                || string.Equals(op, "message", StringComparison.Ordinal))
            {
                (string data, bool binary) = ReadMessageData(payload);
                route?.ReceiveFromPage(data, binary);
                return true;
            }

            if (string.Equals(type, "onMessageFromServer", StringComparison.Ordinal))
            {
                (string data, bool binary) = ReadMessageData(payload);
                route?.ReceiveFromServer(data, binary);
                return true;
            }

            if (string.Equals(type, "onClosePage", StringComparison.Ordinal)
                || string.Equals(op, "close", StringComparison.Ordinal))
            {
                route?.ClosedFromPage(ReadInt(payload, "code"), ReadString(payload, "reason"));
                return true;
            }

            if (string.Equals(type, "onCloseServer", StringComparison.Ordinal))
            {
                int? code = ReadInt(payload, "code");
                bool wasClean = payload.TryGetProperty("wasClean", out JsonElement clean)
                    ? clean.ValueKind == JsonValueKind.True
                    : code != 1006;
                route?.ClosedFromServer(code, ReadString(payload, "reason"), wasClean);
                return true;
            }

            return false;
        }

        private static async Task<bool> HandleOpenAsync(
            IPage page,
            IBrowserContext context,
            string id,
            string url,
            IReadOnlyList<string> protocols,
            bool createdInMainFrame)
        {
            Func<IWebSocketRoute, Task> handler = FindHandler(page, context, url);

            if (handler == null || page == null)
            {
                return false;
            }

            PageState pageState = GetPage(page);
            lock (pageState.Lock)
            {
                if (pageState.Routes.ContainsKey(id))
                {
                    return true;
                }
            }

            WebSocketRoute route = new WebSocketRoute(page, id, url, protocols, createdInMainFrame);
            lock (pageState.Lock)
            {
                if (pageState.Routes.ContainsKey(id))
                {
                    route.Dispose();
                    return true;
                }

                pageState.Routes[id] = route;
            }

            if (context != null)
            {
                ContextState contextState = GetContext(context);
                lock (contextState.Lock)
                {
                    contextState.Routes[id] = route;
                }
            }

            string routeId = id;
            _ = Task.Run(() => InvokeHandlerAsync(pageState, routeId, handler));
            return true;
        }

        private static async Task InvokeHandlerAsync(
            PageState pageState,
            string id,
            Func<IWebSocketRoute, Task> handler)
        {
            WebSocketRoute route;
            lock (pageState.Lock)
            {
                if (!pageState.Routes.TryGetValue(id, out route))
                {
                    return;
                }
            }

            try
            {
                await route.WaitUntilPageReadyAsync().ConfigureAwait(false);
                Task task = handler(route);
                if (task != null)
                {
                    await task.ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _ = ex.Message;
            }

            await route.FlushAfterHandlerAsync().ConfigureAwait(false);
        }

        private static IPage ResolvePage(IBrowserContext context, string pageId)
        {
            if (context == null)
            {
                return null;
            }

            ContextState state = GetContext(context);
            if (!string.IsNullOrEmpty(pageId))
            {
                lock (state.Lock)
                {
                    if (state.PagesById.TryGetValue(pageId, out IPage tagged))
                    {
                        return tagged;
                    }
                }
            }

            IPage withHandlers = null;
            IPage first = null;
            foreach (IPage candidate in context.Pages)
            {
                if (candidate == null)
                {
                    continue;
                }

                first ??= candidate;
                PageState pageState = GetPage(candidate);
                lock (pageState.Lock)
                {
                    if (pageState.Handlers.Count > 0)
                    {
                        withHandlers = candidate;
                    }
                }
            }

            return withHandlers ?? first;
        }

        private static Func<IWebSocketRoute, Task> FindHandler(IPage page, IBrowserContext context, string url)
        {
            string baseUrl = ContextBaseUrl(page, context);
            if (page != null)
            {
                PageState pageState = GetPage(page);
                lock (pageState.Lock)
                {
                    for (int i = pageState.Handlers.Count - 1; i >= 0; i--)
                    {
                        if (Matches(url, pageState.Handlers[i], baseUrl))
                        {
                            return pageState.Handlers[i].Handler;
                        }
                    }
                }
            }

            if (context == null)
            {
                return null;
            }

            ContextState contextState = GetContext(context);
            lock (contextState.Lock)
            {
                for (int i = contextState.Handlers.Count - 1; i >= 0; i--)
                {
                    if (Matches(url, contextState.Handlers[i], baseUrl))
                    {
                        return contextState.Handlers[i].Handler;
                    }
                }
            }

            return null;
        }

        private static void RemoveHandler(List<RouteEntry> handlers, string url, object identity)
        {
            string pattern = url ?? string.Empty;
            for (int i = handlers.Count - 1; i >= 0; i--)
            {
                if (handlers[i].PatternRegex != null
                    || handlers[i].PatternFunc != null
                    || !string.Equals(handlers[i].Pattern, pattern, StringComparison.Ordinal))
                {
                    continue;
                }

                if (identity != null && !ReferenceEquals(handlers[i].Identity, identity))
                {
                    continue;
                }

                handlers.RemoveAt(i);
                return;
            }
        }

        private static void RemoveHandler(List<RouteEntry> handlers, Regex url, object identity)
        {
            for (int i = handlers.Count - 1; i >= 0; i--)
            {
                if (!SameRegex(handlers[i].PatternRegex, url))
                {
                    continue;
                }

                if (identity != null && !ReferenceEquals(handlers[i].Identity, identity))
                {
                    continue;
                }

                handlers.RemoveAt(i);
                return;
            }
        }

        private static void RemoveHandler(List<RouteEntry> handlers, Func<string, bool> url, object identity)
        {
            for (int i = handlers.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(handlers[i].PatternFunc, url))
                {
                    continue;
                }

                if (identity != null && !ReferenceEquals(handlers[i].Identity, identity))
                {
                    continue;
                }

                handlers.RemoveAt(i);
                return;
            }
        }

        private static bool SameRegex(Regex left, Regex right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(left.ToString(), right.ToString(), StringComparison.Ordinal)
                && left.Options == right.Options;
        }

        private static bool Matches(string url, RouteEntry entry, string baseUrl)
        {
            if (entry.PatternFunc != null)
            {
                return entry.PatternFunc(url ?? string.Empty);
            }

            if (entry.PatternRegex != null)
            {
                return entry.PatternRegex.IsMatch(url ?? string.Empty);
            }

            return Matches(url, entry.Pattern, baseUrl);
        }

        private static string ContextBaseUrl(IPage page, IBrowserContext context)
        {
            IBrowserContext owner = context ?? page?.Context;
            return owner is IHasBaseUrl has ? has.BaseURL : null;
        }

        private static (string Data, bool Binary) ReadMessageData(JsonElement payload)
        {
            if (payload.TryGetProperty("data", out JsonElement data)
                && data.ValueKind == JsonValueKind.Object)
            {
                string text = data.TryGetProperty("data", out JsonElement inner)
                    && inner.ValueKind == JsonValueKind.String
                    ? inner.GetString() ?? string.Empty
                    : string.Empty;
                bool binary = data.TryGetProperty("isBase64", out JsonElement flag)
                    && flag.ValueKind == JsonValueKind.True;
                return (text, binary);
            }

            return (ReadString(payload, "data"), ReadBool(payload, "binary"));
        }

        private static bool Matches(string url, string pattern, string baseUrl)
        {
            if (string.IsNullOrEmpty(pattern) || pattern == "**/*" || pattern == "**")
            {
                return true;
            }

            if (UrlMatcher.UrlMatches(baseUrl, url ?? string.Empty, pattern, webSocketUrl: true))
            {
                return true;
            }

            if (pattern[0] == '/' && pattern.IndexOf('*') < 0
                && Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return string.Equals(uri.AbsolutePath, pattern, StringComparison.Ordinal);
            }

            return false;
        }

        private static WebSocketRoute FindRoute(IPage page, IBrowserContext context, string id)
        {
            if (page != null)
            {
                PageState pageState = GetPage(page);
                lock (pageState.Lock)
                {
                    if (pageState.Routes.TryGetValue(id, out WebSocketRoute fromPage))
                    {
                        return fromPage;
                    }
                }
            }

            if (context == null)
            {
                return null;
            }

            ContextState contextState = GetContext(context);
            lock (contextState.Lock)
            {
                return contextState.Routes.TryGetValue(id, out WebSocketRoute fromContext) ? fromContext : null;
            }
        }

        private static PageState GetPage(IPage page)
            => _pages.GetValue(page, _ => new PageState());

        private static ContextState GetContext(IBrowserContext context)
            => _contexts.GetValue(context, _ => new ContextState());

        private static IReadOnlyList<string> ReadStringArray(JsonElement payload, string name)
        {
            if (!payload.TryGetProperty(name, out JsonElement value))
            {
                return Array.Empty<string>();
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return new[] { value.GetString() ?? string.Empty };
            }

            if (value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            List<string> result = new List<string>();
            foreach (JsonElement item in value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    result.Add(item.GetString() ?? string.Empty);
                }
            }

            return result;
        }

        private static string ReadString(JsonElement payload, string name)
        {
            if (!payload.TryGetProperty(name, out JsonElement value)
                || value.ValueKind != JsonValueKind.String)
            {
                return string.Empty;
            }

            return value.GetString() ?? string.Empty;
        }

        private static bool ReadBool(JsonElement payload, string name)
            => payload.TryGetProperty(name, out JsonElement value)
                && value.ValueKind == JsonValueKind.True;

        private static int? ReadInt(JsonElement payload, string name)
        {
            if (!payload.TryGetProperty(name, out JsonElement value)
                || value.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            if (value.TryGetInt32(out int number))
            {
                return number;
            }

            return null;
        }

        private sealed class RouteEntry
        {
            internal RouteEntry(string pattern, Func<IWebSocketRoute, Task> handler, object identity)
            {
                Pattern = pattern;
                PatternRegex = null;
                PatternFunc = null;
                Handler = handler;
                Identity = identity;
            }

            internal RouteEntry(Regex pattern, Func<IWebSocketRoute, Task> handler, object identity)
            {
                Pattern = string.Empty;
                PatternRegex = pattern;
                PatternFunc = null;
                Handler = handler;
                Identity = identity;
            }

            internal RouteEntry(Func<string, bool> pattern, Func<IWebSocketRoute, Task> handler, object identity)
            {
                Pattern = string.Empty;
                PatternRegex = null;
                PatternFunc = pattern;
                Handler = handler;
                Identity = identity;
            }

            internal string Pattern { get; }

            internal Regex PatternRegex { get; }

            internal Func<string, bool> PatternFunc { get; }

            internal Func<IWebSocketRoute, Task> Handler { get; }

            internal object Identity { get; }
        }

        private sealed class PageState
        {
            internal object Lock { get; } = new object();

            internal bool BindingInstalled { get; set; }

            internal string PageId { get; set; }

            internal List<RouteEntry> Handlers { get; } = new List<RouteEntry>();

            internal Dictionary<string, WebSocketRoute> Routes { get; } = new Dictionary<string, WebSocketRoute>(StringComparer.Ordinal);

            internal List<(Dictionary<string, object> Request, TaskCompletionSource<bool> Done)> PendingDispatch { get; } =
                new List<(Dictionary<string, object>, TaskCompletionSource<bool>)>();

            internal int DispatchSeq { get; set; }
        }

        private sealed class ContextState
        {
            internal object Lock { get; } = new object();

            internal bool BindingInstalled { get; set; }

            internal bool Subscribed { get; set; }

            internal List<RouteEntry> Handlers { get; } = new List<RouteEntry>();

            internal Dictionary<string, WebSocketRoute> Routes { get; } = new Dictionary<string, WebSocketRoute>(StringComparer.Ordinal);

            internal Dictionary<string, IPage> PagesById { get; } = new Dictionary<string, IPage>(StringComparer.Ordinal);
        }
    }
}
