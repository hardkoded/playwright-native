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
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <see cref="Header"/> does not override <see cref="object.Equals(object)"/>,
    /// so NUnit <c>Is.EqualTo</c> / <c>Does.Contain</c> against header arrays always fail.
    /// Returning this subclass makes wire-accurate HeadersArray entries compare by name/value.
    /// </summary>
    internal sealed class EquatableHeader : Header, IEquatable<Header>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EquatableHeader"/> class.
        /// </summary>
        /// <param name="name">Header name (wire casing preserved).</param>
        /// <param name="value">Header value.</param>
        public EquatableHeader(string name, string value)
        {
            Name = name;
            Value = value;
        }

        /// <summary>Builds an equatable header list from name/value entries.</summary>
        /// <param name="entries">Wire or normalized header entries.</param>
        /// <returns>A new list of equatable headers.</returns>
        public static IReadOnlyList<Header> FromEntries(IEnumerable<NameValueEntry> entries)
        {
            if (entries == null)
            {
                return Array.Empty<Header>();
            }

            List<Header> list = new List<Header>();
            foreach (NameValueEntry entry in entries)
            {
                list.Add(new EquatableHeader(entry.Name, entry.Value));
            }

            return list;
        }

        /// <inheritdoc/>
        public bool Equals(Header other)
            => other != null
                && string.Equals(Name, other.Name, StringComparison.Ordinal)
                && string.Equals(Value, other.Value, StringComparison.Ordinal);

        /// <inheritdoc/>
        public override bool Equals(object obj)
            => obj is Header header && Equals(header);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            StringComparer ordinal = StringComparer.Ordinal;
            return HashCode.Combine(
                Name == null ? 0 : ordinal.GetHashCode(Name),
                Value == null ? 0 : ordinal.GetHashCode(Value));
        }

        /// <inheritdoc/>
        [ExcludeFromCodeCoverage]
        public override string ToString()
            => Name + ": " + Value;
    }
}
