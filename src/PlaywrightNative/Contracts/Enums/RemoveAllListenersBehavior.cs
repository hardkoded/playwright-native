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
namespace PlaywrightNative
{
    /// <summary>
    /// How <see cref="IPage.RemoveAllListenersAsync(string, RemoveAllListenersBehavior)"/>
    /// treats in-flight async listeners. Official Playwright values are
    /// <c>wait</c>, <c>ignoreErrors</c>, and <c>default</c>.
    /// </summary>
    public enum RemoveAllListenersBehavior
    {
        /// <summary>
        /// Official <c>default</c>. Do not wait for in-flight listeners.
        /// Later handler errors are not swallowed.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Official <c>wait</c>. Wait for in-flight listeners and rethrow
        /// the first handler error.
        /// </summary>
        Wait,

        /// <summary>
        /// Official <c>ignoreErrors</c>. Return immediately and swallow
        /// later errors from in-flight handlers.
        /// </summary>
        IgnoreErrors,
    }
}
