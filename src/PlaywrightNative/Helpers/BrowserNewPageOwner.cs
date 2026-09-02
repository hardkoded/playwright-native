/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
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
