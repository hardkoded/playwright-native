/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Threading.Tasks;
using PlaywrightSharp.Input;

namespace PlaywrightSharp.Chromium
{
    /// <summary>
    /// Sends CDP <c>Input.dispatchKeyEvent</c> and <c>Input.insertText</c> commands.
    /// </summary>
    internal class CRRawKeyboard : IRawKeyboard
    {
        private readonly CRSession _session;

        /// <summary>
        /// Initializes a new instance of the <see cref="CRRawKeyboard"/> class.
        /// </summary>
        /// <param name="session">The CDP session to send commands on.</param>
        public CRRawKeyboard(CRSession session)
        {
            _session = session;
        }

        /// <summary>
        /// Dispatches a CDP <c>Input.dispatchKeyEvent</c> <c>keyDown</c> (or <c>rawKeyDown</c>
        /// when there is no text to emit).
        /// </summary>
        public Task KeyDownAsync(IReadOnlyCollection<Input.KeyboardModifier> modifiers, Input.KeyDefinition key, bool autoRepeat)
        {
            string type = string.IsNullOrEmpty(key.Text) ? "rawKeyDown" : "keyDown";

            return _session.SendAsync("Input.dispatchKeyEvent", new
            {
                type,
                modifiers = modifiers.ToCdpMask(),
                windowsVirtualKeyCode = key.KeyCodeWithoutLocation == 0 ? key.KeyCode : key.KeyCodeWithoutLocation,
                code = key.Code,
                key = key.Key,
                text = key.Text,
                unmodifiedText = key.Text,
                autoRepeat,
                location = key.Location,
                isKeypad = key.Location == 3,
            });
        }

        /// <summary>
        /// Dispatches a CDP <c>Input.dispatchKeyEvent</c> <c>keyUp</c>.
        /// </summary>
        public Task KeyUpAsync(IReadOnlyCollection<Input.KeyboardModifier> modifiers, Input.KeyDefinition key)
        {
            return _session.SendAsync("Input.dispatchKeyEvent", new
            {
                type = "keyUp",
                modifiers = modifiers.ToCdpMask(),
                key = key.Key,
                windowsVirtualKeyCode = key.KeyCodeWithoutLocation == 0 ? key.KeyCode : key.KeyCodeWithoutLocation,
                code = key.Code,
                location = key.Location,
            });
        }

        /// <summary>
        /// Dispatches a CDP <c>Input.insertText</c> command — used for characters that are
        /// not in the US keyboard layout or when inserting literal text.
        /// </summary>
        public Task InsertTextAsync(string text)
        {
            return _session.SendAsync("Input.insertText", new { text });
        }
    }
}
