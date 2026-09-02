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
namespace PlaywrightNative.Helpers
{
    /// <summary>Bridges optional/unset same-site values to official cookie enums.</summary>
    internal static class SameSiteCompat
    {
        /// <summary>Legacy unset sentinel stored as <c>-1</c> before official nullable enums.</summary>
        internal const Microsoft.Playwright.SameSiteAttribute UndefinedSentinel =
            (Microsoft.Playwright.SameSiteAttribute)(-1);

        internal static Microsoft.Playwright.SameSiteAttribute? ToOfficial(Microsoft.Playwright.SameSiteAttribute value)
        {
            if (value == UndefinedSentinel)
            {
                return null;
            }

            return value;
        }

        internal static Microsoft.Playwright.SameSiteAttribute? ToOfficial(Microsoft.Playwright.SameSiteAttribute? value)
            => value.HasValue ? ToOfficial(value.Value) : null;

        internal static Microsoft.Playwright.SameSiteAttribute FromOfficial(Microsoft.Playwright.SameSiteAttribute? value)
            => value ?? UndefinedSentinel;
    }
}
