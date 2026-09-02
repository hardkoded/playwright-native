/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
#pragma warning disable CA1062
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy helpers over official <see cref="IResponse"/>.
    /// </summary>
    public static class ResponseCompatExtensions
    {
        /// <summary>Legacy alias for <see cref="IResponse.BodyAsync"/>.</summary>
        public static Task<byte[]> GetBodyAsync(this IResponse response)
            => response.BodyAsync();
    }
}
