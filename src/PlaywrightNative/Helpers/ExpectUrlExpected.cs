/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Text.Json;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>toHaveURL</c> invalid-argument error.
    /// </summary>
    internal static class ExpectUrlExpected
    {
        /// <summary>
        /// Builds the official invalid-argument message.
        /// </summary>
        /// <param name="expected">The rejected value.</param>
        /// <returns>The official error.</returns>
        internal static PlaywrightNativeException Invalid(object expected)
        {
            string type = expected == null ? "null" : "object";
            string value = expected == null ? "null" : JsonSerializer.Serialize(expected);
            return new PlaywrightNativeException(
                "expect(page).toHaveURL(expected) failed\n\n" +
                "Error: expected value must be a string or regular expression\n" +
                "Expected has type:  " + type + "\n" +
                "Expected has value: " + value + "\n");
        }
    }
}
