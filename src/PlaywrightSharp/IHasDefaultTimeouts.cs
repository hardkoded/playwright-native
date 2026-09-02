/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightSharp
{
    /// <summary>
    /// PlaywrightSharp pages expose default timeout getters.
    /// </summary>
    public interface IHasDefaultTimeouts
    {
        /// <summary>Default action timeout in milliseconds.</summary>
        float DefaultTimeout { get; set; }

        /// <summary>Default navigation timeout in milliseconds.</summary>
        float DefaultNavigationTimeout { get; set; }
    }
}
