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
#pragma warning disable CA1062
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy helpers over official <see cref="IRequest"/>.
    /// </summary>
    public static class RequestCompatExtensions
    {
        /// <summary>Legacy alias for <see cref="IRequest.ResponseAsync"/>.</summary>
        public static Task<IResponse> GetResponseAsync(this IRequest request)
            => request.ResponseAsync();

        /// <summary>Legacy alias for <see cref="IRequest.SizesAsync"/>.</summary>
        public static Task<RequestSizesResult> GetSizesAsync(this IRequest request)
            => request.SizesAsync();

        /// <summary>Reads post data as a <see cref="System.Text.Json.JsonDocument"/>.</summary>
        public static System.Text.Json.JsonDocument GetPayloadAsJson(this IRequest request)
        {
            switch (request)
            {
                case Chromium.ChromiumRequest chromium:
                    return chromium.GetPayloadAsJson();
                case Firefox.FirefoxRequest firefox:
                    return firefox.GetPayloadAsJson();
                case WebKit.WKRequest webkit:
                    return webkit.GetPayloadAsJson();
                default:
                    System.Text.Json.JsonElement? element = request.PostDataJSON();
                    if (element == null)
                    {
                        return null;
                    }

                    return System.Text.Json.JsonDocument.Parse(element.Value.GetRawText());
            }
        }
    }
}
