/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Reads <c>document.title</c> without throwing when the execution context
    /// is destroyed mid-navigation. Official Playwright <c>page.title()</c>
    /// returns a string (including empty or Chrome's <c>Loading …</c> title)
    /// rather than rejecting during that race.
    /// </summary>
    internal static class PageTitle
    {
        /// <summary>
        /// Evaluates the title, retrying when the context is replaced.
        /// </summary>
        /// <param name="evaluateAsync">Reads <c>document.title</c>.</param>
        /// <returns>The document title, or an empty string if evaluation never succeeds.</returns>
        internal static async Task<string> ReadAsync(Func<Task<string>> evaluateAsync)
        {
            if (evaluateAsync == null)
            {
                throw new ArgumentNullException(nameof(evaluateAsync));
            }

            const int attempts = 8;
            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    return await evaluateAsync().ConfigureAwait(false) ?? string.Empty;
                }
                catch (PlaywrightSharpException) when (i < attempts - 1)
                {
                    await Task.Delay(25).ConfigureAwait(false);
                }
            }

            return string.Empty;
        }
    }
}
