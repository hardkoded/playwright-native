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
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Chromium
{
    /// <summary>Public <see cref="IResponse"/> wrapping <see cref="CRResponse"/>.</summary>
    internal sealed partial class ChromiumResponse : IResponse
    {
        private readonly CRResponse _crResponse;
        private readonly Func<CRRequest, ChromiumRequest> _resolveRequest;

        internal ChromiumResponse(CRResponse crResponse, Func<CRRequest, ChromiumRequest> resolveRequest)
        {
            _crResponse = crResponse ?? throw new ArgumentNullException(nameof(crResponse));
            _resolveRequest = resolveRequest ?? throw new ArgumentNullException(nameof(resolveRequest));
        }

        /// <inheritdoc/>
        public IFrame Frame => Request?.Frame;

        /// <inheritdoc/>
        public Dictionary<string, string> Headers => HeaderMap.All(_crResponse.HeaderPairs);

        /// <inheritdoc/>
        public bool Ok => _crResponse.Ok;

        /// <inheritdoc/>
        public IRequest Request => _resolveRequest(_crResponse.Request);

        /// <inheritdoc/>
        public int Status => _crResponse.Status;

        /// <inheritdoc/>
        public string StatusText => _crResponse.StatusText;

        /// <inheritdoc/>
        public string Url => _crResponse.Request.Url;

        /// <inheritdoc/>
        public bool FromServiceWorker => _crResponse.FromServiceWorker;

        /// <inheritdoc/>
        public HttpStatusCode StatusCode => (HttpStatusCode)_crResponse.Status;

        /// <inheritdoc/>
        public Task<byte[]> BodyAsync()
            => _crResponse.GetBodyBytesAsync();

        /// <inheritdoc/>
        public Task<string> FinishedAsync()
            => _crResponse.Request.WaitUntilFinishedAsync();

        /// <inheritdoc/>
        public Task<T> JsonAsync<T>()
            => ResponseContent.ReadJsonAsync<T>(_crResponse.GetBodyBytesAsync);

        /// <inheritdoc/>
        public Task<string> TextAsync()
            => ResponseContent.ReadTextAsync(_crResponse.GetBodyBytesAsync);

        /// <inheritdoc/>
        public async Task<Dictionary<string, string>> AllHeadersAsync()
        {
            await _crResponse.WaitForExtraHeadersAsync().ConfigureAwait(false);
            return HeaderMap.All(_crResponse.HeaderPairs);
        }

        /// <inheritdoc/>
        public async Task<string> HeaderValueAsync(string name)
        {
            await _crResponse.WaitForExtraHeadersAsync().ConfigureAwait(false);
            return HeaderMap.Value(_crResponse.HeaderPairs, name);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> HeaderValuesAsync(string name)
        {
            await _crResponse.WaitForExtraHeadersAsync().ConfigureAwait(false);
            return HeaderMap.Values(_crResponse.HeaderPairs, name);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<Header>> HeadersArrayAsync()
        {
            await _crResponse.WaitForExtraHeadersAsync().ConfigureAwait(false);
            return EquatableHeader.FromEntries(HeaderMap.Array(_crResponse.HeaderPairs));
        }

        /// <inheritdoc/>
        public Task<string> HttpVersionAsync()
            => Task.FromResult(ResponseNetworkInfo.NormalizeHttpVersion(_crResponse.HttpVersion));

        public Task<ResponseServerAddrResult> ServerAddrAsync()
            => Task.FromResult(_crResponse.ServerAddr);

        /// <inheritdoc/>
        public Task<ResponseSecurityDetailsResult> SecurityDetailsAsync()
            => Task.FromResult(_crResponse.SecurityDetails);

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task<JsonElement?> IResponse.JsonAsync() => Task.FromResult<JsonElement?>(default!);
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
