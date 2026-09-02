/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlaywrightNative.Input
{
    /// <summary>
    /// Low-level keyboard transport. Mirrors upstream <c>input.RawKeyboard</c>: a per-browser
    /// implementation that translates the simulator's high-level requests into protocol commands
    /// (CDP for Chromium, WIP for WebKit). The shared <see cref="Keyboard"/> simulator owns the
    /// pressed-key/modifier state and drives this interface.
    /// </summary>
    internal interface IRawKeyboard
    {
        /// <summary>
        /// Dispatches a key-down event for <paramref name="key"/> while <paramref name="modifiers"/> are held.
        /// </summary>
        /// <param name="modifiers">The modifiers currently held down.</param>
        /// <param name="key">The resolved key definition to dispatch.</param>
        /// <param name="autoRepeat">Whether this is an auto-repeat (the key was already down).</param>
        /// <returns>A task that completes when the event has been dispatched.</returns>
        Task KeyDownAsync(IReadOnlyCollection<KeyboardModifier> modifiers, KeyDefinition key, bool autoRepeat);

        /// <summary>
        /// Dispatches a key-up event for <paramref name="key"/> while <paramref name="modifiers"/> are held.
        /// </summary>
        /// <param name="modifiers">The modifiers currently held down.</param>
        /// <param name="key">The resolved key definition to dispatch.</param>
        /// <returns>A task that completes when the event has been dispatched.</returns>
        Task KeyUpAsync(IReadOnlyCollection<KeyboardModifier> modifiers, KeyDefinition key);

        /// <summary>
        /// Inserts arbitrary text without simulating individual keystrokes.
        /// </summary>
        /// <param name="text">The text to insert.</param>
        /// <returns>A task that completes when the text has been inserted.</returns>
        Task InsertTextAsync(string text);
    }
}
