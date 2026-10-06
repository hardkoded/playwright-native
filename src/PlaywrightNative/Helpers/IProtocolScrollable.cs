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
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// An element handle that can scroll itself into view with the browser
    /// protocol (<c>DOM.scrollIntoViewIfNeeded</c>). Protocol scrolls are
    /// instant and ignore page <c>scroll-behavior: smooth</c>.
    /// </summary>
    internal interface IProtocolScrollable
    {
        /// <summary>
        /// Scrolls the element into view if needed.
        /// </summary>
        /// <returns>
        /// <see cref="ScrollIntoViewIfNeededAction.ResultDone"/>,
        /// <see cref="ScrollIntoViewIfNeededAction.ResultNotVisible"/>, or
        /// <see cref="ScrollIntoViewIfNeededAction.ResultNotConnected"/>.
        /// </returns>
        Task<string> ScrollRectIntoViewIfNeededAsync();
    }
}
