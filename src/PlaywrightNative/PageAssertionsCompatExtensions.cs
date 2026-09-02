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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy page assertion helpers.
    /// </summary>
    public static class PageAssertionsCompatExtensions
    {
        /// <summary>Legacy URL assertion with predicate.</summary>
        public static Task ToHaveURLAsync(
            this IPageAssertions assertions,
            Func<string, bool> predicate,
            float? timeout = default)
            => assertions is PageAssertions sharp
                ? sharp.ToHaveURLAsync(predicate, timeout)
                : throw new NotSupportedException("ToHaveURLAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy URL assertion with predicate and timeout options bag.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task ToHaveURLAsync(
            this IPageAssertions assertions,
            Func<string, bool> predicate,
            PageAssertionsToHaveURLOptions options)
            => assertions.ToHaveURLAsync(predicate, options?.Timeout);

        /// <summary>Legacy title assertion with timeout.</summary>
        public static Task ToHaveTitleAsync(this IPageAssertions assertions, string title, float? timeout = default)
            => assertions is PageAssertions sharp
                ? sharp.ToHaveTitleAsync(title, timeout)
                : throw new NotSupportedException("ToHaveTitleAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy title assertion with regex and timeout.</summary>
        public static Task ToHaveTitleAsync(this IPageAssertions assertions, Regex title, float? timeout = default)
            => assertions is PageAssertions sharp
                ? sharp.ToHaveTitleAsync(title, timeout)
                : throw new NotSupportedException("ToHaveTitleAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy URL assertion with timeout.</summary>
        public static Task ToHaveURLAsync(this IPageAssertions assertions, string url, float? timeout = default, bool? ignoreCase = default)
            => assertions is PageAssertions sharp
                ? sharp.ToHaveURLAsync(url, timeout, ignoreCase)
                : throw new NotSupportedException("ToHaveURLAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy URL assertion with object expected (invalid-arg tests).</summary>
        public static Task ToHaveURLAsync(this IPageAssertions assertions, object expected, float? timeout = default)
            => assertions is PageAssertions sharp
                ? sharp.ToHaveURLAsync(expected, timeout)
                : throw new NotSupportedException("ToHaveURLAsync requires PlaywrightNative assertions.");

        /// <summary>Legacy URL assertion with regex and timeout.</summary>
        public static Task ToHaveURLAsync(this IPageAssertions assertions, Regex url, float? timeout = default, bool? ignoreCase = default)
            => assertions is PageAssertions sharp
                ? sharp.ToHaveURLAsync(url, timeout, ignoreCase)
                : throw new NotSupportedException("ToHaveURLAsync requires PlaywrightNative assertions.");
    }
}
