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
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Chromium
{
    /// <summary>Public <see cref="IKeyboard"/> wrapping <see cref="Input.Keyboard"/>.</summary>
    internal sealed partial class ChromiumKeyboard : IKeyboard
    {
        private readonly Input.Keyboard _keyboard;
        private readonly IBrowserContext _context;

        internal ChromiumKeyboard(Input.Keyboard keyboard, IBrowserContext context)
        {
            _keyboard = keyboard ?? throw new ArgumentNullException(nameof(keyboard));
            _context = context;
        }

        /// <inheritdoc/>
        public Task DownAsync(string key) => _keyboard.DownAsync(key);

        /// <inheritdoc/>
        public Task InsertTextAsync(string text)
            => Helpers.ActionTrace.RunAsync(_context, "Insert \"" + text + "\"", "Keyboard", "insertText", () => _keyboard.InsertTextAsync(text));

        /// <inheritdoc/>
        public Task PressAsync(string key, float? delay = null)
            => _keyboard.PressAsync(key, delay.HasValue ? (int)delay.Value : 0);

        /// <inheritdoc/>
        public Task TypeAsync(string text, float? delay = null)
            => _keyboard.TypeAsync(text, delay.HasValue ? (int)delay.Value : 0);

        /// <inheritdoc/>
        public Task UpAsync(string key) => _keyboard.UpAsync(key);

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        // Official IKeyboard Press/Type are options-only. Compat extensions call these;
        // stubs made every PressAsync/TypeAsync via IKeyboard a silent no-op.
        Task IKeyboard.PressAsync(string key, KeyboardPressOptions options)
            => PressAsync(key, options?.Delay);

        Task IKeyboard.TypeAsync(string text, KeyboardTypeOptions options)
            => TypeAsync(text, options?.Delay);
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
