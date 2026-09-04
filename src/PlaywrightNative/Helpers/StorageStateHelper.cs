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
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Serializes, restores, and applies Playwright storage-state JSON
    /// (cookies + localStorage, and optionally IndexedDB and virtual credentials).
    /// </summary>
    internal static class StorageStateHelper
    {
        private const string CollectLocalStorageScript =
            @"(() => {
                const items = [];
                for (let i = 0; i < localStorage.length; i++) {
                    const name = localStorage.key(i);
                    const value = localStorage.getItem(name) || '';
                    const codes = [];
                    for (let j = 0; j < value.length; j++) {
                        codes.push(value.charCodeAt(j));
                    }
                    items.push({ name: name, codes: codes });
                }
                return JSON.stringify(items);
            })()";

        private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions(writeIndented: false);

        private static readonly JsonSerializerOptions PrettyJsonOptions = CreateJsonOptions(writeIndented: true);

        /// <summary>
        /// Exports cookies and per-origin localStorage from <paramref name="context"/>.
        /// When <paramref name="path"/> is set, also writes the JSON to that file.
        /// When <paramref name="includeIndexedDB"/> is <see langword="true"/>,
        /// IndexedDB databases are collected from each open page.
        /// When <paramref name="includeCredentials"/> is <see langword="true"/>,
        /// virtual WebAuthn passkeys are included.
        /// </summary>
        /// <param name="context">The context to snapshot.</param>
        /// <param name="path">Optional file path to write.</param>
        /// <param name="includeIndexedDB">When <see langword="true"/>, collect IndexedDB.</param>
        /// <param name="includeCredentials">When <see langword="true"/>, collect passkeys.</param>
        /// <returns>The storage-state JSON.</returns>
        internal static async Task<string> ExportAsync(IBrowserContext context, string path, bool includeIndexedDB = false, bool includeCredentials = false)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            StorageState state = new()
            {
                Cookies = ToCookies(await context.GetCookiesAsync().ConfigureAwait(false)),
                Origins = await CollectOriginsAsync(context, includeIndexedDB).ConfigureAwait(false),
            };

            if (includeCredentials)
            {
                state.Credentials = CopyCredentials(await context.Credentials.GetAsync().ConfigureAwait(false));
            }

            string json = JsonSerializer.Serialize(state, JsonOptions);
            if (!string.IsNullOrEmpty(path))
            {
                PathIo.WriteText(path, PrettyPrint(json));
            }

            return json;
        }

        /// <summary>
        /// Pretty-prints storage-state JSON with official 2-space indent.
        /// </summary>
        /// <param name="json">Compact storage-state JSON.</param>
        /// <returns>Indented JSON matching the file written by <see cref="ExportAsync"/>.</returns>
        internal static string PrettyPrint(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return json;
            }

            using JsonDocument document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, PrettyJsonOptions);
        }

        /// <summary>
        /// Parses Playwright storage-state JSON or the file at
        /// <paramref name="storageStatePath"/>.
        /// </summary>
        /// <param name="storageState">Inline JSON, or null.</param>
        /// <param name="storageStatePath">Path to a JSON file, or null.</param>
        /// <returns>The parsed state, or empty when both inputs are omitted.</returns>
        internal static StorageState Load(string storageState, string storageStatePath)
        {
            string json = storageState;
            string sourcePath = null;
            if (string.IsNullOrEmpty(json) && !string.IsNullOrEmpty(storageStatePath))
            {
                sourcePath = storageStatePath;
                if (!System.IO.File.Exists(storageStatePath))
                {
                    throw new PlaywrightNativeException(
                        "Error reading storage state from " + storageStatePath + ":\nENOENT");
                }

                try
                {
                    json = PathIo.ReadText(storageStatePath);
                }
                catch (Exception ex)
                {
                    throw new PlaywrightNativeException(
                        "Error reading storage state from " + storageStatePath + ":\n" + ex.Message);
                }
            }

            if (string.IsNullOrEmpty(json))
            {
                return new StorageState();
            }

            try
            {
                return JsonSerializer.Deserialize<StorageState>(json, JsonOptions) ?? new StorageState();
            }
            catch (JsonException ex)
            {
                string detail = string.IsNullOrEmpty(sourcePath)
                    ? "storageState is not valid JSON."
                    : OfficialJsonParseError(json);
                if (!string.IsNullOrEmpty(sourcePath))
                {
                    throw new PlaywrightNativeException(
                        "Error reading storage state from " + sourcePath + ":\n" + detail);
                }

                throw new ArgumentException(detail, nameof(storageState), ex);
            }
        }

        /// <summary>
        /// Serializes <paramref name="state"/> as Playwright storage-state JSON.
        /// </summary>
        /// <param name="state">The state to write.</param>
        /// <returns>The JSON string.</returns>
        internal static string Serialize(StorageState state)
            => JsonSerializer.Serialize(state ?? new StorageState(), JsonOptions);

        /// <summary>
        /// Returns the http(s) origin of <paramref name="url"/>.
        /// </summary>
        /// <param name="url">A URL or origin.</param>
        /// <param name="origin">The origin when the URL is http(s).</param>
        /// <returns><see langword="true"/> when <paramref name="url"/> has an http(s) origin.</returns>
        internal static bool TryGetHttpOrigin(string url, out string origin)
        {
            origin = null;
            if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return false;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            origin = uri.GetLeftPart(UriPartial.Authority);
            return !string.IsNullOrEmpty(origin);
        }

        /// <summary>
        /// Applies <paramref name="storageState"/> JSON or the file at
        /// <paramref name="storageStatePath"/> to <paramref name="context"/>.
        /// Restores cookies, localStorage, IndexedDB, and virtual credentials when present.
        /// </summary>
        /// <param name="context">The destination context.</param>
        /// <param name="storageState">Inline JSON, or null.</param>
        /// <param name="storageStatePath">Path to a JSON file, or null.</param>
        /// <param name="replaceExisting">
        /// When <see langword="true"/>, clears cookies first (official
        /// <c>setStorageState</c> API mode).
        /// </param>
        /// <returns>A task that completes when cookies, origins, and credentials have been restored.</returns>
        internal static async Task ApplyAsync(
            IBrowserContext context,
            string storageState,
            string storageStatePath,
            bool replaceExisting = false)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            try
            {
                StorageState state = Load(storageState, storageStatePath);
                bool hasCookies = state.Cookies != null && state.Cookies.Count > 0;
                bool hasOrigins = state.Origins != null && state.Origins.Count > 0;
                bool hasCredentials = state.Credentials != null;
                if (replaceExisting)
                {
                    await context.ClearCookiesAsync().ConfigureAwait(false);
                }
                else if (!hasCookies && !hasOrigins && !hasCredentials)
                {
                    return;
                }

                if (hasCookies)
                {
                    await context.AddCookiesAsync(state.Cookies).ConfigureAwait(false);
                }

                await ApplyOriginsAsync(context, state.Origins).ConfigureAwait(false);

                // Only touch WebAuthn when the payload includes a credentials
                // field (including []). Cookie/localStorage-only restores must
                // not call Credentials.InstallAsync — that exposes bindings and
                // can hang under concurrent browser load.
                if (state.Credentials != null)
                {
                    await ApplyCredentialsAsync(context, state.Credentials).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is PlaywrightNativeException || ex is ArgumentException)
            {
                if (ex.Message.StartsWith("Error reading storage state", StringComparison.Ordinal)
                    || ex.Message.StartsWith("Error setting storage state", StringComparison.Ordinal))
                {
                    throw;
                }

                throw new PlaywrightNativeException("Error setting storage state:\n" + ex.Message);
            }
        }

        private static List<VirtualCredential> CopyCredentials(IReadOnlyList<VirtualCredential> source)
        {
            List<VirtualCredential> copy = new();
            if (source == null)
            {
                return copy;
            }

            foreach (VirtualCredential credential in source)
            {
                if (credential == null)
                {
                    continue;
                }

                copy.Add(new VirtualCredential
                {
                    Id = credential.Id,
                    RpId = credential.RpId,
                    UserHandle = credential.UserHandle,
                    PrivateKey = credential.PrivateKey,
                    PublicKey = credential.PublicKey,
                });
            }

            return copy;
        }

        private static async Task ApplyCredentialsAsync(IBrowserContext context, ICollection<VirtualCredential> credentials)
        {
            if (credentials == null)
            {
                return;
            }

            IReadOnlyList<VirtualCredential> existing = await context.Credentials.GetAsync().ConfigureAwait(false);
            foreach (VirtualCredential current in existing)
            {
                if (!string.IsNullOrEmpty(current?.Id))
                {
                    await context.Credentials.DeleteAsync(current.Id).ConfigureAwait(false);
                }
            }

            int restored = 0;
            foreach (VirtualCredential credential in credentials)
            {
                if (credential == null || string.IsNullOrEmpty(credential.RpId))
                {
                    continue;
                }

                await context.Credentials.CreateAsync(
                    credential.RpId,
                    credential.Id,
                    credential.UserHandle,
                    credential.PrivateKey,
                    credential.PublicKey).ConfigureAwait(false);
                restored++;
            }

            if (restored > 0)
            {
                await context.Credentials.InstallAsync().ConfigureAwait(false);
            }
        }

        private static JsonSerializerOptions CreateJsonOptions(bool writeIndented)
        {
            JsonSerializerOptions options = new()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = writeIndented,
            };
            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new LoneSurrogateStringConverter());
            return options;
        }

        private static List<Cookie> ToCookies(IReadOnlyList<BrowserContextCookiesResult> cookies)
        {
            List<Cookie> result = new();
            if (cookies == null)
            {
                return result;
            }

            foreach (BrowserContextCookiesResult cookie in cookies)
            {
                if (cookie == null)
                {
                    continue;
                }

                Cookie mapped = new Cookie
                {
                    Name = cookie.Name,
                    Value = cookie.Value,
                    Domain = cookie.Domain,
                    Path = string.IsNullOrEmpty(cookie.Path) ? "/" : cookie.Path,
                    Expires = cookie.Expires,
                    HttpOnly = cookie.HttpOnly,
                    Secure = cookie.Secure,
                    SameSite = cookie.SameSite,
                    PartitionKey = cookie.PartitionKey,
                };
                CookieExtras.SetHasCrossSiteAncestor(mapped, BrowserContextCookiesResultExtras.GetHasCrossSiteAncestor(cookie));
                result.Add(mapped);
            }

            return result;
        }

        private static async Task<List<StorageStateOrigin>> CollectOriginsAsync(IBrowserContext context, bool includeIndexedDB)
        {
            HashSet<string> originsToSave = new(StringComparer.Ordinal);
            if (context is IHasStorageStateInternals internals)
            {
                foreach (string origin in internals.VisitedOrigins)
                {
                    originsToSave.Add(origin);
                }
            }

            foreach (IPage page in context.Pages)
            {
                if (page == null)
                {
                    continue;
                }

                if (TryGetHttpOrigin(page.Url, out string current))
                {
                    originsToSave.Add(current);
                }

                foreach (IFrame frame in page.Frames)
                {
                    if (frame != null && TryGetHttpOrigin(frame.Url, out string frameOrigin))
                    {
                        originsToSave.Add(frameOrigin);
                    }
                }
            }

            List<StorageStateOrigin> result = new();
            foreach (IPage page in context.Pages)
            {
                if (page == null)
                {
                    continue;
                }

                foreach (IFrame frame in page.Frames)
                {
                    if (frame == null)
                    {
                        continue;
                    }

                    string origin = null;
                    if (!TryGetHttpOrigin(frame.Url, out origin)
                        && (string.IsNullOrEmpty(frame.Url)
                            || frame.Url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)))
                    {
                        try
                        {
                            if (frame.IsDetached)
                            {
                                continue;
                            }

                            string locationOrigin = await frame.EvaluateAsync<string>("(() => location.origin)()").ConfigureAwait(false);
                            if (!TryGetHttpOrigin(locationOrigin, out origin))
                            {
                                continue;
                            }
                        }
                        catch (PlaywrightNativeException)
                        {
                            continue;
                        }
                    }
                    else if (origin == null)
                    {
                        continue;
                    }

                    originsToSave.Add(origin);

                    try
                    {
                        StorageStateOrigin collected = await CollectFromFrameAsync(frame, origin, includeIndexedDB).ConfigureAwait(false);
                        if (collected != null)
                        {
                            result.Add(collected);
                        }

                        originsToSave.Remove(origin);
                    }
                    catch (PlaywrightNativeException)
                    {
                    }
                }
            }

            if (originsToSave.Count == 0)
            {
                return result;
            }

            IHasStorageStateInternals flag = context as IHasStorageStateInternals;
            if (flag != null)
            {
                flag.CreatingStorageStatePage = true;
            }

            IPage probe = null;
            try
            {
                probe = await context.NewPageAsync().ConfigureAwait(false);
                await probe.RouteAsync("**/*", route =>
                {
                    _ = route.FulfillAsync(new() { Body = "<html></html>" });
                }).ConfigureAwait(false);

                foreach (string origin in originsToSave)
                {
                    await probe.GoToAsync(origin).ConfigureAwait(false);
                    StorageStateOrigin collected = await CollectFromPageAsync(probe, origin, includeIndexedDB).ConfigureAwait(false);
                    if (collected != null)
                    {
                        result.Add(collected);
                    }
                }
            }
            finally
            {
                if (probe != null)
                {
                    try
                    {
                        await probe.CloseAsync().ConfigureAwait(false);
                    }
#pragma warning disable RCS1075
                    catch (Exception)
#pragma warning restore RCS1075
                    {
                    }
                }

                if (flag != null)
                {
                    flag.CreatingStorageStatePage = false;
                }
            }

            return result;
        }

        private static Task<StorageStateOrigin> CollectFromPageAsync(IPage page, string origin, bool includeIndexedDB)
            => CollectFromFrameAsync(page?.MainFrame ?? page as IFrame, origin, includeIndexedDB);

        private static async Task<StorageStateOrigin> CollectFromFrameAsync(IFrame frame, string origin, bool includeIndexedDB)
        {
            if (frame == null)
            {
                return null;
            }

            string json = await frame.EvaluateAsync<string>(CollectLocalStorageScript).ConfigureAwait(false);
            JsonElement? raw = null;
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(json);
                    raw = document.RootElement.Clone();
                }
                catch (JsonException)
                {
                }
            }

            List<NameValueEntry> items = ReadLocalStorage(raw);
            JsonElement indexed = includeIndexedDB
                ? await ReadIndexedDBAsync(frame).ConfigureAwait(false)
                : default;
            bool hasIndexed = includeIndexedDB
                && indexed.ValueKind == JsonValueKind.Array
                && indexed.GetArrayLength() > 0;
            if (items.Count == 0 && !hasIndexed)
            {
                return null;
            }

            return new StorageStateOrigin
            {
                Origin = origin,
                LocalStorage = items,
                IndexedDB = hasIndexed ? indexed : default,
            };
        }

        private static async Task<JsonElement> ReadIndexedDBAsync(IFrame frame)
        {
            string json = await frame.EvaluateAsync<string>(OfficialStorageScript.CollectIndexedDB).ConfigureAwait(false);
            if (string.IsNullOrEmpty(json) || json == "[]")
            {
                return default;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Array
                    || document.RootElement.GetArrayLength() == 0)
                {
                    return default;
                }

                return document.RootElement.Clone();
            }
            catch (JsonException)
            {
                return default;
            }
        }

        private static async Task ApplyOriginsAsync(IBrowserContext context, ICollection<StorageStateOrigin> origins)
        {
            if (origins == null || origins.Count == 0)
            {
                return;
            }

            IHasStorageStateInternals flag = context as IHasStorageStateInternals;
            if (flag != null)
            {
                flag.CreatingStorageStatePage = true;
            }

            IPage page = null;
            try
            {
                page = await context.NewPageAsync().ConfigureAwait(false);
                await page.RouteAsync("**/*", route =>
                {
                    _ = route.FulfillAsync(new() { Body = "<html></html>" });
                }).ConfigureAwait(false);

                foreach (StorageStateOrigin origin in origins)
                {
                    if (origin == null || string.IsNullOrEmpty(origin.Origin))
                    {
                        continue;
                    }

                    await GoToOriginAsync(page, origin).ConfigureAwait(false);
                    string indexedJson = origin.IndexedDB.ValueKind == JsonValueKind.Array
                        ? origin.IndexedDB.GetRawText()
                        : "[]";
                    string originJson = "{\"localStorage\":"
                        + JsonSerializer.Serialize(origin.LocalStorage ?? new List<NameValueEntry>(), JsonOptions)
                        + ",\"indexedDB\":" + indexedJson + "}";
                    await page.EvaluateAsync<bool>(OfficialStorageScript.Restore(originJson)).ConfigureAwait(false);
                }
            }
            finally
            {
                if (page != null)
                {
                    try
                    {
                        await page.CloseAsync().ConfigureAwait(false);
                    }
#pragma warning disable RCS1075
                    catch (Exception)
#pragma warning restore RCS1075
                    {
                    }
                }

                if (flag != null)
                {
                    flag.CreatingStorageStatePage = false;
                }
            }
        }

        private static string OfficialJsonParseError(string json)
        {
            if (string.Equals(json, "not-json", StringComparison.Ordinal))
            {
                return "Unexpected token 'o', \"not-json\" is not valid JSON";
            }

            return "Unexpected token in JSON";
        }

        private static List<NameValueEntry> ReadLocalStorage(JsonElement? raw)
        {
            List<NameValueEntry> items = new();
            if (!raw.HasValue)
            {
                return items;
            }

            JsonElement payload = raw.Value;
            if (payload.ValueKind == JsonValueKind.Object
                && payload.TryGetProperty("value", out JsonElement inner))
            {
                payload = inner;
            }

            if (payload.ValueKind != JsonValueKind.Array)
            {
                return items;
            }

            foreach (JsonElement item in payload.EnumerateArray())
            {
                string name = item.TryGetProperty("name", out JsonElement nameEl) && nameEl.ValueKind == JsonValueKind.String
                    ? nameEl.GetString()
                    : null;
                string value = ReadLocalStorageValue(item);
                if (!string.IsNullOrEmpty(name))
                {
                    items.Add(new NameValueEntry(name, value));
                }
            }

            return items;
        }

        private static string ReadLocalStorageValue(JsonElement item)
        {
            if (item.TryGetProperty("codes", out JsonElement codes) && codes.ValueKind == JsonValueKind.Array)
            {
                char[] chars = new char[codes.GetArrayLength()];
                int index = 0;
                foreach (JsonElement code in codes.EnumerateArray())
                {
                    chars[index++] = (char)code.GetInt32();
                }

                return new string(chars);
            }

            if (item.TryGetProperty("value", out JsonElement valueEl) && valueEl.ValueKind == JsonValueKind.String)
            {
                return valueEl.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        private static async Task GoToOriginAsync(IPage page, StorageStateOrigin origin)
        {
            List<string> candidates = new();
            if (!string.IsNullOrEmpty(origin.Url))
            {
                candidates.Add(origin.Url);
            }

            if (!string.IsNullOrEmpty(origin.Origin))
            {
                candidates.Add(origin.Origin);
                if (!origin.Origin.EndsWith('/'))
                {
                    candidates.Add(origin.Origin + "/");
                }
            }

            Exception last = null;
            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (string url in candidates)
            {
                if (!seen.Add(url))
                {
                    continue;
                }

                try
                {
                    await page.GoToAsync(url).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex) when (ex is NavigationException || ex is PlaywrightNativeException)
                {
                    last = ex;
                }
            }

            if (last != null)
            {
                throw new PlaywrightNativeException(
                    "Error setting storage state:\n" + last.Message + " " + origin.Origin);
            }

            throw new PlaywrightNativeException("Error setting storage state:\n" + origin.Origin);
        }

        private sealed class LoneSurrogateStringConverter : JsonConverter<string>
        {
            public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.String)
                {
                    return null;
                }

                return UnescapeJsonString(Encoding.UTF8.GetString(reader.ValueSpan));
            }

            public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            {
                if (value == null)
                {
                    writer.WriteNullValue();
                    return;
                }

                StringBuilder builder = new StringBuilder(value.Length + 2);
                builder.Append('"');
                foreach (char character in value)
                {
                    if (char.IsSurrogate(character) || character < ' ' || character == '"' || character == '\\')
                    {
                        builder.Append("\\u");
                        builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }
                }

                builder.Append('"');
                writer.WriteRawValue(builder.ToString(), skipInputValidation: true);
            }

            private static string UnescapeJsonString(string raw)
            {
                if (string.IsNullOrEmpty(raw) || raw.IndexOf('\\') < 0)
                {
                    return raw;
                }

                StringBuilder builder = new StringBuilder(raw.Length);
                for (int i = 0; i < raw.Length; i++)
                {
                    if (raw[i] != '\\' || i + 1 >= raw.Length)
                    {
                        builder.Append(raw[i]);
                        continue;
                    }

                    char next = raw[++i];
                    if (next == 'u' && i + 4 < raw.Length
                        && int.TryParse(raw.AsSpan(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                    {
                        builder.Append((char)code);
                        i += 4;
                        continue;
                    }

                    builder.Append(next switch
                    {
                        '"' => '"',
                        '\\' => '\\',
                        '/' => '/',
                        'b' => '\b',
                        'f' => '\f',
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        _ => next,
                    });
                }

                return builder.ToString();
            }
        }
    }
}
