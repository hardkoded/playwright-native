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
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Playwright <c>trial</c> actions run actionability checks without
    /// dispatching the pointer or keyboard event.
    /// </summary>
    internal static class ActionTrial
    {
        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="trial"/> is set.
        /// </summary>
        /// <param name="trial">The trial option.</param>
        /// <returns>Whether this is a dry-run action.</returns>
        internal static bool IsTrial(bool? trial) => trial == true;
    }
}
