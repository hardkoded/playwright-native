/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
#pragma warning disable CA1062
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightSharp
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
