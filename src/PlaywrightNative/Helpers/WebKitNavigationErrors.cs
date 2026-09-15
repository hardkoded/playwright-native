/*
 * Copyright (c) Microsoft Corporation.
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

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Normalizes WebKit navigation failure strings across platforms so
    /// <c>page.goto</c> parity matches upstream Playwright expectations.
    /// </summary>
    internal static class WebKitNavigationErrors
    {
        /// <summary>
        /// Maps macOS / platform-specific connection-failure text to the
        /// upstream-expected <c>Could not connect</c> form used by page-goto tests.
        /// </summary>
        /// <param name="reason">Raw WebKit / WIP error text.</param>
        /// <returns>A normalized reason string.</returns>
        internal static string Normalize(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                return reason;
            }

            if (reason.Contains("Could not connect", StringComparison.OrdinalIgnoreCase))
            {
                return reason;
            }

            if (reason.Contains("network connection was lost", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("Could not connect to the server", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("Connection refused", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("NSURLError", StringComparison.OrdinalIgnoreCase))
            {
                return "Could not connect to the server";
            }

            return reason;
        }
    }
}
