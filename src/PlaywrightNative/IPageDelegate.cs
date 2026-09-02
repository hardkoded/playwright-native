/*
 * Copyright (c) 2020 Darío Kondratiuk
 * Copyright (c) 2020 Meir Blachman
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

namespace PlaywrightNative
{
    /// <summary>
    /// Browser-specific page delegate, matching Node Playwright <c>PageDelegate</c>.
    /// Shared <see cref="Page"/> owns one of these (<see cref="Chromium.CRPage"/>,
    /// Firefox <c>FFPage</c>, or WebKit <c>WKPage</c>) and implements the public
    /// <see cref="Microsoft.Playwright.IPage"/> surface.
    /// </summary>
    internal interface IPageDelegate
    {
        /// <summary>
        /// Closes the page. If <paramref name="runBeforeUnload"/> is <c>true</c>,
        /// sends a soft close (e.g. <c>Page.close</c> in CDP) that triggers beforeunload handlers.
        /// Otherwise, performs a hard close (e.g. <c>Target.closeTarget</c> in CDP).
        /// </summary>
        /// <param name="runBeforeUnload">
        /// When <c>true</c>, the browser fires the <c>beforeunload</c> event and waits
        /// for the page to handle it before closing. When <c>false</c>, the page is
        /// closed immediately without firing beforeunload.
        /// </param>
        /// <returns>A <see cref="Task"/> representing the asynchronous close operation.</returns>
        Task ClosePageAsync(bool runBeforeUnload);

        /// <summary>
        /// Called when the page is fully initialized and ready. Implementations
        /// should send the minimum set of protocol commands required to make the
        /// page functional (e.g. enabling CDP domains for Chromium).
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous initialization.</returns>
        Task InitializeAsync();

        /// <summary>
        /// Navigates a frame to the given URL using the browser-specific protocol command.
        /// </summary>
        /// <param name="frame">The frame to navigate.</param>
        /// <param name="url">The target URL.</param>
        /// <param name="referrer">Optional referrer URL.</param>
        /// <returns>
        /// A <see cref="GotoResult"/> containing the new document ID (loader ID),
        /// or a result with a <c>null</c> document ID for same-document navigations.
        /// </returns>
        Task<GotoResult> NavigateFrameAsync(Frame frame, string url, string referrer);

        /// <summary>
        /// Evaluates a JavaScript expression in the given frame's execution context.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="frame">The frame to evaluate in.</param>
        /// <param name="expression">The JavaScript expression to evaluate.</param>
        /// <returns>The deserialized result.</returns>
        Task<T> EvaluateInFrameAsync<T>(Frame frame, string expression);

        /// <summary>
        /// Evaluates a JavaScript function with arguments in the given frame's execution context.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="frame">The frame to evaluate in.</param>
        /// <param name="functionDeclaration">The JavaScript function declaration.</param>
        /// <param name="args">Arguments to pass to the function.</param>
        /// <returns>The deserialized result.</returns>
        Task<T> EvaluateFunctionInFrameAsync<T>(Frame frame, string functionDeclaration, params object[] args);
    }
}
