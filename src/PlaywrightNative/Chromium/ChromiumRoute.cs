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
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Chromium
{
    /// <summary>Public <see cref="IRoute"/> wrapping <see cref="CRRoute"/>.</summary>
    internal sealed partial class ChromiumRoute : IRoute
    {
        private readonly CRRoute _crRoute;
        private readonly ChromiumRequest _request;

        internal ChromiumRoute(CRRoute crRoute, Func<CRRequest, ChromiumRequest> resolveRequest)
        {
            _crRoute = crRoute ?? throw new ArgumentNullException(nameof(crRoute));
            if (resolveRequest == null)
            {
                throw new ArgumentNullException(nameof(resolveRequest));
            }

            _request = resolveRequest(crRoute.Request);
        }

        /// <inheritdoc/>
        public IRequest Request => _request;

        /// <inheritdoc/>
        public Task AbortAsync(string errorCode = default)
            => _crRoute.AbortAsync(errorCode ?? "Failed");

        /// <inheritdoc/>
        public Task FallbackAsync(
            IEnumerable<KeyValuePair<string, string>> headers = default,
            string method = default,
            byte[] postData = default,
            string url = default,
            string postDataText = default,
            object postDataJson = default)
        {
            byte[] body = postDataJson != null
                ? Encoding.UTF8.GetBytes(JsonSerializer.Serialize(postDataJson, postDataJson.GetType(), JsonExtensions.DefaultJsonSerializerOptions))
                : postDataText != null ? Encoding.UTF8.GetBytes(postDataText) : postData;
            IDictionary<string, string> headerDict = ToDictionary(headers);
            return _crRoute.FallbackAsync(url: url, method: method, headers: headerDict, postDataBytes: body);
        }

        /// <inheritdoc/>
        public Task ResumeAsync(
            IEnumerable<KeyValuePair<string, string>> headers = default,
            string method = default,
            byte[] postData = default,
            string url = default,
            string postDataText = default)
        {
            byte[] body = postDataText != null ? Encoding.UTF8.GetBytes(postDataText) : postData;
            IDictionary<string, string> headerDict = ToDictionary(headers);
            IBrowserContext context = _request.Frame?.Page?.Context;
            return ActionTrace.RunAsync(
                context,
                "Continue request",
                "Route",
                "continue",
                () => _crRoute.ContinueAsync(url: url, method: method, headers: headerDict, postDataBytes: body));
        }

        /// <inheritdoc/>
        public Task FulfillAsync(
            string body = default,
            byte[] bodyBytes = default,
            string contentType = default,
            IEnumerable<KeyValuePair<string, string>> headers = default,
            string path = default,
            int? status = default,
            object json = default)
        {
            if (json != null)
            {
                body = JsonSerializer.Serialize(json, json.GetType(), JsonExtensions.DefaultJsonSerializerOptions);
                contentType ??= "application/json";
            }

            if (!string.IsNullOrEmpty(path))
            {
                bodyBytes = PathIo.ReadBytes(path);
                contentType = FilePayloadHelper.MimeTypeFromPath(path);
            }

            IDictionary<string, string> headerDict = RouteFulfill.ToHeaderMap(headers);
            return _crRoute.FulfillAsync(
                statusCode: status ?? 200,
                body: body,
                bodyBytes: bodyBytes,
                contentType: contentType,
                headers: headerDict);
        }

        /// <inheritdoc/>
        public Task FulfillAsync(
            HttpStatusCode status,
            string body = default,
            byte[] bodyBytes = default,
            string contentType = default,
            IEnumerable<KeyValuePair<string, string>> headers = default,
            string path = default,
            object json = default)
            => FulfillAsync(
                body: body,
                bodyBytes: bodyBytes,
                contentType: contentType,
                headers: headers,
                path: path,
                status: (int)status,
                json: json);

        /// <summary>
        /// Converts a sequence of key/value pairs to a case-insensitive dictionary.
        /// Duplicated keys collapse to the last-seen value — the dictionary API
        /// can't carry duplicates, and last-wins matches how most callers expect
        /// header overrides to behave.
        /// </summary>
        private static IDictionary<string, string> ToDictionary(IEnumerable<KeyValuePair<string, string>> headers)
        {
            if (headers == null)
            {
                return null;
            }

            Dictionary<string, string> dict = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> kvp in headers)
            {
                dict[kvp.Key] = kvp.Value;
            }

            return dict;
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task IRoute.ContinueAsync(RouteContinueOptions options)
            => ResumeAsync(
                headers: options?.Headers,
                method: options?.Method,
                postData: options?.PostData,
                url: options?.Url);

        Task IRoute.FallbackAsync(RouteFallbackOptions options)
            => FallbackAsync(
                headers: options?.Headers,
                method: options?.Method,
                postData: options?.PostData,
                url: options?.Url);

        async Task<IAPIResponse> IRoute.FetchAsync(RouteFetchOptions options)
        {
            RouteFetchResult fetched = await RouteFetch.FetchAsync(
                Request,
                url: options?.Url,
                method: options?.Method,
                headers: options?.Headers,
                postData: options?.PostData,
                timeout: options?.Timeout.HasValue == true ? (int?)options.Timeout.Value : null,
                maxRedirects: options?.MaxRedirects,
                maxRetries: options?.MaxRetries ?? 0).ConfigureAwait(false);
            return new APIResponse(
                fetched.Status,
                fetched.StatusText,
                fetched.Url,
                fetched.Headers,
                fetched.Body);
        }

        Task IRoute.FulfillAsync(RouteFulfillOptions options)
            => FulfillFromOptionsAsync(options);

        private async Task FulfillFromOptionsAsync(RouteFulfillOptions options)
        {
            options ??= new RouteFulfillOptions();
            string body = options.Body;
            byte[] bodyBytes = options.BodyBytes;
            string contentType = options.ContentType;
            IEnumerable<KeyValuePair<string, string>> headers = options.Headers;
            int? status = options.Status;

            if (options.Response != null)
            {
                status ??= options.Response.Status;
                headers ??= options.Response.Headers;
                if (body == null && bodyBytes == null && options.Path == null && options.Json == null)
                {
                    bodyBytes = await options.Response.BodyAsync().ConfigureAwait(false);
                }

                if (contentType == null
                    && options.Response.Headers != null
                    && options.Response.Headers.TryGetValue("content-type", out string responseType))
                {
                    contentType = responseType;
                }
            }

            await FulfillAsync(
                body: body,
                bodyBytes: bodyBytes,
                contentType: contentType,
                headers: headers,
                path: options.Path,
                status: status,
                json: options.Json).ConfigureAwait(false);
        }
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
