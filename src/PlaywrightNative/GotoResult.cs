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
    /// Result of a frame navigation command. Contains the document ID (loader ID)
    /// that can be matched against <c>Page.frameNavigated</c> events to determine
    /// when the navigation has committed.
    /// </summary>
    internal class GotoResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GotoResult"/> class.
        /// </summary>
        /// <param name="newDocumentId">
        /// The loader ID from the CDP <c>Page.navigate</c> response.
        /// <c>null</c> for same-document navigations (anchor, pushState).
        /// </param>
        internal GotoResult(string newDocumentId)
        {
            NewDocumentId = newDocumentId;
        }

        /// <summary>
        /// Gets the loader ID from the CDP <c>Page.navigate</c> response.
        /// <c>null</c> for same-document navigations (anchor, pushState).
        /// </summary>
        internal string NewDocumentId { get; }
    }
}
