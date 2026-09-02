/*
 * Copyright (c) 2020 Darío Kondratiuk
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
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Firefox
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
