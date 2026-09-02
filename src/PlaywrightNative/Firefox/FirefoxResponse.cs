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
#pragma warning disable SA1201
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Firefox
{
    /// <summary>Public <see cref="IResponse"/> wrapping <see cref="FFResponse"/>.</summary>
    internal sealed partial class FirefoxResponse : IResponse
    {
        private readonly FFResponse _response;

        internal FirefoxResponse(FFResponse response) => _response = response;

        /// <inheritdoc/>
        public IFrame Frame => throw NotImplementedHelper.ForMethod(nameof(Frame));

        /// <inheritdoc/>
        public Dictionary<string, string> Headers
            => _response.Headers is Dictionary<string, string> dict
                ? dict
                : new Dictionary<string, string>(_response.Headers, StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public bool Ok => _response.Status >= 200 && _response.Status < 300;

        /// <inheritdoc/>
        public IRequest Request => new FirefoxRequest(_response.Request);

        /// <inheritdoc/>
        public int Status => _response.Status;

        /// <inheritdoc/>
        public HttpStatusCode StatusCode => (HttpStatusCode)_response.Status;

        /// <inheritdoc/>
        public string StatusText => _response.StatusText;

        /// <inheritdoc/>
        public string Url => _response.Url;

        /// <inheritdoc/>
        public Task<byte[]> GetBodyAsync() => throw NotImplementedHelper.ForMethod(nameof(GetBodyAsync));

        /// <inheritdoc/>
        public Task<byte[]> BodyAsync() => GetBodyAsync();

        /// <inheritdoc/>
        public Task<string> GetFinishedAsync() => throw NotImplementedHelper.ForMethod(nameof(GetFinishedAsync));

        /// <inheritdoc/>
        public Task<string> FinishedAsync() => GetFinishedAsync();

        /// <inheritdoc/>
        public Task<T> GetJsonAsync<T>() => throw NotImplementedHelper.ForMethod(nameof(GetJsonAsync));

        /// <inheritdoc/>
        public Task<T> JsonAsync<T>() => GetJsonAsync<T>();

        /// <inheritdoc/>
        public Task<JsonDocument> GetJsonAsync(JsonDocumentOptions options = default) => throw NotImplementedHelper.ForMethod(nameof(GetJsonAsync));

        /// <inheritdoc/>
        public Task<string> GetTextAsync() => throw NotImplementedHelper.ForMethod(nameof(GetTextAsync));

        /// <inheritdoc/>
        public Task<string> TextAsync() => GetTextAsync();

        /// <inheritdoc/>
        public bool FromServiceWorker => false;

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task<Dictionary<string, string>> IResponse.AllHeadersAsync() => Task.FromResult<Dictionary<string, string>>(default!);

        Task<IReadOnlyList<Header>> IResponse.HeadersArrayAsync() => Task.FromResult<IReadOnlyList<Header>>(default!);

        Task<string> IResponse.HeaderValueAsync(string name) => Task.FromResult<string>(default!);

        Task<IReadOnlyList<string>> IResponse.HeaderValuesAsync(string name) => Task.FromResult<IReadOnlyList<string>>(default!);

        Task<string> IResponse.HttpVersionAsync() => Task.FromResult<string>(default!);

        Task<JsonElement?> IResponse.JsonAsync() => Task.FromResult<JsonElement?>(default!);

        Task<ResponseSecurityDetailsResult> IResponse.SecurityDetailsAsync() => Task.FromResult<ResponseSecurityDetailsResult>(default!);

        Task<ResponseServerAddrResult> IResponse.ServerAddrAsync() => Task.FromResult<ResponseServerAddrResult>(default!);
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
