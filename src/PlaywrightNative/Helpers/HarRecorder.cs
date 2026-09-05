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
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Records network traffic for a context and writes a HAR 1.2 file on close.
    /// </summary>
    internal static class HarRecorder
    {
        private static readonly ConditionalWeakTable<IBrowserContext, SessionBag> Bags = new();
        private static readonly ConditionalWeakTable<IAPIRequestContext, List<Session>> ApiSessions = new();

        /// <summary>
        /// Starts recording when <paramref name="recordHarPath"/> is set.
        /// </summary>
        /// <param name="context">The context to observe.</param>
        /// <param name="recordHarPath">Destination HAR path, or <see langword="null"/>.</param>
        /// <param name="recordHarOmitContent">When <see langword="true"/>, response bodies are omitted.</param>
        /// <param name="recordHarUrl">Optional glob; when set, only matching URLs are recorded.</param>
        /// <param name="recordHarMode">When <see cref="HarMode.Minimal"/>, response bodies are omitted.</param>
        /// <param name="recordHarContent">When <see cref="HarContentPolicy.Attach"/>, bodies are written beside the HAR.</param>
        /// <param name="recordHarUrlRegex">Optional regular expression; when set, only matching URLs are recorded.</param>
        internal static void Start(IBrowserContext context, string recordHarPath, bool? recordHarOmitContent, string recordHarUrl = default, HarMode recordHarMode = default, HarContentPolicy recordHarContent = EnumCompat.UndefinedHarContentPolicy, Regex recordHarUrlRegex = default)
        {
            if (context == null || string.IsNullOrEmpty(recordHarPath))
            {
                return;
            }

            SessionBag bag = GetOrCreateBag(context);
            bag.RecordHar?.Detach();
            bag.RecordHar = CreateSession(
                context,
                recordHarPath,
                recordHarOmitContent,
                recordHarUrl,
                recordHarMode,
                recordHarContent,
                recordHarUrlRegex,
                recordBrowser: true,
                recordApi: false,
                slimMode: recordHarMode == HarMode.Minimal);
        }

        /// <summary>
        /// Starts a browser-only tracing HAR session for
        /// <c>context.tracing.startHar</c>.
        /// </summary>
        internal static void StartTracing(
            IBrowserContext context,
            IAPIRequestContext api,
            string recordHarPath,
            HarMode recordHarMode = default,
            HarContentPolicy recordHarContent = EnumCompat.UndefinedHarContentPolicy,
            string recordHarUrl = default,
            Regex recordHarUrlRegex = default,
            string resourcesDir = default)
        {
            _ = api;
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (string.IsNullOrEmpty(recordHarPath))
            {
                throw new ArgumentException("path must be non-empty", nameof(recordHarPath));
            }

            SessionBag bag = GetOrCreateBag(context);
            if (bag.Tracing != null)
            {
                throw new PlaywrightNativeException("HAR recording has already been started");
            }

            bag.Tracing = CreateSession(
                context,
                recordHarPath,
                recordHarOmitContent: false,
                recordHarUrl,
                recordHarMode,
                recordHarContent,
                recordHarUrlRegex,
                recordBrowser: true,
                recordApi: false,
                slimMode: recordHarMode == HarMode.Minimal,
                resourcesDir: resourcesDir);
        }

        /// <summary>
        /// Starts API-only HAR recording for a standalone request context.
        /// </summary>
        internal static void StartApi(
            IAPIRequestContext api,
            string recordHarPath,
            HarMode recordHarMode = default,
            HarContentPolicy recordHarContent = EnumCompat.UndefinedHarContentPolicy,
            string recordHarUrl = default,
            Regex recordHarUrlRegex = default,
            string resourcesDir = default)
        {
            if (api == null)
            {
                throw new ArgumentNullException(nameof(api));
            }

            if (string.IsNullOrEmpty(recordHarPath))
            {
                throw new ArgumentException("path must be non-empty", nameof(recordHarPath));
            }

            if (ApiSessions.TryGetValue(api, out List<Session> existing) && existing.Count > 0)
            {
                throw new PlaywrightNativeException("HAR recording has already been started");
            }

            Session session = CreateSession(
                context: null,
                recordHarPath,
                recordHarOmitContent: false,
                recordHarUrl,
                recordHarMode,
                recordHarContent,
                recordHarUrlRegex,
                recordBrowser: false,
                recordApi: true,
                slimMode: recordHarMode == HarMode.Minimal,
                api,
                resourcesDir);
            RegisterApiSession(api, session);
        }

        /// <summary>
        /// Observes a routed <c>connectToServer</c> WebSocket as a HAR entry.
        /// </summary>
        /// <param name="page">Page that owns the route.</param>
        /// <param name="socket">Synthetic socket that reports wire frames.</param>
        internal static void ObserveWebSocket(IPage page, IWebSocket socket)
        {
            if (page?.Context == null || socket == null)
            {
                return;
            }

            if (!Bags.TryGetValue(page.Context, out SessionBag bag))
            {
                return;
            }

            bag.RecordHar?.Observe(page, socket);
            bag.Tracing?.Observe(page, socket);
        }

        /// <summary>
        /// Records one API-request hop into any matching HAR session.
        /// </summary>
        internal static void RecordApiHop(IAPIRequestContext api, ApiHarHop hop)
        {
            if (api == null || hop == null)
            {
                return;
            }

            if (!ApiSessions.TryGetValue(api, out List<Session> sessions) || sessions.Count == 0)
            {
                return;
            }

            foreach (Session session in sessions.ToArray())
            {
                session.AddApiHop(hop);
            }
        }

        /// <summary>
        /// Writes the HAR file for <paramref name="context"/> when recording is active.
        /// Safe to call more than once.
        /// </summary>
        /// <param name="context">The context that is closing.</param>
        /// <returns>A task that completes when the file has been written.</returns>
        internal static async Task FlushAsync(IBrowserContext context)
        {
            if (context == null || !Bags.TryGetValue(context, out SessionBag bag))
            {
                return;
            }

            Bags.Remove(context);
            await FlushBagAsync(bag).ConfigureAwait(false);
        }

        /// <summary>
        /// Flushes the tracing HAR session without touching <c>recordHar</c>.
        /// </summary>
        internal static async Task FlushTracingAsync(IBrowserContext context, IAPIRequestContext api)
        {
            Session session = null;
            if (context != null && Bags.TryGetValue(context, out SessionBag bag))
            {
                session = bag.Tracing;
                bag.Tracing = null;
            }

            if (session == null && api != null && ApiSessions.TryGetValue(api, out List<Session> sessions))
            {
                if (sessions.Count > 0)
                {
                    session = sessions[sessions.Count - 1];
                    sessions.RemoveAt(sessions.Count - 1);
                }
            }

            if (session == null)
            {
                return;
            }

            UnregisterApiSession(api, session);
            await session.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Gets whether <paramref name="context"/> already has an active HAR session.
        /// </summary>
        /// <param name="context">The context to inspect.</param>
        /// <returns><see langword="true"/> when a session is recording.</returns>
        internal static bool IsRecording(IBrowserContext context)
            => context != null
                && Bags.TryGetValue(context, out SessionBag bag)
                && (bag.RecordHar != null || bag.Tracing != null);

        /// <summary>
        /// Gets whether a tracing HAR session is active on <paramref name="context"/>.
        /// </summary>
        internal static bool IsTracing(IBrowserContext context)
            => context != null
                && Bags.TryGetValue(context, out SessionBag bag)
                && bag.Tracing != null;

        /// <summary>
        /// Records matching requests into <paramref name="har"/> until the context closes.
        /// Used by <see cref="IBrowserContext.RouteFromHARAsync(string, string, HarNotFound, bool, HarMode, RouteFromHarUpdateContentPolicy)"/>
        /// when <c>update</c> is <see langword="true"/>.
        /// </summary>
        /// <param name="context">The context to observe.</param>
        /// <param name="har">Destination HAR path.</param>
        /// <param name="url">Optional glob; when set, only matching URLs are recorded.</param>
        /// <param name="updateMode">When <see cref="HarMode.Minimal"/>, response bodies are omitted.</param>
        /// <param name="updateContent">When <see cref="RouteFromHarUpdateContentPolicy.Attach"/>, bodies are written beside the HAR.</param>
        /// <returns>A completed task after recording starts.</returns>
        internal static Task StartForRouteAsync(IBrowserContext context, string har, string url = default, HarMode updateMode = default, RouteFromHarUpdateContentPolicy updateContent = default)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (string.IsNullOrEmpty(har))
            {
                throw new ArgumentException("HAR path must be non-empty.", nameof(har));
            }

            Start(context, har, recordHarOmitContent: false, recordHarUrl: url, recordHarMode: updateMode, recordHarContent: ToContentPolicy(updateContent));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Records URLs matching <paramref name="url"/> into <paramref name="har"/>
        /// until the context closes.
        /// </summary>
        /// <param name="context">The context to observe.</param>
        /// <param name="har">Destination HAR path.</param>
        /// <param name="url">URL regular expression.</param>
        /// <param name="updateMode">When <see cref="HarMode.Minimal"/>, response bodies are omitted.</param>
        /// <param name="updateContent">When <see cref="RouteFromHarUpdateContentPolicy.Attach"/>, bodies are written beside the HAR.</param>
        /// <returns>A completed task after recording starts.</returns>
        internal static Task StartForRouteAsync(IBrowserContext context, string har, Regex url, HarMode updateMode = default, RouteFromHarUpdateContentPolicy updateContent = default)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (string.IsNullOrEmpty(har))
            {
                throw new ArgumentException("HAR path must be non-empty.", nameof(har));
            }

            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            Start(context, har, recordHarOmitContent: false, recordHarUrlRegex: url, recordHarMode: updateMode, recordHarContent: ToContentPolicy(updateContent));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Records matching requests from <paramref name="page"/>'s context into <paramref name="har"/>.
        /// </summary>
        /// <param name="page">The page whose context is observed.</param>
        /// <param name="har">Destination HAR path.</param>
        /// <param name="url">Optional glob; when set, only matching URLs are recorded.</param>
        /// <param name="updateMode">When <see cref="HarMode.Minimal"/>, response bodies are omitted.</param>
        /// <param name="updateContent">When <see cref="RouteFromHarUpdateContentPolicy.Attach"/>, bodies are written beside the HAR.</param>
        /// <returns>A completed task after recording starts.</returns>
        internal static Task StartForRouteAsync(IPage page, string har, string url = default, HarMode updateMode = default, RouteFromHarUpdateContentPolicy updateContent = default)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            return StartForRouteAsync(page.Context, har, url, updateMode, updateContent);
        }

        /// <summary>
        /// Records URLs matching <paramref name="url"/> from <paramref name="page"/>'s context.
        /// </summary>
        /// <param name="page">The page whose context is observed.</param>
        /// <param name="har">Destination HAR path.</param>
        /// <param name="url">URL regular expression.</param>
        /// <param name="updateMode">When <see cref="HarMode.Minimal"/>, response bodies are omitted.</param>
        /// <param name="updateContent">When <see cref="RouteFromHarUpdateContentPolicy.Attach"/>, bodies are written beside the HAR.</param>
        /// <returns>A completed task after recording starts.</returns>
        internal static Task StartForRouteAsync(IPage page, string har, Regex url, HarMode updateMode = default, RouteFromHarUpdateContentPolicy updateContent = default)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            return StartForRouteAsync(page.Context, har, url, updateMode, updateContent);
        }

        private static HarContentPolicy ToContentPolicy(RouteFromHarUpdateContentPolicy updateContent)
        {
            if (updateContent == RouteFromHarUpdateContentPolicy.Attach)
            {
                return HarContentPolicy.Attach;
            }

            if (updateContent == RouteFromHarUpdateContentPolicy.Embed)
            {
                return HarContentPolicy.Embed;
            }

            return EnumCompat.UndefinedHarContentPolicy;
        }

        private static SessionBag GetOrCreateBag(IBrowserContext context)
        {
            if (Bags.TryGetValue(context, out SessionBag bag))
            {
                return bag;
            }

            bag = new SessionBag();
            Bags.Add(context, bag);
            return bag;
        }

        private static Session CreateSession(
            IBrowserContext context,
            string recordHarPath,
            bool? recordHarOmitContent,
            string recordHarUrl,
            HarMode recordHarMode,
            HarContentPolicy recordHarContent,
            Regex recordHarUrlRegex,
            bool recordBrowser,
            bool recordApi,
            bool slimMode,
            IAPIRequestContext api = null,
            string resourcesDir = default)
        {
            bool isZip = recordHarPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            bool attachContent = recordHarContent == HarContentPolicy.Attach
                || (recordHarContent == EnumCompat.UndefinedHarContentPolicy && isZip);
            bool omitContent = recordHarOmitContent == true
                || recordHarContent == HarContentPolicy.Omit
                || (recordHarMode == HarMode.Minimal && !attachContent);
            return new Session(
                context,
                recordHarPath,
                omitContent,
                recordHarUrl,
                attachContent,
                recordHarUrlRegex,
                recordBrowser,
                recordApi,
                slimMode,
                api,
                resourcesDir);
        }

        private static void RegisterApiSession(IAPIRequestContext api, Session session)
        {
            if (api == null || session == null)
            {
                return;
            }

            if (!ApiSessions.TryGetValue(api, out List<Session> sessions))
            {
                sessions = new List<Session>();
                ApiSessions.Add(api, sessions);
            }

            sessions.Add(session);
        }

        private static void UnregisterApiSession(IAPIRequestContext api, Session session)
        {
            if (api == null || session == null || !ApiSessions.TryGetValue(api, out List<Session> sessions))
            {
                return;
            }

            sessions.Remove(session);
        }

        private static async Task FlushBagAsync(SessionBag bag)
        {
            if (bag == null)
            {
                return;
            }

            Session recordHar = bag.RecordHar;
            Session tracing = bag.Tracing;
            bag.RecordHar = null;
            bag.Tracing = null;
            if (recordHar != null)
            {
                await recordHar.FlushAsync().ConfigureAwait(false);
            }

            if (tracing != null)
            {
                await tracing.FlushAsync().ConfigureAwait(false);
            }
        }

        private static async Task<IEnumerable<KeyValuePair<string, string>>> HeadersFromAsync(IRequest request)
        {
            if (request == null)
            {
                return null;
            }

            try
            {
                IReadOnlyList<Header> array = await AwaitOrDefaultAsync(request.HeadersArrayAsync()).ConfigureAwait(false);
                List<KeyValuePair<string, string>> list = ToPairs(array);
                if (list.Count > 0)
                {
                    return list;
                }
            }
            catch (PlaywrightNativeException)
            {
            }

            return request.Headers;
        }

        private static async Task<IEnumerable<KeyValuePair<string, string>>> HeadersFromAsync(IResponse response)
        {
            if (response == null)
            {
                return null;
            }

            try
            {
                IReadOnlyList<Header> array = await AwaitOrDefaultAsync(response.HeadersArrayAsync()).ConfigureAwait(false);
                List<KeyValuePair<string, string>> list = ToPairs(array);
                if (list.Count > 0)
                {
                    return list;
                }
            }
            catch (PlaywrightNativeException)
            {
            }

            return response.Headers;
        }

        private static async Task<T> AwaitOrDefaultAsync<T>(Task<T> task)
        {
            if (task == null)
            {
                return default;
            }

            if (task.IsCompleted)
            {
                return await task.ConfigureAwait(false);
            }

            try
            {
                return await task.WaitAsync(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return default;
            }
            catch (PlaywrightNativeException)
            {
                return default;
            }
        }

        private static List<KeyValuePair<string, string>> ToPairs(IReadOnlyList<NameValueEntry> array)
            => ToPairsFromHeaders(array?.Select(entry => new Header { Name = entry?.Name, Value = entry?.Value }));

        private static List<KeyValuePair<string, string>> ToPairs(IReadOnlyList<Header> array)
            => ToPairsFromHeaders(array);

        private static List<KeyValuePair<string, string>> ToPairsFromHeaders(IEnumerable<Header> array)
        {
            List<KeyValuePair<string, string>> list = new();
            if (array == null)
            {
                return list;
            }

            foreach (Header entry in array)
            {
                if (!string.IsNullOrEmpty(entry?.Name))
                {
                    list.Add(new KeyValuePair<string, string>(entry.Name, entry.Value ?? string.Empty));
                }
            }

            return list;
        }

        private static string CreatorVersion()
        {
            Version version = typeof(HarRecorder).Assembly.GetName().Version;
            return version == null ? "1.0.0" : version.ToString();
        }

        private static JsonObject BrowserNode(IBrowserContext context)
        {
            IBrowser browser = context?.Browser;
            return new JsonObject
            {
                ["name"] = browser?.BrowserType?.Name ?? string.Empty,
                ["version"] = browser?.Version ?? string.Empty,
            };
        }

        private static string FindHeader(IEnumerable<KeyValuePair<string, string>> headers, string name)
        {
            if (headers == null)
            {
                return null;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return header.Value;
                }
            }

            return null;
        }

        private static JsonArray ToHeaderArray(IEnumerable<KeyValuePair<string, string>> headers)
        {
            JsonArray array = new();
            if (headers == null)
            {
                return array;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                array.Add(new JsonObject
                {
                    ["name"] = header.Key ?? string.Empty,
                    ["value"] = header.Value ?? string.Empty,
                });
            }

            return array;
        }

        private static JsonArray ToQueryString(string url)
        {
            JsonArray array = new();
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) || string.IsNullOrEmpty(uri.Query))
            {
                return array;
            }

            string query = uri.Query[0] == '?' ? uri.Query.Substring(1) : uri.Query;
            foreach (string part in query.Split('&'))
            {
                if (string.IsNullOrEmpty(part))
                {
                    continue;
                }

                int eq = part.IndexOf('=', StringComparison.Ordinal);
                string name = eq < 0 ? part : part.Substring(0, eq);
                string value = eq < 0 ? string.Empty : part.Substring(eq + 1);
                array.Add(new JsonObject
                {
                    ["name"] = Uri.UnescapeDataString(name.Replace('+', ' ')),
                    ["value"] = Uri.UnescapeDataString(value.Replace('+', ' ')),
                });
            }

            return array;
        }

        private static JsonArray ToCookies(string cookieHeader)
        {
            JsonArray array = new();
            if (string.IsNullOrEmpty(cookieHeader))
            {
                return array;
            }

            foreach (string part in cookieHeader.Split(';'))
            {
                string trimmed = part.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                JsonObject cookie = ParseHarCookie(trimmed, attributes: false);
                if (cookie != null)
                {
                    array.Add(cookie);
                }
            }

            return array;
        }

        private static JsonArray ToSetCookies(IEnumerable<KeyValuePair<string, string>> headers)
        {
            JsonArray array = new();
            if (headers == null)
            {
                return array;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (!string.Equals(header.Key, "set-cookie", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrEmpty(header.Value))
                {
                    continue;
                }

                JsonObject cookie = ParseHarCookie(header.Value, attributes: true);
                if (cookie != null)
                {
                    array.Add(cookie);
                }
            }

            return array;
        }

        private static JsonObject ParseHarCookie(string raw, bool attributes)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            string[] pairs = raw.Split(';');
            if (pairs.Length == 0)
            {
                return null;
            }

            JsonObject cookie = new JsonObject();
            bool first = true;
            foreach (string pair in pairs)
            {
                string trimmed = pair.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                int eq = trimmed.IndexOf('=', StringComparison.Ordinal);
                string name = eq >= 0 ? trimmed.Substring(0, eq).Trim() : trimmed;
                string value = eq >= 0 ? trimmed.Substring(eq + 1).Trim() : string.Empty;
                if (first)
                {
                    first = false;
                    cookie["name"] = name;
                    cookie["value"] = value;
                    if (!attributes)
                    {
                        return cookie;
                    }

                    continue;
                }

                if (string.Equals(name, "domain", StringComparison.OrdinalIgnoreCase))
                {
                    cookie["domain"] = value;
                }
                else if (string.Equals(name, "path", StringComparison.OrdinalIgnoreCase))
                {
                    cookie["path"] = value;
                }
                else if (string.Equals(name, "expires", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryHarCookieDate(value, out string expires))
                    {
                        cookie["expires"] = expires;
                    }
                }
                else if (string.Equals(name, "max-age", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out double maxAge))
                    {
                        cookie["expires"] = DateTimeOffset.UtcNow.AddSeconds(maxAge).ToString("o", CultureInfo.InvariantCulture);
                    }
                }
                else if (string.Equals(name, "httponly", StringComparison.OrdinalIgnoreCase))
                {
                    cookie["httpOnly"] = true;
                }
                else if (string.Equals(name, "secure", StringComparison.OrdinalIgnoreCase))
                {
                    cookie["secure"] = true;
                }
                else if (string.Equals(name, "samesite", StringComparison.OrdinalIgnoreCase))
                {
                    cookie["sameSite"] = value;
                }
            }

            return first ? null : cookie;
        }

        private static bool TryHarCookieDate(string value, out string iso)
        {
            iso = null;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            if (DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out DateTimeOffset parsed)
                || DateTimeOffset.TryParse(value, out parsed))
            {
                iso = parsed.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
                return true;
            }

            return false;
        }

        private static JsonObject BuildPostData(byte[] postBytes, string mimeType)
        {
            if (postBytes == null || postBytes.Length == 0)
            {
                return null;
            }

            string mime = string.IsNullOrEmpty(mimeType) ? "application/octet-stream" : mimeType;
            string media = mime;
            int semi = media.IndexOf(';', StringComparison.Ordinal);
            if (semi >= 0)
            {
                media = media.Substring(0, semi).Trim();
            }

            JsonArray parameters = new JsonArray();
            string text = string.Empty;
            if (string.Equals(media, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                text = Encoding.UTF8.GetString(postBytes);
                parameters = ToQueryString("http://playwright.invalid/?" + text);
            }
            else if (!string.Equals(media, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                text = Encoding.UTF8.GetString(postBytes);
            }

            return new JsonObject
            {
                ["mimeType"] = mime,
                ["params"] = parameters,
                ["text"] = text,
            };
        }

        private static string AbsoluteRedirectUrl(string requestUrl, string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return string.Empty;
            }

            if (Uri.TryCreate(location, UriKind.Absolute, out Uri absolute))
            {
                return absolute.AbsoluteUri;
            }

            if (Uri.TryCreate(requestUrl, UriKind.Absolute, out Uri request)
                && Uri.TryCreate(request, location, out Uri resolved))
            {
                return resolved.AbsoluteUri;
            }

            return location;
        }

        private static JsonObject ToTimings(RequestTimingResult timing)
        {
            if (timing == null)
            {
                return new JsonObject
                {
                    ["send"] = 0,
                    ["wait"] = 0,
                    ["receive"] = 0,
                };
            }

            return new JsonObject
            {
                ["blocked"] = -1,
                ["dns"] = Elapsed(timing.DomainLookupStart, timing.DomainLookupEnd),
                ["connect"] = Elapsed(timing.ConnectStart, timing.ConnectEnd),
                ["ssl"] = Elapsed(timing.SecureConnectionStart, timing.ConnectEnd),
                ["send"] = 0,
                ["wait"] = Elapsed(timing.RequestStart, timing.ResponseStart),
                ["receive"] = Elapsed(timing.ResponseStart, timing.ResponseEnd),
            };
        }

        private static double Elapsed(float start, float end)
        {
            if (start < 0 || end < 0 || end < start)
            {
                return -1;
            }

            return end - start;
        }

        private static DateTimeOffset StartedAt(IRequest request)
        {
            RequestTimingResult timing = request?.Timing;
            if (timing != null && timing.StartTime > 0)
            {
                return DateTimeOffset.FromUnixTimeMilliseconds((long)timing.StartTime);
            }

            return DateTimeOffset.UtcNow;
        }

        private static double TotalTime(RequestTimingResult timing)
        {
            if (timing == null || timing.ResponseEnd < 0 || timing.RequestStart < 0)
            {
                return 0;
            }

            float start = timing.RequestStart >= 0 ? timing.RequestStart : 0;
            return timing.ResponseEnd >= start ? timing.ResponseEnd - start : 0;
        }

        private static bool LooksLikeText(string mimeType)
        {
            if (string.IsNullOrEmpty(mimeType))
            {
                return true;
            }

            string mime = mimeType;
            int semi = mime.IndexOf(';', StringComparison.Ordinal);
            if (semi >= 0)
            {
                mime = mime.Substring(0, semi);
            }

            mime = mime.Trim();
            return mime.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                || mime.EndsWith("json", StringComparison.OrdinalIgnoreCase)
                || mime.EndsWith("javascript", StringComparison.OrdinalIgnoreCase)
                || mime.EndsWith("xml", StringComparison.OrdinalIgnoreCase)
                || mime.EndsWith("svg+xml", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mime, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFavicon(string url)
        {
            return !string.IsNullOrEmpty(url)
                && url.Contains("favicon", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGzip(IEnumerable<KeyValuePair<string, string>> headers)
        {
            if (headers == null)
            {
                return false;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.Equals(header.Key, "content-encoding", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(header.Value)
                    && header.Value.Contains("gzip", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static ResponseServerAddrResult AddrFromUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return null;
            }

            string host = uri.IdnHost;
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "127.0.0.1", StringComparison.Ordinal)
                || string.Equals(host, "::1", StringComparison.Ordinal))
            {
                return new ResponseServerAddrResult
                {
                    IpAddress = string.Equals(host, "::1", StringComparison.Ordinal) ? "[::1]" : "127.0.0.1",
                    Port = uri.Port,
                };
            }

            return null;
        }

        private static byte[] MaybeDecompressGzip(byte[] body, IEnumerable<KeyValuePair<string, string>> headers)
        {
            if (body == null || body.Length < 2)
            {
                return body;
            }

            bool magic = body[0] == 0x1f && body[1] == 0x8b;
            if (!magic && !IsGzip(headers))
            {
                return body;
            }

            if (!magic)
            {
                return body;
            }

            try
            {
                using MemoryStream input = new MemoryStream(body);
                using GZipStream gzip = new GZipStream(input, CompressionMode.Decompress);
                using MemoryStream output = new MemoryStream();
                gzip.CopyTo(output);
                return output.ToArray();
            }
            catch (InvalidDataException)
            {
                return body;
            }
        }

        private static bool PathsMatch(string left, string right)
        {
            if (string.Equals(left, right, StringComparison.Ordinal))
            {
                return true;
            }

            if (!Uri.TryCreate(left, UriKind.Absolute, out Uri leftUri)
                || !Uri.TryCreate(right, UriKind.Absolute, out Uri rightUri))
            {
                return false;
            }

            return string.Equals(leftUri.AbsolutePath, rightUri.AbsolutePath, StringComparison.Ordinal);
        }

        private static async Task<byte[]> BodyFromPageAsync(IRequest request)
        {
            if (request == null || !request.IsNavigationRequest)
            {
                return null;
            }

            IPage page = PageOf(request);
            if (page == null)
            {
                return null;
            }

            try
            {
                string pageUrl = page.Url ?? string.Empty;
                string requestUrl = request.Url ?? string.Empty;
                if (string.IsNullOrEmpty(pageUrl)
                    || string.IsNullOrEmpty(requestUrl)
                    || !PathsMatch(pageUrl, requestUrl))
                {
                    return null;
                }

                string text = await page.EvaluateAsync<string>(
                    @"(() => {
                        if (document.body)
                            return document.body.innerHTML;
                        return document.documentElement ? document.documentElement.outerHTML : '';
                    })()").ConfigureAwait(false);
                return string.IsNullOrEmpty(text) ? null : Encoding.UTF8.GetBytes(text);
            }
            catch (PlaywrightNativeException)
            {
                return null;
            }
        }

        private static int GzipLength(byte[] body)
        {
            if (body == null || body.Length == 0)
            {
                return 0;
            }

            using MemoryStream output = new MemoryStream();
            using (GZipStream gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                gzip.Write(body, 0, body.Length);
            }

            return (int)output.Length;
        }

        private static IPage PageOf(IRequest request)
        {
            if (request is IHasOwningPage hasPage)
            {
                return hasPage.OwningPage;
            }

            try
            {
                return request?.Frame?.Page;
            }
            catch (PlaywrightNativeException)
            {
                return null;
            }
        }

        private static string FormatIp(string ip)
        {
            if (string.IsNullOrEmpty(ip))
            {
                return ip;
            }

            if (IPAddress.TryParse(ip, out IPAddress address)
                && address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return "[" + address + "]";
            }

            return ip;
        }

        private static JsonObject SecurityNode(ResponseSecurityDetailsResult details)
        {
            JsonObject node = new JsonObject();
            if (details == null)
            {
                return node;
            }

            if (!string.IsNullOrEmpty(details.Protocol))
            {
                node["protocol"] = details.Protocol;
            }

            if (!string.IsNullOrEmpty(details.SubjectName))
            {
                node["subjectName"] = details.SubjectName;
            }

            if (!string.IsNullOrEmpty(details.Issuer))
            {
                node["issuer"] = details.Issuer;
            }

            if (SecurityDetailsUnix.TryGet(details, out long validFromUnix, out long validToUnix))
            {
                if (validFromUnix > 0)
                {
                    node["validFrom"] = validFromUnix;
                }

                if (validToUnix > 0)
                {
                    node["validTo"] = validToUnix;
                }
            }
            else
            {
                if (details.ValidFrom.GetValueOrDefault() > 0)
                {
                    node["validFrom"] = (long)Math.Round((double)details.ValidFrom.Value);
                }

                if (details.ValidTo.GetValueOrDefault() > 0)
                {
                    node["validTo"] = (long)Math.Round((double)details.ValidTo.Value);
                }
            }

            return node;
        }

        private static string Sha1Name(byte[] body, string mimeType)
        {
#pragma warning disable CA5350 // Official Playwright attach mode names bodies with SHA-1.
            byte[] hash = SHA1.HashData(body ?? Array.Empty<byte>());
#pragma warning restore CA5350
            StringBuilder hex = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
            {
                hex.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return hex + "." + MimeExtension(mimeType);
        }

        private static string MimeExtension(string mimeType)
        {
            if (string.IsNullOrEmpty(mimeType))
            {
                return "dat";
            }

            string mime = mimeType;
            int semi = mime.IndexOf(';', StringComparison.Ordinal);
            if (semi >= 0)
            {
                mime = mime.Substring(0, semi);
            }

            mime = mime.Trim();
            if (string.Equals(mime, "text/html", StringComparison.OrdinalIgnoreCase))
            {
                return "html";
            }

            if (string.Equals(mime, "text/css", StringComparison.OrdinalIgnoreCase))
            {
                return "css";
            }

            if (string.Equals(mime, "text/plain", StringComparison.OrdinalIgnoreCase))
            {
                return "txt";
            }

            if (string.Equals(mime, "image/png", StringComparison.OrdinalIgnoreCase))
            {
                return "png";
            }

            if (string.Equals(mime, "image/jpeg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mime, "image/jpg", StringComparison.OrdinalIgnoreCase))
            {
                return "jpeg";
            }

            if (string.Equals(mime, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                return "json";
            }

            if (string.Equals(mime, "application/javascript", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mime, "text/javascript", StringComparison.OrdinalIgnoreCase))
            {
                return "js";
            }

            return "dat";
        }

        private static async Task ApplyConnectionAsync(JsonObject entry, IResponse response)
        {
            if (response == null)
            {
                entry["_securityDetails"] = new JsonObject();
                return;
            }

            ResponseServerAddrResult addr = null;
            try
            {
                addr = await response.ServerAddrAsync().ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }

            // Official WebKit omits IP/port on 3xx hops (no loadingFinished
            // metrics). Final 2xx hops must still report localhost when the
            // inspector never attached remoteAddress (process-swap / close).
            if (NeedsLocalAddrFallback(addr, response.Status))
            {
                ResponseServerAddrResult fromUrl = AddrFromUrl(response.Url);
                if (fromUrl != null)
                {
                    addr = MergeAddr(addr, fromUrl);
                }
            }

            if (addr != null)
            {
                if (!string.IsNullOrEmpty(addr.IpAddress))
                {
                    entry["serverIPAddress"] = FormatIp(addr.IpAddress);
                }

                if (addr.Port > 0)
                {
                    entry["_serverPort"] = addr.Port;
                }
            }

            try
            {
                ResponseSecurityDetailsResult details = await response.SecurityDetailsAsync().ConfigureAwait(false);
                entry["_securityDetails"] = SecurityNode(details);
            }
            catch (PlaywrightNativeException)
            {
                entry["_securityDetails"] = new JsonObject();
            }
        }

        private static bool NeedsLocalAddrFallback(ResponseServerAddrResult addr, int status)
            => status >= 200
                && status < 300
                && (addr == null || string.IsNullOrEmpty(addr.IpAddress) || addr.Port <= 0);

        private static ResponseServerAddrResult MergeAddr(ResponseServerAddrResult existing, ResponseServerAddrResult fallback)
        {
            if (existing == null)
            {
                return fallback;
            }

            return new ResponseServerAddrResult
            {
                IpAddress = string.IsNullOrEmpty(existing.IpAddress) ? fallback.IpAddress : existing.IpAddress,
                Port = existing.Port > 0 ? existing.Port : fallback.Port,
            };
        }

        private static bool IsWebSocketTraffic(IRequest request)
        {
            if (request == null)
            {
                return false;
            }

            if (LocaleAcceptLanguage.IsWebSocket(request.ResourceType))
            {
                return true;
            }

            string url = request.Url ?? string.Empty;
            if (url.StartsWith("ws://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (KeyValuePair<string, string> header in request.Headers)
            {
                if (string.Equals(header.Key, "Upgrade", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(header.Value)
                    && header.Value.Contains("websocket", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool OmitWebSocketFrames()
            => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLAYWRIGHT_HAR_NO_WEBSOCKET_FRAMES"));

        private static int HeaderBlockSize(IEnumerable<KeyValuePair<string, string>> headers)
        {
            int result = 0;
            if (headers == null)
            {
                return result;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                result += (header.Key ?? string.Empty).Length + (header.Value ?? string.Empty).Length + 4;
            }

            return result;
        }

        private static int RequestHeadersSize(IEnumerable<KeyValuePair<string, string>> headers, string url, string method)
        {
            int result = 4;
            result += string.IsNullOrEmpty(method) ? 3 : method.Length;
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                result += uri.AbsolutePath.Length;
            }

            result += 8;
            result += HeaderBlockSize(headers);
            return result;
        }

        private static int ResponseHeadersSize(IEnumerable<KeyValuePair<string, string>> headers, string statusText)
        {
            int result = 4;
            result += 8;
            result += 3;
            result += (statusText ?? string.Empty).Length;
            result += HeaderBlockSize(headers);
            result += 2;
            return result;
        }

        private static int WebSocketFrameHeaderSize(int length)
        {
            int headerSize = 6;
            if (length >= 65536)
            {
                headerSize += 8;
            }
            else if (length > 125)
            {
                headerSize += 2;
            }

            return headerSize;
        }

        private sealed class Session
        {
            private readonly IBrowserContext _context;
            private readonly IAPIRequestContext _api;
            private readonly string _path;
            private readonly string _jsonPath;
            private readonly string _stagingDir;
            private readonly bool _isZip;
            private readonly bool _omitContent;
            private readonly bool _attachContent;
            private readonly bool _omitWebSocketFrames;
            private readonly bool _recordBrowser;
            private readonly bool _recordApi;
            private readonly bool _slimMode;
            private readonly string _resourcesDir;
            private readonly string _urlFilter;
            private readonly Regex _urlRegex;
            private readonly object _gate = new();
            private readonly List<PendingEntry> _entries = new();
            private readonly Dictionary<IRequest, PendingEntry> _byRequest = new();
            private readonly Dictionary<IPage, PageRecord> _pages = new();
            private readonly List<JsonObject> _apiEntries = new();
            private bool _detached;

            internal Session(
                IBrowserContext context,
                string path,
                bool omitContent,
                string urlFilter,
                bool attachContent,
                Regex urlRegex,
                bool recordBrowser,
                bool recordApi,
                bool slimMode,
                IAPIRequestContext api,
                string resourcesDir)
            {
                _context = context;
                _api = api;
                _path = path;
                _omitContent = omitContent;
                _attachContent = attachContent;
                _omitWebSocketFrames = omitContent || OmitWebSocketFrames();
                _recordBrowser = recordBrowser && context != null;
                _recordApi = recordApi;
                _slimMode = slimMode;
                _resourcesDir = resourcesDir;
                _urlFilter = urlFilter;
                _urlRegex = urlRegex;
                _isZip = path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
                if (_isZip)
                {
                    _stagingDir = Path.Combine(Path.GetTempPath(), "pw-har-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(_stagingDir);
                    _jsonPath = Path.Combine(_stagingDir, "har.har");
                }
                else
                {
                    _stagingDir = Path.GetDirectoryName(path);
                    _jsonPath = path;
                }

                if (_recordBrowser)
                {
                    _context.Request += OnRequest;
                    _context.Response += OnResponse;
                    _context.RequestFailed += OnRequestFailed;
                    _context.Page += OnPage;
                    if (_context.Pages != null)
                    {
                        foreach (IPage existing in _context.Pages)
                        {
                            TrackPage(existing);
                        }
                    }
                }
            }

            internal void AddApiHop(ApiHarHop hop)
            {
                if (!_recordApi || hop == null)
                {
                    return;
                }

                string url = hop.Url ?? string.Empty;
                string baseUrl = _context is IHasBaseUrl hasBase ? hasBase.BaseURL : null;
                if ((!string.IsNullOrEmpty(_urlFilter) || _urlRegex != null)
                    && !UrlMatcher.Matches(url, _urlFilter, _urlRegex, null, baseUrl))
                {
                    return;
                }

                JsonObject entry = BuildApiEntry(hop);
                lock (_gate)
                {
                    _apiEntries.Add(entry);
                }
            }

            internal void Detach()
            {
                if (_detached)
                {
                    return;
                }

                _detached = true;
                if (_recordBrowser && _context != null)
                {
                    _context.Request -= OnRequest;
                    _context.Response -= OnResponse;
                    _context.RequestFailed -= OnRequestFailed;
                    _context.Page -= OnPage;
                }

                lock (_gate)
                {
                    foreach (PageRecord record in _pages.Values)
                    {
                        if (record.Page != null)
                        {
                            record.Page.WebSocket -= OnPageWebSocket;
                        }

                        record.Detach();
                    }

                    UnhookWebSockets();
                }

                if (_api != null)
                {
                    UnregisterApiSession(_api, this);
                }
            }

            internal void Observe(IPage page, IWebSocket socket)
                => OnPageWebSocket(page, socket);

            internal async Task FlushAsync()
            {
                PendingEntry[] snapshot;
                lock (_gate)
                {
                    snapshot = _entries.ToArray();
                }

                // Capture bodies before Detach so page evaluate / protocol
                // fallbacks still work (macOS WebKit CI was losing content.text).
                foreach (PendingEntry pending in snapshot)
                {
                    if (pending.BodyTask == null
                        && pending.Response != null
                        && !_omitContent)
                    {
                        pending.BodyTask = CaptureBodyAsync(pending, pending.Response);
                    }

                    if (pending.BodyTask == null || pending.BodyTask.IsCompleted)
                    {
                        continue;
                    }

                    // Finished responses may still be retrying getResponseBody
                    // (WebKit gzip / HTTP2). Unfinished chunked bodies stay at
                    // 250ms so context.CloseAsync cannot hang.
                    TimeSpan wait = pending.Response != null && string.IsNullOrEmpty(pending.FailureText)
                        ? TimeSpan.FromSeconds(5)
                        : TimeSpan.FromMilliseconds(250);
                    try
                    {
                        await pending.BodyTask.WaitAsync(wait).ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                    }
                    catch (PlaywrightNativeException)
                    {
                    }
                }

                // Refresh page HTML while the context is still live so navigation
                // entries can embed/attach content when getResponseBody raced away.
                if (!_omitContent && !_slimMode)
                {
                    List<PageRecord> pageRecords;
                    lock (_gate)
                    {
                        pageRecords = new List<PageRecord>(_pages.Values);
                    }

                    foreach (PageRecord record in pageRecords)
                    {
                        await record.RefreshAsync().ConfigureAwait(false);
                    }

                    foreach (PendingEntry pending in snapshot)
                    {
                        if (pending.Body != null && pending.Body.Length > 0)
                        {
                            continue;
                        }

                        if (pending.Request == null || !pending.Request.IsNavigationRequest)
                        {
                            continue;
                        }

                        byte[] fromPage = PageText(pending.Request)
                            ?? await BodyFromPageAsync(pending.Request).ConfigureAwait(false);
                        if (fromPage != null && fromPage.Length > 0)
                        {
                            pending.Body = fromPage;
                        }
                    }
                }

                Detach();

                JsonArray pages = _slimMode
                    ? new JsonArray()
                    : await BuildPagesAsync(snapshot).ConfigureAwait(false);
                JsonArray entries = new();
                foreach (PendingEntry pending in snapshot)
                {
                    entries.Add(await BuildEntryAsync(pending).ConfigureAwait(false));
                }

                lock (_gate)
                {
                    foreach (JsonObject apiEntry in _apiEntries)
                    {
                        entries.Add(apiEntry);
                    }
                }

                JsonObject log = new JsonObject
                {
                    ["version"] = "1.2",
                    ["creator"] = new JsonObject
                    {
                        ["name"] = "Playwright",
                        ["version"] = CreatorVersion(),
                    },
                    ["browser"] = BrowserNode(_context),
                    ["entries"] = entries,
                };
                if (!_slimMode)
                {
                    log["pages"] = pages;
                }

                JsonObject root = new()
                {
                    ["log"] = log,
                };

                string json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                PathIo.WriteText(_jsonPath, json);
                if (_isZip)
                {
                    if (File.Exists(_path))
                    {
                        File.Delete(_path);
                    }

                    string zipDir = Path.GetDirectoryName(_path);
                    if (!string.IsNullOrEmpty(zipDir))
                    {
                        Directory.CreateDirectory(zipDir);
                    }

                    ZipFile.CreateFromDirectory(_stagingDir, _path);
                    try
                    {
                        Directory.Delete(_stagingDir, recursive: true);
                    }
                    catch (IOException)
                    {
                    }
                }
            }

            private void OnRequest(object sender, IRequest request)
            {
                if (request == null)
                {
                    return;
                }

                if (IsWebSocketTraffic(request))
                {
                    return;
                }

                if (IsFavicon(request.Url) || IsFavicon(request.RedirectedFrom?.Url))
                {
                    return;
                }

                string baseUrl = _context is IHasBaseUrl hasBase ? hasBase.BaseURL : null;
                if ((!string.IsNullOrEmpty(_urlFilter) || _urlRegex != null)
                    && !UrlMatcher.Matches(request.Url, _urlFilter, _urlRegex, null, baseUrl))
                {
                    return;
                }

                TrackPage(PageOf(request));
                PendingEntry pending = new()
                {
                    Request = request,
                    Started = StartedAt(request),
                };

                lock (_gate)
                {
                    _entries.Add(pending);
                    _byRequest[request] = pending;
                    if (request.RedirectedFrom != null
                        && _byRequest.TryGetValue(request.RedirectedFrom, out PendingEntry from))
                    {
                        from.RedirectUrl = request.Url;
                    }

                    IPage page = PageOf(request);
                    if (page != null && _pages.TryGetValue(page, out PageRecord record))
                    {
                        pending.PageId = record.Id;
                    }
                }
            }

            private void OnResponse(object sender, IResponse response)
            {
                if (response?.Request == null)
                {
                    return;
                }

                PendingEntry pending = null;
                lock (_gate)
                {
                    if (_byRequest.TryGetValue(response.Request, out pending))
                    {
                        pending.Response = response;
                    }
                }

                if (pending != null && !_omitContent)
                {
                    // Start body capture while the page/network are still live.
                    // WebKit often returns empty if getResponseBody runs too
                    // early, so CaptureBodyAsync settles briefly then retries.
                    pending.BodyTask = CaptureBodyAsync(pending, response);
                }
            }

            private async Task CaptureBodyAsync(PendingEntry pending, IResponse response)
            {
                if (pending == null || response == null)
                {
                    return;
                }

                try
                {
                    byte[] body = await ReadBodyWithTimeoutAsync(response, TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                    if (body != null && body.Length > 0)
                    {
                        pending.Body = body;
                        return;
                    }

                    // One more attempt after the response has had time to buffer.
                    await Task.Delay(50).ConfigureAwait(false);
                    body = await ReadBodyWithTimeoutAsync(response, TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                    if (body != null && body.Length > 0)
                    {
                        pending.Body = body;
                        return;
                    }

                    // WebKit Linux CI often loses Network.getResponseBody under
                    // load; fall back to the live page document while it is open.
                    if (pending.Request != null && pending.Request.IsNavigationRequest)
                    {
                        byte[] fromPage = PageText(pending.Request)
                            ?? await BodyFromPageAsync(pending.Request).ConfigureAwait(false);
                        if (fromPage != null && fromPage.Length > 0)
                        {
                            pending.Body = fromPage;
                        }
                    }
                }
                catch (TimeoutException)
                {
                }
                catch (PlaywrightNativeException)
                {
                }
            }

            private async Task<byte[]> ReadBodyWithTimeoutAsync(IResponse response, TimeSpan timeout)
            {
                if (response is PlaywrightNative.WebKit.WKResponse wkResponse)
                {
                    return await wkResponse.PrefetchBodyAsync().WaitAsync(timeout).ConfigureAwait(false);
                }

                return await response.GetBodyAsync().WaitAsync(timeout).ConfigureAwait(false);
            }

            private void OnRequestFailed(object sender, IRequest request)
            {
                if (request == null)
                {
                    return;
                }

                lock (_gate)
                {
                    if (_byRequest.TryGetValue(request, out PendingEntry pending))
                    {
                        pending.FailureText = request.Failure;
                    }
                }
            }

            private void OnPage(object sender, IPage page) => TrackPage(page);

            private void TrackPage(IPage page)
            {
                if (page == null)
                {
                    return;
                }

                lock (_gate)
                {
                    if (_pages.ContainsKey(page))
                    {
                        return;
                    }

                    PageRecord record = new PageRecord
                    {
                        Page = page,
                        Id = Guid.NewGuid().ToString(),
                        Started = DateTimeOffset.UtcNow,
                    };
                    record.Attach();
                    page.WebSocket += OnPageWebSocket;
                    _pages[page] = record;
                }
            }

            private void OnPageWebSocket(object sender, IWebSocket socket)
            {
                if (socket == null)
                {
                    return;
                }

                string url = socket.Url ?? string.Empty;
                string baseUrl = _context is IHasBaseUrl hasBase ? hasBase.BaseURL : null;
                if ((!string.IsNullOrEmpty(_urlFilter) || _urlRegex != null)
                    && !UrlMatcher.Matches(url, _urlFilter, _urlRegex, null, baseUrl))
                {
                    return;
                }

                IPage page = sender as IPage;
                TrackPage(page);
                DateTimeOffset started = DateTimeOffset.UtcNow;
                if (socket is IHasHarWebSocket hasSocket && hasSocket.Har.WallTimeMs > 0)
                {
                    started = hasSocket.Har.Started;
                }

                PendingEntry pending = new()
                {
                    WebSocket = socket,
                    Started = started,
                };

                EventHandler<IWebSocketFrame> onSent = (_, frame) => RecordWebSocketMessage(pending, "send", frame);
                EventHandler<IWebSocketFrame> onReceived = (_, frame) => RecordWebSocketMessage(pending, "receive", frame);
                EventHandler<string> onError = (_, message) =>
                {
                    if (!string.IsNullOrEmpty(message))
                    {
                        pending.FailureText = message;
                    }
                };

                pending.OnWebSocketFrameSent = onSent;
                pending.OnWebSocketFrameReceived = onReceived;
                pending.OnWebSocketError = onError;

                lock (_gate)
                {
                    foreach (PendingEntry existing in _entries)
                    {
                        if (existing.WebSocket != null
                            && string.Equals(existing.WebSocket.Url, url, StringComparison.Ordinal))
                        {
                            return;
                        }
                    }

                    socket.FrameSent += onSent;
                    socket.FrameReceived += onReceived;
                    socket.SocketError += onError;
                    _entries.Add(pending);
                    if (page != null && _pages.TryGetValue(page, out PageRecord record))
                    {
                        pending.PageId = record.Id;
                    }
                }
            }

            private void UnhookWebSockets()
            {
                foreach (PendingEntry pending in _entries)
                {
                    IWebSocket socket = pending.WebSocket;
                    if (socket == null)
                    {
                        continue;
                    }

                    if (pending.OnWebSocketFrameSent != null)
                    {
                        socket.FrameSent -= pending.OnWebSocketFrameSent;
                    }

                    if (pending.OnWebSocketFrameReceived != null)
                    {
                        socket.FrameReceived -= pending.OnWebSocketFrameReceived;
                    }

                    if (pending.OnWebSocketError != null)
                    {
                        socket.SocketError -= pending.OnWebSocketError;
                    }
                }
            }

            private void RecordWebSocketMessage(PendingEntry pending, string type, IWebSocketFrame frame)
            {
                if (pending == null || frame == null)
                {
                    return;
                }

                int opcode = 1;
                string data;
                int length;
                byte[] binary = frame.Binary;
                bool isBinary = (frame is WebSocketFrame parsed && parsed.Opcode == 2)
                    || (binary != null && binary.Length > 0 && string.IsNullOrEmpty(frame.Text));
                if (isBinary)
                {
                    opcode = 2;
                    binary ??= Array.Empty<byte>();
                    data = Convert.ToBase64String(binary);
                    length = binary.Length;
                }
                else
                {
                    data = frame.Text ?? string.Empty;
                    length = Encoding.UTF8.GetByteCount(data);
                }

                if (frame is WebSocketFrame typed && typed.Opcode > 0)
                {
                    opcode = typed.Opcode;
                }

                double wall = frame is WebSocketFrame timed && timed.WallTimeMs > 0
                    ? timed.WallTimeMs
                    : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                lock (_gate)
                {
                    if (wall < pending.OldestMessageMs)
                    {
                        pending.OldestMessageMs = wall;
                    }

                    if (wall > pending.NewestMessageMs)
                    {
                        pending.NewestMessageMs = wall;
                    }

                    if (pending.OldestMessageMs != double.MaxValue)
                    {
                        pending.WebSocketTime = Math.Max(0, pending.NewestMessageMs - pending.OldestMessageMs);
                    }

                    if (string.Equals(type, "receive", StringComparison.Ordinal) && !_slimMode)
                    {
                        int incoming = pending.WebSocketTransferSize < 0 ? 0 : pending.WebSocketTransferSize;
                        pending.WebSocketTransferSize = incoming + WebSocketFrameHeaderSize(length) + length;
                    }

                    if (_omitWebSocketFrames)
                    {
                        return;
                    }

                    JsonObject message = new JsonObject
                    {
                        ["type"] = type,
                        ["time"] = wall,
                        ["opcode"] = opcode,
                        ["data"] = data,
                    };

                    if (_attachContent)
                    {
                        AppendWebSocketJsonl(pending, message);
                    }
                    else
                    {
                        pending.WebSocketMessages ??= new JsonArray();
                        pending.WebSocketMessages.Add(message);
                    }
                }
            }

            private void AppendWebSocketJsonl(PendingEntry pending, JsonObject message)
            {
                if (string.IsNullOrEmpty(pending.WebSocketAttachFile))
                {
                    pending.WebSocketAttachFile = Guid.NewGuid().ToString("N") + ".jsonl";
                }

                string dir = !string.IsNullOrEmpty(_stagingDir)
                    ? _stagingDir
                    : Path.GetDirectoryName(_jsonPath);
                if (string.IsNullOrEmpty(dir))
                {
                    dir = ".";
                }

                Directory.CreateDirectory(dir);
                File.AppendAllText(
                    Path.Combine(dir, pending.WebSocketAttachFile),
                    message.ToJsonString() + "\n");
            }

            private async Task<JsonArray> BuildPagesAsync(PendingEntry[] snapshot)
            {
                List<PageRecord> records;
                lock (_gate)
                {
                    foreach (PendingEntry pending in snapshot)
                    {
                        TrackPage(PageOf(pending.Request));
                    }

                    records = new List<PageRecord>(_pages.Values);
                }

                JsonArray pages = new();
                foreach (PageRecord record in records)
                {
                    await record.RefreshAsync().ConfigureAwait(false);
                    pages.Add(record.ToJson());
                }

                foreach (PendingEntry pending in snapshot)
                {
                    IPage page = PageOf(pending.Request);
                    lock (_gate)
                    {
                        if (page != null && _pages.TryGetValue(page, out PageRecord record))
                        {
                            pending.PageId = record.Id;
                        }
                    }
                }

                return pages;
            }

            private async Task<JsonObject> BuildEntryAsync(PendingEntry pending)
            {
                if (pending.WebSocket != null)
                {
                    return BuildWebSocketEntry(pending);
                }

                IRequest request = pending.Request;
                IResponse response = pending.Response;
                if (request != null)
                {
                    try
                    {
                        IResponse latest = await AwaitOrDefaultAsync(request.GetResponseAsync()).ConfigureAwait(false);
                        if (latest != null)
                        {
                            response = latest;
                        }
                    }
                    catch (PlaywrightNativeException)
                    {
                    }
                }

                string url = request?.Url ?? string.Empty;
                string method = string.IsNullOrEmpty(request?.Method) ? "GET" : request.Method;
                byte[] postBytes = request?.PostDataBuffer;
                if ((postBytes == null || postBytes.Length == 0) && !string.IsNullOrEmpty(request?.PostData))
                {
                    postBytes = Encoding.UTF8.GetBytes(request.PostData);
                }

                IEnumerable<KeyValuePair<string, string>> requestHeaders = await HeadersFromAsync(request).ConfigureAwait(false);
                string requestMime = FindHeader(requestHeaders, "content-type") ?? "application/octet-stream";
                string httpVersion = "HTTP/1.1";
                if (response != null)
                {
                    try
                    {
                        httpVersion = await response.HttpVersionAsync().ConfigureAwait(false) ?? httpVersion;
                    }
                    catch (PlaywrightNativeException)
                    {
                    }
                }

                RequestSizesResult sizes = null;
                if (request != null)
                {
                    try
                    {
                        // Official HAR flush races sizes with page/context close so a
                        // hanging chunked response cannot block context.CloseAsync.
                        sizes = await AwaitOrDefaultAsync(request.GetSizesAsync()).ConfigureAwait(false);
                    }
                    catch (PlaywrightNativeException)
                    {
                        sizes = null;
                    }
                }

                int requestHeadersSize = _slimMode ? -1 : (sizes?.RequestHeadersSize ?? -1);
                int requestBodySize = _slimMode
                    ? -1
                    : (sizes != null ? sizes.RequestBodySize : (postBytes == null ? 0 : postBytes.Length));

                JsonObject requestNode = new()
                {
                    ["method"] = method,
                    ["url"] = url,
                    ["httpVersion"] = httpVersion,
                    ["cookies"] = ToCookies(FindHeader(requestHeaders, "cookie")),
                    ["headers"] = ToHeaderArray(requestHeaders),
                    ["queryString"] = ToQueryString(url),
                    ["headersSize"] = requestHeadersSize,
                    ["bodySize"] = requestBodySize,
                };

                JsonObject postData = BuildPostData(postBytes, requestMime);
                if (postData != null)
                {
                    requestNode["postData"] = postData;
                }

                int status = -1;
                string statusText = request?.Failure ?? string.Empty;
                IEnumerable<KeyValuePair<string, string>> responseHeaders = null;
                string mimeType = "x-unknown";
                string redirectUrl = string.Empty;
                byte[] body = null;

                if (response != null)
                {
                    status = response.Status;
                    statusText = response.StatusText ?? string.Empty;
                    responseHeaders = await HeadersFromAsync(response).ConfigureAwait(false);
                    mimeType = FindHeader(responseHeaders, "content-type") ?? mimeType;
                    redirectUrl = FindHeader(responseHeaders, "location") ?? string.Empty;
                    if (!_omitContent)
                    {
                        body = pending.Body;
                        if (body == null || body.Length == 0)
                        {
                            try
                            {
                                body = await response.GetBodyAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                                if (body != null && body.Length > 0)
                                {
                                    pending.Body = body;
                                }
                            }
                            catch (TimeoutException)
                            {
                            }
                            catch (PlaywrightNativeException)
                            {
                            }
                        }
                    }
                }

                bool gzip = IsGzip(responseHeaders);
                if (!gzip && response != null)
                {
                    try
                    {
                        string encoding = await response.HeaderValueAsync("content-encoding").ConfigureAwait(false);
                        gzip = !string.IsNullOrEmpty(encoding)
                            && encoding.Contains("gzip", StringComparison.OrdinalIgnoreCase);
                    }
                    catch (PlaywrightNativeException)
                    {
                    }
                }

                if (!_omitContent)
                {
                    body = MaybeDecompressGzip(body, responseHeaders);
                    if (request != null
                        && request.IsNavigationRequest
                        && (body == null || body.Length < 1000))
                    {
                        byte[] fromPage = PageText(request) ?? await BodyFromPageAsync(request).ConfigureAwait(false);
                        if (fromPage != null && fromPage.Length > (body == null ? 0 : body.Length))
                        {
                            body = fromPage;
                        }
                    }
                }

                int decodedSize = body == null ? 0 : body.Length;
                int encodedSize = sizes?.ResponseBodySize ?? decodedSize;
                if (gzip && decodedSize > 0 && (encodedSize <= 0 || encodedSize >= decodedSize))
                {
                    // WebKit often reports decoded metrics.responseBodyBytesReceived
                    // for gzip responses; recompute encoded size from the body.
                    encodedSize = GzipLength(body);
                }

                int compression = encodedSize >= 0 && decodedSize > encodedSize
                    ? decodedSize - encodedSize
                    : 0;
                JsonObject content = new()
                {
                    ["size"] = decodedSize,
                    ["mimeType"] = mimeType,
                    ["compression"] = compression,
                };

                if (_attachContent && body != null && body.Length > 0)
                {
                    content["_file"] = WriteAttachedBody(body, mimeType);
                }
                else if (!_omitContent && body != null && body.Length > 0)
                {
                    if (LooksLikeText(mimeType))
                    {
                        content["text"] = Encoding.UTF8.GetString(body);
                    }
                    else
                    {
                        content["text"] = Convert.ToBase64String(body);
                        content["encoding"] = "base64";
                    }
                }

                if (string.IsNullOrEmpty(pending.RedirectUrl))
                {
                    redirectUrl = AbsoluteRedirectUrl(url, redirectUrl);
                }
                else
                {
                    redirectUrl = pending.RedirectUrl;
                }

                bool failed = response == null || !string.IsNullOrEmpty(pending.FailureText) || !string.IsNullOrEmpty(request?.Failure);
                int responseHeadersSize = _slimMode ? -1 : (sizes?.ResponseHeadersSize ?? -1);
                int responseBodySize = _slimMode
                    ? -1
                    : (failed && body == null
                        ? -1
                        : (sizes?.ResponseBodySize ?? (body == null ? -1 : body.Length)));
                int transferSize = _slimMode || failed
                    ? -1
                    : (responseHeadersSize >= 0 && responseBodySize >= 0
                        ? responseHeadersSize + responseBodySize
                        : -1);

                JsonObject responseNode = new()
                {
                    ["status"] = status,
                    ["statusText"] = statusText,
                    ["httpVersion"] = httpVersion,
                    ["cookies"] = ToSetCookies(responseHeaders),
                    ["headers"] = ToHeaderArray(responseHeaders),
                    ["content"] = content,
                    ["redirectURL"] = redirectUrl,
                    ["headersSize"] = responseHeadersSize,
                    ["bodySize"] = responseBodySize,
                    ["_transferSize"] = transferSize,
                };

                string failureText = pending.FailureText ?? request?.Failure;
                if (!string.IsNullOrEmpty(failureText))
                {
                    responseNode["_failureText"] = failureText;
                }

                JsonObject entry = new()
                {
                    ["startedDateTime"] = pending.Started.ToString("o", CultureInfo.InvariantCulture),
                    ["time"] = TotalTime(request?.Timing),
                    ["request"] = requestNode,
                    ["response"] = responseNode,
                    ["cache"] = new JsonObject(),
                    ["timings"] = ToTimings(request?.Timing),
                    ["_resourceType"] = request?.ResourceType ?? string.Empty,
                };

                if (!string.IsNullOrEmpty(pending.PageId))
                {
                    entry["pageref"] = pending.PageId;
                }

                await ApplyConnectionAsync(entry, response).ConfigureAwait(false);
                return entry;
            }

            private JsonObject BuildWebSocketEntry(PendingEntry pending)
            {
                IWebSocket socket = pending.WebSocket;
                string url = socket?.Url ?? string.Empty;
                IReadOnlyList<KeyValuePair<string, string>> requestHeaders = Array.Empty<KeyValuePair<string, string>>();
                IReadOnlyList<KeyValuePair<string, string>> responseHeaders = Array.Empty<KeyValuePair<string, string>>();
                int status = -1;
                string statusText = string.Empty;
                string failureText = pending.FailureText;
                DateTimeOffset started = pending.Started;

                if (socket is IHasHarWebSocket has)
                {
                    requestHeaders = has.Har.RequestHeaders;
                    responseHeaders = has.Har.ResponseHeaders;
                    status = has.Har.Status;
                    statusText = has.Har.StatusText ?? string.Empty;
                    if (has.Har.WallTimeMs > 0)
                    {
                        started = has.Har.Started;
                    }

                    if (string.IsNullOrEmpty(failureText))
                    {
                        failureText = has.Har.FailureText;
                    }
                }

                int requestHeadersSize = _slimMode || requestHeaders.Count == 0
                    ? -1
                    : RequestHeadersSize(requestHeaders, url, "GET");
                int responseHeadersSize = _slimMode || status < 0
                    ? -1
                    : ResponseHeadersSize(responseHeaders, statusText);
                int transferSize = -1;
                if (!_slimMode && (status >= 0 || pending.WebSocketTransferSize >= 0))
                {
                    int incoming = pending.WebSocketTransferSize < 0 ? 0 : pending.WebSocketTransferSize;
                    int headerPart = responseHeadersSize < 0 ? 0 : responseHeadersSize;
                    transferSize = headerPart + incoming;
                }

                JsonObject content = new JsonObject
                {
                    ["size"] = -1,
                    ["mimeType"] = "x-unknown",
                };
                if (!_omitWebSocketFrames && _attachContent && !string.IsNullOrEmpty(pending.WebSocketAttachFile))
                {
                    content["_file"] = pending.WebSocketAttachFile;
                }

                JsonObject requestNode = new()
                {
                    ["method"] = "GET",
                    ["url"] = url,
                    ["httpVersion"] = "HTTP/1.1",
                    ["cookies"] = new JsonArray(),
                    ["headers"] = ToHeaderArray(requestHeaders),
                    ["queryString"] = ToQueryString(url),
                    ["headersSize"] = requestHeadersSize,
                    ["bodySize"] = -1,
                };

                JsonObject responseNode = new()
                {
                    ["status"] = status,
                    ["statusText"] = statusText,
                    ["httpVersion"] = "HTTP/1.1",
                    ["cookies"] = new JsonArray(),
                    ["headers"] = ToHeaderArray(responseHeaders),
                    ["content"] = content,
                    ["redirectURL"] = string.Empty,
                    ["headersSize"] = responseHeadersSize,
                    ["bodySize"] = -1,
                    ["_transferSize"] = transferSize,
                };

                if (!string.IsNullOrEmpty(failureText))
                {
                    responseNode["_failureText"] = failureText;
                }

                JsonObject entry = new()
                {
                    ["startedDateTime"] = started.ToString("o", CultureInfo.InvariantCulture),
                    ["time"] = pending.WebSocketTime,
                    ["request"] = requestNode,
                    ["response"] = responseNode,
                    ["cache"] = new JsonObject(),
                    ["timings"] = new JsonObject
                    {
                        ["send"] = -1,
                        ["wait"] = -1,
                        ["receive"] = -1,
                    },
                    ["_resourceType"] = "websocket",
                };

                if (!string.IsNullOrEmpty(pending.PageId))
                {
                    entry["pageref"] = pending.PageId;
                }

                if (!_omitWebSocketFrames && !_attachContent && pending.WebSocketMessages != null)
                {
                    entry["_webSocketMessages"] = pending.WebSocketMessages;
                }

                return entry;
            }

            private JsonObject BuildApiEntry(ApiHarHop hop)
            {
                IEnumerable<KeyValuePair<string, string>> requestHeaders = hop.RequestHeaders;
                IEnumerable<KeyValuePair<string, string>> responseHeaders = hop.ResponseHeaders;
                byte[] postBytes = hop.PostData;
                byte[] body = hop.ResponseBody;
                string requestMime = FindHeader(requestHeaders, "content-type") ?? "application/octet-stream";
                string mimeType = FindHeader(responseHeaders, "content-type") ?? "x-unknown";
                string redirectUrl = FindHeader(responseHeaders, "location") ?? string.Empty;
                redirectUrl = AbsoluteRedirectUrl(hop.Url, redirectUrl);

                int requestBodySize = _slimMode ? -1 : (postBytes == null ? 0 : postBytes.Length);
                int responseBodySize = _slimMode ? -1 : (body == null ? 0 : body.Length);

                JsonObject requestNode = new()
                {
                    ["method"] = hop.Method ?? "GET",
                    ["url"] = hop.Url ?? string.Empty,
                    ["httpVersion"] = string.IsNullOrEmpty(hop.HttpVersion) ? "HTTP/1.1" : hop.HttpVersion,
                    ["cookies"] = _slimMode ? new JsonArray() : ToCookies(FindHeader(requestHeaders, "cookie")),
                    ["headers"] = ToHeaderArray(requestHeaders),
                    ["queryString"] = ToQueryString(hop.Url),
                    ["headersSize"] = _slimMode ? -1 : -1,
                    ["bodySize"] = requestBodySize,
                };

                if (!_slimMode)
                {
                    JsonObject postData = BuildPostData(postBytes, requestMime);
                    if (postData != null)
                    {
                        requestNode["postData"] = postData;
                    }
                    else if (postBytes != null)
                    {
                        requestNode["postData"] = new JsonObject
                        {
                            ["mimeType"] = requestMime,
                            ["params"] = new JsonArray(),
                            ["text"] = LooksLikeText(requestMime) ? Encoding.UTF8.GetString(postBytes) : string.Empty,
                        };
                    }
                }

                JsonObject content = new()
                {
                    ["size"] = body == null ? 0 : body.Length,
                    ["mimeType"] = mimeType,
                    ["compression"] = 0,
                };

                if (!_slimMode && !_omitContent && body != null && body.Length > 0)
                {
                    if (_attachContent)
                    {
                        content["_file"] = WriteAttachedBody(body, mimeType);
                    }
                    else if (LooksLikeText(mimeType))
                    {
                        content["text"] = Encoding.UTF8.GetString(body);
                    }
                    else
                    {
                        content["text"] = Convert.ToBase64String(body);
                        content["encoding"] = "base64";
                    }
                }

                JsonObject responseNode = new()
                {
                    ["status"] = hop.Status,
                    ["statusText"] = hop.StatusText ?? string.Empty,
                    ["httpVersion"] = string.IsNullOrEmpty(hop.HttpVersion) ? "HTTP/1.1" : hop.HttpVersion,
                    ["cookies"] = _slimMode ? new JsonArray() : ToSetCookies(responseHeaders),
                    ["headers"] = ToHeaderArray(responseHeaders),
                    ["content"] = content,
                    ["redirectURL"] = redirectUrl,
                    ["headersSize"] = -1,
                    ["bodySize"] = responseBodySize,
                };

                JsonObject timings = _slimMode
                    ? new JsonObject
                    {
                        ["send"] = -1,
                        ["wait"] = -1,
                        ["receive"] = -1,
                    }
                    : ToTimings(hop.Timing);

                JsonObject entry = new()
                {
                    ["startedDateTime"] = hop.Started.ToString("o", CultureInfo.InvariantCulture),
                    ["time"] = _slimMode ? -1 : TotalTime(hop.Timing),
                    ["request"] = requestNode,
                    ["response"] = responseNode,
                    ["cache"] = new JsonObject(),
                    ["timings"] = timings,
                };

                if (!_slimMode)
                {
                    if (!string.IsNullOrEmpty(hop.ServerIpAddress))
                    {
                        entry["serverIPAddress"] = hop.ServerIpAddress;
                    }

                    if (hop.ServerPort.HasValue)
                    {
                        entry["_serverPort"] = hop.ServerPort.Value;
                    }

                    if (hop.SecurityDetails != null)
                    {
                        entry["_securityDetails"] = SecurityNode(hop.SecurityDetails);
                    }
                }

                return entry;
            }

            private byte[] PageText(IRequest request)
            {
                IPage page = PageOf(request);
                if (page == null)
                {
                    return null;
                }

                lock (_gate)
                {
                    if (_pages.TryGetValue(page, out PageRecord record)
                        && !string.IsNullOrEmpty(record.Text))
                    {
                        return Encoding.UTF8.GetBytes(record.Text);
                    }
                }

                return null;
            }

            private string WriteAttachedBody(byte[] body, string mimeType)
            {
                string harDir = Path.GetDirectoryName(_jsonPath);
                if (string.IsNullOrEmpty(harDir))
                {
                    harDir = string.IsNullOrEmpty(_stagingDir) ? "." : _stagingDir;
                }

                string fileName = Sha1Name(body, mimeType);
                string destDir = !string.IsNullOrEmpty(_resourcesDir) ? _resourcesDir : harDir;
                Directory.CreateDirectory(destDir);
                string fullPath = Path.Combine(destDir, fileName);
                PathIo.WriteBytes(fullPath, body);
                if (string.IsNullOrEmpty(_resourcesDir))
                {
                    return fileName;
                }

                string harFileDir = Path.GetDirectoryName(_path);
                if (string.IsNullOrEmpty(harFileDir))
                {
                    harFileDir = harDir;
                }

                return Path.GetRelativePath(harFileDir, fullPath);
            }
        }

        private sealed class PendingEntry
        {
            internal IRequest Request { get; set; }

            internal IResponse Response { get; set; }

            internal IWebSocket WebSocket { get; set; }

            internal JsonArray WebSocketMessages { get; set; }

            internal string WebSocketAttachFile { get; set; }

            internal int WebSocketTransferSize { get; set; } = -1;

            internal double WebSocketTime { get; set; } = -1;

            internal double OldestMessageMs { get; set; } = double.MaxValue;

            internal double NewestMessageMs { get; set; } = double.MinValue;

            internal EventHandler<IWebSocketFrame> OnWebSocketFrameSent { get; set; }

            internal EventHandler<IWebSocketFrame> OnWebSocketFrameReceived { get; set; }

            internal EventHandler<string> OnWebSocketError { get; set; }

            internal byte[] Body { get; set; }

            internal Task BodyTask { get; set; }

            internal DateTimeOffset Started { get; set; }

            internal string PageId { get; set; }

            internal string RedirectUrl { get; set; }

            internal string FailureText { get; set; }
        }

        private sealed class SessionBag
        {
            internal Session RecordHar { get; set; }

            internal Session Tracing { get; set; }
        }

        private sealed class PageRecord
        {
            internal IPage Page { get; set; }

            internal string Id { get; set; }

            internal DateTimeOffset Started { get; set; }

            internal string Title { get; set; } = string.Empty;

            internal double OnContentLoad { get; set; } = -1;

            internal double OnLoad { get; set; } = -1;

            internal string Text { get; set; }

            internal void Attach()
            {
                if (Page == null)
                {
                    return;
                }

                Page.DOMContentLoaded += OnDomContentLoaded;
                Page.Load += OnLoadEvent;
            }

            internal void Detach()
            {
                if (Page == null)
                {
                    return;
                }

                Page.DOMContentLoaded -= OnDomContentLoaded;
                Page.Load -= OnLoadEvent;
            }

            internal async Task RefreshAsync()
            {
                if (Page == null)
                {
                    return;
                }

                try
                {
                    JsonElement result = await AwaitPageJsonAsync(
                        @"(() => {
                            return {
                                title: document.title,
                                dcl: performance.timing.domContentLoadedEventStart,
                                load: performance.timing.loadEventStart,
                                text: document.body
                                    ? document.body.innerHTML
                                    : (document.documentElement ? document.documentElement.outerHTML : '')
                            };
                        })()").ConfigureAwait(false);
                    if (result.ValueKind == JsonValueKind.Object)
                    {
                        if (result.TryGetProperty("title", out JsonElement title))
                        {
                            Title = title.GetString() ?? Title;
                        }

                        if (result.TryGetProperty("text", out JsonElement text))
                        {
                            string captured = text.GetString();
                            if (!string.IsNullOrEmpty(captured))
                            {
                                Text = captured;
                            }
                        }

                        ApplyTiming(result, "dcl", contentLoad: true);
                        ApplyTiming(result, "load", contentLoad: false);
                    }
                }
                catch (PlaywrightNativeException)
                {
                    try
                    {
                        string title = await AwaitPageTitleAsync().ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(title))
                        {
                            Title = title;
                        }
                    }
                    catch (PlaywrightNativeException)
                    {
                    }
                }
            }

            internal JsonObject ToJson()
                => new JsonObject
                {
                    ["id"] = Id,
                    ["title"] = Title ?? string.Empty,
                    ["startedDateTime"] = Started.ToString("o", CultureInfo.InvariantCulture),
                    ["pageTimings"] = new JsonObject
                    {
                        ["onContentLoad"] = OnContentLoad,
                        ["onLoad"] = OnLoad,
                    },
                };

            private void OnDomContentLoaded(object sender, IPage page)
            {
                if (OnContentLoad < 0)
                {
                    OnContentLoad = ElapsedMs();
                }

                _ = CaptureTitleAsync();
            }

            private void OnLoadEvent(object sender, IPage page)
            {
                if (OnLoad < 0)
                {
                    OnLoad = ElapsedMs();
                }

                _ = CaptureTitleAsync();
            }

            private async Task CaptureTitleAsync()
            {
                if (Page == null)
                {
                    return;
                }

                try
                {
                    JsonElement snapshot = await Page.EvaluateAsync<JsonElement>(
                        @"(() => {
                            return {
                                title: document.title,
                                text: document.body
                                    ? document.body.innerHTML
                                    : (document.documentElement ? document.documentElement.outerHTML : '')
                            };
                        })()").ConfigureAwait(false);
                    if (snapshot.ValueKind == JsonValueKind.Object)
                    {
                        if (snapshot.TryGetProperty("title", out JsonElement title)
                            && !string.IsNullOrEmpty(title.GetString()))
                        {
                            Title = title.GetString();
                        }

                        if (snapshot.TryGetProperty("text", out JsonElement text)
                            && !string.IsNullOrEmpty(text.GetString()))
                        {
                            Text = text.GetString();
                        }
                    }
                }
                catch (PlaywrightNativeException)
                {
                }
            }

            private double ElapsedMs()
            {
                double relative = (DateTimeOffset.UtcNow - Started).TotalMilliseconds;
                return relative < 0 ? 0 : relative;
            }

            private async Task<JsonElement> AwaitPageJsonAsync(string expression)
            {
                Task<JsonElement> task = Page.EvaluateAsync<JsonElement>(expression);
                try
                {
                    return await task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    return default;
                }
            }

            private async Task<string> AwaitPageTitleAsync()
            {
                Task<string> task = Page.TitleAsync();
                try
                {
                    return await task.WaitAsync(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    return Title;
                }
            }

            private void ApplyTiming(JsonElement result, string name, bool contentLoad)
            {
                if (!result.TryGetProperty(name, out JsonElement value)
                    || value.ValueKind != JsonValueKind.Number)
                {
                    return;
                }

                double raw = value.GetDouble();
                if (raw <= 0)
                {
                    return;
                }

                double relative = raw - Started.ToUnixTimeMilliseconds();
                if (relative < 0)
                {
                    relative = -1;
                }

                if (contentLoad)
                {
                    OnContentLoad = relative;
                }
                else
                {
                    OnLoad = relative;
                }
            }
        }
    }
}
