/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

namespace PlaywrightSharp
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
