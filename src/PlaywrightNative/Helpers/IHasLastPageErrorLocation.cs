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
    /// Page implementations that stash the location of the last
    /// <see cref="IPage.PageError"/> so context <see cref="IWebError"/>
    /// can expose official <see cref="IWebError.Location"/>.
    /// </summary>
    internal interface IHasLastPageErrorLocation
    {
        /// <summary>
        /// Location of the most recently raised page error.
        /// </summary>
        WebErrorLocation LastPageErrorLocation { get; }
    }
}
