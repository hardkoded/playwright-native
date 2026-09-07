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
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Actions that accept <see cref="ActionScroll"/> but whose official
    /// options bag has no <c>scroll</c> property. The legacy expanded-parameter
    /// extensions dispatch here so the option is not lost on the way down.
    /// </summary>
    internal interface IHasScrollAwareActions
    {
        /// <summary>Fill honouring <paramref name="scroll"/>.</summary>
        /// <param name="selector">The selector.</param>
        /// <param name="value">The value to fill.</param>
        /// <param name="noWaitAfter">Official <c>noWaitAfter</c>.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <param name="force">Official <c>force</c>.</param>
        /// <param name="scroll">Official <c>scroll</c>.</param>
        /// <param name="strict">Official <c>strict</c>.</param>
        /// <returns>A task that completes when the fill finishes.</returns>
        Task FillAsync(string selector, string value, bool? noWaitAfter, float? timeout, bool? force, ActionScroll scroll, bool? strict);

        /// <summary>Focus honouring <paramref name="scroll"/>.</summary>
        /// <param name="selector">The selector.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <param name="scroll">Official <c>scroll</c>.</param>
        /// <param name="strict">Official <c>strict</c>.</param>
        /// <returns>A task that completes when the focus finishes.</returns>
        Task FocusAsync(string selector, float? timeout, ActionScroll scroll, bool? strict);

        /// <summary>Press honouring <paramref name="scroll"/>.</summary>
        /// <param name="selector">The selector.</param>
        /// <param name="key">The key to press.</param>
        /// <param name="delay">Delay between down and up, in milliseconds.</param>
        /// <param name="noWaitAfter">Official <c>noWaitAfter</c>.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <param name="force">Official <c>force</c>.</param>
        /// <param name="scroll">Official <c>scroll</c>.</param>
        /// <param name="strict">Official <c>strict</c>.</param>
        /// <returns>A task that completes when the press finishes.</returns>
        Task PressAsync(string selector, string key, float? delay, bool? noWaitAfter, float? timeout, bool? force, ActionScroll scroll, bool? strict);

        /// <summary>Type honouring <paramref name="scroll"/>.</summary>
        /// <param name="selector">The selector.</param>
        /// <param name="text">The text to type.</param>
        /// <param name="delay">Delay between key presses, in milliseconds.</param>
        /// <param name="noWaitAfter">Official <c>noWaitAfter</c>.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <param name="force">Official <c>force</c>.</param>
        /// <param name="scroll">Official <c>scroll</c>.</param>
        /// <param name="strict">Official <c>strict</c>.</param>
        /// <returns>A task that completes when the typing finishes.</returns>
        Task TypeAsync(string selector, string text, float? delay, bool? noWaitAfter, float? timeout, bool? force, ActionScroll scroll, bool? strict);

        /// <summary>Select option honouring <paramref name="scroll"/>.</summary>
        /// <param name="selector">The selector.</param>
        /// <param name="values">The options to select.</param>
        /// <param name="noWaitAfter">Official <c>noWaitAfter</c>.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <param name="force">Official <c>force</c>.</param>
        /// <param name="scroll">Official <c>scroll</c>.</param>
        /// <param name="strict">Official <c>strict</c>.</param>
        /// <returns>The selected option values.</returns>
        Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, IEnumerable<SelectOptionValue> values, bool? noWaitAfter, float? timeout, bool? force, ActionScroll scroll, bool? strict);

        /// <summary>Set input files honouring <paramref name="scroll"/>.</summary>
        /// <param name="selector">The selector.</param>
        /// <param name="files">The file payloads.</param>
        /// <param name="noWaitAfter">Official <c>noWaitAfter</c>.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <param name="force">Official <c>force</c>.</param>
        /// <param name="scroll">Official <c>scroll</c>.</param>
        /// <param name="strict">Official <c>strict</c>.</param>
        /// <returns>A task that completes when the files are set.</returns>
        Task SetInputFilesAsync(string selector, IEnumerable<FilePayload> files, bool? noWaitAfter, float? timeout, bool? force, ActionScroll scroll, bool? strict);
    }
}
