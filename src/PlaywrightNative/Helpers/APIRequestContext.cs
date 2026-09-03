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
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Cookie-aware HTTP client for <see cref="IBrowserContext.APIRequest"/>.
    /// </summary>
    internal sealed partial class APIRequestContext : IAPIRequestContext
    {
        private static readonly ConditionalWeakTable<IBrowserContext, APIRequestContext> _contexts =
            new ConditionalWeakTable<IBrowserContext, APIRequestContext>();

        private readonly IBrowserContext _context;
        private readonly APIRequestStandaloneOptions _standalone;
        private readonly ITracing _tracing;
        private readonly List<IAPIResponse> _responses = new();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private bool _disposed;
        private string _closeReason;
        private string _cachedUserAgent;

        private APIRequestContext(IBrowserContext context, APIRequestStandaloneOptions standalone = null)
        {
            _context = context;
            _standalone = standalone;
            _tracing = new EmptyTracing();
            ((EmptyTracing)_tracing).AttachApi(this);

            if (context != null)
            {
                context.Close += OnOwningContextClosed;
            }
        }

        /// <inheritdoc/>
        public ITracing Tracing => _tracing;

        /// <inheritdoc/>
        public async Task<IAPIResponse> FetchAsync(
            string url,
            string method = "GET",
            string data = null,
            IEnumerable<KeyValuePair<string, string>> headers = null,
            bool? failOnStatusCode = null,
            float? timeout = null,
            int? maxRedirects = null,
            bool ignoreHTTPSErrors = false,
            object json = null,
            IFormData form = null,
            IFormData multipart = null,
            IEnumerable<KeyValuePair<string, string>> queryParams = null,
            int maxRetries = 0,
            byte[] dataBytes = null)
        {
            EnsureNotDisposed();

            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("URL must be non-empty.", nameof(url));
            }

            url = ApplyQueryParams(url, queryParams);
            url = ResolveUrl(url);

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                throw new ArgumentException("URL must be absolute.", nameof(url));
            }

            if (!string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                throw new PlaywrightNativeException("Protocol \"" + uri.Scheme + ":\" not supported");
            }

            int bodyKinds = 0;
            if (data != null)
            {
                bodyKinds++;
            }

            if (json != null)
            {
                bodyKinds++;
            }

            if (form != null)
            {
                bodyKinds++;
            }

            if (multipart != null)
            {
                bodyKinds++;
            }

            if (dataBytes != null)
            {
                bodyKinds++;
            }

            if (bodyKinds > 1)
            {
                throw new ArgumentException("Pass only one of data, dataBytes, json, form, or multipart.");
            }

            if (json is Delegate)
            {
                throw new PlaywrightNativeException("Unexpected 'data' type given: " + json.GetType().Name);
            }

            string payload = data;
            string contentType = "text/plain";
            if (json != null)
            {
                payload = JsonSerializer.Serialize(json, json.GetType(), JsonExtensions.DefaultJsonSerializerOptions);
                contentType = "application/json";
            }
            else if (form != null && !RequireFormData(form).ContainsFiles())
            {
                payload = EncodeForm(form);
                contentType = "application/x-www-form-urlencoded";
            }
            else if (dataBytes != null)
            {
                contentType = "application/octet-stream";
            }

            string verb = string.IsNullOrEmpty(method) ? "GET" : method;

            IReadOnlyDictionary<string, string> contextHeaders = _standalone?.ExtraHttpHeaders;
            if (contextHeaders == null && _context is IHasExtraHttpHeaders extra)
            {
                contextHeaders = extra.ExtraHttpHeaders;
            }

            contextHeaders = WithDefaultApiHeaders(contextHeaders);
            contextHeaders = WithUserAgent(contextHeaders, await ResolveUserAgentAsync().ConfigureAwait(false));
            contextHeaders = WithHttpCredentials(contextHeaders, _standalone?.HttpCredentials, url);
            if (_context is IHasHttpCredentials contextCredentials)
            {
                contextHeaders = WithHttpCredentials(contextHeaders, contextCredentials.HttpCredentialsList, url);
            }

            int timeoutMs = timeout.HasValue
                ? (int)timeout.Value
                : _standalone?.TimeoutMs ?? (_context != null ? (int)_context.DefaultTimeout() : 30_000);
            if (timeoutMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be zero or positive.");
            }

            int redirectLimit = maxRedirects ?? _standalone?.MaxRedirects ?? 20;
            if (redirectLimit < 0)
            {
                throw new PlaywrightNativeException("'maxRedirects' must be greater than or equal to '0'");
            }

            if (maxRetries < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRetries), "maxRetries must be zero or positive.");
            }

            bool ignoreTls = ignoreHTTPSErrors
                || (_standalone != null && _standalone.IgnoreHttpsErrors)
                || (_context is IHasIgnoreHttpsErrors https && https.IgnoreHttpsErrors);

            IReadOnlyList<ClientCertificate> clientCertificates = ResolveClientCertificates();
            X509Certificate2 initialClientCertificate = ClientCertificateHelper.LoadMatching(clientCertificates, url);
            try
            {
                TlsCapture tlsCapture = new TlsCapture();
                FetchTiming timing = new FetchTiming(string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase));
                Proxy proxy = _standalone?.Proxy;
                if (proxy == null && _context is IHasProxy hasProxy)
                {
                    proxy = hasProxy.Proxy;
                }

                if (proxy == null && _context?.Browser is IHasLaunchProxy hasLaunch)
                {
                    proxy = hasLaunch.LaunchProxy;
                }

                using HttpClient client = CreateClient(
                    ignoreTls,
                    proxy,
                    clientCertificates,
                    initialClientCertificate,
                    uri.Host,
                    tlsCapture,
                    captureRawHeaders: string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase));

                (HttpResponseMessage response, string finalUrl, byte[] body) = await SendWithRetriesAsync(
                    client,
                    uri,
                    url,
                    verb,
                    payload,
                    dataBytes,
                    contentType,
                    form,
                    multipart,
                    contextHeaders,
                    headers,
                    redirectLimit,
                    maxRetries,
                    timeoutMs,
                    timing,
                    tlsCapture).ConfigureAwait(false);

                using (response)
                {
                    if (_disposed || _lifetime.IsCancellationRequested)
                    {
                        throw DisposedException(inFlight: true);
                    }

                    timing.MarkResponseEnd();
                    IReadOnlyList<NameValueEntry> rawHeaders = tlsCapture.RawHeaders.Count > 0
                        ? tlsCapture.RawHeaders
                        : ReadRawHeaders(response);
                    IReadOnlyDictionary<string, string> responseHeaders = ToHeaderMap(rawHeaders);
                    if (_context != null)
                    {
                        await IngestSetCookiesAsync(_context, finalUrl ?? url, response).ConfigureAwait(false);
                    }
                    else
                    {
                        IngestStandaloneSetCookies(finalUrl ?? url, response);
                    }

                    try
                    {
                        body = RouteFulfill.DecodeEncodedBody(body, responseHeaders);
                    }
                    catch (Exception ex) when (ex is not PlaywrightNativeException)
                    {
                        throw new PlaywrightNativeException(
                            "failed to decompress '" + (HeaderMap.Value(responseHeaders, "content-encoding") ?? string.Empty).Trim() + "' encoding",
                            ex);
                    }

                    // Ownership of the response transfers to the caller.
                    APIResponse result = new APIResponse(
                        (int)response.StatusCode,
                        response.ReasonPhrase ?? string.Empty,
                        finalUrl ?? response.RequestMessage?.RequestUri?.AbsoluteUri ?? url,
                        responseHeaders,
                        body,
                        tlsCapture.Details,
                        timing.Result,
                        rawHeaders);

                    bool fail = failOnStatusCode ?? _standalone?.FailOnStatusCode ?? false;
                    if (fail && !result.Ok)
                    {
                        string bodyText = Encoding.UTF8.GetString(body ?? Array.Empty<byte>());
                        await result.DisposeAsync().ConfigureAwait(false);
                        throw new PlaywrightNativeException(
                            result.Status + " " + result.StatusText + "\nResponse text:\n" + bodyText);
                    }

                    lock (_responses)
                    {
                        _responses.Add(result);
                    }

                    OfficialTraceSession apiTrace = (_tracing as EmptyTracing)?.OwnOfficialSession();
                    if (apiTrace != null && apiTrace.IsRecording)
                    {
                        string pathAndQuery = uri.PathAndQuery;
                        apiTrace.RecordAction(
                            verb.ToUpperInvariant() + " " + pathAndQuery,
                            "APIRequestContext",
                            "fetch");
                        byte[] postBytes = dataBytes ?? (payload != null ? Encoding.UTF8.GetBytes(payload) : null);
                        apiTrace.AddApiResource(
                            verb.ToUpperInvariant(),
                            finalUrl ?? url,
                            result.Status,
                            result.StatusText,
                            result.Headers,
                            postBytes,
                            body);
                    }

                    return result;
                }
            }
            finally
            {
                initialClientCertificate?.Dispose();
            }
        }

        /// <inheritdoc/>
        public Task<string> StorageStateAsync(string path = default, bool? indexedDB = default)
        {
            EnsureNotDisposed();
            if (_context != null)
            {
                return _context.StorageStateAsync(path, indexedDB);
            }

            if (_standalone?.Cookies != null)
            {
                _standalone.Cookies.RemoveAll(IsExpiredSetCookie);
            }

            StorageState state = new StorageState
            {
                Cookies = ExportStandaloneCookies(_standalone?.Cookies),
                Origins = ExportStandaloneOrigins(_standalone?.Origins, indexedDB == true),
            };
            string json = StorageStateHelper.Serialize(state);
            if (!string.IsNullOrEmpty(path))
            {
                PathIo.WriteText(path, StorageStateHelper.PrettyPrint(json));
            }

            return Task.FromResult(json);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (!_lifetime.IsCancellationRequested)
            {
                await _lifetime.CancelAsync().ConfigureAwait(false);
            }

            if (_context != null)
            {
                _context.Close -= OnOwningContextClosed;
            }

            IAPIResponse[] copy;
            lock (_responses)
            {
                copy = _responses.ToArray();
                _responses.Clear();
            }

            foreach (IAPIResponse response in copy)
            {
                await response.DisposeAsync().ConfigureAwait(false);
            }

            _lifetime.Dispose();
        }

        /// <summary>
        /// Returns the request context bound to <paramref name="context"/>.
        /// </summary>
        /// <param name="context">The browser context whose cookies are sent.</param>
        /// <returns>The shared request context.</returns>
        internal static IAPIRequestContext For(IBrowserContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            APIRequestContext request = _contexts.GetValue(context, key => new APIRequestContext(key));
            if (request._disposed)
            {
                // Upstream returns a usable context.request after the previous
                // APIRequestContext was disposed; replace the cached instance.
                _contexts.Remove(context);
                request = _contexts.GetValue(context, key => new APIRequestContext(key));
            }

            if (context.IsClosed)
            {
                request.MarkOwnerClosed();
            }

            return request;
        }

        /// <summary>
        /// Creates a request context that is not bound to a browser.
        /// </summary>
        /// <param name="ignoreHttpsErrors">Ignore TLS certificate errors.</param>
        /// <param name="extraHttpHeaders">Headers sent with every request.</param>
        /// <param name="baseUrl">Prefix used to resolve relative request URLs.</param>
        /// <param name="userAgent">Value for the User-Agent header.</param>
        /// <param name="timeout">Default request timeout in milliseconds.</param>
        /// <param name="failOnStatusCode">Throw on a non-2xx status.</param>
        /// <param name="maxRedirects">Default redirect limit.</param>
        /// <param name="storageState">Playwright storage-state JSON.</param>
        /// <param name="storageStatePath">Path to a storage-state JSON file.</param>
        /// <param name="httpCredentials">HTTP Basic credentials.</param>
        /// <param name="proxy">Network proxy, or null.</param>
        /// <param name="clientCertificates">TLS client certificates, or null.</param>
        /// <returns>A new standalone context.</returns>
        internal static IAPIRequestContext CreateStandalone(
            bool ignoreHttpsErrors,
            IEnumerable<KeyValuePair<string, string>> extraHttpHeaders,
            string baseUrl = null,
            string userAgent = null,
            float? timeout = null,
            bool failOnStatusCode = false,
            int? maxRedirects = null,
            string storageState = null,
            string storageStatePath = null,
            IEnumerable<HttpCredentials> httpCredentials = null,
            Proxy proxy = null,
            IEnumerable<ClientCertificate> clientCertificates = null)
        {
            if (timeout.HasValue && timeout.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be zero or positive.");
            }

            if (maxRedirects.HasValue && maxRedirects.Value < 0)
            {
                throw new PlaywrightNativeException("'maxRedirects' must be greater than or equal to '0'");
            }

            Dictionary<string, string> headers = null;
            if (extraHttpHeaders != null)
            {
                headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, string> header in extraHttpHeaders)
                {
                    if (string.IsNullOrEmpty(header.Key))
                    {
                        continue;
                    }

                    headers[header.Key] = header.Value ?? string.Empty;
                }
            }

            StorageState state = StorageStateHelper.Load(storageState, storageStatePath);
            List<Cookie> cookies = state.Cookies == null
                ? new List<Cookie>()
                : new List<Cookie>(state.Cookies);

            return new APIRequestContext(
                null,
                new APIRequestStandaloneOptions
                {
                    IgnoreHttpsErrors = ignoreHttpsErrors,
                    ExtraHttpHeaders = headers,
                    BaseUrl = baseUrl,
                    UserAgent = userAgent,
                    TimeoutMs = timeout.HasValue ? (int)timeout.Value : (int?)null,
                    FailOnStatusCode = failOnStatusCode,
                    MaxRedirects = maxRedirects,
                    Cookies = cookies,
                    Origins = CopyStandaloneOrigins(state.Origins, includeIndexedDB: true),
                    HttpCredentials = SnapshotCredentials(httpCredentials),
                    Proxy = proxy,
                    ClientCertificates = ClientCertificateHelper.Snapshot(clientCertificates),
                });
        }

        private static HttpClient CreateClient(
            bool ignoreTls,
            Proxy proxy,
            IReadOnlyList<ClientCertificate> clientCertificates,
            X509Certificate2 initialClientCertificate,
            string initialHost,
            TlsCapture tlsCapture,
            bool captureRawHeaders = true)
        {
            SocketsHttpHandler handler = null;
            try
            {
                handler = new SocketsHttpHandler
                {
                    AutomaticDecompression = DecompressionMethods.None,
                    AllowAutoRedirect = false,
                    UseCookies = false,
                };

#pragma warning disable CA5359 // Intentional: ignoreHTTPSErrors accepts any certificate.
                handler.SslOptions.RemoteCertificateValidationCallback =
                    (_, certificate, _, errors) =>
                    {
                        if (certificate != null && tlsCapture != null)
                        {
                            tlsCapture.Details = FromCertificate(certificate);
                        }

                        return ignoreTls || errors == System.Net.Security.SslPolicyErrors.None;
                    };
#pragma warning restore CA5359
                if (ignoreTls)
                {
                    handler.SslOptions.CertificateRevocationCheckMode = X509RevocationMode.NoCheck;
                }

                if (clientCertificates != null && clientCertificates.Count > 0)
                {
                    if (initialClientCertificate != null)
                    {
                        handler.SslOptions.ClientCertificates = new X509CertificateCollection { initialClientCertificate };
                    }

                    handler.SslOptions.LocalCertificateSelectionCallback =
                        (_, targetHost, _, _, _) =>
                        {
                            if (string.IsNullOrEmpty(targetHost))
                            {
                                return null;
                            }

                            if (initialClientCertificate != null
                                && string.Equals(targetHost, initialHost, StringComparison.OrdinalIgnoreCase))
                            {
                                return initialClientCertificate;
                            }

                            foreach (ClientCertificate option in clientCertificates)
                            {
                                if (option == null || string.IsNullOrEmpty(option.Origin))
                                {
                                    continue;
                                }

                                if (!Uri.TryCreate(option.Origin, UriKind.Absolute, out Uri origin))
                                {
                                    continue;
                                }

                                if (!string.Equals(origin.Host, targetHost, StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                return ClientCertificateHelper.Load(option);
                            }

                            return null;
                        };
                }

                APIRequestProxyConnect.Apply(handler, proxy, ignoreTls);

                // Capture wire header order/casing. Skip on plain HTTP: the filter has
                // raced connection teardown on macOS/Linux CI (ResponseEnded → "socket
                // hang up"). HAR/raw-header consumers fall back to ReadRawHeaders.
                if (tlsCapture != null && captureRawHeaders)
                {
                    handler.PlaintextStreamFilter = (context, _) =>
                    {
                        Stream stream = new HeaderCaptureStream(context.PlaintextStream, tlsCapture);
                        return ValueTask.FromResult(stream);
                    };
                }

                HttpClient client = new HttpClient(handler, disposeHandler: true)
                {
                    Timeout = System.Threading.Timeout.InfiniteTimeSpan,
                    DefaultRequestVersion = HttpVersion.Version11,
                    DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
                };
                client.DefaultRequestHeaders.UserAgent.Clear();
                handler = null;
                return client;
            }
            finally
            {
                handler?.Dispose();
            }
        }

        private static ResponseSecurityDetailsResult FromCertificate(X509Certificate certificate)
        {
            X509Certificate2 existing = certificate as X509Certificate2;
            X509Certificate2 created = null;
            X509Certificate2 cert = existing;
            if (cert == null)
            {
                created = new X509Certificate2(certificate);
                cert = created;
            }

            try
            {
                string subject = cert.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
                if (string.IsNullOrEmpty(subject))
                {
                    subject = cert.Subject;
                }

                string issuer = cert.GetNameInfo(X509NameType.SimpleName, forIssuer: true);
                if (string.IsNullOrEmpty(issuer))
                {
                    issuer = cert.Issuer;
                }

                DateTimeOffset notBefore = new DateTimeOffset(DateTime.SpecifyKind(cert.NotBefore.ToUniversalTime(), DateTimeKind.Utc));
                DateTimeOffset notAfter = new DateTimeOffset(DateTime.SpecifyKind(cert.NotAfter.ToUniversalTime(), DateTimeKind.Utc));
                return new ResponseSecurityDetailsResult
                {
                    Protocol = "TLSv1.3",
                    SubjectName = subject,
                    Issuer = issuer,
                    ValidFrom = notBefore.ToUnixTimeSeconds(),
                    ValidTo = notAfter.ToUnixTimeSeconds(),
                };
            }
            finally
            {
                created?.Dispose();
            }
        }

        private static HttpContent CreateTypedContent(string payload, string contentType)
        {
            ByteArrayContent content = new ByteArrayContent(Encoding.UTF8.GetBytes(payload ?? string.Empty));
            if (!string.IsNullOrEmpty(contentType))
            {
                content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            }

            return content;
        }

        private static bool TryParseCookieDate(string value, out DateTimeOffset expires)
        {
            if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out expires))
            {
                return true;
            }

            return DateTimeOffset.TryParseExact(
                value,
                "r",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out expires);
        }

        private static FormData RequireFormData(IFormData form, string paramName = "form")
        {
            if (form is FormData fields)
            {
                return fields;
            }

            throw new ArgumentException(paramName + " must be created with CreateFormData.", paramName);
        }

        private static HttpContent CreateMultipart(FormData fields)
        {
            string boundary = "----WebKitFormBoundary" + RandomBoundaryToken();
            using MemoryStream buffer = new MemoryStream();
            byte[] dash = Encoding.ASCII.GetBytes("--");
            byte[] crlf = Encoding.ASCII.GetBytes("\r\n");
            byte[] boundaryBytes = Encoding.ASCII.GetBytes(boundary);
            foreach ((string name, string text, FilePayload file) in fields.GetFields())
            {
                buffer.Write(dash);
                buffer.Write(boundaryBytes);
                buffer.Write(crlf);
                if (file != null)
                {
                    string fileName = string.IsNullOrEmpty(file.Name) ? "blob" : file.Name;
                    string mime = string.IsNullOrEmpty(file.MimeType) ? "application/octet-stream" : file.MimeType;
                    string disposition = "content-disposition: form-data; name=\"" + name + "\"; filename=\"" + fileName + "\"\r\n";
                    buffer.Write(Encoding.UTF8.GetBytes(disposition));
                    buffer.Write(Encoding.ASCII.GetBytes("content-type: " + mime + "\r\n\r\n"));
                    if (file.Buffer != null && file.Buffer.Length > 0)
                    {
                        buffer.Write(file.Buffer);
                    }
                }
                else
                {
                    string disposition = "content-disposition: form-data; name=\"" + name + "\"\r\n\r\n";
                    buffer.Write(Encoding.UTF8.GetBytes(disposition));
                    buffer.Write(Encoding.UTF8.GetBytes(text ?? string.Empty));
                }

                buffer.Write(crlf);
            }

            buffer.Write(dash);
            buffer.Write(boundaryBytes);
            buffer.Write(dash);
            buffer.Write(crlf);

            ByteArrayContent content = new ByteArrayContent(buffer.ToArray());
            content.Headers.TryAddWithoutValidation("Content-Type", "multipart/form-data; boundary=" + boundary);
            return content;
        }

        private static string RandomBoundaryToken()
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789AB";
            Span<char> chars = stackalloc char[16];
            Span<byte> bytes = stackalloc byte[16];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = alphabet[bytes[i] % alphabet.Length];
            }

            return new string(chars);
        }

        private static string EncodeForm(IFormData form)
        {
            FormData fields = RequireFormData(form);

            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string, string> entry in fields.GetEntries())
            {
                if (builder.Length > 0)
                {
                    builder.Append('&');
                }

                builder.Append(Uri.EscapeDataString(entry.Key));
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(entry.Value ?? string.Empty));
            }

            return builder.ToString();
        }

        private static string ApplyQueryParams(
            string url,
            IEnumerable<KeyValuePair<string, string>> queryParams)
        {
            if (queryParams == null)
            {
                return url;
            }

            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string, string> entry in queryParams)
            {
                if (string.IsNullOrEmpty(entry.Key))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('&');
                }

                builder.Append(Uri.EscapeDataString(entry.Key));
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(entry.Value ?? string.Empty));
            }

            if (builder.Length == 0)
            {
                return url;
            }

            int hash = url.IndexOf('#');
            string fragment = hash >= 0 ? url.Substring(hash) : string.Empty;
            string withoutFragment = hash >= 0 ? url.Substring(0, hash) : url;
            string separator = withoutFragment.Contains('?') ? "&" : "?";
            return withoutFragment + separator + builder + fragment;
        }

        private static bool IsRedirectLimitExceeded(Exception exception)
        {
            Exception current = exception;
            while (current != null)
            {
                if (!string.IsNullOrEmpty(current.Message)
                    && current.Message.Contains("redirect", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                current = current.InnerException;
            }

            return false;
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

        private static bool IsPrematureResponseEnd(Exception exception)
        {
            Exception current = exception;
            while (current != null)
            {
                if (!string.IsNullOrEmpty(current.Message)
                    && (current.Message.Contains("ended prematurely", StringComparison.OrdinalIgnoreCase)
                        || current.Message.Contains("response ended", StringComparison.OrdinalIgnoreCase)
                        || current.Message.Contains("unexpected end of stream", StringComparison.OrdinalIgnoreCase)
                        || current.Message.Contains("The response ended prematurely", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                current = current.InnerException;
            }

            return false;
        }

        private static bool MidBodyFromRawHeaders(TlsCapture tlsCapture)
        {
            if (tlsCapture?.RawHeaders == null)
            {
                return false;
            }

            foreach (NameValueEntry header in tlsCapture.RawHeaders)
            {
                if (header.Name != null
                    && header.Name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
                    && long.TryParse(header.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long length)
                    && length > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyDictionary<string, string> WithDefaultApiHeaders(
            IReadOnlyDictionary<string, string> headers)
        {
            Dictionary<string, string> merged = headers == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
            if (!merged.ContainsKey("Accept"))
            {
                merged["Accept"] = "*/*";
            }

            if (!merged.ContainsKey("Accept-Encoding"))
            {
                merged["Accept-Encoding"] = "gzip,deflate,br";
            }

            return merged;
        }

        private static IReadOnlyDictionary<string, string> WithUserAgent(
            IReadOnlyDictionary<string, string> headers,
            string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent))
            {
                return headers;
            }

            Dictionary<string, string> merged = headers == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
            if (!merged.ContainsKey("User-Agent"))
            {
                merged["User-Agent"] = userAgent;
            }

            return merged;
        }

        private static IReadOnlyDictionary<string, string> WithHttpCredentials(
            IReadOnlyDictionary<string, string> headers,
            IEnumerable<HttpCredentials> credentials,
            string requestUrl)
            => WithHttpCredentials(headers, HttpBasicAuth.Pick(credentials, requestUrl), requestUrl);

        private static IReadOnlyDictionary<string, string> WithHttpCredentials(
            IReadOnlyDictionary<string, string> headers,
            HttpCredentials credentials,
            string requestUrl)
        {
            if (!HttpBasicAuth.ShouldSendPreemptively(credentials, defaultAlways: true)
                || !HttpBasicAuth.MatchesOrigin(credentials, requestUrl))
            {
                return headers;
            }

            Dictionary<string, string> merged = headers == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
            if (!merged.ContainsKey("Authorization"))
            {
                string token = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes((credentials.Username ?? string.Empty) + ":" + (credentials.Password ?? string.Empty)));
                merged["Authorization"] = "Basic " + token;
            }

            return merged;
        }

        private static void ApplyHeaders(
            HttpRequestMessage request,
            IEnumerable<KeyValuePair<string, string>> headers,
            bool overwrite,
            bool skipHost = false)
        {
            if (headers == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.IsNullOrEmpty(header.Key))
                {
                    continue;
                }

                if (skipHost && string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string value = header.Value ?? string.Empty;
                if (!IsAsciiHeader(header.Key) || !IsAsciiHeader(value))
                {
                    throw new PlaywrightNativeException("Invalid character in header content");
                }

                if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)
                    && request.Content != null)
                {
                    request.Content.Headers.Remove("Content-Type");
                    request.Content.Headers.TryAddWithoutValidation("Content-Type", value);
                    continue;
                }

                if (overwrite)
                {
                    request.Headers.Remove(header.Key);
                }

                request.Headers.TryAddWithoutValidation(header.Key, value);
            }
        }

        private static bool IsAsciiHeader(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }

            foreach (char c in value)
            {
                if (c > 0x7F)
                {
                    return false;
                }
            }

            return true;
        }

        private static IReadOnlyList<NameValueEntry> ReadRawHeaders(HttpResponseMessage response)
        {
            List<NameValueEntry> raw = new List<NameValueEntry>();
            AppendRawHeaders(raw, response.Headers);
            if (response.Content?.Headers != null)
            {
                AppendRawHeaders(raw, response.Content.Headers);
            }

            return raw;
        }

        private static void AppendRawHeaders(List<NameValueEntry> raw, HttpHeaders headers)
        {
            foreach (KeyValuePair<string, HeaderStringValues> header in headers.NonValidated)
            {
                string name = header.Key;
                if (string.IsNullOrEmpty(name) || name.StartsWith(':'))
                {
                    continue;
                }

                foreach (string value in header.Value)
                {
                    raw.Add(new NameValueEntry(name, value ?? string.Empty));
                }
            }
        }

        private static IReadOnlyDictionary<string, string> ToHeaderMap(IReadOnlyList<NameValueEntry> raw)
        {
            Dictionary<string, List<string>> grouped = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (raw != null)
            {
                foreach (NameValueEntry header in raw)
                {
                    if (header == null || string.IsNullOrEmpty(header.Name))
                    {
                        continue;
                    }

                    if (!grouped.TryGetValue(header.Name, out List<string> values))
                    {
                        values = new List<string>();
                        grouped[header.Name] = values;
                    }

                    values.Add(header.Value ?? string.Empty);
                }
            }

            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, List<string>> entry in grouped)
            {
                headers[entry.Key] = RouteFulfill.JoinValues(entry.Key, entry.Value);
            }

            return headers;
        }

        private static bool IsRedirect(HttpResponseMessage response)
        {
            int status = (int)response.StatusCode;
            return status == 301 || status == 302 || status == 303 || status == 307 || status == 308;
        }

        private static bool PreservesMethodOnRedirect(HttpStatusCode status)
            => status == HttpStatusCode.TemporaryRedirect || status == (HttpStatusCode)308;

        private static IReadOnlyList<HttpCredentials> SnapshotCredentials(IEnumerable<HttpCredentials> credentials)
        {
            if (credentials == null)
            {
                return null;
            }

            List<HttpCredentials> copy = new List<HttpCredentials>();
            foreach (HttpCredentials item in credentials)
            {
                if (item != null)
                {
                    copy.Add(item);
                }
            }

            return copy.Count == 0 ? null : copy;
        }

        private static bool SameOrigin(Uri left, Uri right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.IdnHost, right.IdnHost, StringComparison.OrdinalIgnoreCase)
                && left.Port == right.Port;
        }

        private static IReadOnlyDictionary<string, string> WithoutAuthorization(IReadOnlyDictionary<string, string> headers)
        {
            if (headers == null)
            {
                return null;
            }

            Dictionary<string, string> copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> header in headers)
            {
                if (!header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    copy[header.Key] = header.Value;
                }
            }

            return copy;
        }

        private static IEnumerable<KeyValuePair<string, string>> WithoutAuthorization(IEnumerable<KeyValuePair<string, string>> headers)
        {
            if (headers == null)
            {
                return null;
            }

            List<KeyValuePair<string, string>> copy = new List<KeyValuePair<string, string>>();
            foreach (KeyValuePair<string, string> header in headers)
            {
                if (!header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    copy.Add(header);
                }
            }

            return copy;
        }

        private static Uri ResolveRedirectUri(Uri current, HttpResponseMessage response)
        {
            string raw = ReadLocation(response);
            if (!string.IsNullOrEmpty(raw))
            {
                return CombineLocation(current, raw);
            }

            Uri location = response.Headers.Location;
            if (location == null)
            {
                return null;
            }

            return location.IsAbsoluteUri ? location : new Uri(current, location);
        }

        private static string ReadLocation(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("Location", out IEnumerable<string> headerValues))
            {
                foreach (string value in headerValues)
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }
            }

            if (response.Content?.Headers != null
                && response.Content.Headers.TryGetValues("Location", out IEnumerable<string> contentValues))
            {
                foreach (string value in contentValues)
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }
            }

            return null;
        }

        private static Uri CombineLocation(Uri current, string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return current;
            }

            // Unix treats "/empty.html" as an absolute file path. Only honor a
            // scheme or a protocol-relative URL as an absolute redirect target.
            if (location.StartsWith("//", StringComparison.Ordinal))
            {
                string protocolRelative = EncodeIri((current?.Scheme ?? Uri.UriSchemeHttp) + ":" + location);
                if (Uri.TryCreate(protocolRelative, UriKind.Absolute, out Uri combined)
                    && (string.Equals(combined.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(combined.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
                {
                    return combined;
                }
            }
            else if (location.Contains("://", StringComparison.Ordinal))
            {
                string encoded = EncodeIri(location);
                if (Uri.TryCreate(encoded, UriKind.Absolute, out Uri absolute)
                    && (string.Equals(absolute.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(absolute.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
                {
                    return absolute;
                }
            }

            return new Uri(current, EncodeIri(location));
        }

        private static string EncodeIri(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            StringBuilder builder = new StringBuilder(value.Length * 2);
            foreach (char c in value)
            {
                if (c <= 0x7F)
                {
                    builder.Append(c);
                }
                else
                {
                    builder.Append(Uri.EscapeDataString(c.ToString()));
                }
            }

            return builder.ToString();
        }

        private static bool HasHttpScheme(string url)
            => url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        private static bool HasHeader(IEnumerable<KeyValuePair<string, string>> headers, string name)
        {
            if (headers == null || string.IsNullOrEmpty(name))
            {
                return false;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CookieMatches(Cookie cookie, Uri uri)
        {
            if (cookie == null || string.IsNullOrEmpty(cookie.Name) || uri == null)
            {
                return false;
            }

            if (cookie.Expires.HasValue
                && cookie.Expires.Value >= 0
                && (cookie.Expires.Value <= 1
                    || (cookie.Expires.Value * 1000) < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
            {
                return false;
            }

            string domain = cookie.Domain;
            string path = cookie.Path;
            bool? secure = cookie.Secure;
            if (string.IsNullOrEmpty(domain)
                && !string.IsNullOrEmpty(cookie.Url)
                && Uri.TryCreate(cookie.Url, UriKind.Absolute, out Uri cookieUri))
            {
                domain = cookieUri.Host;
                if (string.IsNullOrEmpty(path))
                {
                    path = cookieUri.AbsolutePath;
                }

                if (!secure.HasValue
                    && string.Equals(cookieUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    secure = true;
                }
            }

            if (secure == true
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !IsLocalHostname(uri.Host))
            {
                return false;
            }

            if (string.IsNullOrEmpty(domain) || !CookieDomainMatches(uri.Host, domain))
            {
                return false;
            }

            return CookiePathMatches(uri.AbsolutePath, string.IsNullOrEmpty(path) ? "/" : path);
        }

        private static bool CookieDomainMatches(string host, string domain)
        {
            string trimmed = domain.Trim().TrimStart('.');
            if (string.IsNullOrEmpty(trimmed))
            {
                return false;
            }

            if (string.Equals(host, trimmed, StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, domain, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return host.EndsWith("." + trimmed, StringComparison.OrdinalIgnoreCase);
        }

        private static bool CookiePathMatches(string requestPath, string cookiePath)
        {
            string path = string.IsNullOrEmpty(requestPath) ? "/" : requestPath;
            if (cookiePath == "/")
            {
                return true;
            }

            if (!path.StartsWith(cookiePath, StringComparison.Ordinal))
            {
                return false;
            }

            return path.Length == cookiePath.Length
                || cookiePath.EndsWith('/')
                || path[cookiePath.Length] == '/';
        }

        private static string MethodLabel(string verb)
        {
            if (string.Equals(verb, "GET", StringComparison.OrdinalIgnoreCase))
            {
                return "get";
            }

            if (string.Equals(verb, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                return "head";
            }

            if (string.Equals(verb, "POST", StringComparison.OrdinalIgnoreCase))
            {
                return "post";
            }

            if (string.Equals(verb, "PUT", StringComparison.OrdinalIgnoreCase))
            {
                return "put";
            }

            if (string.Equals(verb, "PATCH", StringComparison.OrdinalIgnoreCase))
            {
                return "patch";
            }

            if (string.Equals(verb, "DELETE", StringComparison.OrdinalIgnoreCase))
            {
                return "delete";
            }

            return "fetch";
        }

        private static async Task IngestSetCookiesAsync(IBrowserContext context, string url, HttpResponseMessage response)
        {
            if (context == null || response == null)
            {
                return;
            }

            List<string> raw = new List<string>();
            if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string> headerValues))
            {
                raw.AddRange(headerValues);
            }

            if (response.Content?.Headers != null
                && response.Content.Headers.TryGetValues("Set-Cookie", out IEnumerable<string> contentValues))
            {
                raw.AddRange(contentValues);
            }

            if (raw.Count == 0)
            {
                return;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            List<Cookie> cookies = new List<Cookie>();
            foreach (string line in raw)
            {
                if (!seen.Add(line ?? string.Empty))
                {
                    continue;
                }

                Cookie cookie = ParseSetCookie(line, url);
                if (cookie == null)
                {
                    continue;
                }

                if (Uri.TryCreate(url, UriKind.Absolute, out Uri requestUri)
                    && !string.IsNullOrEmpty(cookie.Domain)
                    && !CookieDomainMatches(requestUri.Host, cookie.Domain))
                {
                    continue;
                }

                if (IsExpiredSetCookie(cookie))
                {
                    await context.ClearCookiesAsync(cookie.Name).ConfigureAwait(false);
                    continue;
                }

                cookies.Add(cookie);
            }

            if (cookies.Count == 0)
            {
                return;
            }

            try
            {
                await context.AddCookiesAsync(cookies).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                // Official fetch: cookie values over 4096 characters are rejected;
                // remaining Set-Cookie lines are applied one by one.
                foreach (Cookie cookie in cookies)
                {
                    try
                    {
                        await context.AddCookiesAsync(new[] { cookie }).ConfigureAwait(false);
                    }
                    catch (PlaywrightNativeException)
                    {
                    }
                }
            }
        }

        private static bool IsExpiredSetCookie(Cookie cookie)
        {
            if (cookie == null || !cookie.Expires.HasValue || cookie.Expires.Value < 0)
            {
                return false;
            }

            return cookie.Expires.Value <= 1
                || (cookie.Expires.Value * 1000) < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private static bool IsLocalHostname(string hostname)
            => string.Equals(hostname, "localhost", StringComparison.OrdinalIgnoreCase)
                || hostname.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);

        private static bool SameStandaloneCookie(Cookie left, Cookie right)
            => left != null
                && right != null
                && string.Equals(left.Name, right.Name, StringComparison.Ordinal)
                && string.Equals(left.Domain ?? string.Empty, right.Domain ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    string.IsNullOrEmpty(left.Path) ? "/" : left.Path,
                    string.IsNullOrEmpty(right.Path) ? "/" : right.Path,
                    StringComparison.Ordinal);

        private static List<Cookie> ExportStandaloneCookies(IEnumerable<Cookie> cookies)
        {
            List<Cookie> result = new List<Cookie>();
            if (cookies == null)
            {
                return result;
            }

            foreach (Cookie cookie in cookies)
            {
                if (cookie == null || IsExpiredSetCookie(cookie))
                {
                    continue;
                }

                result.Add(new Cookie
                {
                    Name = cookie.Name,
                    Value = cookie.Value ?? string.Empty,
                    Domain = cookie.Domain,
                    Path = string.IsNullOrEmpty(cookie.Path) ? "/" : cookie.Path,
                    Expires = cookie.Expires ?? -1,
                    HttpOnly = cookie.HttpOnly ?? false,
                    Secure = cookie.Secure ?? false,
                    SameSite = cookie.SameSite == default(Microsoft.Playwright.SameSiteAttribute?)
                        ? Microsoft.Playwright.SameSiteAttribute.Lax
                        : cookie.SameSite,
                });
            }

            return result;
        }

        private static List<StorageStateOrigin> CopyStandaloneOrigins(
            IEnumerable<StorageStateOrigin> origins,
            bool includeIndexedDB)
        {
            List<StorageStateOrigin> result = new List<StorageStateOrigin>();
            if (origins == null)
            {
                return result;
            }

            foreach (StorageStateOrigin origin in origins)
            {
                if (origin == null)
                {
                    continue;
                }

                StorageStateOrigin copy = new StorageStateOrigin
                {
                    Origin = origin.Origin,
                    Url = origin.Url,
                    LocalStorage = origin.LocalStorage == null
                        ? new List<NameValueEntry>()
                        : new List<NameValueEntry>(origin.LocalStorage),
                };
                if (includeIndexedDB
                    && origin.IndexedDB.ValueKind != JsonValueKind.Undefined
                    && origin.IndexedDB.ValueKind != JsonValueKind.Null)
                {
                    copy.IndexedDB = origin.IndexedDB.Clone();
                }
                else if (!includeIndexedDB)
                {
                    copy.IndexedDB = JsonSerializer.SerializeToElement(Array.Empty<object>());
                }

                result.Add(copy);
            }

            return result;
        }

        private static List<StorageStateOrigin> ExportStandaloneOrigins(
            IEnumerable<StorageStateOrigin> origins,
            bool includeIndexedDB)
            => CopyStandaloneOrigins(origins, includeIndexedDB);

        private static Cookie ParseSetCookie(string header, string url)
        {
            if (string.IsNullOrEmpty(header))
            {
                return null;
            }

            string[] parts = header.Split(';');
            if (parts.Length == 0)
            {
                return null;
            }

            int eq = parts[0].IndexOf('=');
            if (eq <= 0)
            {
                return null;
            }

            Cookie cookie = new Cookie
            {
                Name = parts[0].Substring(0, eq).Trim(),
                Value = parts[0].Substring(eq + 1).Trim(),
                SameSite = Microsoft.Playwright.SameSiteAttribute.Lax,
            };

            if (Uri.TryCreate(url, UriKind.Absolute, out Uri requestUri))
            {
                cookie.Domain = requestUri.Host;
                cookie.Path = DefaultCookiePath(requestUri.AbsolutePath);
            }

            for (int i = 1; i < parts.Length; i++)
            {
                string attr = parts[i].Trim();
                int attrEq = attr.IndexOf('=');
                string key = attrEq >= 0 ? attr.Substring(0, attrEq).Trim() : attr;
                string value = attrEq >= 0 ? attr.Substring(attrEq + 1).Trim() : string.Empty;
                if (string.Equals(key, "Path", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(value) && value.StartsWith('/'))
                    {
                        cookie.Path = value;
                    }
                }
                else if (string.Equals(key, "Domain", StringComparison.OrdinalIgnoreCase))
                {
                    cookie.Domain = value;
                }
                else if (string.Equals(key, "HttpOnly", StringComparison.OrdinalIgnoreCase))
                {
                    cookie.HttpOnly = true;
                }
                else if (string.Equals(key, "Secure", StringComparison.OrdinalIgnoreCase))
                {
                    cookie.Secure = true;
                }
                else if (string.Equals(key, "SameSite", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(value, "None", StringComparison.OrdinalIgnoreCase))
                    {
                        cookie.SameSite = Microsoft.Playwright.SameSiteAttribute.None;
                    }
                    else if (string.Equals(value, "Strict", StringComparison.OrdinalIgnoreCase))
                    {
                        cookie.SameSite = Microsoft.Playwright.SameSiteAttribute.Strict;
                    }
                    else
                    {
                        cookie.SameSite = Microsoft.Playwright.SameSiteAttribute.Lax;
                    }
                }
                else if (string.Equals(key, "Max-Age", StringComparison.OrdinalIgnoreCase)
                    && double.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out double maxAge))
                {
                    double expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + maxAge;
                    cookie.Expires = (float?)(expiresAt <= 0 ? 1 : expiresAt);
                }
                else if (string.Equals(key, "Expires", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseCookieDate(value, out DateTimeOffset expires))
                    {
                        long seconds = expires.ToUnixTimeSeconds();
                        cookie.Expires = seconds <= 0 ? 1 : seconds;
                    }
                    else
                    {
                        cookie.Expires = 1;
                    }
                }
            }

            return cookie;
        }

        private static string DefaultCookiePath(string pathname)
        {
            // RFC 6265 §5.1.4 / official fetch defaultPath.
            string path = string.IsNullOrEmpty(pathname) ? "/" : pathname;
            if (!path.StartsWith('/'))
            {
                path = "/" + path;
            }

            string rest = path.Length > 1 ? path.Substring(1) : string.Empty;
            string[] segments = rest.Split('/');
            if (segments.Length <= 1)
            {
                return "/";
            }

            return "/" + string.Join("/", segments, 0, segments.Length - 1);
        }

        private string ResolveUrl(string url)
        {
            if (HasHttpScheme(url))
            {
                return url;
            }

            string baseUrl = _standalone?.BaseUrl;
            if (string.IsNullOrEmpty(baseUrl) && _context is IHasBaseUrl hasBase)
            {
                baseUrl = hasBase.BaseURL;
            }

            if (string.IsNullOrEmpty(baseUrl)
                || !Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri baseUri))
            {
                return url;
            }

            return new Uri(baseUri, url).ToString();
        }

        private async Task<(HttpResponseMessage Response, string Url, byte[] Body)> SendWithRetriesAsync(
            HttpClient client,
            Uri uri,
            string url,
            string verb,
            string payload,
            byte[] dataBytes,
            string contentType,
            IFormData form,
            IFormData multipart,
            IReadOnlyDictionary<string, string> contextHeaders,
            IEnumerable<KeyValuePair<string, string>> headers,
            int maxRedirects,
            int maxRetries,
            int timeoutMs,
            FetchTiming timing,
            TlsCapture tlsCapture)
        {
            CancellationTokenSource timeoutCts = timeoutMs > 0
                ? new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs))
                : null;
            CancellationTokenSource linked = timeoutCts == null
                ? null
                : CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, timeoutCts.Token);
            CancellationToken token = linked?.Token ?? _lifetime.Token;
            try
            {
                int backoffMs = 250;
                for (int attempt = 0; attempt <= maxRetries; attempt++)
                {
                    HttpRequestMessage request = await CreateRequestAsync(
                        uri,
                        url,
                        verb,
                        payload,
                        dataBytes,
                        contentType,
                        form,
                        multipart,
                        contextHeaders,
                        headers).ConfigureAwait(false);
                    try
                    {
                        return await SendFollowingRedirectsAsync(
                            client,
                            request,
                            url,
                            verb,
                            payload,
                            dataBytes,
                            contentType,
                            form,
                            multipart,
                            contextHeaders,
                            headers,
                            maxRedirects,
                            timing,
                            tlsCapture,
                            token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException ex)
                    {
                        request.Dispose();
                        if (_disposed || _lifetime.IsCancellationRequested)
                        {
                            throw DisposedException(inFlight: true);
                        }

                        throw new PlaywrightNativeException(
                            "apiRequestContext." + MethodLabel(verb) + ": Timeout " + timeoutMs + "ms exceeded",
                            ex);
                    }
                    catch (HttpRequestException ex) when (IsRedirectLimitExceeded(ex))
                    {
                        request.Dispose();
                        throw new PlaywrightNativeException("Max redirect count exceeded", ex);
                    }
                    catch (ResetAfterHeadersException ex) when (ex.MidBody)
                    {
                        // Upstream: response aborted after headers/body started → "aborted", no retry.
                        request.Dispose();
                        throw new PlaywrightNativeException(
                            "apiRequestContext." + MethodLabel(verb) + ": aborted",
                            ex);
                    }
                    catch (Exception ex) when (ex is ResetAfterHeadersException || IsPrematureResponseEnd(ex) || IsConnectionReset(ex))
                    {
                        // Upstream retries ECONNRESET-style drops. Premature end before a
                        // mid-body abort (HttpContext.Abort / socket destroy) maps to
                        // "socket hang up" on Node.
                        request.Dispose();
                        if (maxRetries == 0)
                        {
                            throw new PlaywrightNativeException(
                                "apiRequestContext." + MethodLabel(verb) + ": socket hang up",
                                ex);
                        }

                        if (attempt == maxRetries)
                        {
                            throw new PlaywrightNativeException(
                                "APIRequest maxRetries exceeded: " + maxRetries + " " + url,
                                ex);
                        }

                        await Task.Delay(backoffMs, token).ConfigureAwait(false);
                        backoffMs *= 2;
                    }
                    catch
                    {
                        request.Dispose();
                        throw;
                    }
                }

                throw new PlaywrightNativeException("APIRequest maxRetries exceeded: " + maxRetries + " " + url);
            }
            finally
            {
                linked?.Dispose();
                timeoutCts?.Dispose();
            }
        }

        private async Task<HttpRequestMessage> CreateRequestAsync(
            Uri uri,
            string url,
            string verb,
            string payload,
            byte[] dataBytes,
            string contentType,
            IFormData form,
            IFormData multipart,
            IReadOnlyDictionary<string, string> contextHeaders,
            IEnumerable<KeyValuePair<string, string>> headers)
        {
            HttpRequestMessage request = null;
            try
            {
                request = new HttpRequestMessage(new HttpMethod(verb), uri);
                request.Headers.ExpectContinue = false;
                request.Headers.ConnectionClose = true;
                HttpContent content = null;
                if (multipart != null)
                {
                    content = CreateMultipart(RequireFormData(multipart, nameof(multipart)));
                }
                else if (form != null)
                {
                    FormData fields = RequireFormData(form);
                    if (fields.ContainsFiles())
                    {
                        content = CreateMultipart(fields);
                    }
                }

                if (content != null)
                {
                    await content.LoadIntoBufferAsync().ConfigureAwait(false);
                    request.Content = content;
                }
                else if (dataBytes != null)
                {
                    ByteArrayContent binary = new ByteArrayContent(dataBytes);
                    if (MediaTypeHeaderValue.TryParse(contentType ?? "application/octet-stream", out MediaTypeHeaderValue mediaType))
                    {
                        binary.Headers.ContentType = mediaType;
                    }

                    request.Content = binary;
                }
                else if (payload != null)
                {
                    request.Content = CreateTypedContent(payload, contentType);
                }

                ApplyHeaders(request, contextHeaders, overwrite: false);
                ApplyHeaders(request, headers, overwrite: true);
                await ApplyCookiesAsync(request, url).ConfigureAwait(false);
                HttpRequestMessage result = request;
                request = null;
                return result;
            }
            finally
            {
                request?.Dispose();
            }
        }

        private List<KeyValuePair<string, string>> SnapshotRequestHeaders(HttpRequestMessage request)
        {
            List<KeyValuePair<string, string>> headers = new List<KeyValuePair<string, string>>();
            if (request == null)
            {
                return headers;
            }

            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
            {
                foreach (string value in header.Value)
                {
                    headers.Add(new KeyValuePair<string, string>(header.Key, value));
                }
            }

            if (request.Content != null)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
                {
                    foreach (string value in header.Value)
                    {
                        headers.Add(new KeyValuePair<string, string>(header.Key, value));
                    }
                }

                if (!HasHeader(headers, "content-length")
                    && request.Content.Headers.ContentLength is long length)
                {
                    headers.Add(new KeyValuePair<string, string>(
                        "content-length",
                        length.ToString(CultureInfo.InvariantCulture)));
                }
            }

            return headers;
        }

        private async Task<byte[]> SnapshotRequestBodyAsync(HttpRequestMessage request)
        {
            if (request?.Content == null)
            {
                return null;
            }

            try
            {
                return await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is HttpRequestException)
            {
                return null;
            }
        }

        private (string Ip, int? Port) ResolveServer(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) || string.IsNullOrEmpty(uri.Host))
            {
                return (null, null);
            }

            try
            {
                if (IPAddress.TryParse(uri.Host, out IPAddress parsed))
                {
                    return (FormatHarIp(parsed), uri.Port);
                }

                IPAddress[] addresses = Dns.GetHostAddresses(uri.Host);
                if (addresses == null || addresses.Length == 0)
                {
                    return (null, uri.Port);
                }

                IPAddress chosen = addresses[0];
                foreach (IPAddress address in addresses)
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork && IPAddress.IsLoopback(address))
                    {
                        chosen = address;
                        break;
                    }

                    if (IPAddress.IsLoopback(address))
                    {
                        chosen = address;
                    }
                }

                return (FormatHarIp(chosen), uri.Port);
            }
            catch (SocketException)
            {
                return (null, uri.Port);
            }
        }

        private string FormatHarIp(IPAddress address)
        {
            if (address == null)
            {
                return null;
            }

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return "[" + address + "]";
            }

            return address.ToString();
        }

        private void RecordApiHop(
            string method,
            string url,
            IEnumerable<KeyValuePair<string, string>> requestHeaders,
            byte[] requestBody,
            HttpResponseMessage response,
            byte[] responseBody,
            FetchTiming timing,
            TlsCapture tlsCapture)
        {
            if (response == null)
            {
                return;
            }

            List<KeyValuePair<string, string>> responseHeaders = new List<KeyValuePair<string, string>>();
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
            {
                foreach (string value in header.Value)
                {
                    responseHeaders.Add(new KeyValuePair<string, string>(header.Key, value));
                }
            }

            if (response.Content != null)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
                {
                    foreach (string value in header.Value)
                    {
                        responseHeaders.Add(new KeyValuePair<string, string>(header.Key, value));
                    }
                }
            }

            (string ip, int? port) = ResolveServer(url);
            HarRecorder.RecordApiHop(this, new ApiHarHop
            {
                Method = method,
                Url = url,
                HttpVersion = "HTTP/1.1",
                RequestHeaders = requestHeaders,
                ResponseHeaders = responseHeaders,
                PostData = requestBody,
                ResponseBody = responseBody,
                Status = (int)response.StatusCode,
                StatusText = response.ReasonPhrase ?? string.Empty,
                Started = DateTimeOffset.UtcNow,
                Timing = timing?.Result,
                ServerIpAddress = ip,
                ServerPort = port,
                SecurityDetails = tlsCapture?.Details,
            });
        }

        private async Task<(HttpResponseMessage Response, string Url, byte[] Body)> SendFollowingRedirectsAsync(
            HttpClient client,
            HttpRequestMessage request,
            string originalUrl,
            string method,
            string data,
            byte[] dataBytes,
            string contentType,
            IFormData form,
            IFormData multipart,
            IReadOnlyDictionary<string, string> contextHeaders,
            IEnumerable<KeyValuePair<string, string>> headers,
            int maxRedirects,
            FetchTiming timing,
            TlsCapture tlsCapture,
            CancellationToken token)
        {
            int remaining = maxRedirects;
            HttpRequestMessage current = request;
            HttpRequestMessage owned = null;
            string currentUrl = originalUrl;
            try
            {
                while (true)
                {
                    timing.MarkStart();
                    List<KeyValuePair<string, string>> requestHeaders = SnapshotRequestHeaders(current);
                    byte[] requestBody = await SnapshotRequestBodyAsync(current).ConfigureAwait(false);
                    if (!HasHeader(requestHeaders, "content-length") && requestBody != null)
                    {
                        requestHeaders.Add(new KeyValuePair<string, string>(
                            "content-length",
                            requestBody.Length.ToString(CultureInfo.InvariantCulture)));
                    }

                    string requestUrl = current.RequestUri?.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped)
                        ?? currentUrl;
                    string requestMethod = current.Method?.Method ?? method;
                    HttpResponseMessage response;
                    try
                    {
                        // Buffer the full response in one shot. ResponseHeadersRead + a
                        // later ReadAsByteArrayAsync races HeaderCaptureStream disposal on
                        // some CI hosts and surfaces as HttpIOException ResponseEnded.
                        response = await client.SendAsync(
                            current,
                            HttpCompletionOption.ResponseContentRead,
                            token).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (IsConnectionReset(ex) || IsPrematureResponseEnd(ex))
                    {
                        if (tlsCapture != null && tlsCapture.RawHeaders.Count > 0)
                        {
                            throw new ResetAfterHeadersException(ex, MidBodyFromRawHeaders(tlsCapture));
                        }

                        throw;
                    }

                    if (_disposed || _lifetime.IsCancellationRequested)
                    {
                        response.Dispose();
                        throw DisposedException(inFlight: true);
                    }

                    if (!IsRedirect(response) || (remaining == 0 && maxRedirects == 0))
                    {
                        owned = null;
                        timing.MarkResponseStart();
                        byte[] body = await ReadBodyAsync(response, token).ConfigureAwait(false);
                        timing.MarkResponseEnd();
                        RecordApiHop(
                            requestMethod,
                            requestUrl,
                            requestHeaders,
                            requestBody,
                            response,
                            body,
                            timing,
                            tlsCapture);
                        return (response, currentUrl, body);
                    }

                    if (remaining == 0)
                    {
                        response.Dispose();
                        throw new PlaywrightNativeException("Max redirect count exceeded");
                    }

                    timing.MarkResponseStart();
                    timing.MarkResponseEnd();
                    RecordApiHop(
                        requestMethod,
                        requestUrl,
                        requestHeaders,
                        requestBody,
                        response,
                        Array.Empty<byte>(),
                        timing,
                        tlsCapture);

                    if (_context != null)
                    {
                        await IngestSetCookiesAsync(_context, currentUrl, response).ConfigureAwait(false);
                    }
                    else
                    {
                        IngestStandaloneSetCookies(currentUrl, response);
                    }

                    Uri next = ResolveRedirectUri(current.RequestUri, response);
                    if (next == null)
                    {
                        owned = null;
                        timing.MarkResponseStart();
                        return (response, currentUrl, await ReadBodyAsync(response, token).ConfigureAwait(false));
                    }

                    bool preserve = PreservesMethodOnRedirect(response.StatusCode);
                    remaining--;

                    // Do not drain the redirect body. With Connection: close, servers often
                    // omit Content-Length on empty 3xx responses; CopyToAsync then waits for
                    // EOF forever (or until the test timeout). Dispose is enough — we never
                    // reuse the connection.
                    response.Dispose();
                    owned?.Dispose();
                    currentUrl = next.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped);
                    owned = await CreateFollowRequestAsync(
                        current.RequestUri,
                        next,
                        method,
                        data,
                        dataBytes,
                        contentType,
                        form,
                        multipart,
                        contextHeaders,
                        headers,
                        preserve).ConfigureAwait(false);
                    current = owned;
                }
            }
            finally
            {
                owned?.Dispose();
            }
        }

        private async Task<HttpRequestMessage> CreateFollowRequestAsync(
            Uri from,
            Uri next,
            string method,
            string data,
            byte[] dataBytes,
            string contentType,
            IFormData form,
            IFormData multipart,
            IReadOnlyDictionary<string, string> contextHeaders,
            IEnumerable<KeyValuePair<string, string>> headers,
            bool preserveMethod)
        {
            HttpRequestMessage follow = new HttpRequestMessage(
                preserveMethod ? new HttpMethod(method) : HttpMethod.Get,
                next);
            follow.Headers.ExpectContinue = false;
            follow.Headers.ConnectionClose = true;
            if (preserveMethod && multipart != null)
            {
                HttpContent body = CreateMultipart(RequireFormData(multipart, nameof(multipart)));
                await body.LoadIntoBufferAsync().ConfigureAwait(false);
                follow.Content = body;
            }
            else if (preserveMethod && form is FormData fields && fields.ContainsFiles())
            {
                HttpContent body = CreateMultipart(fields);
                await body.LoadIntoBufferAsync().ConfigureAwait(false);
                follow.Content = body;
            }
            else if (preserveMethod && dataBytes != null)
            {
                ByteArrayContent binary = new ByteArrayContent(dataBytes);
                if (MediaTypeHeaderValue.TryParse(contentType ?? "application/octet-stream", out MediaTypeHeaderValue mediaType))
                {
                    binary.Headers.ContentType = mediaType;
                }

                follow.Content = binary;
            }
            else if (preserveMethod && data != null)
            {
                follow.Content = CreateTypedContent(data, contentType ?? "text/plain");
            }

            if (!SameOrigin(from, next))
            {
                contextHeaders = WithoutAuthorization(contextHeaders);
                headers = WithoutAuthorization(headers);
            }

            ApplyHeaders(follow, contextHeaders, overwrite: false, skipHost: true);
            ApplyHeaders(follow, headers, overwrite: true, skipHost: true);
            await ApplyCookiesAsync(follow, next.ToString()).ConfigureAwait(false);
            return follow;
        }

        private IReadOnlyList<ClientCertificate> ResolveClientCertificates()
        {
            if (_standalone?.ClientCertificates != null)
            {
                return _standalone.ClientCertificates;
            }

            return _context is IHasClientCertificates has ? has.ClientCertificates : null;
        }

        private async Task ApplyCookiesAsync(HttpRequestMessage request, string url)
        {
            if (_context == null)
            {
                ApplyStandaloneCookies(request, url);
                return;
            }

            IReadOnlyList<BrowserContextCookiesResult> cookies;
            try
            {
                cookies = await _context.GetCookiesAsync(new[] { url }).ConfigureAwait(false);
            }
            catch (Exception ex) when (_disposed || ClosedTarget.IsClosed(ex))
            {
                throw DisposedException();
            }

            if (cookies == null || cookies.Count == 0)
            {
                return;
            }

            StringBuilder builder = new StringBuilder();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (BrowserContextCookiesResult cookie in cookies)
            {
                if (cookie == null || string.IsNullOrEmpty(cookie.Name))
                {
                    continue;
                }

                string pair = cookie.Name + "=" + cookie.Value;
                if (!seen.Add(pair))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append("; ");
                }

                builder.Append(pair);
            }

            if (builder.Length > 0)
            {
                request.Headers.TryAddWithoutValidation("Cookie", builder.ToString());
            }
        }

        private void IngestStandaloneSetCookies(string url, HttpResponseMessage response)
        {
            if (_standalone == null || response == null)
            {
                return;
            }

            List<string> raw = new List<string>();
            if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string> headerValues))
            {
                raw.AddRange(headerValues);
            }

            if (response.Content?.Headers != null
                && response.Content.Headers.TryGetValues("Set-Cookie", out IEnumerable<string> contentValues))
            {
                raw.AddRange(contentValues);
            }

            if (raw.Count == 0)
            {
                return;
            }

            _standalone.Cookies ??= new List<Cookie>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string line in raw)
            {
                if (!seen.Add(line ?? string.Empty))
                {
                    continue;
                }

                Cookie cookie = ParseSetCookie(line, url);
                if (cookie == null)
                {
                    continue;
                }

                if (Uri.TryCreate(url, UriKind.Absolute, out Uri requestUri)
                    && !string.IsNullOrEmpty(cookie.Domain)
                    && !CookieDomainMatches(requestUri.Host, cookie.Domain))
                {
                    continue;
                }

                cookie.Expires ??= -1;
                cookie.HttpOnly ??= false;
                cookie.Secure ??= false;
                if (cookie.SameSite == default(Microsoft.Playwright.SameSiteAttribute?))
                {
                    cookie.SameSite = Microsoft.Playwright.SameSiteAttribute.Lax;
                }

                _standalone.Cookies.RemoveAll(existing => SameStandaloneCookie(existing, cookie));
                if (!IsExpiredSetCookie(cookie))
                {
                    _standalone.Cookies.Add(cookie);
                }
            }

            _standalone.Cookies.RemoveAll(IsExpiredSetCookie);
        }

        private void ApplyStandaloneCookies(HttpRequestMessage request, string url)
        {
            IReadOnlyList<Cookie> cookies = _standalone?.Cookies;
            if (cookies == null || cookies.Count == 0
                || !Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return;
            }

            StringBuilder builder = new StringBuilder();
            foreach (Cookie cookie in cookies)
            {
                if (!CookieMatches(cookie, uri))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append("; ");
                }

                builder.Append(cookie.Name);
                builder.Append('=');
                builder.Append(cookie.Value ?? string.Empty);
            }

            if (builder.Length > 0)
            {
                request.Headers.TryAddWithoutValidation("Cookie", builder.ToString());
            }
        }

        private void OnOwningContextClosed(object sender, IBrowserContext context)
        {
            MarkOwnerClosed();
            DisposeResponses();
        }

        private void MarkOwnerClosed()
        {
            if (_context is IHasCloseReason closed && !string.IsNullOrEmpty(closed.CloseReason))
            {
                _closeReason = closed.CloseReason;
            }

            _disposed = true;
            try
            {
                if (!_lifetime.IsCancellationRequested)
                {
#pragma warning disable VSTHRD103 // Event handler cannot await CancelAsync.
                    _lifetime.Cancel();
#pragma warning restore VSTHRD103
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void DisposeResponses()
        {
            IAPIResponse[] copy;
            lock (_responses)
            {
                copy = _responses.ToArray();
                _responses.Clear();
            }

            foreach (IAPIResponse response in copy)
            {
                _ = response.DisposeAsync().AsTask();
            }
        }

        private async Task<byte[]> ReadBodyAsync(HttpResponseMessage response, CancellationToken token)
        {
            try
            {
                byte[] body = await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
                return body;
            }
            catch (Exception) when (_disposed || _lifetime.IsCancellationRequested)
            {
                throw DisposedException();
            }
            catch (Exception ex) when (IsConnectionReset(ex) || IsPrematureResponseEnd(ex))
            {
                // Content-Length > 0 means the server started an explicit body (Node
                // writeHead + destroy → "aborted"). Abort()/destroy before a body maps
                // to "socket hang up" even when headers were observed.
                long? contentLength = response.Content?.Headers?.ContentLength;
                bool midBody = contentLength.GetValueOrDefault() > 0;
                throw new ResetAfterHeadersException(ex, midBody);
            }
        }

        private PlaywrightNativeException DisposedException(bool inFlight = false)
        {
            // Match upstream fetchRequest: closeReason || "Request context disposed."
            _ = inFlight;
            if (!string.IsNullOrEmpty(_closeReason))
            {
                return new PlaywrightNativeException(_closeReason);
            }

            return new PlaywrightNativeException("Request context disposed.");
        }

        private void EnsureNotDisposed()
        {
            if (!_disposed)
            {
                return;
            }

            throw DisposedException();
        }

        private async Task<string> ResolveUserAgentAsync()
        {
            if (!string.IsNullOrEmpty(_standalone?.UserAgent))
            {
                return _standalone.UserAgent;
            }

            if (_context is IHasUserAgent has && !string.IsNullOrEmpty(has.UserAgent))
            {
                return has.UserAgent;
            }

            if (!string.IsNullOrEmpty(_cachedUserAgent))
            {
                return _cachedUserAgent;
            }

            if (_context?.Browser is IHasDefaultUserAgent browser
                && !string.IsNullOrEmpty(browser.DefaultUserAgent))
            {
                _cachedUserAgent = browser.DefaultUserAgent;
                return _cachedUserAgent;
            }

            if (_context != null)
            {
                foreach (IPage page in _context.Pages)
                {
                    try
                    {
                        string ua = await page.EvaluateAsync<string>("(() => navigator.userAgent)()").ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(ua))
                        {
                            _cachedUserAgent = ua;
                            return ua;
                        }
                    }
                    catch (Exception ex) when (ex is PlaywrightNativeException || ClosedTarget.IsClosed(ex))
                    {
                        // Page may be closing while we read the default User-Agent.
                    }
                }
            }

            return _cachedUserAgent;
        }

        private sealed class FetchTiming
        {
            private readonly bool _https;
            private DateTimeOffset _started;

            internal FetchTiming(bool https)
            {
                _https = https;
            }

            internal RequestTimingResult Result { get; } = ResourceTimingParser.Create();

            internal void MarkStart()
            {
                _started = DateTimeOffset.UtcNow;
                Result.StartTime = _started.ToUnixTimeMilliseconds();
                Result.DomainLookupStart = 0;
                Result.DomainLookupEnd = 0;
                Result.ConnectStart = 0;
                Result.SecureConnectionStart = _https ? 0 : -1;
                Result.ConnectEnd = 0;
                Result.RequestStart = 0;
                Result.ResponseStart = -1;
                Result.ResponseEnd = -1;
            }

            internal void MarkResponseStart()
            {
                Result.ResponseStart = ElapsedMs();
            }

            internal void MarkResponseEnd()
            {
                Result.ResponseEnd = ElapsedMs();
                if (Result.ResponseStart < 0)
                {
                    Result.ResponseStart = Result.ResponseEnd;
                }
            }

            private float ElapsedMs()
            {
                double ms = (DateTimeOffset.UtcNow - _started).TotalMilliseconds;
                return ms < 0 ? 0 : (float)ms;
            }
        }

        private sealed class TlsCapture
        {
            internal ResponseSecurityDetailsResult Details { get; set; }

            internal List<NameValueEntry> RawHeaders { get; } = new List<NameValueEntry>();
        }

#pragma warning disable CA1032, CA1064, RCS1194
        private sealed class ResetAfterHeadersException : Exception
        {
            internal ResetAfterHeadersException()
            {
            }

            internal ResetAfterHeadersException(string message)
                : base(message)
            {
            }

            internal ResetAfterHeadersException(Exception inner)
                : base(inner?.Message, inner)
            {
            }

            internal ResetAfterHeadersException(Exception inner, bool midBody)
                : base(inner?.Message, inner)
            {
                MidBody = midBody;
            }

            internal ResetAfterHeadersException(string message, Exception innerException)
                : base(message, innerException)
            {
            }

            /// <summary>
            /// True when the server advertised a non-empty body (Content-Length &gt; 0)
            /// before the connection dropped — upstream reports this as <c>aborted</c>.
            /// </summary>
            internal bool MidBody { get; }
        }
#pragma warning restore CA1032, CA1064, RCS1194

        private sealed class HeaderCaptureStream : Stream
        {
            private static readonly string[] HeaderLineSeparator = { "\r\n" };

            private readonly Stream _inner;
            private readonly TlsCapture _capture;
            private readonly MemoryStream _buffer = new MemoryStream();
            private bool _captured;

            internal HeaderCaptureStream(Stream inner, TlsCapture capture)
            {
                _inner = inner;
                _capture = capture;
            }

            public override bool CanRead => _inner.CanRead;

            public override bool CanSeek => false;

            public override bool CanWrite => _inner.CanWrite;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() => _inner.Flush();

            public override Task FlushAsync(CancellationToken cancellationToken)
                => _inner.FlushAsync(cancellationToken);

            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = _inner.Read(buffer, offset, count);
                Capture(buffer, offset, read);
                return read;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                int read = await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
                Capture(buffer, offset, read);
                return read;
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                int read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read > 0)
                {
                    Capture(buffer.Span.Slice(0, read));
                }

                return read;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                ResetCapture();
                _inner.Write(buffer, offset, count);
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                ResetCapture();
                return _inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
            }

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                ResetCapture();
                return _inner.WriteAsync(buffer, cancellationToken);
            }

            public override long Seek(long offset, SeekOrigin origin)
                => throw new NotSupportedException();

            public override void SetLength(long value)
                => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _buffer.Dispose();
                    _inner.Dispose();
                }

                base.Dispose(disposing);
            }

            private static int IndexOfHeaderEnd(byte[] raw)
            {
                for (int i = 0; i + 3 < raw.Length; i++)
                {
                    if (raw[i] == (byte)'\r'
                        && raw[i + 1] == (byte)'\n'
                        && raw[i + 2] == (byte)'\r'
                        && raw[i + 3] == (byte)'\n')
                    {
                        return i;
                    }
                }

                return -1;
            }

            private void Capture(byte[] buffer, int offset, int count)
            {
                if (count > 0)
                {
                    Capture(buffer.AsSpan(offset, count));
                }
            }

            private void ResetCapture()
            {
                _captured = false;
                _buffer.SetLength(0);
                _capture.RawHeaders.Clear();
            }

            private void Capture(ReadOnlySpan<byte> data)
            {
                if (_captured || data.Length == 0)
                {
                    return;
                }

                _buffer.Write(data);
                byte[] raw = _buffer.ToArray();
                int end = IndexOfHeaderEnd(raw);
                if (end < 0)
                {
                    return;
                }

                _captured = true;
                _capture.RawHeaders.Clear();
                string text = Encoding.ASCII.GetString(raw, 0, end);
                string[] lines = text.Split(HeaderLineSeparator, StringSplitOptions.None);
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    int colon = line.IndexOf(':');
                    if (colon <= 0)
                    {
                        continue;
                    }

                    _capture.RawHeaders.Add(new NameValueEntry(
                        line.Substring(0, colon),
                        line.Substring(colon + 1).TrimStart()));
                }
            }
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1204, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648, CA1846
        private Task<IAPIResponse> FetchWithOptionsAsync(string url, string defaultMethod, APIRequestContextOptions options)
        {
            options ??= new APIRequestContextOptions();
            string method = options.Method ?? defaultMethod ?? "GET";
            string data = options.Data ?? options.DataString;
            object json = options.DataObject;
            byte[] dataBytes = options.DataByte;
            IEnumerable<KeyValuePair<string, string>> queryParams = ConvertQueryParams(options.Params, options.ParamsString);
            return FetchAsync(
                url,
                method: method,
                data: data,
                headers: options.Headers,
                failOnStatusCode: options.FailOnStatusCode,
                timeout: options.Timeout,
                maxRedirects: options.MaxRedirects,
                ignoreHTTPSErrors: options.IgnoreHTTPSErrors ?? false,
                json: json,
                form: options.Form,
                multipart: options.Multipart,
                queryParams: queryParams,
                maxRetries: options.MaxRetries ?? 0,
                dataBytes: dataBytes);
        }

        private Task<IAPIResponse> FetchRequestWithOptionsAsync(IRequest request, APIRequestContextOptions options)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            options ??= new APIRequestContextOptions();
            string method = options.Method ?? request.Method ?? "GET";
            IEnumerable<KeyValuePair<string, string>> headers = options.Headers ?? request.Headers;
            string data = options.Data ?? options.DataString;
            object json = options.DataObject;
            byte[] dataBytes = options.DataByte;
            if (data == null && json == null && dataBytes == null && options.Form == null && options.Multipart == null)
            {
                dataBytes = request.PostDataBuffer;
                if (dataBytes == null)
                {
                    data = request.PostData;
                }
            }

            IEnumerable<KeyValuePair<string, string>> queryParams = ConvertQueryParams(options.Params, options.ParamsString);
            return FetchAsync(
                request.Url,
                method: method,
                data: data,
                headers: headers,
                failOnStatusCode: options.FailOnStatusCode,
                timeout: options.Timeout,
                maxRedirects: options.MaxRedirects,
                ignoreHTTPSErrors: options.IgnoreHTTPSErrors ?? false,
                json: json,
                form: options.Form,
                multipart: options.Multipart,
                queryParams: queryParams,
                maxRetries: options.MaxRetries ?? 0,
                dataBytes: dataBytes);
        }

        private static IEnumerable<KeyValuePair<string, string>> ConvertQueryParams(
            IEnumerable<KeyValuePair<string, object>> parameters,
            string paramsString)
        {
            if (parameters == null && string.IsNullOrEmpty(paramsString))
            {
                return null;
            }

            List<KeyValuePair<string, string>> result = new List<KeyValuePair<string, string>>();
            if (parameters != null)
            {
                foreach (KeyValuePair<string, object> entry in parameters)
                {
                    if (string.IsNullOrEmpty(entry.Key))
                    {
                        continue;
                    }

                    result.Add(new KeyValuePair<string, string>(entry.Key, FormatQueryParamValue(entry.Value)));
                }
            }

            if (!string.IsNullOrEmpty(paramsString))
            {
                foreach (string part in paramsString.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    int eq = part.IndexOf('=');
                    if (eq <= 0)
                    {
                        result.Add(new KeyValuePair<string, string>(Uri.UnescapeDataString(part), string.Empty));
                        continue;
                    }

                    string key = Uri.UnescapeDataString(part[..eq]);
                    string value = Uri.UnescapeDataString(part[(eq + 1)..].Replace('+', ' '));
                    result.Add(new KeyValuePair<string, string>(key, value));
                }
            }

            return result;
        }

        private static string FormatQueryParamValue(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is bool boolean)
            {
                return boolean ? "true" : "false";
            }

            if (value is IFormattable formattable)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        IFormData IAPIRequestContext.CreateFormData() => new FormData();

        Task<IAPIResponse> IAPIRequestContext.DeleteAsync(string url, APIRequestContextOptions options) =>
            FetchWithOptionsAsync(url, "DELETE", options);

        Task<IAPIResponse> IAPIRequestContext.FetchAsync(string urlOrRequest, APIRequestContextOptions options) =>
            FetchWithOptionsAsync(urlOrRequest, options?.Method ?? "GET", options);

        Task<IAPIResponse> IAPIRequestContext.FetchAsync(IRequest urlOrRequest, APIRequestContextOptions options) =>
            FetchRequestWithOptionsAsync(urlOrRequest, options);

        Task<IAPIResponse> IAPIRequestContext.GetAsync(string url, APIRequestContextOptions options) =>
            FetchWithOptionsAsync(url, "GET", options);

        Task<IAPIResponse> IAPIRequestContext.HeadAsync(string url, APIRequestContextOptions options) =>
            FetchWithOptionsAsync(url, "HEAD", options);

        Task<IAPIResponse> IAPIRequestContext.PatchAsync(string url, APIRequestContextOptions options) =>
            FetchWithOptionsAsync(url, "PATCH", options);

        Task<IAPIResponse> IAPIRequestContext.PostAsync(string url, APIRequestContextOptions options) =>
            FetchWithOptionsAsync(url, "POST", options);

        Task<IAPIResponse> IAPIRequestContext.PutAsync(string url, APIRequestContextOptions options) =>
            FetchWithOptionsAsync(url, "PUT", options);

        Task<string> IAPIRequestContext.StorageStateAsync(APIRequestContextStorageStateOptions options) =>
            StorageStateAsync(options?.Path, options?.IndexedDB);
#pragma warning restore SA1137, SA1201, SA1202, SA1204, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648, CA1846
    }
}
