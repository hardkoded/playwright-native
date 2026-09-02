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
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy expanded-parameter helpers over official <see cref="IKeyboard"/>.
    /// </summary>
    public static class KeyboardCompatExtensions
    {
        /// <summary>Legacy expanded-parameter press.</summary>
        public static Task PressAsync(this IKeyboard keyboard, string key, float? delay = default)
            => keyboard.PressAsync(key, delay.HasValue ? new KeyboardPressOptions { Delay = delay } : null);

        /// <summary>Legacy expanded-parameter type.</summary>
        public static Task TypeAsync(this IKeyboard keyboard, string text, float? delay = default)
            => keyboard.TypeAsync(text, delay.HasValue ? new KeyboardTypeOptions { Delay = delay } : null);
    }
}
