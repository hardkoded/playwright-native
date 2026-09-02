/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;

namespace PlaywrightNative.Input
{
    /// <summary>
    /// Helpers for building CDP modifier bitmasks from a set of <see cref="KeyboardModifier"/> values.
    /// </summary>
    internal static class KeyboardModifierExtensions
    {
        /// <summary>
        /// Combines an enumerable of modifiers into a single bitmask value.
        /// </summary>
        /// <param name="modifiers">The modifiers to combine.</param>
        /// <returns>The bitwise OR of the modifier flags.</returns>
        internal static int ToCdpMask(this IEnumerable<KeyboardModifier> modifiers)
        {
            int mask = 0;
            if (modifiers != null)
            {
                foreach (KeyboardModifier modifier in modifiers)
                {
                    mask |= (int)modifier;
                }
            }

            return mask;
        }
    }
}
