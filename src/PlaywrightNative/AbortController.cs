// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

namespace PlaywrightNative
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
