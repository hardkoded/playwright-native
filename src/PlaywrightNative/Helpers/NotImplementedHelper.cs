/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;

namespace PlaywrightNative
{
    /// <summary>
    /// Throws a consistent <see cref="NotImplementedException"/> for methods outside
    /// the retained PlaywrightNative surface.
    /// </summary>
    internal static class NotImplementedHelper
    {
        /// <summary>
        /// Returns an exception for the named method.
        /// </summary>
        /// <param name="methodName">The method name (use <c>nameof(...)</c>).</param>
        /// <returns>A ready-to-throw <see cref="NotImplementedException"/>.</returns>
        internal static NotImplementedException ForMethod(string methodName)
        {
            return new NotImplementedException(
                $"{methodName} is not part of the retained PlaywrightNative surface.");
        }
    }
}
