/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
#pragma warning disable CA1062
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightSharp.Chromium;
using PlaywrightSharp.WebKit;

namespace PlaywrightSharp
{
    /// <summary>
    /// Legacy route helpers missing from official <see cref="IRoute"/>.
    /// </summary>
    public static class RouteCompatExtensions
    {
        /// <summary>Fulfill from a prior <see cref="RouteFetchResult"/>.</summary>
        public static Task FulfillAsync(this IRoute route, RouteFetchResult fetched)
        {
            if (fetched == null)
            {
                throw new System.ArgumentNullException(nameof(fetched));
            }

            return route.FulfillAsync(new RouteFulfillOptions
            {
                Status = fetched.Status,
                Headers = fetched.Headers,
                BodyBytes = fetched.Body,
            });
        }

        /// <summary>Fulfill from an API response.</summary>
        public static Task FulfillAsync(this IRoute route, IAPIResponse response)
            => route.FulfillAsync(new RouteFulfillOptions { Response = response });

        /// <summary>Fulfill from an API response with optional overrides.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task FulfillAsync(
            this IRoute route,
            IAPIResponse response,
            int? status = default,
            IEnumerable<KeyValuePair<string, string>> headers = default,
            string contentType = default,
            string body = default,
            byte[] bodyBytes = default,
            string path = default)
            => route.FulfillAsync(new RouteFulfillOptions
            {
                Response = response,
                Status = status,
                Headers = headers,
                ContentType = contentType,
                Body = body,
                BodyBytes = bodyBytes,
                Path = path,
            });

        /// <summary>
        /// PlaywrightSharp fetch that returns <see cref="RouteFetchResult"/> (official
        /// <see cref="IRoute.FetchAsync"/> returns <see cref="IAPIResponse"/>).
        /// </summary>
        public static Task<RouteFetchResult> FetchResultAsync(this IRoute route, RouteFetchOptions options = default)
        {
            if (route == null)
            {
                throw new System.ArgumentNullException(nameof(route));
            }

            return Helpers.RouteFetch.FetchAsync(
                route.Request,
                url: options?.Url,
                method: options?.Method,
                headers: options?.Headers,
                postData: options?.PostData,
                timeout: options?.Timeout.HasValue == true ? (int?)options.Timeout.Value : null,
                maxRedirects: options?.MaxRedirects,
                maxRetries: options?.MaxRetries ?? 0);
        }

        /// <summary>Parameterless PlaywrightSharp fetch result helper.</summary>
        public static Task<RouteFetchResult> FetchResultAsync(this IRoute route)
            => route.FetchResultAsync(null);

        /// <summary>Legacy fulfill with HTTP status enum.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task FulfillAsync(
            this IRoute route,
            System.Net.HttpStatusCode status,
            string body = default,
            byte[] bodyBytes = default,
            IEnumerable<KeyValuePair<string, string>> headers = default,
            object json = default,
            string contentType = default,
            string path = default)
            => route.FulfillAsync((int)status, body, bodyBytes, headers, json, contentType, path);

        /// <summary>Legacy fulfill with body/headers/json named parameters.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task FulfillAsync(
            this IRoute route,
            int? status = default,
            string body = default,
            byte[] bodyBytes = default,
            IEnumerable<KeyValuePair<string, string>> headers = default,
            object json = default,
            string contentType = default,
            string path = default)
            => route.FulfillAsync(new RouteFulfillOptions
            {
                Status = status,
                Body = body,
                BodyBytes = bodyBytes,
                Headers = headers,
                Json = json,
                ContentType = contentType,
                Path = path,
            });

        /// <summary>Legacy fulfill from fetch result with body override.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task FulfillAsync(this IRoute route, RouteFetchResult fetched, string body)
            => route.FulfillAsync(new RouteFulfillOptions
            {
                Status = fetched?.Status,
                Headers = fetched?.Headers,
                BodyBytes = fetched?.Body,
                Body = body,
            });

        /// <summary>Legacy resume/continue spelling.</summary>
        public static Task ResumeAsync(
            this IRoute route,
            IEnumerable<KeyValuePair<string, string>> headers = default,
            string method = default,
            byte[] postData = default,
            string url = default,
            string postDataText = default)
        {
            switch (route)
            {
                case ChromiumRoute chromium:
                    return chromium.ResumeAsync(headers, method, postData, url, postDataText);
                case WebKitRoute webkit:
                    return webkit.ResumeAsync(headers, method, postData, url, postDataText);
                default:
                    return route.ContinueAsync(new RouteContinueOptions
                    {
                        Headers = headers,
                        Method = method,
                        PostData = postData,
                        Url = url,
                    });
            }
        }

        /// <summary>Legacy continue with expanded parameters.</summary>
        public static Task ContinueAsync(
            this IRoute route,
            IEnumerable<KeyValuePair<string, string>> headers = default,
            string method = default,
            byte[] postData = default,
            string url = default)
            => route.ContinueAsync(new RouteContinueOptions
            {
                Headers = headers,
                Method = method,
                PostData = postData,
                Url = url,
            });

        /// <summary>Legacy fallback with JSON post data.</summary>
        public static Task FallbackAsync(
            this IRoute route,
            IEnumerable<KeyValuePair<string, string>> headers = default,
            string method = default,
            byte[] postData = default,
            string url = default,
            object postDataJson = default)
        {
            switch (route)
            {
                case ChromiumRoute chromium:
                    return chromium.FallbackAsync(headers, method, postData, url, postDataText: null, postDataJson: postDataJson);
                case WebKitRoute webkit:
                    return webkit.FallbackAsync(headers, method, postData, url, postDataText: null, postDataJson: postDataJson);
                default:
                    return route.FallbackAsync(new RouteFallbackOptions
                    {
                        Headers = headers,
                        Method = method,
                        PostData = postData,
                        Url = url,
                    });
            }
        }

        /// <summary>Legacy fetch with method parameter.</summary>
        public static Task<IAPIResponse> FetchAsync(
            this IRoute route,
            string method = default,
            IEnumerable<KeyValuePair<string, string>> headers = default,
            byte[] postData = default,
            string url = default,
            float? timeout = default,
            int? maxRedirects = default)
            => route.FetchAsync(new RouteFetchOptions
            {
                Method = method,
                Headers = headers,
                PostData = postData,
                Url = url,
                Timeout = timeout,
                MaxRedirects = maxRedirects,
            });
    }
}
