// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;

namespace PlaywrightSharp
{
    /// <summary>
    /// Thrown when an aria snapshot YAML template cannot be parsed.
    /// </summary>
    public sealed class AriaSnapshotParseException : PlaywrightSharpException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AriaSnapshotParseException"/> class.
        /// </summary>
        public AriaSnapshotParseException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AriaSnapshotParseException"/> class.
        /// </summary>
        /// <param name="message">The parse error.</param>
        public AriaSnapshotParseException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AriaSnapshotParseException"/> class.
        /// </summary>
        /// <param name="message">The parse error.</param>
        /// <param name="innerException">The exception that caused this failure.</param>
        public AriaSnapshotParseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
