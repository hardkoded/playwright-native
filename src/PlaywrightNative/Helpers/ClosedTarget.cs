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

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Builds <see cref="TargetClosedException"/> instances that optionally
    /// include the caller-supplied close reason.
    /// </summary>
    internal static class ClosedTarget
    {
        /// <summary>
        /// Creates a closed-target error. When <paramref name="closeReason"/> is
        /// set, it is stored on <see cref="TargetClosedException.CloseReason"/>
        /// and appended to the message.
        /// </summary>
        /// <param name="message">The base error message.</param>
        /// <param name="closeReason">Optional reason passed to <c>CloseAsync</c>.</param>
        /// <returns>The exception to throw.</returns>
        internal static TargetClosedException Exception(string message, string closeReason)
            => string.IsNullOrEmpty(closeReason)
                ? new TargetClosedException(message)
                : new TargetClosedException(message, closeReason);

        /// <summary>
        /// Official <c>jsHandle.getProperty</c> prefix when the owning target is gone.
        /// </summary>
        /// <param name="ex">The protocol or session failure.</param>
        /// <returns>A closed-target error whose message names <c>jsHandle.getProperty</c>.</returns>
        internal static TargetClosedException WrapGetProperty(Exception ex)
        {
            string inner = ex?.Message ?? "Target closed";
            return new TargetClosedException("jsHandle.getProperty: " + inner);
        }

        /// <summary>
        /// Returns whether <paramref name="ex"/> is a closed session or target.
        /// </summary>
        /// <param name="ex">The exception to inspect.</param>
        /// <returns><see langword="true"/> when the target or session is closed.</returns>
        internal static bool IsClosed(Exception ex)
        {
            if (ex is TargetClosedException)
            {
                return true;
            }

            if (ex is ArgumentNullException ane
                && (string.Equals(ane.ParamName, "session", StringComparison.Ordinal)
                    || (ane.Message != null
                        && ane.Message.Contains("Parameter 'session'", StringComparison.Ordinal))))
            {
                // WebKit may surface a disposed/null session mid-probe after the page closes.
                return true;
            }

            string message = ex?.Message;
            return !string.IsNullOrEmpty(message)
                && (message.Contains("closed", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("Parameter 'session'", StringComparison.Ordinal));
        }
    }
}
