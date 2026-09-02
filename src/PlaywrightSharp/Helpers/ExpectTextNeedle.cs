// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Text.RegularExpressions;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// One official <c>toHaveText</c> / <c>toContainText</c> expected item.
    /// </summary>
    internal sealed class ExpectTextNeedle
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExpectTextNeedle"/> class.
        /// </summary>
        /// <param name="value">Expected string.</param>
        internal ExpectTextNeedle(string value)
        {
            String = value ?? string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpectTextNeedle"/> class.
        /// </summary>
        /// <param name="value">Expected pattern.</param>
        internal ExpectTextNeedle(Regex value)
        {
            Regex = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Expected string, or <see langword="null"/> when this is a pattern.
        /// </summary>
        internal string String { get; }

        /// <summary>
        /// Expected pattern, or <see langword="null"/> when this is a string.
        /// </summary>
        internal Regex Regex { get; }
    }
}
