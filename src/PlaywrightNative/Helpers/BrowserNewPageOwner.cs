/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>browser.newPage()</c> context is limited to one page.
    /// A second <c>context.newPage()</c> must tell the caller to use
    /// <c>browser.newContext()</c>.
    /// </summary>
    internal static class BrowserNewPageOwner
    {
        /// <summary>
        /// Official error fragment from <c>library/browser.spec.ts</c>.
        /// </summary>
        internal const string SecondPageMessage = "Please use browser.newContext()";

        /// <summary>
        /// Throws when this context was created by <c>browser.newPage()</c>.
        /// </summary>
        /// <param name="owned">Whether the context already owns its single page.</param>
        internal static void ThrowIfOwned(bool owned)
        {
            if (owned)
            {
                throw new PlaywrightNativeException(SecondPageMessage);
            }
        }
    }
}
