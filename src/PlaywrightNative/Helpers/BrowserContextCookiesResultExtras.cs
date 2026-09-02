/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Concurrent;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Stores PlaywrightNative-only fields on official <see cref="BrowserContextCookiesResult"/>.
    /// </summary>
    internal static class BrowserContextCookiesResultExtras
    {
        private static readonly ConcurrentDictionary<int, bool?> HasCrossSiteAncestorByHash = new();

        /// <summary>Gets <c>_crHasCrossSiteAncestor</c> when set.</summary>
        internal static bool? GetHasCrossSiteAncestor(BrowserContextCookiesResult cookie)
            => cookie != null && HasCrossSiteAncestorByHash.TryGetValue(cookie.GetHashCode(), out bool? value)
                ? value
                : null;

        /// <summary>Sets <c>_crHasCrossSiteAncestor</c>.</summary>
        internal static void SetHasCrossSiteAncestor(BrowserContextCookiesResult cookie, bool? value)
        {
            if (cookie == null)
            {
                return;
            }

            HasCrossSiteAncestorByHash[cookie.GetHashCode()] = value;
        }
    }
}
