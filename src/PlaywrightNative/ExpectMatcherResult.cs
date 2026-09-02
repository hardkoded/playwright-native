// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;

namespace PlaywrightNative
{
    /// <summary>
    /// Structured result attached to a failed Playwright expect assertion
    /// (Node <c>error.matcherResult</c>).
    /// </summary>
    public sealed class ExpectMatcherResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExpectMatcherResult"/> class.
        /// </summary>
        /// <param name="actual">Received value.</param>
        /// <param name="expected">Expected value.</param>
        /// <param name="message">Human-readable failure message.</param>
        /// <param name="name">Matcher name (for example <c>toHaveText</c>).</param>
        /// <param name="pass">Whether the inner matcher passed (true for failed <c>not.*</c>).</param>
        /// <param name="log">Call-log lines.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <param name="ariaSnapshot">Optional aria snapshot printed by some matchers.</param>
        public ExpectMatcherResult(
            object actual,
            object expected,
            string message,
            string name,
            bool pass,
            IReadOnlyList<string> log,
            int timeout,
            string ariaSnapshot)
        {
            Actual = actual;
            Expected = expected;
            Message = message ?? string.Empty;
            Name = name ?? string.Empty;
            Pass = pass;
            Log = log ?? Array.Empty<string>();
            Timeout = timeout;
            AriaSnapshot = ariaSnapshot;
        }

        /// <summary>Received value.</summary>
        public object Actual { get; }

        /// <summary>Expected value.</summary>
        public object Expected { get; }

        /// <summary>Human-readable failure message.</summary>
        public string Message { get; }

        /// <summary>Matcher name.</summary>
        public string Name { get; }

        /// <summary>Whether the inner matcher passed (true for failed <c>not.*</c>).</summary>
        public bool Pass { get; }

        /// <summary>Call-log lines.</summary>
        public IReadOnlyList<string> Log { get; }

        /// <summary>Timeout in milliseconds.</summary>
        public int Timeout { get; }

        /// <summary>Optional aria snapshot.</summary>
        public string AriaSnapshot { get; }
    }
}
