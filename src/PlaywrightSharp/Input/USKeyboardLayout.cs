/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Subset of upstream usKeyboardLayout.ts — covers the keys exercised by
 * tests. Extend as additional keys are needed by later tests.
 */
using System.Collections.Generic;

namespace PlaywrightSharp.Input
{
    /// <summary>
    /// Static mapping from key name (e.g. "KeyA", "Digit3", "Enter", "ArrowLeft") to the
    /// <see cref="KeyDefinition"/> used when dispatching CDP <c>Input.dispatchKeyEvent</c>.
    /// Also maps lowercase <c>event.key</c> aliases ("a", "3") and display aliases
    /// ("Enter" — same as "Enter", but exposed for lookup by semantic name).
    /// </summary>
    internal static class USKeyboardLayout
    {
        private static readonly Dictionary<string, KeyDefinition> _byName = BuildLayout();

        /// <summary>
        /// Attempts to resolve a user-supplied key string to a <see cref="KeyDefinition"/>.
        /// Accepts "KeyA", "a", "A", "Digit1", "1", "Enter", "ArrowLeft", etc.
        /// </summary>
        /// <param name="keyName">The key name to look up.</param>
        /// <returns>The matching definition, or <c>null</c> if not mapped.</returns>
        internal static KeyDefinition TryResolve(string keyName)
        {
            if (string.IsNullOrEmpty(keyName))
            {
                return null;
            }

            return _byName.TryGetValue(keyName, out KeyDefinition def) ? def : null;
        }

