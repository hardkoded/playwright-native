/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
 */
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Contexts and pages that track official <c>exposeFunction</c> names
    /// for duplicate-registration errors.
    /// </summary>
    internal interface IHasExposedFunctionNames
    {
        /// <summary>
        /// Returns whether <paramref name="name"/> is already exposed.
        /// </summary>
        /// <param name="name">The JS global name.</param>
        /// <returns>True when the name is registered.</returns>
        bool HasExposedFunction(string name);
    }
}
