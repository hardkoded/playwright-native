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
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Stores the official <c>signCount</c> field, which is not present on
    /// <see cref="VirtualCredential"/>.
    /// </summary>
    internal static class VirtualCredentialExtras
    {
        private static readonly ConditionalWeakTable<VirtualCredential, StrongBox<long>> SignCountByCredential = new();

        /// <summary>Gets the signature counter.</summary>
        /// <param name="credential">The credential instance.</param>
        /// <returns>The stored counter, or 0 when unset.</returns>
        internal static long GetSignCount(VirtualCredential credential)
            => credential != null
                && SignCountByCredential.TryGetValue(credential, out StrongBox<long> box)
                ? box.Value
                : 0;

        /// <summary>Sets the signature counter.</summary>
        /// <param name="credential">The credential instance.</param>
        /// <param name="value">The counter value.</param>
        internal static void SetSignCount(VirtualCredential credential, long value)
            => SignCountByCredential.AddOrUpdate(credential, new StrongBox<long>(value));
    }
}
