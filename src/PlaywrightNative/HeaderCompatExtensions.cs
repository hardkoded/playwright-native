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
#pragma warning disable CA1002
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Header and name/value conversion helpers.
    /// </summary>
    public static class HeaderCompatExtensions
    {
        /// <summary>Converts official headers to PlaywrightNative name/value entries.</summary>
        public static IReadOnlyList<NameValueEntry> AsNameValueEntries(this IReadOnlyList<Header> headers)
            => headers?.Select(h => new NameValueEntry(h.Name, h.Value)).ToArray()
                ?? Array.Empty<NameValueEntry>();

        /// <summary>Converts official headers to PlaywrightNative name/value entries.</summary>
        public static List<NameValueEntry> AsNameValueEntries(this List<Header> headers)
            => headers?.Select(h => new NameValueEntry(h.Name, h.Value)).ToList()
                ?? new List<NameValueEntry>();

        /// <summary>Converts a single official header.</summary>
        public static NameValueEntry AsNameValueEntry(this Header header)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }

            return new NameValueEntry(header.Name, header.Value);
        }

        /// <summary>Converts PlaywrightNative entries to official header pairs.</summary>
        public static IEnumerable<KeyValuePair<string, string>> AsHeaderPairs(this IEnumerable<NameValueEntry> entries)
            => entries?.Select(e => new KeyValuePair<string, string>(e.Name, e.Value))
                ?? Enumerable.Empty<KeyValuePair<string, string>>();

        /// <summary>Converts string dictionary to object-valued pairs for fetch APIs.</summary>
        public static IEnumerable<KeyValuePair<string, object>> AsObjectPairs(this Dictionary<string, string> dictionary)
            => dictionary?.Select(p => new KeyValuePair<string, object>(p.Key, p.Value))
                ?? Enumerable.Empty<KeyValuePair<string, object>>();

        /// <summary>Converts string pairs to object-valued pairs for fetch APIs.</summary>
        public static IEnumerable<KeyValuePair<string, object>> AsObjectPairs(this KeyValuePair<string, string>[] pairs)
            => pairs?.Select(p => new KeyValuePair<string, object>(p.Key, p.Value))
                ?? Enumerable.Empty<KeyValuePair<string, object>>();

        /// <summary>Converts string pairs to object-valued pairs for fetch APIs.</summary>
        public static IEnumerable<KeyValuePair<string, object>> AsObjectPairs(this IEnumerable<KeyValuePair<string, string>> pairs)
            => pairs?.Select(p => new KeyValuePair<string, object>(p.Key, p.Value))
                ?? Enumerable.Empty<KeyValuePair<string, object>>();
    }
}
