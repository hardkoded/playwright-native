/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
#pragma warning disable CA1062
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightSharp
{
    /// <summary>
    /// Legacy assertion helpers on official assertion interfaces.
    /// </summary>
    public static class AssertionsCompatExtensions
    {
        /// <summary>Legacy string-role assertion.</summary>
        public static Task ToHaveRoleAsync(this ILocatorAssertions assertions, string role, float? timeout = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToHaveRoleAsync(role, timeout)
                : throw new System.NotSupportedException("ToHaveRoleAsync requires PlaywrightSharp assertions.");

        /// <summary>Legacy accessible-name assertion with exact flag.</summary>
        public static Task ToHaveAccessibleNameAsync(
            this ILocatorAssertions assertions,
            string name,
            bool? exact = null,
            float? timeout = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToHaveAccessibleNameAsync(name, exact, timeout)
                : throw new System.NotSupportedException("ToHaveAccessibleNameAsync requires PlaywrightSharp assertions.");

        /// <summary>Legacy class assertion with object array values.</summary>
        public static Task ToHaveClassAsync(
            this ILocatorAssertions assertions,
            object[] expected,
            float? timeout = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToHaveClassAsync((IEnumerable<object>)expected, timeout)
                : throw new System.NotSupportedException("ToHaveClassAsync requires PlaywrightSharp assertions.");

        /// <summary>Legacy text assertion with object array values.</summary>
        public static Task ToHaveTextAsync(
            this ILocatorAssertions assertions,
            object[] expected,
            float? timeout = default,
            bool? ignoreCase = default,
            bool? useInnerText = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToHaveTextAsync(expected, timeout, ignoreCase, useInnerText)
                : throw new System.NotSupportedException("ToHaveTextAsync requires PlaywrightSharp assertions.");

        /// <summary>Legacy contain-class assertion with regex.</summary>
        public static Task ToContainClassAsync(
            this ILocatorAssertions assertions,
            Regex expected,
            float? timeout = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToContainClassAsync(expected, timeout)
                : throw new System.NotSupportedException("ToContainClassAsync requires PlaywrightSharp assertions.");

        /// <summary>Legacy contain-class assertion with object array.</summary>
        public static Task ToContainClassAsync(
            this ILocatorAssertions assertions,
            object[] expected,
            float? timeout = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToContainClassAsync((System.Collections.Generic.IEnumerable<object>)expected, timeout)
                : throw new System.NotSupportedException("ToContainClassAsync requires PlaywrightSharp assertions.");

        /// <summary>Legacy text assertion with a single object value.</summary>
        public static Task ToHaveTextAsync(
            this ILocatorAssertions assertions,
            object expected,
            float? timeout = default,
            bool? ignoreCase = default,
            bool? useInnerText = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToHaveTextAsync(expected, timeout, ignoreCase, useInnerText)
                : throw new System.NotSupportedException("ToHaveTextAsync requires PlaywrightSharp assertions.");

        /// <summary>Legacy text assertion with regex.</summary>
        public static Task ToHaveTextAsync(
            this ILocatorAssertions assertions,
            Regex expected,
            float? timeout = default,
            bool? ignoreCase = default,
            bool? useInnerText = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToHaveTextAsync(expected, timeout, ignoreCase, useInnerText)
                : throw new System.NotSupportedException("ToHaveTextAsync requires PlaywrightSharp assertions.");

        /// <summary>Legacy text assertion with predicate.</summary>
        public static Task ToHaveTextAsync(
            this ILocatorAssertions assertions,
            Func<string, bool> predicate,
            float? timeout = default,
            bool? ignoreCase = default,
            bool? useInnerText = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToHaveTextAsync(predicate, timeout, ignoreCase, useInnerText)
                : throw new System.NotSupportedException("ToHaveTextAsync requires PlaywrightSharp assertions.");

        /// <summary>Legacy CSS assertion with string pseudo-element.</summary>
        public static Task ToHaveCSSAsync(
            this ILocatorAssertions assertions,
            string name,
            string value,
            string pseudoElement = default,
            float? timeout = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToHaveCSSAsync(name, value, pseudoElement, timeout)
                : throw new System.NotSupportedException("ToHaveCSSAsync requires PlaywrightSharp assertions.");

        /// <summary>Legacy API response OK assertion with timeout.</summary>
        public static Task ToBeOKAsync(this IAPIResponseAssertions assertions, float? timeout = default)
            => assertions is APIResponseAssertions sharp
                ? sharp.ToBeOKAsync(timeout)
                : throw new System.NotSupportedException("ToBeOKAsync requires PlaywrightSharp assertions.");
    }
}
