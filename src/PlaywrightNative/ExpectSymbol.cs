// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace PlaywrightNative
{
    /// <summary>
    /// JS <c>Symbol</c> key used by generic expect equality
    /// (Node <c>expect</c> symbol properties on arrays).
    /// </summary>
    public sealed class ExpectSymbol
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExpectSymbol"/> class.
        /// </summary>
        /// <param name="description">Symbol description.</param>
        public ExpectSymbol(string description)
        {
            Description = description ?? string.Empty;
        }

        /// <summary>Symbol description.</summary>
        public string Description { get; }
    }
}
