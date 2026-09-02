/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightSharp.Chromium
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
        Task IKeyboard.PressAsync(string key, KeyboardPressOptions options) => Task.CompletedTask;

        Task IKeyboard.TypeAsync(string text, KeyboardTypeOptions options) => Task.CompletedTask;
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
