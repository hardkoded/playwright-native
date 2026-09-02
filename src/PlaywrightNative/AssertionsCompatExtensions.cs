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
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative
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
                : throw new System.NotSupportedException("ToHaveRoleAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy accessible-name assertion with exact flag.</summary>
        public static Task ToHaveAccessibleNameAsync(
            this ILocatorAssertions assertions,
            string name,
            bool? exact = null,
            float? timeout = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToHaveAccessibleNameAsync(name, exact, timeout)
                : throw new System.NotSupportedException("ToHaveAccessibleNameAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy class assertion with object array values.</summary>
        public static Task ToHaveClassAsync(
            this ILocatorAssertions assertions,
            object[] expected,
            float? timeout = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToHaveClassAsync((IEnumerable<object>)expected, timeout)
                : throw new System.NotSupportedException("ToHaveClassAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy text assertion with object array values.</summary>
        public static Task ToHaveTextAsync(
            this ILocatorAssertions assertions,
            object[] expected,
            float? timeout = default,
            bool? ignoreCase = default,
            bool? useInnerText = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToHaveTextAsync(expected, timeout, ignoreCase, useInnerText)
                : throw new System.NotSupportedException("ToHaveTextAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy contain-class assertion with regex.</summary>
        public static Task ToContainClassAsync(
            this ILocatorAssertions assertions,
            Regex expected,
            float? timeout = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToContainClassAsync(expected, timeout)
                : throw new System.NotSupportedException("ToContainClassAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy contain-class assertion with object array.</summary>
        public static Task ToContainClassAsync(
            this ILocatorAssertions assertions,
            object[] expected,
            float? timeout = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToContainClassAsync((System.Collections.Generic.IEnumerable<object>)expected, timeout)
                : throw new System.NotSupportedException("ToContainClassAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy text assertion with a single object value.</summary>
        public static Task ToHaveTextAsync(
            this ILocatorAssertions assertions,
            object expected,
            float? timeout = default,
            bool? ignoreCase = default,
            bool? useInnerText = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToHaveTextAsync(expected, timeout, ignoreCase, useInnerText)
                : throw new System.NotSupportedException("ToHaveTextAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy text assertion with regex.</summary>
        public static Task ToHaveTextAsync(
            this ILocatorAssertions assertions,
            Regex expected,
            float? timeout = default,
            bool? ignoreCase = default,
            bool? useInnerText = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToHaveTextAsync(expected, timeout, ignoreCase, useInnerText)
                : throw new System.NotSupportedException("ToHaveTextAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy text assertion with predicate.</summary>
        public static Task ToHaveTextAsync(
            this ILocatorAssertions assertions,
            Func<string, bool> predicate,
            float? timeout = default,
            bool? ignoreCase = default,
            bool? useInnerText = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToHaveTextAsync(predicate, timeout, ignoreCase, useInnerText)
                : throw new System.NotSupportedException("ToHaveTextAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy CSS assertion with string pseudo-element.</summary>
        public static Task ToHaveCSSAsync(
            this ILocatorAssertions assertions,
            string name,
            string value,
            string pseudoElement = default,
            float? timeout = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToHaveCSSAsync(name, value, pseudoElement, timeout)
                : throw new System.NotSupportedException("ToHaveCSSAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy API response OK assertion with timeout.</summary>
        public static Task ToBeOKAsync(this IAPIResponseAssertions assertions, float? timeout = default)
            => assertions is APIResponseAssertions sharp
                ? sharp.ToBeOKAsync(timeout)
                : throw new System.NotSupportedException("ToBeOKAsync requires PlaywrightNative assertions.");
    }
}
