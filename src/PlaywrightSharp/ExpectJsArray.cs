// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Collections.Generic;

namespace PlaywrightSharp
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
