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
using System;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Maps official <see cref="AriaRole"/> values to Playwright role strings.
    /// </summary>
    internal static class AriaRoleExtensions
    {
        /// <summary>
        /// Returns the official lowercase ARIA role name.
        /// </summary>
        /// <param name="role">Typed ARIA role.</param>
        /// <returns>The role string, e.g. <c>button</c>.</returns>
        internal static string ToRoleString(this AriaRole role)
        {
            if (role == EnumCompat.UndefinedAriaRole)
            {
                throw new ArgumentOutOfRangeException(nameof(role));
            }

#pragma warning disable CA1308 // Official Playwright role names are lowercase ASCII.
            return role.ToString().ToLowerInvariant();
#pragma warning restore CA1308
        }
    }
}
