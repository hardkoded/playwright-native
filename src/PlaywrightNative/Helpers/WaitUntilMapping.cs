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
    /// Maps <see cref="WaitUntilState"/> to the lifecycle event name recorded
    /// by the browser frame trackers.
    /// </summary>
    internal static class WaitUntilMapping
    {
        /// <summary>
        /// Returns the lifecycle event to wait for.
        /// </summary>
        /// <param name="waitUntil">The public wait-until option.</param>
        /// <returns>
        /// <c>commit</c>, <c>DOMContentLoaded</c>, <c>networkidle</c>, or <c>load</c>.
        /// </returns>
        internal static string ToLifecycleEvent(WaitUntilState waitUntil)
        {
            return waitUntil switch
            {
                WaitUntilState.Commit => "commit",
                WaitUntilState.DOMContentLoaded => "DOMContentLoaded",
                WaitUntilState.NetworkIdle => "networkidle",
                _ => "load",
            };
        }
    }
}
