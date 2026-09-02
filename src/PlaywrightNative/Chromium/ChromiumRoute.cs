/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
        Task IRoute.ContinueAsync(RouteContinueOptions options) => Task.CompletedTask;

        Task IRoute.FallbackAsync(RouteFallbackOptions options) => Task.CompletedTask;

        Task<IAPIResponse> IRoute.FetchAsync(RouteFetchOptions options) => Task.FromResult<IAPIResponse>(default!);

        Task IRoute.FulfillAsync(RouteFulfillOptions options) => Task.CompletedTask;
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
