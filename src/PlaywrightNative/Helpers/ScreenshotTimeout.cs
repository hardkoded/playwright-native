/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Applies <see cref="IPage.ScreenshotAsync"/> timeout to a capture.
    /// </summary>
    internal static class ScreenshotTimeout
    {
        /// <summary>
        /// Runs <paramref name="capture"/> and fails when <paramref name="timeout"/> elapses.
        /// </summary>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <param name="capture">The screenshot capture.</param>
        /// <param name="apiName">Name used in the timeout message.</param>
        /// <returns>The screenshot bytes.</returns>
        internal static Task<byte[]> RunAsync(
            float? timeout,
            Func<Task<byte[]>> capture,
            string apiName = "page.screenshot")
        {
            if (capture == null)
            {
                throw new ArgumentNullException(nameof(capture));
            }

            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            return capture().WithTimeout(
                timeoutMs,
                _ => new TimeoutException(
                    apiName + ": Timeout " + timeoutMs + "ms exceeded.\nwaiting for fonts to load..."));
        }
    }
}
