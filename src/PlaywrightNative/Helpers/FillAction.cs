/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Shared fill actionability wait and official-style error wrapping
    /// (<c>page.fill: Error: ...\nCall log:</c>) without the injected-script stack.
    /// </summary>
    internal static class FillAction
    {
        private static readonly Regex InputTypeCannotBeFilled = new Regex(
            @"Input of type ""[^""]+"" cannot be filled",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Waits until <paramref name="handle"/> is visible and editable unless
        /// <paramref name="force"/> is <see langword="true"/>.
        /// </summary>
        /// <param name="handle">The element to observe.</param>
        /// <param name="force">When <see langword="true"/>, skip actionability.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <returns>A task that completes when the wait is done or skipped.</returns>
        internal static async Task WaitUnlessForcedAsync(IElementHandle handle, bool? force, float? timeout)
        {
            if (force == true)
            {
                return;
            }

            await WaitForElementStateHelper.WaitAsync(handle, ElementState.Visible, timeout).ConfigureAwait(false);
            await WaitForElementStateHelper.WaitAsync(handle, ElementState.Editable, timeout).ConfigureAwait(false);
        }

        /// <summary>
        /// Whether <paramref name="ex"/> is a fill validation error from
        /// <see cref="ElementStateScript.FillFunction"/>.
        /// </summary>
        /// <param name="ex">The exception to inspect.</param>
        /// <returns><see langword="true"/> when the message is a fill validation error.</returns>
        internal static bool IsValidation(Exception ex)
        {
            if (ex == null)
            {
                return false;
            }

            string message = ex.Message ?? string.Empty;
            return message.Contains("Malformed value", StringComparison.Ordinal)
                || message.Contains("Cannot type text into input[type=number]", StringComparison.Ordinal)
                || message.Contains("cannot be filled", StringComparison.Ordinal)
                || message.Contains("Element is not an <input>", StringComparison.Ordinal);
        }

        /// <summary>
        /// Rewrites a fill validation error as <c>{apiName}: Error: {message}\nCall log:</c>.
        /// </summary>
        /// <param name="ex">The original evaluate exception.</param>
        /// <param name="apiName">Official API name such as <c>page.fill</c>.</param>
        /// <returns>A stackless <see cref="PlaywrightNativeException"/>.</returns>
        internal static PlaywrightNativeException Wrap(Exception ex, string apiName)
        {
            string name = string.IsNullOrEmpty(apiName) ? "page.fill" : apiName;
            return new PlaywrightNativeException(name + ": Error: " + Extract(ex) + "\nCall log:");
        }

        private static string Extract(Exception ex)
        {
            string raw = ex?.Message ?? string.Empty;
            if (raw.Contains("Malformed value", StringComparison.Ordinal))
            {
                return "Malformed value";
            }

            if (raw.Contains("Cannot type text into input[type=number]", StringComparison.Ordinal))
            {
                return "Cannot type text into input[type=number]";
            }

            Match typeMatch = InputTypeCannotBeFilled.Match(raw);
            if (typeMatch.Success)
            {
                return typeMatch.Value;
            }

            const string notFillable = "Element is not an <input>, <textarea> or [contenteditable] element";
            if (raw.Contains(notFillable, StringComparison.Ordinal))
            {
                return notFillable;
            }

            string firstLine = raw;
            int newline = raw.IndexOf('\n');
            if (newline >= 0)
            {
                firstLine = raw.Substring(0, newline);
            }

            const string errorPrefix = "Error: ";
            int errorAt = firstLine.IndexOf(errorPrefix, StringComparison.Ordinal);
            if (errorAt >= 0)
            {
                return firstLine.Substring(errorAt + errorPrefix.Length).Trim();
            }

            return firstLine.Trim();
        }
    }
}
