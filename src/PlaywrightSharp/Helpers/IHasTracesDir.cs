/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Launch-level official <c>tracesDir</c>.
    /// </summary>
    internal interface IHasTracesDir
    {
        /// <summary>Directory for incremental <c>{name}.trace</c> files.</summary>
        string TracesDir { get; set; }
    }
}
