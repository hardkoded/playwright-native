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
    /// Context-level dialog fan-out. Page implementations raise the context
    /// <see cref="IBrowserContext.Dialog"/> event through this instead of
    /// permanently subscribing to <see cref="IPage.Dialog"/> (that subscription
    /// would look like a user listener and block Playwright auto-dismiss).
    /// </summary>
    internal interface IDialogHost
    {
        /// <summary>
        /// Returns true when the context has at least one <c>Dialog</c> subscriber.
        /// </summary>
        /// <returns>True when a context dialog listener is present.</returns>
        bool HasDialogListeners();

        /// <summary>
        /// Forwards an opened dialog to context subscribers.
        /// </summary>
        /// <param name="dialog">The dialog that opened.</param>
        void RaiseDialog(IDialog dialog);
    }
}
