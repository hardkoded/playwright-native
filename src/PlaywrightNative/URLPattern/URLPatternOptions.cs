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
namespace PlaywrightNative
{
    /// <summary>
    /// Options for <see cref="URLPattern"/>. Mirrors the JavaScript <c>URLPatternOptions</c> dictionary.
    /// </summary>
    public sealed class URLPatternOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether the pathname, search, and hash match without regard to case.
        /// Defaults to <c>false</c>.
        /// </summary>
        public bool IgnoreCase { get; set; }
    }
}
