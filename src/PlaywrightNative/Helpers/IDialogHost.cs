/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
