// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace PlaywrightSharp
{
    /// <summary>
    /// Official Playwright <c>AbortController</c>. Call <see cref="Abort(object)"/>
    /// to cancel an in-flight action or assertion that received
    /// <see cref="Signal"/>.
    /// </summary>
    public sealed class AbortController
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AbortController"/> class.
        /// </summary>
        public AbortController()
        {
            Signal = new AbortSignal();
        }

        /// <summary>
        /// Gets the signal passed to Playwright actions and assertions.
        /// </summary>
        public AbortSignal Signal { get; }

        /// <summary>
        /// Aborts <see cref="Signal"/> with <paramref name="reason"/>.
        /// </summary>
        /// <param name="reason">
        /// Official abort reason. An <see cref="System.Exception"/> contributes
        /// its message; a string is used as-is.
        /// </param>
        public void Abort(object reason = null)
        {
            Signal.Abort(reason);
        }
    }
}
