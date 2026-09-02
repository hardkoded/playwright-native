/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Serves recorded HAR entries through <see cref="IPage.RouteFromHARAsync(string, string, HarNotFound, bool, HarMode, RouteFromHarUpdateContentPolicy)"/> /
    /// <see cref="IBrowserContext.RouteFromHARAsync(string, string, HarNotFound, bool, HarMode, RouteFromHarUpdateContentPolicy)"/>.
    /// </summary>
    internal static class HarPlayback
    {
        private static readonly int[] RedirectStatus = { 301, 302, 303, 307, 308 };

        /// <summary>
        /// Registers a context route that fulfills from <paramref name="har"/>.
        /// </summary>
        /// <param name="context">The context to intercept.</param>
        /// <param name="har">Path to a HAR 1.2 file or zip archive.</param>
        /// <param name="url">Optional glob; when omitted every request is considered.</param>
        /// <param name="notFound">Miss behavior. Defaults to abort.</param>
        /// <returns>A task that completes when the route is registered.</returns>
        internal static Task InstallAsync(IBrowserContext context, string har, string url, HarNotFound notFound = default)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Store store = Store.Load(har, notFound);
            return context.RouteAsync(Pattern(url), route => store.HandleAsync(route));
        }

        /// <summary>
        /// Registers a context route that fulfills from <paramref name="har"/>
        /// for URLs matching <paramref name="url"/>.
        /// </summary>
        /// <param name="context">The context to intercept.</param>
        /// <param name="har">Path to a HAR 1.2 file or zip archive.</param>
        /// <param name="url">URL regular expression.</param>
        /// <param name="notFound">Miss behavior. Defaults to abort.</param>
        /// <returns>A task that completes when the route is registered.</returns>
        internal static Task InstallAsync(IBrowserContext context, string har, Regex url, HarNotFound notFound = default)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            Store store = Store.Load(har, notFound);
            return context.RouteAsync("**/*", route => MatchAsync(store, url, route));
        }

        /// <summary>
        /// Registers a page route that fulfills from <paramref name="har"/>.
        /// </summary>
        /// <param name="page">The page to intercept.</param>
        /// <param name="har">Path to a HAR 1.2 file or zip archive.</param>
        /// <param name="url">Optional glob; when omitted every request is considered.</param>
        /// <param name="notFound">Miss behavior. Defaults to abort.</param>
        /// <returns>A task that completes when the route is registered.</returns>
        internal static Task InstallAsync(IPage page, string har, string url, HarNotFound notFound = default)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            Store store = Store.Load(har, notFound);
            return page.RouteAsync(Pattern(url), route => store.HandleAsync(route));
        }

        /// <summary>
        /// Registers a page route that fulfills from <paramref name="har"/>
        /// for URLs matching <paramref name="url"/>.
        /// </summary>
        /// <param name="page">The page to intercept.</param>
        /// <param name="har">Path to a HAR 1.2 file or zip archive.</param>
        /// <param name="url">URL regular expression.</param>
        /// <param name="notFound">Miss behavior. Defaults to abort.</param>
        /// <returns>A task that completes when the route is registered.</returns>
        internal static Task InstallAsync(IPage page, string har, Regex url, HarNotFound notFound = default)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            Store store = Store.Load(har, notFound);
            return page.RouteAsync("**/*", route => MatchAsync(store, url, route));
        }

        private static string Pattern(string url)
            => string.IsNullOrEmpty(url) ? "**/*" : url;

        private static Task MatchAsync(Store store, Regex url, IRoute route)
        {
            if (route?.Request == null)
            {
                return Task.CompletedTask;
            }

            if (!url.IsMatch(route.Request.Url))
            {
                return route.FallbackAsync();
            }

            return store.HandleAsync(route);
        }

        private static string ReadContentString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement property)
                && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }

        private static bool LooksLikeZip(string path)
        {
            if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return File.Exists(path);
            }

            if (!File.Exists(path))
            {
                return false;
            }

            using FileStream stream = File.OpenRead(path);
            return stream.Length >= 2 && stream.ReadByte() == (int)'P' && stream.ReadByte() == (int)'K';
        }

        private sealed class ContentSource
        {
            private readonly string _baseDir;
            private readonly Dictionary<string, byte[]> _zipFiles;

            private ContentSource(string baseDir, Dictionary<string, byte[]> zipFiles)
            {
                _baseDir = baseDir;
                _zipFiles = zipFiles;
            }

            internal static ContentSource Open(string har, out string json)
            {
                if (string.IsNullOrEmpty(har))
                {
                    throw new ArgumentException("HAR path must be non-empty.", nameof(har));
                }

                if (LooksLikeZip(har))
                {
                    Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
                    string harJson = null;
                    using (ZipArchive archive = ZipFile.OpenRead(har))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            if (string.IsNullOrEmpty(entry.Name) || entry.FullName.EndsWith('/'))
                            {
                                continue;
                            }

                            using Stream stream = entry.Open();
                            using MemoryStream copy = new();
                            stream.CopyTo(copy);
                            byte[] bytes = copy.ToArray();
                            files[entry.FullName.Replace('\\', '/')] = bytes;
                            if (string.Equals(entry.Name, "har.har", StringComparison.OrdinalIgnoreCase)
                                || (harJson == null && entry.Name.EndsWith(".har", StringComparison.OrdinalIgnoreCase)))
                            {
                                harJson = Encoding.UTF8.GetString(bytes);
                            }
                        }
                    }

                    json = harJson ?? "{\"log\":{}}";
                    return new ContentSource(null, files);
                }

                json = PathIo.ReadText(har);
                string dir = Path.GetDirectoryName(har);
                return new ContentSource(string.IsNullOrEmpty(dir) ? "." : dir, null);
            }

            internal byte[] ReadContent(JsonElement content)
            {
                if (content.ValueKind != JsonValueKind.Object)
                {
                    return Array.Empty<byte>();
                }

                string file = ReadContentString(content, "_file");
                if (!string.IsNullOrEmpty(file))
                {
                    return ReadFile(file);
                }

                if (!content.TryGetProperty("text", out JsonElement textEl)
                    || textEl.ValueKind != JsonValueKind.String)
                {
                    return Array.Empty<byte>();
                }

                string text = textEl.GetString() ?? string.Empty;
                string encoding = ReadContentString(content, "encoding");
                return string.Equals(encoding, "base64", StringComparison.OrdinalIgnoreCase)
                    ? Convert.FromBase64String(text)
                    : Encoding.UTF8.GetBytes(text);
            }

            private byte[] ReadFile(string file)
            {
                string normalized = file.Replace('\\', '/');
                if (_zipFiles != null)
                {
                    if (_zipFiles.TryGetValue(normalized, out byte[] zipped))
                    {
                        return zipped;
                    }

                    string name = Path.GetFileName(normalized);
                    foreach (KeyValuePair<string, byte[]> pair in _zipFiles)
                    {
                        if (string.Equals(Path.GetFileName(pair.Key), name, StringComparison.Ordinal))
                        {
                            return pair.Value;
                        }
                    }

                    return Array.Empty<byte>();
                }

                string combined = Path.Combine(_baseDir, normalized.Replace('/', Path.DirectorySeparatorChar));
                return File.Exists(combined) ? PathIo.ReadBytes(combined) : Array.Empty<byte>();
            }
        }

        private sealed class Store
        {
            private readonly List<Entry> _entries = new();
            private readonly HarNotFound _notFound;

            private Store(HarNotFound notFound)
            {
                _notFound = notFound;
            }

            internal static Store Load(string har, HarNotFound notFound)
            {
                ContentSource source = ContentSource.Open(har, out string json);
                using JsonDocument document = JsonDocument.Parse(string.IsNullOrEmpty(json) ? "{}" : json);
                Store store = new(notFound);
                if (!document.RootElement.TryGetProperty("log", out JsonElement log)
                    || !log.TryGetProperty("entries", out JsonElement entries)
                    || entries.ValueKind != JsonValueKind.Array)
                {
                    return store;
                }

                foreach (JsonElement item in entries.EnumerateArray())
                {
                    if (TryRead(item, source, out Entry entry))
                    {
                        store._entries.Add(entry);
                    }
                }

                return store;
            }

            internal async Task HandleAsync(IRoute route)
            {
                if (route?.Request == null)
                {
                    return;
                }

                IRequest request = route.Request;
                List<KeyValuePair<string, string>> headers = await RequestHeadersAsync(request).ConfigureAwait(false);
                byte[] postData = request.PostDataBuffer;
                if (postData == null && !string.IsNullOrEmpty(request.PostData))
                {
                    postData = Encoding.UTF8.GetBytes(request.PostData);
                }

                if (!TryFind(request.Url, request.Method, headers, postData, out Entry entry))
                {
                    if (_notFound == HarNotFound.Fallback)
                    {
                        await route.FallbackAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        await route.AbortAsync().ConfigureAwait(false);
                    }

                    return;
                }

                if (request.IsNavigationRequest
                    && !string.Equals(entry.Url, Normalize(request.Url), StringComparison.Ordinal))
                {
                    await RedirectNavigationAsync(route, entry.Url).ConfigureAwait(false);
                    return;
                }

                if (entry.Status <= 0)
                {
                    return;
                }

                await route.FulfillAsync(
                    bodyBytes: entry.Body,
                    contentType: entry.ContentType,
                    headers: entry.Headers,
                    status: entry.Status).ConfigureAwait(false);
            }

            private static bool TryRead(JsonElement item, ContentSource source, out Entry entry)
            {
                entry = default;
                if (!item.TryGetProperty("request", out JsonElement request)
                    || !item.TryGetProperty("response", out JsonElement response))
                {
                    return false;
                }

                string url = ReadString(request, "url");
                if (string.IsNullOrEmpty(url))
                {
                    return false;
                }

                string method = ReadString(request, "method") ?? "GET";
                int status = 0;
                if (response.TryGetProperty("status", out JsonElement statusEl)
                    && statusEl.TryGetInt32(out int parsed))
                {
                    status = parsed;
                }

                List<KeyValuePair<string, string>> headers = ReadHeaders(response);
                List<KeyValuePair<string, string>> requestHeaders = ReadHeaders(request);
                string contentType = HeaderValue(headers, "content-type");
                byte[] body = Array.Empty<byte>();
                if (response.TryGetProperty("content", out JsonElement content))
                {
                    body = source.ReadContent(content);
                    contentType ??= ReadString(content, "mimeType");
                }

                byte[] postData = null;
                if (request.TryGetProperty("postData", out JsonElement post)
                    && post.ValueKind == JsonValueKind.Object)
                {
                    postData = source.ReadContent(post);
                    if (postData.Length == 0 && post.TryGetProperty("text", out JsonElement postText)
                        && postText.ValueKind == JsonValueKind.String)
                    {
                        postData = Encoding.UTF8.GetBytes(postText.GetString() ?? string.Empty);
                    }
                }

                entry = new Entry
                {
                    Url = Normalize(url),
                    Method = method,
                    Status = status,
                    Headers = headers,
                    RequestHeaders = requestHeaders,
                    ContentType = contentType,
                    Body = body,
                    PostData = postData,
                };
                return true;
            }

            private static List<KeyValuePair<string, string>> ReadHeaders(JsonElement owner)
            {
                List<KeyValuePair<string, string>> headers = new();
                if (!owner.TryGetProperty("headers", out JsonElement headerArray)
                    || headerArray.ValueKind != JsonValueKind.Array)
                {
                    return headers;
                }

                foreach (JsonElement header in headerArray.EnumerateArray())
                {
                    string name = ReadString(header, "name");
                    string value = ReadString(header, "value") ?? string.Empty;
                    if (string.IsNullOrEmpty(name)
                        || string.Equals(name, "content-encoding", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, "transfer-encoding", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, "content-length", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    headers.Add(new KeyValuePair<string, string>(name, value));
                }

                return headers;
            }

            private static Task RedirectNavigationAsync(IRoute route, string url)
            {
                if (route is IHarRedirectRoute redirect)
                {
                    return redirect.RedirectNavigationAsync(url);
                }

                return route.FulfillAsync(
                    status: 302,
                    headers: new[]
                    {
                        new KeyValuePair<string, string>("location", url),
                    });
            }

            private static Task<List<KeyValuePair<string, string>>> RequestHeadersAsync(IRequest request)
            {
                List<KeyValuePair<string, string>> headers = new();
                if (request?.Headers != null)
                {
                    foreach (KeyValuePair<string, string> header in request.Headers)
                    {
                        headers.Add(header);
                    }
                }

                return Task.FromResult(headers);
            }

            private static string ReadString(JsonElement element, string name)
            {
                return element.TryGetProperty(name, out JsonElement property)
                    && property.ValueKind == JsonValueKind.String
                    ? property.GetString()
                    : null;
            }

            private static string Normalize(string url)
            {
                if (string.IsNullOrEmpty(url))
                {
                    return string.Empty;
                }

                int hash = url.IndexOf('#', StringComparison.Ordinal);
                return hash < 0 ? url : url.Substring(0, hash);
            }

            private static string HeaderValue(List<KeyValuePair<string, string>> headers, string name)
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

            private static string MultipartBoundary(List<KeyValuePair<string, string>> headers)
            {
                string contentType = HeaderValue(headers, "content-type");
                if (string.IsNullOrEmpty(contentType)
                    || contentType.IndexOf("multipart/form-data", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return null;
                }

                const string marker = "boundary=";
                int index = contentType.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    return null;
                }

                string value = contentType.Substring(index + marker.Length).Trim();
                if (value.Length >= 2 && value[0] == '"')
                {
                    int end = value.IndexOf('"', 1);
                    return end > 0 ? value.Substring(1, end - 1) : value.Trim('"');
                }

                int semi = value.IndexOf(';', StringComparison.Ordinal);
                return semi < 0 ? value : value.Substring(0, semi);
            }

            private static bool PostDataMatches(Entry candidate, byte[] postData, List<KeyValuePair<string, string>> headers)
            {
                if (candidate.PostData == null || postData == null)
                {
                    return true;
                }

                if (candidate.PostData.Length == postData.Length)
                {
                    bool same = true;
                    for (int i = 0; i < postData.Length; i++)
                    {
                        if (candidate.PostData[i] != postData[i])
                        {
                            same = false;
                            break;
                        }
                    }

                    if (same)
                    {
                        return true;
                    }
                }

                string boundary = MultipartBoundary(headers);
                string candidateBoundary = MultipartBoundary(candidate.RequestHeaders);
                if (string.IsNullOrEmpty(boundary) || string.IsNullOrEmpty(candidateBoundary))
                {
                    return false;
                }

                string left = Encoding.UTF8.GetString(postData).Replace(boundary, string.Empty, StringComparison.Ordinal);
                string right = Encoding.UTF8.GetString(candidate.PostData).Replace(candidateBoundary, string.Empty, StringComparison.Ordinal);
                return string.Equals(left, right, StringComparison.Ordinal);
            }

            private static int CountMatchingHeaders(List<KeyValuePair<string, string>> harHeaders, List<KeyValuePair<string, string>> headers)
            {
                HashSet<string> set = new(StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> header in headers)
                {
                    set.Add((header.Key ?? string.Empty).ToUpperInvariant() + ":" + (header.Value ?? string.Empty));
                }

                int matches = 0;
                foreach (KeyValuePair<string, string> header in harHeaders)
                {
                    if (set.Contains((header.Key ?? string.Empty).ToUpperInvariant() + ":" + (header.Value ?? string.Empty)))
                    {
                        matches++;
                    }
                }

                return matches;
            }

            private static bool IsRedirect(int status)
            {
                foreach (int code in RedirectStatus)
                {
                    if (code == status)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static string CombineLocation(string baseUrl, string location)
            {
                if (string.IsNullOrEmpty(location))
                {
                    return baseUrl;
                }

                if (location.Contains("://", StringComparison.Ordinal)
                    || location.StartsWith("//", StringComparison.Ordinal))
                {
                    if (location.StartsWith("//", StringComparison.Ordinal)
                        && Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri schemeBase))
                    {
                        return schemeBase.Scheme + ":" + location;
                    }

                    return Uri.TryCreate(location, UriKind.Absolute, out Uri absolute)
                        ? absolute.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped)
                        : location;
                }

                if (Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri baseUri)
                    && Uri.TryCreate(baseUri, location, out Uri combined))
                {
                    return combined.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped);
                }

                return location;
            }

            private bool TryFind(
                string url,
                string method,
                List<KeyValuePair<string, string>> headers,
                byte[] postData,
                out Entry entry)
            {
                string currentUrl = Normalize(url);
                string currentMethod = method ?? "GET";
                HashSet<int> visited = new();
                while (true)
                {
                    List<int> matches = new();
                    for (int i = 0; i < _entries.Count; i++)
                    {
                        Entry candidate = _entries[i];
                        if (!string.Equals(candidate.Url, currentUrl, StringComparison.Ordinal)
                            || !string.Equals(candidate.Method, currentMethod, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (string.Equals(currentMethod, "POST", StringComparison.OrdinalIgnoreCase)
                            && postData != null
                            && candidate.PostData != null
                            && !PostDataMatches(candidate, postData, headers))
                        {
                            continue;
                        }

                        matches.Add(i);
                    }

                    if (matches.Count == 0)
                    {
                        entry = default;
                        return false;
                    }

                    int chosen = matches[0];
                    if (matches.Count > 1)
                    {
                        int bestScore = -1;
                        foreach (int index in matches)
                        {
                            int score = CountMatchingHeaders(_entries[index].RequestHeaders, headers);
                            if (score > bestScore)
                            {
                                bestScore = score;
                                chosen = index;
                            }
                        }
                    }

                    if (!visited.Add(chosen))
                    {
                        entry = default;
                        return false;
                    }

                    Entry selected = _entries[chosen];
                    string location = HeaderValue(selected.Headers, "location");
                    if (IsRedirect(selected.Status) && !string.IsNullOrEmpty(location))
                    {
                        currentUrl = Normalize(CombineLocation(currentUrl, location));
                        if (((selected.Status == 301 || selected.Status == 302)
                                && string.Equals(currentMethod, "POST", StringComparison.OrdinalIgnoreCase))
                            || (selected.Status == 303
                                && !string.Equals(currentMethod, "GET", StringComparison.OrdinalIgnoreCase)
                                && !string.Equals(currentMethod, "HEAD", StringComparison.OrdinalIgnoreCase)))
                        {
                            currentMethod = "GET";
                            postData = null;
                        }

                        continue;
                    }

                    entry = selected;
                    return true;
                }
            }

            private struct Entry
            {
                internal string Url { get; set; }

                internal string Method { get; set; }

                internal int Status { get; set; }

                internal List<KeyValuePair<string, string>> Headers { get; set; }

                internal List<KeyValuePair<string, string>> RequestHeaders { get; set; }

                internal string ContentType { get; set; }

                internal byte[] Body { get; set; }

                internal byte[] PostData { get; set; }
            }
        }
    }
}
