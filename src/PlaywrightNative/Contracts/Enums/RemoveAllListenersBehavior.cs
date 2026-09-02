/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightNative
{
    /// <summary>
    /// How <see cref="IPage.RemoveAllListenersAsync(string, RemoveAllListenersBehavior)"/>
    /// treats in-flight async listeners. Official Playwright values are
    /// <c>wait</c>, <c>ignoreErrors</c>, and <c>default</c>.
    /// </summary>
    public enum RemoveAllListenersBehavior
    {
        /// <summary>
        /// Official <c>default</c>. Do not wait for in-flight listeners.
        /// Later handler errors are not swallowed.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Official <c>wait</c>. Wait for in-flight listeners and rethrow
        /// the first handler error.
        /// </summary>
        Wait,

        /// <summary>
        /// Official <c>ignoreErrors</c>. Return immediately and swallow
        /// later errors from in-flight handlers.
        /// </summary>
        IgnoreErrors,
    }
}
