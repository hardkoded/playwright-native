/*
 * Copyright (c) Microsoft Corporation.
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
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Preserves certificate validity timestamps as Unix seconds without float32
    /// rounding. <see cref="ResponseSecurityDetailsResult.ValidFrom"/> is
    /// <see cref="float"/> and cannot hold all Unix timestamps exactly.
    /// </summary>
    internal static class SecurityDetailsUnix
    {
        private static readonly ConditionalWeakTable<ResponseSecurityDetailsResult, Times> Table =
            new ConditionalWeakTable<ResponseSecurityDetailsResult, Times>();

        /// <summary>
        /// Remembers exact Unix seconds for <paramref name="details"/>.
        /// </summary>
        /// <param name="details">The public security-details object.</param>
        /// <param name="validFrom">Certificate not-before, Unix seconds.</param>
        /// <param name="validTo">Certificate not-after, Unix seconds.</param>
        internal static void Attach(ResponseSecurityDetailsResult details, long validFrom, long validTo)
        {
            if (details == null)
            {
                return;
            }

            Times times = Table.GetValue(details, static _ => new Times());
            times.ValidFrom = validFrom;
            times.ValidTo = validTo;
        }

        /// <summary>
        /// Tries to read exact Unix seconds previously attached to <paramref name="details"/>.
        /// </summary>
        /// <param name="details">The public security-details object.</param>
        /// <param name="validFrom">Receives not-before when found.</param>
        /// <param name="validTo">Receives not-after when found.</param>
        /// <returns><see langword="true"/> when exact timestamps are available.</returns>
        internal static bool TryGet(ResponseSecurityDetailsResult details, out long validFrom, out long validTo)
        {
            validFrom = 0;
            validTo = 0;
            if (details == null || !Table.TryGetValue(details, out Times times))
            {
                return false;
            }

            validFrom = times.ValidFrom;
            validTo = times.ValidTo;
            return true;
        }

        private sealed class Times
        {
            internal long ValidFrom { get; set; }

            internal long ValidTo { get; set; }
        }
    }
}
