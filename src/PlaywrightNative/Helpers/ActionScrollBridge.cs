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
    /// Maps legacy <see cref="ActionScroll"/> to official <c>scroll</c> option bags.
    /// </summary>
    internal static class ActionScrollBridge
    {
        /// <summary>
        /// Converts <see cref="ActionScroll"/> to the official <see cref="ScrollMode"/> option.
        /// </summary>
        /// <param name="scroll">Legacy scroll option.</param>
        /// <returns><see cref="ScrollMode.None"/> for <see cref="ActionScroll.None"/>; otherwise <see langword="null"/>.</returns>
        internal static ScrollMode? ToScrollOption(ActionScroll scroll)
            => scroll == ActionScroll.None ? ScrollMode.None : null;

        /// <summary>
        /// Converts an official <see cref="ScrollMode"/> option to <see cref="ActionScroll"/>.
        /// </summary>
        /// <param name="scroll">Official scroll option, or <see langword="null"/> for default auto.</param>
        /// <returns><see cref="ActionScroll.None"/> only when <paramref name="scroll"/> is <see cref="ScrollMode.None"/>.</returns>
        internal static ActionScroll FromScrollOption(ScrollMode? scroll)
            => scroll == ScrollMode.None ? ActionScroll.None : ActionScroll.Undefined;
    }
}
