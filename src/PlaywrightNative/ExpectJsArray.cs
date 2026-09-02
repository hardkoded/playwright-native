// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
using System.Collections.Generic;

namespace PlaywrightNative
{
    /// <summary>
    /// JS array that can carry <see cref="ExpectSymbol"/> properties
    /// (Node <c>expect</c> array equality).
    /// </summary>
    public sealed class ExpectJsArray
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExpectJsArray"/> class.
        /// </summary>
        public ExpectJsArray()
        {
            Items = new List<object>();
            Symbols = new Dictionary<ExpectSymbol, object>();
        }

        /// <summary>Indexed elements.</summary>
        public IList<object> Items { get; }

        /// <summary>Symbol-keyed properties.</summary>
        public IDictionary<ExpectSymbol, object> Symbols { get; }
    }
}
