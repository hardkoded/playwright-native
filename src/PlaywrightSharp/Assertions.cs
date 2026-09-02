// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Collections;
using System.Text.RegularExpressions;
using PlaywrightSharp.Helpers;

namespace PlaywrightSharp
{
    /// <summary>
    /// Factory for retrying locator and page assertions and Node
    /// <c>expect(value)</c> generic builtins.
    /// </summary>
    public static class Assertions
    {
        /// <summary>
        /// JS <c>undefined</c> sentinel for generic expect.
        /// </summary>
        public static object Undefined { get; } = ExpectEquality.Undefined;

        /// <summary>
        /// Sets the default timeout for <see cref="Expect(ILocator)"/>,
        /// <see cref="Expect(IPage)"/>, and <see cref="Expect(IAPIResponse)"/>
        /// when a per-call timeout is omitted.
        /// </summary>
        /// <param name="timeout">Timeout in milliseconds. Pass <c>0</c> to disable timeout.</param>
        public static void SetDefaultExpectTimeout(float timeout)
            => TimeoutSettings.SetExpectTimeout(timeout);

        /// <summary>
        /// Node <c>expect.any()</c> with no constructor.
        /// </summary>
        /// <returns>Never returns; always throws.</returns>
        public static object Any()
        {
            ExpectAsymmetric.AnyMatcher.ThrowIfMissing();
            return null;
        }

        /// <summary>
        /// Node <c>expect.any(Constructor)</c>.
        /// </summary>
        /// <param name="constructor">Expected constructor or primitive boxed type.</param>
        /// <returns>Asymmetric matcher.</returns>
        public static object Any(Type constructor)
            => new ExpectAsymmetric.AnyMatcher(constructor);

        /// <summary>
        /// Node <c>expect.anything()</c>.
        /// </summary>
        /// <returns>Asymmetric matcher.</returns>
        public static object Anything()
            => new ExpectAsymmetric.AnythingMatcher();

        /// <summary>
        /// Node <c>expect.arrayContaining()</c>.
        /// </summary>
        /// <param name="expected">Expected subset items.</param>
        /// <returns>Asymmetric matcher.</returns>
        public static object ArrayContaining(IEnumerable expected)
            => new ExpectAsymmetric.ArrayContainingMatcher(expected);

        /// <summary>
        /// Node <c>expect.arrayOf()</c>.
        /// </summary>
        /// <param name="expected">Expected per-item value or matcher.</param>
        /// <returns>Asymmetric matcher.</returns>
        public static object ArrayOf(object expected)
            => new ExpectAsymmetric.ArrayOfMatcher(expected);

        /// <summary>
        /// Node <c>expect.objectContaining()</c>.
        /// </summary>
        /// <param name="expected">Expected subset object.</param>
        /// <returns>Asymmetric matcher.</returns>
        public static object ObjectContaining(object expected)
            => new ExpectAsymmetric.ObjectContainingMatcher(expected);

        /// <summary>
        /// Node <c>expect.stringContaining()</c>.
        /// </summary>
        /// <param name="expected">Expected substring.</param>
        /// <returns>Asymmetric matcher.</returns>
        public static object StringContaining(string expected)
            => new ExpectAsymmetric.StringContainingMatcher(expected);

        /// <summary>
        /// Node <c>expect.stringMatching()</c>.
        /// </summary>
        /// <param name="expected">String or <see cref="Regex"/>.</param>
        /// <returns>Asymmetric matcher.</returns>
        public static object StringMatching(object expected)
            => new ExpectAsymmetric.StringMatchingMatcher(expected);

        /// <summary>
        /// Node <c>expect.closeTo()</c>.
        /// </summary>
        /// <param name="expected">Expected number.</param>
        /// <param name="precision">Decimal digits. Defaults to 2.</param>
        /// <returns>Asymmetric matcher.</returns>
        public static object CloseTo(object expected, int precision = 2)
            => new ExpectAsymmetric.CloseToMatcher(expected, precision);

        /// <summary>
        /// Creates generic assertions for an arbitrary value
        /// (Node <c>expect(value)</c>).
        /// </summary>
        /// <param name="value">Received value.</param>
        /// <returns>Generic assertions.</returns>
        public static IGenericAssertions Expect(object value)
            => new GenericAssertions(value);

        /// <summary>
        /// Creates assertions that poll <paramref name="locator"/> until they pass.
        /// </summary>
        /// <param name="locator">The locator to assert against.</param>
        /// <returns>Retrying assertions for <paramref name="locator"/>.</returns>
        public static ILocatorAssertions Expect(ILocator locator)
        {
            if (locator == null)
            {
                throw new ArgumentNullException(nameof(locator));
            }

            return new LocatorAssertions(locator);
        }

        /// <summary>
        /// Creates assertions that poll <paramref name="page"/> until they pass.
        /// </summary>
        /// <param name="page">The page to assert against.</param>
        /// <returns>Retrying assertions for <paramref name="page"/>.</returns>
        public static IPageAssertions Expect(IPage page)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            return new PageAssertions(page);
        }

        /// <summary>
        /// Creates assertions that check <paramref name="response"/> until they pass.
        /// </summary>
        /// <param name="response">The API response to assert against.</param>
        /// <returns>Assertions for <paramref name="response"/>.</returns>
        public static IAPIResponseAssertions Expect(IAPIResponse response)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            return new APIResponseAssertions(response);
        }
    }
}
