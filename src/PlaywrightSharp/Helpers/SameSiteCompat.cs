/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightSharp.Helpers
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
