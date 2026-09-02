// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

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
