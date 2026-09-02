/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Launch-level <c>proxy</c> from <c>browserType.launch</c>. Official
    /// <c>APIRequestContext.fetch</c> uses
    /// <c>context._options.proxy || browser.options.proxy</c>.
    /// </summary>
    internal interface IHasLaunchProxy
    {
        /// <summary>
        /// Proxy from <c>LaunchAsync(proxy)</c>, or <see langword="null"/>.
        /// </summary>
        Proxy LaunchProxy { get; }
    }
}
