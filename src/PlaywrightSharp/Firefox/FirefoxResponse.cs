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
#pragma warning disable SA1201
#pragma warning disable CA2000
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightSharp.Firefox
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
