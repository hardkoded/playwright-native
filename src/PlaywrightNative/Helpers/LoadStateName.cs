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
    /// Official <c>waitForLoadState</c> string names.
    /// </summary>
    internal static class LoadStateName
    {
        /// <summary>
        /// Parses an official load-state name or throws the official error.
        /// </summary>
        /// <param name="state">The official string state.</param>
        /// <returns>The matching <see cref="LoadState"/>.</returns>
        internal static LoadState Parse(string state)
        {
            if (string.Equals(state, "load", StringComparison.OrdinalIgnoreCase))
            {
                return LoadState.Load;
            }

            if (string.Equals(state, "domcontentloaded", StringComparison.OrdinalIgnoreCase))
            {
                return LoadState.DOMContentLoaded;
            }

            if (string.Equals(state, "networkidle", StringComparison.OrdinalIgnoreCase))
            {
                return LoadState.NetworkIdle;
            }

            if (string.Equals(state, "commit", StringComparison.OrdinalIgnoreCase))
            {
                return LoadState.Load;
            }

            throw new PlaywrightNativeException("state: expected one of (load|domcontentloaded|networkidle|commit)");
        }
    }
}
