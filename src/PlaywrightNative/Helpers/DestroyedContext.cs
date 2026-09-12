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
    /// Detects destroyed-execution-context races so selector waits and clicks can retry.
    /// </summary>
    internal static class DestroyedContext
    {
        /// <summary>
        /// Returns whether <paramref name="ex"/> is a destroyed-context race
        /// that selector waits and clicks should retry.
        /// </summary>
        /// <param name="ex">The exception from a protocol evaluate or query.</param>
        /// <returns><see langword="true"/> when the caller should poll again.</returns>
        internal static bool IsDestroyedContext(Exception ex)
        {
            if (ex == null)
            {
                return false;
            }

            string message = ex.Message ?? string.Empty;
            return message.Contains("Cannot find context", StringComparison.Ordinal)
                || message.Contains("Execution context was destroyed", StringComparison.Ordinal)
                || message.Contains("Inspected target navigated", StringComparison.Ordinal);
        }
    }
}
