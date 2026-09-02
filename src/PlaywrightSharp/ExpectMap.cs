// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Collections.Generic;

namespace PlaywrightSharp
{
    /// <summary>
    /// JS <c>Map</c> for generic expect equality (Node <c>expect</c>).
    /// </summary>
    public sealed class ExpectMap : Dictionary<object, object>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExpectMap"/> class.
        /// </summary>
        public ExpectMap()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpectMap"/> class
        /// with the given entries.
        /// </summary>
        /// <param name="entries">Map entries.</param>
        public ExpectMap(IEnumerable<KeyValuePair<object, object>> entries)
        {
            if (entries == null)
            {
                return;
            }

            foreach (KeyValuePair<object, object> entry in entries)
            {
                this[entry.Key] = entry.Value;
            }
        }
    }
}
