// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;

namespace PlaywrightSharp
{
    /// <summary>
    /// Timeout thrown by Playwright expect matchers.
    /// <see cref="ToString"/> matches Node <c>Error: {message}</c>.
    /// </summary>
    public sealed class ExpectException : TimeoutException
    {
        private static readonly ExpectMatcherResult EmptyMatcherResult = new ExpectMatcherResult(
            actual: null,
            expected: null,
            message: string.Empty,
            name: string.Empty,
            pass: false,
            log: Array.Empty<string>(),
            timeout: 0,
            ariaSnapshot: string.Empty);

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpectException"/> class.
        /// </summary>
        public ExpectException()
            : this(string.Empty, EmptyMatcherResult)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpectException"/> class.
        /// </summary>
        /// <param name="message">Failure message (Node <c>Error.message</c>).</param>
        public ExpectException(string message)
            : this(message, EmptyMatcherResult)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpectException"/> class.
        /// </summary>
        /// <param name="message">Failure message (Node <c>Error.message</c>).</param>
        /// <param name="innerException">The exception that caused this failure.</param>
        public ExpectException(string message, Exception innerException)
            : base(message, innerException)
        {
            MatcherResult = EmptyMatcherResult;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpectException"/> class.
        /// </summary>
        /// <param name="message">Failure message (Node <c>Error.message</c>).</param>
        /// <param name="matcherResult">Structured matcher result.</param>
        public ExpectException(string message, ExpectMatcherResult matcherResult)
            : base(message)
        {
            MatcherResult = matcherResult ?? throw new ArgumentNullException(nameof(matcherResult));
        }

        /// <summary>Structured matcher result (Node <c>error.matcherResult</c>).</summary>
        public ExpectMatcherResult MatcherResult { get; }

        /// <inheritdoc />
        public override string ToString() => "Error: " + Message;

        /// <summary>
        /// Creates a failed expect exception with a structured matcher result.
        /// </summary>
        /// <param name="message">Failure message.</param>
        /// <param name="actual">Received value.</param>
        /// <param name="expected">Expected value.</param>
        /// <param name="name">Matcher name.</param>
        /// <param name="pass">Whether the inner matcher passed.</param>
        /// <param name="timeoutMs">Timeout in milliseconds.</param>
        /// <param name="ariaSnapshot">Optional aria snapshot, or <see langword="null"/>.</param>
        /// <returns>The exception to throw.</returns>
        internal static ExpectException Fail(
            string message,
            object actual,
            object expected,
            string name,
            bool pass,
            int timeoutMs,
            string ariaSnapshot)
        {
            string text = message ?? string.Empty;
            string[] log = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            return new ExpectException(
                text,
                new ExpectMatcherResult(
                    actual,
                    expected,
                    text,
                    name,
                    pass,
                    log,
                    timeoutMs,
                    ariaSnapshot));
        }
    }
}
