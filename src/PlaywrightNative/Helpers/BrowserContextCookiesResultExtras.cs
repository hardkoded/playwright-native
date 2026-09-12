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
using System.Runtime.CompilerServices;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Stores PlaywrightNative-only fields on official <see cref="BrowserContextCookiesResult"/>.
    /// </summary>
    internal static class BrowserContextCookiesResultExtras
    {
        private static readonly ConditionalWeakTable<BrowserContextCookiesResult, StrongBox<bool?>> HasCrossSiteAncestorByCookie = new();

        /// <summary>Gets <c>_crHasCrossSiteAncestor</c> when set.</summary>
        /// <param name="cookie">The cookie result instance.</param>
        /// <returns>The stored flag, or <see langword="null"/> when unset.</returns>
        internal static bool? GetHasCrossSiteAncestor(BrowserContextCookiesResult cookie)
            => cookie != null
                && HasCrossSiteAncestorByCookie.TryGetValue(cookie, out StrongBox<bool?> box)
                ? box.Value
                : null;

        /// <summary>Sets <c>_crHasCrossSiteAncestor</c>.</summary>
        /// <param name="cookie">The cookie result instance.</param>
        /// <param name="value">The Chromium partition ancestor flag.</param>
        internal static void SetHasCrossSiteAncestor(BrowserContextCookiesResult cookie, bool? value)
        {
            if (cookie == null)
            {
                return;
            }

            StrongBox<bool?> box = HasCrossSiteAncestorByCookie.GetOrCreateValue(cookie);
            box.Value = value;
        }
    }
}
