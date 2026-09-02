// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;

namespace PlaywrightNative
{
    /// <summary>
    /// Thrown when an aria snapshot YAML template cannot be parsed.
    /// </summary>
    public sealed class AriaSnapshotParseException : PlaywrightNativeException
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
