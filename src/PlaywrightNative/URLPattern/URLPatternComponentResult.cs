/*
 * Copyright (c) 2026 Dario Kondratiuk
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
using System.Collections.Generic;

namespace PlaywrightNative
{
    /// <summary>
    /// The match of one URL component. Mirrors the JavaScript <c>URLPatternComponentResult</c> dictionary.
    /// </summary>
    public sealed class URLPatternComponentResult
    {
        internal URLPatternComponentResult(string input, IReadOnlyDictionary<string, string> groups)
        {
            Input = input;
            Groups = groups;
        }

        /// <summary>
        /// Gets the canonical component value that was matched.
        /// </summary>
        public string Input { get; }

        /// <summary>
        /// Gets the matched groups by name. Unnamed groups use their index ("0", "1", ...).
        /// A group that did not take part in the match maps to <c>null</c> (JavaScript <c>undefined</c>).
        /// </summary>
        public IReadOnlyDictionary<string, string> Groups { get; }
    }
}
