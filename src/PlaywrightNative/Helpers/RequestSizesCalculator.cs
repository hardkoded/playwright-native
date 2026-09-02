/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Builds <see cref="RequestSizesResult"/> from request/response metadata
    /// plus <c>Network.loadingFinished.encodedDataLength</c>.
    /// </summary>
    internal static class RequestSizesCalculator
    {
        /// <summary>
        /// Computes sizes for a finished request.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="url">Request URL.</param>
        /// <param name="requestHeaders">Request headers.</param>
        /// <param name="postData">Request body, if any.</param>
        /// <param name="status">Response status, or <see langword="null"/> when none.</param>
        /// <param name="statusText">Response status text.</param>
        /// <param name="responseHeaders">Response headers, or <see langword="null"/>.</param>
        /// <param name="encodedDataLength">Encoded length from loadingFinished.</param>
        /// <param name="encodedDataLengthIncludesHeaders">
        /// When <see langword="true"/>, <paramref name="encodedDataLength"/> is Chromium
        /// transfer size (headers + body) and the body is derived by subtracting
        /// the computed response headers size. WebKit metrics already report the body.
        /// </param>
        /// <returns>The computed sizes.</returns>
        internal static RequestSizesResult Compute(
            string method,
            string url,
            IEnumerable<KeyValuePair<string, string>> requestHeaders,
            string postData,
            int? status,
            string statusText,
            IEnumerable<KeyValuePair<string, string>> responseHeaders,
            int encodedDataLength,
            bool encodedDataLengthIncludesHeaders = false)
        {
            int requestBodySize = string.IsNullOrEmpty(postData)
                ? 0
                : Encoding.UTF8.GetByteCount(postData);

            int responseHeadersSize = status.HasValue
                ? ResponseHeadersByteCount(status.Value, statusText, responseHeaders)
                : 0;

            return new RequestSizesResult
            {
                RequestBodySize = requestBodySize,
                RequestHeadersSize = RequestHeadersByteCount(method, url, requestHeaders),
                ResponseBodySize = ResolveResponseBodySize(
                    responseHeaders,
                    encodedDataLength,
                    encodedDataLengthIncludesHeaders,
                    responseHeadersSize),
                ResponseHeadersSize = responseHeadersSize,
            };
        }

        private static int ResolveResponseBodySize(
            IEnumerable<KeyValuePair<string, string>> responseHeaders,
            int encodedDataLength,
            bool encodedDataLengthIncludesHeaders,
            int responseHeadersSize)
        {
            int? contentLength = ReadContentLength(responseHeaders);
            bool chunked = HasChunkedEncoding(responseHeaders);
            bool gzip = HasGzipEncoding(responseHeaders);

            if (gzip && encodedDataLength > 0)
            {
                return encodedDataLength;
            }

            if (!chunked && contentLength.HasValue)
            {
                return contentLength.Value;
            }

            if (encodedDataLength < 0)
            {
                return 0;
            }

            if (encodedDataLengthIncludesHeaders)
            {
                int subtracted = encodedDataLength - responseHeadersSize;
                return subtracted >= 0 ? subtracted : 0;
            }

            return encodedDataLength;
        }

        private static int? ReadContentLength(IEnumerable<KeyValuePair<string, string>> headers)
        {
            if (headers == null)
            {
                return null;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (!string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (int.TryParse(header.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int length)
                    && length >= 0)
                {
                    return length;
                }
            }

            return null;
        }

        private static bool HasChunkedEncoding(IEnumerable<KeyValuePair<string, string>> headers)
        {
            if (headers == null)
            {
                return false;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.Equals(header.Key, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(header.Value)
                    && header.Value.Contains("chunked", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasGzipEncoding(IEnumerable<KeyValuePair<string, string>> headers)
        {
            if (headers == null)
            {
                return false;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.Equals(header.Key, "Content-Encoding", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(header.Value)
                    && header.Value.Contains("gzip", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static int RequestHeadersByteCount(
            string method,
            string url,
            IEnumerable<KeyValuePair<string, string>> headers)
        {
            string path = PathAndQuery(url);
            string verb = string.IsNullOrEmpty(method) ? "GET" : method;
            int size = Encoding.UTF8.GetByteCount(verb + " " + path + " HTTP/1.1\r\n");
            size += HeadersByteCount(headers);
            size += 2;
            return size;
        }

        private static int ResponseHeadersByteCount(
            int status,
            string statusText,
            IEnumerable<KeyValuePair<string, string>> headers)
        {
            string text = string.IsNullOrEmpty(statusText) ? "OK" : statusText;
            int size = Encoding.UTF8.GetByteCount("HTTP/1.1 " + status.ToString(CultureInfo.InvariantCulture) + " " + text + "\r\n");
            size += HeadersByteCount(headers);
            size += 2;
            return size;
        }

        private static int HeadersByteCount(IEnumerable<KeyValuePair<string, string>> headers)
        {
            if (headers == null)
            {
                return 0;
            }

            int size = 0;
            foreach (KeyValuePair<string, string> header in headers)
            {
                string name = header.Key ?? string.Empty;
                string value = header.Value ?? string.Empty;
                size += Encoding.UTF8.GetByteCount(name + ": " + value + "\r\n");
            }

            return size;
        }

        private static string PathAndQuery(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return "/";
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return url;
            }

            return string.IsNullOrEmpty(uri.Query) ? uri.AbsolutePath : uri.AbsolutePath + uri.Query;
        }
    }
}
