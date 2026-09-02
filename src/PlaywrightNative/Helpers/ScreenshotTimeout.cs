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
