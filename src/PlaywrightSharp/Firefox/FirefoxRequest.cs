/*
 * MIT License
 *
 * Copyright (c) 2020 Darío Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightSharp.Firefox
{
    /// <summary>Public <see cref="IRequest"/> wrapping <see cref="FFRequest"/>.</summary>
    internal sealed partial class FirefoxRequest : IRequest
    {
        private readonly FFRequest _request;

        internal FirefoxRequest(FFRequest request) => _request = request;

        /// <inheritdoc/>
        public string Failure => _request.FailureText;

        /// <inheritdoc/>
        public IFrame Frame => throw NotImplementedHelper.ForMethod(nameof(Frame));

        /// <inheritdoc/>
        public Dictionary<string, string> Headers
            => _request.Headers is Dictionary<string, string> dict
                ? dict
                : new Dictionary<string, string>(_request.Headers, StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public bool IsNavigationRequest => false;

        /// <inheritdoc/>
        public string Method => _request.Method;

        /// <inheritdoc/>
        public string PostData => _request.PostData;

        /// <inheritdoc/>
        public byte[] PostDataBuffer
            => _request.PostData == null ? null : Encoding.UTF8.GetBytes(_request.PostData);

        /// <inheritdoc/>
        public IRequest RedirectedFrom => null;

        /// <inheritdoc/>
        public IRequest RedirectedTo => null;

        /// <inheritdoc/>
        public string ResourceType => _request.ResourceType;

        /// <summary>Gets the associated response, if received.</summary>
        public IResponse Response => _request.Response != null ? new FirefoxResponse(_request.Response) : null;

        /// <inheritdoc/>
        public IResponse ExistingResponse => Response;

        /// <inheritdoc/>
        public RequestTimingResult Timing => null;

        /// <inheritdoc/>
        public string Url => _request.Url;

        /// <inheritdoc/>
        public Task<IResponse> GetResponseAsync() => throw NotImplementedHelper.ForMethod(nameof(GetResponseAsync));

        /// <inheritdoc/>
        public Task<IResponse> ResponseAsync() => GetResponseAsync();

        /// <inheritdoc/>
        public Task<RequestSizesResult> GetSizesAsync()
            => throw NotImplementedHelper.ForMethod(nameof(GetSizesAsync));

        /// <inheritdoc/>
        public Task<RequestSizesResult> SizesAsync() => GetSizesAsync();

        /// <inheritdoc/>
        public JsonDocument GetPayloadAsJson(JsonDocumentOptions documentOptions = default)
        {
            if (string.IsNullOrEmpty(_request.PostData))
            {
                return null;
            }

            return JsonDocument.Parse(_request.PostData, documentOptions);
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task<Dictionary<string, string>> IRequest.AllHeadersAsync() => Task.FromResult<Dictionary<string, string>>(default!);

        Task<IReadOnlyList<Header>> IRequest.HeadersArrayAsync() => Task.FromResult<IReadOnlyList<Header>>(default!);

        Task<string> IRequest.HeaderValueAsync(string name) => Task.FromResult<string>(default!);

        JsonElement? IRequest.PostDataJSON() => default!;
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
