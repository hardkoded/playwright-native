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
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Holds requested keyboard modifiers for the duration of an input action.
    /// </summary>
    internal static class ActionModifiers
    {
        /// <summary>
        /// Presses <paramref name="modifiers"/>, runs <paramref name="action"/>, then
        /// releases them in reverse order.
        /// </summary>
        /// <param name="modifiers">Modifiers to hold, or <see langword="null"/>.</param>
        /// <param name="downAsync">Key-down callback.</param>
        /// <param name="upAsync">Key-up callback.</param>
        /// <param name="action">The input action to wrap.</param>
        /// <returns>A task that completes when the action and cleanup finish.</returns>
        internal static async Task RunAsync(
            IEnumerable<KeyboardModifier> modifiers,
            Func<string, Task> downAsync,
            Func<string, Task> upAsync,
            Func<Task> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            List<string> pressed = new();
            if (modifiers != null && downAsync != null)
            {
                foreach (KeyboardModifier modifier in modifiers)
                {
                    string key = ToKeyName(modifier);
                    if (key == null)
                    {
                        continue;
                    }

                    await downAsync(key).ConfigureAwait(false);
                    pressed.Add(key);
                }
            }

            try
            {
                await action().ConfigureAwait(false);
            }
            finally
            {
                if (upAsync != null)
                {
                    for (int i = pressed.Count - 1; i >= 0; i--)
                    {
                        await upAsync(pressed[i]).ConfigureAwait(false);
                    }
                }
            }
        }

        private static string ToKeyName(KeyboardModifier modifier)
            => modifier switch
            {
                KeyboardModifier.Alt => "Alt",
                KeyboardModifier.Control => "Control",
                KeyboardModifier.ControlOrMeta => OperatingSystem.IsMacOS() ? "Meta" : "Control",
                KeyboardModifier.Meta => "Meta",
                KeyboardModifier.Shift => "Shift",
                _ => null,
            };
    }
}
