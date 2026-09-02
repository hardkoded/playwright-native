/*
 * Copyright (c) Microsoft Corporation.
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
    /// Contexts and pages that track official <c>exposeFunction</c> names
    /// for duplicate-registration errors.
    /// </summary>
    internal interface IHasExposedFunctionNames
    {
        /// <summary>
        /// Returns whether <paramref name="name"/> is already exposed.
        /// </summary>
        /// <param name="name">The JS global name.</param>
        /// <returns>True when the name is registered.</returns>
        bool HasExposedFunction(string name);
    }
}
