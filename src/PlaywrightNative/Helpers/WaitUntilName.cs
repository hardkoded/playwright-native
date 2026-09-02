/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>goto({ waitUntil })</c> string names.
    /// </summary>
    internal static class WaitUntilName
    {
        /// <summary>
        /// Parses an official waitUntil name or throws the official error.
        /// </summary>
        /// <param name="waitUntil">The official string waitUntil.</param>
        /// <returns>The matching <see cref="WaitUntilState"/>.</returns>
        internal static WaitUntilState Parse(string waitUntil)
        {
            if (string.Equals(waitUntil, "load", StringComparison.OrdinalIgnoreCase))
            {
                return WaitUntilState.Load;
            }

            if (string.Equals(waitUntil, "domcontentloaded", StringComparison.OrdinalIgnoreCase))
            {
                return WaitUntilState.DOMContentLoaded;
            }

            if (string.Equals(waitUntil, "networkidle", StringComparison.OrdinalIgnoreCase))
            {
                return WaitUntilState.NetworkIdle;
            }

            if (string.Equals(waitUntil, "commit", StringComparison.OrdinalIgnoreCase))
            {
                return WaitUntilState.Commit;
            }

            throw new PlaywrightNativeException("waitUntil: expected one of (load|domcontentloaded|networkidle|commit)");
        }
    }
}
