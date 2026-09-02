/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
