/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
#pragma warning disable CA1062
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightSharp
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
