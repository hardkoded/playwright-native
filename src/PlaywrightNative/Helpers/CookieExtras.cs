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
