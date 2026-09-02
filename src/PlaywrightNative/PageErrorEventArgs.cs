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
    /// Official Playwright <c>pageerror</c> Error: <c>name</c>, <c>message</c>, and
    /// <c>stack</c>.
    /// </summary>
    public class PageErrorEventArgs : EventArgs
    {
        private string _message;

        /// <summary>
        /// Error name. May be empty when the page set <c>error.name = ''</c>.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Error message.
        /// </summary>
        public string Message
        {
            get => _message ?? Value;
            set => _message = value;
        }

        /// <summary>
        /// Error value when the thrown object was not an Error.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Error stack. Official Chromium keeps the browser description, which
        /// may still start with <c>Error:</c> even when <see cref="Name"/> is empty.
        /// </summary>
        public string Stack { get; set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Stack))
            {
                return Stack;
            }

            if (string.IsNullOrEmpty(Name))
            {
                return Message ?? string.Empty;
            }

            return Name + ": " + (Message ?? string.Empty);
        }
    }
}
