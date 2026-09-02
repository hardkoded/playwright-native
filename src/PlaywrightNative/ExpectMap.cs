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