        private static Dictionary<string, KeyDefinition> BuildLayout()
        {
            Dictionary<string, KeyDefinition> map = new();

            // Letters: KeyA..KeyZ with Shift variants pointing at uppercase text.
            for (int i = 0; i < 26; i++)
            {
                char lower = (char)('a' + i);
                char upper = (char)('A' + i);
                int keyCode = 'A' + i;
                string code = "Key" + upper;

                KeyDefinition shifted = new()
                {
                    KeyCode = keyCode,
                    KeyCodeWithoutLocation = keyCode,
                    Code = code,
                    Key = upper.ToString(),
                    Text = upper.ToString(),
                };

                KeyDefinition def = new()
                {
                    KeyCode = keyCode,
                    KeyCodeWithoutLocation = keyCode,
                    Code = code,
                    Key = lower.ToString(),
                    Text = lower.ToString(),
                    Shifted = shifted,
                };

                map[code] = def;
                map[lower.ToString()] = def;
                map[upper.ToString()] = shifted;
            }

            // Digits: Digit0..Digit9 with common Shift punctuation.
            (int Code, string Key, string Shifted)[] digits =
            [
                (48, "0", ")"),
                (49, "1", "!"),
                (50, "2", "@"),
                (51, "3", "#"),
                (52, "4", "$"),
                (53, "5", "%"),
                (54, "6", "^"),
                (55, "7", "&"),
                (56, "8", "*"),
                (57, "9", "("),
            ];

            foreach ((int Code, string Key, string Shifted) entry in digits)
            {
                string digitCode = "Digit" + entry.Key;

                KeyDefinition shiftedDef = new()
                {
                    KeyCode = entry.Code,
                    KeyCodeWithoutLocation = entry.Code,
                    Code = digitCode,
                    Key = entry.Shifted,
                    Text = entry.Shifted,
                };

                KeyDefinition def = new()
                {
                    KeyCode = entry.Code,
                    KeyCodeWithoutLocation = entry.Code,
                    Code = digitCode,
                    Key = entry.Key,
                    Text = entry.Key,
                    Shifted = shiftedDef,
                };

                map[digitCode] = def;
                map[entry.Key] = def;
                map[entry.Shifted] = shiftedDef;
            }

            // Common punctuation / whitespace, including Shift variants used by page-keyboard.spec.ts.
            KeyDefinition space = AddKey(map, "Space", 32, " ", " ");
            map[" "] = space;
            AddKey(map, "Tab", 9, "Tab", "\t");
            AddKey(map, "Backspace", 8, "Backspace", string.Empty);
            KeyDefinition enter = AddKey(map, "Enter", 13, "Enter", "\r");
            map["\r"] = enter;
            map["\n"] = enter;
            AddKey(map, "Escape", 27, "Escape", string.Empty);
            AddPrintable(map, "Period", 190, ".", ">");
            AddPrintable(map, "Comma", 188, ",", "<");
            AddPrintable(map, "Minus", 189, "-", "_");
            AddPrintable(map, "Equal", 187, "=", "+");
            AddPrintable(map, "Semicolon", 186, ";", ":");
            AddPrintable(map, "Backquote", 192, "`", "~");
            AddPrintable(map, "Slash", 191, "/", "?");
            AddPrintable(map, "Backslash", 220, "\\", "|");
            AddPrintable(map, "BracketLeft", 219, "[", "{");
            AddPrintable(map, "BracketRight", 221, "]", "}");
            AddPrintable(map, "Quote", 222, "'", "\"");

            // Arrow keys.
            AddKey(map, "ArrowLeft", 37, "ArrowLeft", string.Empty);
            AddKey(map, "ArrowUp", 38, "ArrowUp", string.Empty);
            AddKey(map, "ArrowRight", 39, "ArrowRight", string.Empty);
            AddKey(map, "ArrowDown", 40, "ArrowDown", string.Empty);

            // Navigation.
            AddKey(map, "Home", 36, "Home", string.Empty);
            AddKey(map, "End", 35, "End", string.Empty);
            AddKey(map, "PageUp", 33, "PageUp", string.Empty);
            AddKey(map, "PageDown", 34, "PageDown", string.Empty);
            AddKey(map, "Delete", 46, "Delete", string.Empty);
            AddKey(map, "Insert", 45, "Insert", string.Empty);

            // Function and media keys.
            for (int i = 1; i <= 12; i++)
            {
                string name = "F" + i;
                AddKey(map, name, 111 + i, name, string.Empty);
            }

            AddKey(map, "AudioVolumeMute", 173, "AudioVolumeMute", string.Empty);
            AddKey(map, "AudioVolumeDown", 174, "AudioVolumeDown", string.Empty);
            AddKey(map, "AudioVolumeUp", 175, "AudioVolumeUp", string.Empty);
            AddKey(map, "MediaTrackNext", 176, "MediaTrackNext", string.Empty);
            AddKey(map, "MediaTrackPrevious", 177, "MediaTrackPrevious", string.Empty);
            AddKey(map, "MediaPlayPause", 179, "MediaPlayPause", string.Empty);

            AddKey(map, "NumpadEnter", 13, "Enter", "\r", location: 3);
            AddKey(map, "NumpadSubtract", 109, "-", string.Empty, location: 3);

            // Modifier keys — both location variants share keyCodeWithoutLocation.
            map["ShiftLeft"] = new KeyDefinition { KeyCode = 160, KeyCodeWithoutLocation = 16, Code = "ShiftLeft", Key = "Shift", Location = 1 };
            map["ShiftRight"] = new KeyDefinition { KeyCode = 161, KeyCodeWithoutLocation = 16, Code = "ShiftRight", Key = "Shift", Location = 2 };
            map["Shift"] = map["ShiftLeft"];

            map["ControlLeft"] = new KeyDefinition { KeyCode = 162, KeyCodeWithoutLocation = 17, Code = "ControlLeft", Key = "Control", Location = 1 };
            map["ControlRight"] = new KeyDefinition { KeyCode = 163, KeyCodeWithoutLocation = 17, Code = "ControlRight", Key = "Control", Location = 2 };
            map["Control"] = map["ControlLeft"];

            map["AltLeft"] = new KeyDefinition { KeyCode = 164, KeyCodeWithoutLocation = 18, Code = "AltLeft", Key = "Alt", Location = 1 };
            map["AltRight"] = new KeyDefinition { KeyCode = 165, KeyCodeWithoutLocation = 18, Code = "AltRight", Key = "Alt", Location = 2 };
            map["Alt"] = map["AltLeft"];

            map["MetaLeft"] = new KeyDefinition { KeyCode = 91, KeyCodeWithoutLocation = 91, Code = "MetaLeft", Key = "Meta", Location = 1 };
            map["MetaRight"] = new KeyDefinition { KeyCode = 92, KeyCodeWithoutLocation = 91, Code = "MetaRight", Key = "Meta", Location = 2 };
            map["Meta"] = map["MetaLeft"];

            return map;
        }

        private static KeyDefinition AddKey(Dictionary<string, KeyDefinition> map, string name, int keyCode, string key, string text, int location = 0)
        {
            KeyDefinition def = new()
            {
                KeyCode = keyCode,
                KeyCodeWithoutLocation = keyCode,
                Code = name,
                Key = key,
                Text = text,
                Location = location,
            };

            map[name] = def;
            return def;
        }

        private static void AddPrintable(Dictionary<string, KeyDefinition> map, string code, int keyCode, string key, string shiftedKey)
        {
            KeyDefinition shifted = new()
            {
                KeyCode = keyCode,
                KeyCodeWithoutLocation = keyCode,
                Code = code,
                Key = shiftedKey,
                Text = shiftedKey,
            };

            KeyDefinition def = new()
            {
                KeyCode = keyCode,
                KeyCodeWithoutLocation = keyCode,
                Code = code,
                Key = key,
                Text = key,
                Shifted = shifted,
            };

            map[code] = def;
            map[key] = def;
            map[shiftedKey] = shifted;
        }
    }
}
