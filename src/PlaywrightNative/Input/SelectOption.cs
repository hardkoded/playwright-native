/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
