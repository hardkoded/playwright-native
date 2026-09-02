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
#pragma warning disable CA1062
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy locator assertion helpers with named timeout parameters.
    /// </summary>
    public static class LocatorAssertionsCompatExtensions
    {
        /// <summary>Legacy count assertion with timeout.</summary>
        public static Task ToHaveCountAsync(this ILocatorAssertions assertions, int count, float? timeout = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToHaveCountAsync(count, timeout)
                : throw new NotSupportedException("ToHaveCountAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy JS property assertion with timeout.</summary>
        public static Task ToHaveJSPropertyAsync(
            this ILocatorAssertions assertions,
            string name,
            object value,
            float? timeout = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToHaveJSPropertyAsync(name, value, timeout)
                : throw new NotSupportedException("ToHaveJSPropertyAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy attribute assertion with timeout.</summary>
        public static Task ToHaveAttributeAsync(
            this ILocatorAssertions assertions,
            string name,
            string value,
            float? timeout = default,
            bool? ignoreCase = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToHaveAttributeAsync(name, value, timeout, ignoreCase)
                : throw new NotSupportedException("ToHaveAttributeAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy attribute assertion with regex and timeout.</summary>
        public static Task ToHaveAttributeAsync(
            this ILocatorAssertions assertions,
            string name,
            Regex value,
            float? timeout = default,
            bool? ignoreCase = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToHaveAttributeAsync(name, value, timeout, ignoreCase)
                : throw new NotSupportedException("ToHaveAttributeAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy contain-text assertion with timeout.</summary>
        public static Task ToContainTextAsync(
            this ILocatorAssertions assertions,
            string expected,
            float? timeout = default,
            bool? ignoreCase = default,
            bool? useInnerText = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToContainTextAsync(expected, timeout, ignoreCase, useInnerText)
                : throw new NotSupportedException("ToContainTextAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy contain-text assertion with regex and timeout.</summary>
        public static Task ToContainTextAsync(
            this ILocatorAssertions assertions,
            Regex expected,
            float? timeout = default,
            bool? ignoreCase = default,
            bool? useInnerText = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToContainTextAsync(expected, timeout, ignoreCase, useInnerText)
                : throw new NotSupportedException("ToContainTextAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy contain-text assertion with regex and timeout.</summary>
        public static Task ToContainClassAsync(
            this ILocatorAssertions assertions,
            string expected,
            float? timeout = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToContainClassAsync(expected, timeout)
                : throw new NotSupportedException("ToContainClassAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy contain-text assertion with object array.</summary>
        public static Task ToContainTextAsync(
            this ILocatorAssertions assertions,
            object[] expected,
            float? timeout = default,
            bool? ignoreCase = default,
            bool? useInnerText = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToContainTextAsync(expected, timeout, ignoreCase, useInnerText)
                : throw new System.NotSupportedException("ToContainTextAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy class assertion with string and timeout.</summary>
        public static Task ToHaveClassAsync(
            this ILocatorAssertions assertions,
            string className,
            float? timeout = default)
            => assertions is LocatorAssertions sharp
                ? sharp.ToHaveClassAsync(className, timeout)
                : throw new NotSupportedException("ToHaveClassAsync requires PlaywrightNative assertions.");
    }
}
