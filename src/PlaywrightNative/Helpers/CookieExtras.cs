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
    /// Stores PlaywrightNative-only cookie fields not present on official <see cref="Cookie"/>.
    /// </summary>
    internal static class CookieExtras
    {
        private static readonly ConcurrentDictionary<int, bool?> HasCrossSiteAncestorByHash = new();

        /// <summary>Gets <c>_crHasCrossSiteAncestor</c> when set.</summary>
        internal static bool? GetHasCrossSiteAncestor(Cookie cookie)
            => cookie != null && HasCrossSiteAncestorByHash.TryGetValue(cookie.GetHashCode(), out bool? value)
                ? value
                : null;

        /// <summary>Sets <c>_crHasCrossSiteAncestor</c>.</summary>
        internal static void SetHasCrossSiteAncestor(Cookie cookie, bool? value)
        {
            if (cookie == null)
            {
                return;
            }

            HasCrossSiteAncestorByHash[cookie.GetHashCode()] = value;
        }
    }
}
