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

namespace PlaywrightNative.Input
{
    /// <summary>
    /// Describes which option in a <c>&lt;select&gt;</c> to select. At least one of
    /// <see cref="Value"/>, <see cref="Label"/>, or <see cref="Index"/> must be non-null.
    /// Mirrors upstream Playwright's <c>SelectOption</c> type.
    /// </summary>
    internal sealed class SelectOption
    {
        /// <summary>Match by <c>&lt;option&gt;.value</c>.</summary>
        public string Value { get; init; }

        /// <summary>Match by <c>&lt;option&gt;.label</c> (or textContent when label is empty).</summary>
        public string Label { get; init; }

        /// <summary>
        /// Match by value or normalized label. Used when the public API is given a
        /// bare string (upstream falls back to label).
        /// </summary>
        public string ValueOrLabel { get; init; }

        /// <summary>Match by zero-based option index.</summary>
        public int? Index { get; init; }
    }
}
