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

namespace PlaywrightNative
{
    /// <summary>
    /// Throws a consistent <see cref="NotImplementedException"/> for methods outside
    /// the retained PlaywrightNative surface.
    /// </summary>
    internal static class NotImplementedHelper
    {
        /// <summary>
        /// Returns an exception for the named method.
        /// </summary>
        /// <param name="methodName">The method name (use <c>nameof(...)</c>).</param>
        /// <returns>A ready-to-throw <see cref="NotImplementedException"/>.</returns>
        internal static NotImplementedException ForMethod(string methodName)
        {
            return new NotImplementedException(
                $"{methodName} is not part of the retained PlaywrightNative surface.");
        }
    }
}
