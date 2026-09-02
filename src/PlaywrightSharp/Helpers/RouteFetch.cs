/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Performs <see cref="IRoute.FetchAsync"/> with <see cref="HttpClient"/>.
    /// </summary>
    internal static class RouteFetch
    {
        /// <summary>
        /// Fetches <paramref name="request"/>, applying optional overrides.
        /// </summary>
        /// <param name="request">The intercepted request.</param>
        /// <param name="url">Optional URL override.</param>
        /// <param name="method">Optional method override.</param>
        /// <param name="headers">Optional header override.</param>
        /// <param name="postData">Optional body override.</param>
        /// <param name="timeout">Optional timeout in milliseconds. Defaults to 30 seconds.</param>
        /// <param name="maxRedirects">Optional redirect limit. Pass <c>0</c> to return the redirect.</param>
        /// <param name="maxRetries">Maximum connection-reset retries. Defaults to <c>0</c>.</param>
        /// <param name="postDataText">UTF-8 body override. When set, wins over <paramref name="postData"/>.</param>
        /// <param name="postDataJson">JSON body override. When set, wins over <paramref name="postDataText"/>.</param>
        /// <returns>The fetched response.</returns>
        internal static async Task<RouteFetchResult> FetchAsync(
            IRequest request,
            string url,
            string method,
            IEnumerable<KeyValuePair<string, string>> headers,
            byte[] postData,
            int? timeout,
            int? maxRedirects = default,
            int maxRetries = 0,
            string postDataText = default,
            object postDataJson = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (maxRedirects.HasValue && maxRedirects.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRedirects), "maxRedirects must be zero or positive.");
            }

            if (maxRetries < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRetries), "maxRetries must be zero or positive.");
            }

            string targetUrl = string.IsNullOrEmpty(url) ? request.Url : url;
            string targetMethod = string.IsNullOrEmpty(method) ? request.Method : method;
            if (string.IsNullOrEmpty(targetUrl))
            {
                throw new PlaywrightSharpException("Cannot fetch a route without a URL.");
            }

            if (string.IsNullOrEmpty(targetMethod))
            {
                targetMethod = "GET";
            }

            IEnumerable<KeyValuePair<string, string>> requestHeaders = headers ?? request.Headers;
            byte[] body = postDataJson != null
                ? Encoding.UTF8.GetBytes(JsonSerializer.Serialize(postDataJson, postDataJson.GetType(), JsonExtensions.DefaultJsonSerializerOptions))
                : postDataText != null
                    ? Encoding.UTF8.GetBytes(postDataText)
                    : postData ?? request.PostDataBuffer;
            int timeoutMs = timeout.HasValue && timeout.Value > 0 ? timeout.Value : 30_000;
            using CancellationTokenSource cts = new(timeoutMs);
            HttpClient owned = CreateClient(allowAutoRedirect: !maxRedirects.HasValue);

            HttpResponseMessage response = null;
            int backoffMs = 250;
            try
            {
                for (int attempt = 0; attempt <= maxRetries; attempt++)
                {
                    using HttpRequestMessage message = CreateRequest(targetUrl, targetMethod, requestHeaders, body, out string contentType);
                    try
                    {
                        response = maxRedirects.HasValue
                            ? await SendFollowingRedirectsAsync(
                                owned,
                                message,
                                requestHeaders,
                                body,
                                contentType,
                                targetMethod,
                                maxRedirects.Value,
                                cts.Token).ConfigureAwait(false)
                            : await owned.SendAsync(message, cts.Token).ConfigureAwait(false);
                        break;
                    }
                    catch (OperationCanceledException ex)
                    {
                        throw new TimeoutException($"route.fetch: Timeout {timeoutMs}ms exceeded.", ex);
                    }
                    catch (Exception ex) when (IsConnectionReset(ex))
                    {
                        if (maxRetries == 0)
                        {
                            throw;
                        }

                        if (attempt == maxRetries)
                        {
                            throw new PlaywrightSharpException(
                                "Route.FetchAsync maxRetries exceeded: " + maxRetries,
                                ex);
                        }

                        await Task.Delay(backoffMs).ConfigureAwait(false);
                        backoffMs *= 2;
                    }
                }

                if (response == null)
                {
                    throw new PlaywrightSharpException("Route.FetchAsync maxRetries exceeded: " + maxRetries);
                }

                using (response)
                {
                    byte[] responseBody = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    Dictionary<string, string> responseHeaders = new(StringComparer.OrdinalIgnoreCase);
                    foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
                    {
#pragma warning disable CA1308 // Playwright header maps use lower-cased names.
                        responseHeaders[header.Key.ToLowerInvariant()] = RouteFulfill.JoinValues(header.Key, header.Value);
#pragma warning restore CA1308
                    }

                    foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
                    {
#pragma warning disable CA1308 // Playwright header maps use lower-cased names.
                        responseHeaders[header.Key.ToLowerInvariant()] = RouteFulfill.JoinValues(header.Key, header.Value);
#pragma warning restore CA1308
                    }

                    byte[] decodedBody = RouteFulfill.DecodeEncodedBody(
                        responseBody ?? Array.Empty<byte>(),
                        responseHeaders);
                    return new RouteFetchResult
                    {
                        Status = (int)response.StatusCode,
                        StatusText = response.ReasonPhrase ?? string.Empty,
                        Url = response.RequestMessage?.RequestUri?.ToString() ?? targetUrl,
                        Headers = responseHeaders,
                        Body = decodedBody,
                    };
                }
            }
            finally
            {
                owned?.Dispose();
            }
        }

        private static HttpClient CreateClient(bool allowAutoRedirect = true)
        {
            HttpClientHandler handler = null;
            try
            {
                handler = new HttpClientHandler
                {
                    AllowAutoRedirect = allowAutoRedirect,
                    UseCookies = false,
                    CheckCertificateRevocationList = true,
                    AutomaticDecompression = DecompressionMethods.None,
                };
                HttpClient client = new(handler, disposeHandler: true)
                {
                    Timeout = Timeout.InfiniteTimeSpan,
                };
                handler = null;
                return client;
            }
            finally
            {
                handler?.Dispose();
            }
        }

        private static HttpRequestMessage CreateRequest(
            string targetUrl,
            string targetMethod,
            IEnumerable<KeyValuePair<string, string>> headers,
            byte[] body,
            out string contentType)
        {
            HttpRequestMessage message = new(new HttpMethod(targetMethod), targetUrl);
            contentType = ApplyHeaders(message, headers);
            if (body != null && body.Length > 0 && !IsHead(targetMethod))
            {
                AttachBody(message, body, contentType);
            }

            return message;
        }

        private static void AttachBody(HttpRequestMessage message, byte[] body, string contentType)
        {
            ByteArrayContent content = new(body);
            if (!string.IsNullOrEmpty(contentType))
            {
                content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            }

            message.Content = content;
        }

        private static bool IsConnectionReset(Exception exception)
        {
            Exception current = exception;
            while (current != null)
            {
                if (current is SocketException socket
                    && (socket.SocketErrorCode == SocketError.ConnectionReset
                        || socket.SocketErrorCode == SocketError.ConnectionAborted
                        || socket.SocketErrorCode == SocketError.Shutdown))
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(current.Message)
                    && (current.Message.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase)
                        || current.Message.Contains("connection reset", StringComparison.OrdinalIgnoreCase)
                        || current.Message.Contains("connection was aborted", StringComparison.OrdinalIgnoreCase)
                        || current.Message.Contains("ECONNRESET", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                current = current.InnerException;
            }

            return false;
        }

        private static async Task<HttpResponseMessage> SendFollowingRedirectsAsync(
            HttpClient client,
            HttpRequestMessage request,
            IEnumerable<KeyValuePair<string, string>> headers,
            byte[] postData,
            string contentType,
            string method,
            int maxRedirects,
            CancellationToken token)
        {
            int remaining = maxRedirects;
            HttpRequestMessage current = request;
            HttpRequestMessage owned = null;
            try
            {
                while (true)
                {
                    HttpResponseMessage response = await client.SendAsync(current, token).ConfigureAwait(false);
                    int status = (int)response.StatusCode;
                    bool redirect = status == 301 || status == 302 || status == 303 || status == 307 || status == 308;
                    if (!redirect)
                    {
                        return response;
                    }

                    if (remaining == 0)
                    {
                        if (maxRedirects == 0)
                        {
                            return response;
                        }

                        response.Dispose();
                        throw new PlaywrightSharpException(
                            "Route.FetchAsync maxRedirects exceeded: " + maxRedirects);
                    }

                    Uri next = ResolveRedirectUri(current.RequestUri, response);
                    if (next == null)
                    {
                        return response;
                    }

                    bool preserve = status == 307 || status == 308;
                    remaining--;
                    response.Dispose();
                    owned?.Dispose();
                    owned = CreateFollowRequest(next, method, headers, postData, contentType, preserve);
                    current = owned;
                }
            }
            finally
            {
                owned?.Dispose();
            }
        }

        private static HttpRequestMessage CreateFollowRequest(
            Uri next,
            string method,
            IEnumerable<KeyValuePair<string, string>> headers,
            byte[] postData,
            string contentType,
            bool preserveMethod)
        {
            string followMethod = preserveMethod ? method : "GET";
            HttpRequestMessage follow = new(new HttpMethod(followMethod), next);
            ApplyHeaders(follow, headers);
            if (preserveMethod && postData != null && postData.Length > 0 && !IsHead(followMethod))
            {
                AttachBody(follow, postData, contentType);
            }

            return follow;
        }

        private static Uri ResolveRedirectUri(Uri current, HttpResponseMessage response)
        {
            Uri location = response.Headers.Location;
            if (location == null)
            {
                return null;
            }

            return location.IsAbsoluteUri ? location : new Uri(current, location);
        }

        private static string ApplyHeaders(HttpRequestMessage message, IEnumerable<KeyValuePair<string, string>> headers)
        {
            string contentType = null;
            if (headers == null)
            {
                return contentType;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.IsNullOrEmpty(header.Key) || IsHopByHop(header.Key))
                {
                    continue;
                }

                if (string.Equals(header.Key, "content-type", StringComparison.OrdinalIgnoreCase))
                {
                    contentType = header.Value;
                    continue;
                }

                message.Headers.TryAddWithoutValidation(header.Key, header.Value ?? string.Empty);
            }

            return contentType;
        }

        private static bool IsHead(string method)
            => string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase);

        private static bool IsHopByHop(string name)
            => string.Equals(name, "host", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "content-length", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "transfer-encoding", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "connection", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "keep-alive", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "upgrade", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "accept-encoding", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("proxy-", StringComparison.OrdinalIgnoreCase);
    }
}
