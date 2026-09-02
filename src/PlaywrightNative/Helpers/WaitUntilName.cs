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
    /// Official <c>goto({ waitUntil })</c> string names.
    /// </summary>
    internal static class WaitUntilName
    {
        /// <summary>
        /// Parses an official waitUntil name or throws the official error.
        /// </summary>
        /// <param name="waitUntil">The official string waitUntil.</param>
        /// <returns>The matching <see cref="WaitUntilState"/>.</returns>
        internal static WaitUntilState Parse(string waitUntil)
        {
            if (string.Equals(waitUntil, "load", StringComparison.OrdinalIgnoreCase))
            {
                return WaitUntilState.Load;
            }

            if (string.Equals(waitUntil, "domcontentloaded", StringComparison.OrdinalIgnoreCase))
            {
                return WaitUntilState.DOMContentLoaded;
            }

            if (string.Equals(waitUntil, "networkidle", StringComparison.OrdinalIgnoreCase))
            {
                return WaitUntilState.NetworkIdle;
            }

            if (string.Equals(waitUntil, "commit", StringComparison.OrdinalIgnoreCase))
            {
                return WaitUntilState.Commit;
            }

            throw new PlaywrightNativeException("waitUntil: expected one of (load|domcontentloaded|networkidle|commit)");
        }
    }
}
