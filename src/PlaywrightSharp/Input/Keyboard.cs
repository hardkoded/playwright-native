/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PlaywrightSharp.Input
{
    /// <summary>
    /// Public-facing keyboard simulator. Tracks currently-pressed keys and modifiers, and
    /// composes high-level operations (Type, Press, DownAndUp) on top of the raw transport.
    /// </summary>
    internal class Keyboard
    {
        private readonly IRawKeyboard _raw;
        private readonly HashSet<string> _pressedKeys = new(StringComparer.Ordinal);
        private readonly HashSet<KeyboardModifier> _pressedModifiers = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="Keyboard"/> class.
        /// </summary>
        /// <param name="raw">The low-level keyboard transport.</param>
        public Keyboard(IRawKeyboard raw)
        {
            _raw = raw ?? throw new ArgumentNullException(nameof(raw));
        }

        /// <summary>
        /// The set of modifier keys currently held down. Exposed for Mouse/Touchscreen coordination.
        /// </summary>
        internal IReadOnlyCollection<KeyboardModifier> PressedModifiers => _pressedModifiers;

        /// <summary>
        /// Presses a key (keyDown) and leaves it held. Updates modifier state.
        /// </summary>
        /// <param name="key">The key name (e.g. "a", "Shift", "Enter").</param>
        internal async Task DownAsync(string key)
        {
            KeyDefinition def = ResolveOrThrow(key);
            bool autoRepeat = _pressedKeys.Contains(def.Key);
            _pressedKeys.Add(def.Key);
            KeyboardModifier modifier = ModifierFromKey(def.Key);
            if (modifier != KeyboardModifier.None)
            {
                _pressedModifiers.Add(modifier);
            }

            await _raw.KeyDownAsync(_pressedModifiers, EffectiveKey(def), autoRepeat).ConfigureAwait(false);
        }

        /// <summary>
        /// Releases a previously-pressed key (keyUp). Updates modifier state.
        /// </summary>
        /// <param name="key">The key name.</param>
        internal async Task UpAsync(string key)
        {
            KeyDefinition def = ResolveOrThrow(key);
            _pressedKeys.Remove(def.Key);
            KeyboardModifier modifier = ModifierFromKey(def.Key);
            if (modifier != KeyboardModifier.None)
            {
                _pressedModifiers.Remove(modifier);
            }

            await _raw.KeyUpAsync(_pressedModifiers, EffectiveKey(def)).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a complete keystroke: keyDown, optional delay, keyUp. Supports
        /// modifier prefixes like "Shift+a", "Control+c", or "ControlOrMeta+a"
        /// (<c>Meta</c> on macOS, <c>Control</c> elsewhere).
        /// </summary>
        /// <param name="key">The key or chord.</param>
        /// <param name="delayMs">Optional delay between down and up in milliseconds.</param>
        internal async Task PressAsync(string key, int delayMs = 0)
        {
            List<string> parts = SplitKeyChord(key);
            List<string> modifiers = new();
            string mainKey = parts.Count > 0 ? parts[^1] : string.Empty;

            for (int i = 0; i < parts.Count - 1; i++)
            {
                modifiers.Add(parts[i]);
            }

            // Track modifiers that were actually pressed so the finally block
            // releases only those, even if a mid-sequence failure aborted the down phase.
            // Without this, a throw here would leave stuck modifiers on the keyboard
            // and leak into the next operation on this Page.
            int modifiersPressed = 0;
            bool mainKeyPressed = false;

            try
            {
                foreach (string mod in modifiers)
                {
                    await DownAsync(mod).ConfigureAwait(false);
                    modifiersPressed++;
                }

                await DownAsync(mainKey).ConfigureAwait(false);
                mainKeyPressed = true;

                if (delayMs > 0)
                {
                    await Task.Delay(delayMs).ConfigureAwait(false);
                }

                await UpAsync(mainKey).ConfigureAwait(false);
                mainKeyPressed = false;
            }
            finally
            {
                if (mainKeyPressed)
                {
                    try
                    {
                        await UpAsync(mainKey).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Swallow; nothing else to do on cleanup path.
                    }
                }

                for (int i = modifiersPressed - 1; i >= 0; i--)
                {
                    try
                    {
                        await UpAsync(modifiers[i]).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Swallow; cleanup cannot unwind itself.
                    }
                }
            }
        }

        /// <summary>
        /// Types each character of <paramref name="text"/>: if the character is in the layout,
        /// presses it; otherwise dispatches via <c>Input.insertText</c>.
        /// </summary>
        /// <param name="text">The text to type.</param>
        /// <param name="delayMs">Delay between characters in milliseconds.</param>
        internal async Task TypeAsync(string text, int delayMs = 0)
        {
            foreach (Rune rune in text.EnumerateRunes())
            {
                string asString = rune.ToString();
                if (USKeyboardLayout.TryResolve(asString) != null)
                {
                    await PressAsync(asString, delayMs).ConfigureAwait(false);
                }
                else
                {
                    await _raw.InsertTextAsync(asString).ConfigureAwait(false);

                    if (delayMs > 0)
                    {
                        await Task.Delay(delayMs).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// Dispatches <c>Input.insertText</c> to insert arbitrary text without simulating keystrokes.
        /// </summary>
        /// <param name="text">The text to insert.</param>
        internal Task InsertTextAsync(string text) => _raw.InsertTextAsync(text);

        private static KeyboardModifier ModifierFromKey(string key)
        {
            return key switch
            {
                "Alt" => KeyboardModifier.Alt,
                "Control" => KeyboardModifier.Control,
                "Meta" => KeyboardModifier.Meta,
                "Shift" => KeyboardModifier.Shift,
                _ => KeyboardModifier.None,
            };
        }

        private static KeyDefinition ResolveOrThrow(string key)
        {
            KeyDefinition def = USKeyboardLayout.TryResolve(ResolveControlOrMeta(key));
            if (def == null)
            {
                throw new PlaywrightSharpException($"Unknown key: \"{key}\"");
            }

            return def;
        }

        private static string ResolveControlOrMeta(string key)
        {
            if (!string.Equals(key, "ControlOrMeta", StringComparison.Ordinal))
            {
                return key;
            }

            return OperatingSystem.IsMacOS() ? "Meta" : "Control";
        }

        /// <summary>
        /// Splits a Playwright chord such as <c>Shift++</c>, <c>Control+Shift+~</c>, or <c>+</c>
        /// into tokens. A trailing or doubled <c>+</c> is the literal plus key, not an empty part.
        /// </summary>
        /// <param name="key">The key or chord string.</param>
        /// <returns>Modifier tokens followed by the main key.</returns>
        private static List<string> SplitKeyChord(string key)
        {
            List<string> parts = new();
            string building = string.Empty;
            foreach (char ch in key)
            {
                if (ch == '+' && building.Length > 0)
                {
                    parts.Add(building);
                    building = string.Empty;
                }
                else
                {
                    building += ch;
                }
            }

            parts.Add(building);
            return parts;
        }

        private KeyDefinition EffectiveKey(KeyDefinition def)
        {
            // When Shift is held and this key has a shifted variant, use it (affects text output).
            KeyDefinition resolved = def;
            if (_pressedModifiers.Contains(KeyboardModifier.Shift) && def.Shifted != null)
            {
                resolved = def.Shifted;
            }

            // Non-Shift modifiers suppress insertable text so chords (Ctrl+A, Alt+Arrow)
            // dispatch as shortcuts instead of typing the character. Mirrors upstream input.ts.
            bool suppressText = _pressedModifiers.Contains(KeyboardModifier.Control)
                || _pressedModifiers.Contains(KeyboardModifier.Alt)
                || _pressedModifiers.Contains(KeyboardModifier.Meta);
            if (!suppressText || string.IsNullOrEmpty(resolved.Text))
            {
                return resolved;
            }

            return new KeyDefinition
            {
                KeyCode = resolved.KeyCode,
                KeyCodeWithoutLocation = resolved.KeyCodeWithoutLocation,
                Code = resolved.Code,
                Key = resolved.Key,
                Text = string.Empty,
                Location = resolved.Location,
                Shifted = resolved.Shifted,
            };
        }
    }
}
