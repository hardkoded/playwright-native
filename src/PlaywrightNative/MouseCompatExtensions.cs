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
    /// Legacy expanded-parameter helpers over official <see cref="IMouse"/>.
    /// </summary>
    public static class MouseCompatExtensions
    {
        /// <summary>Legacy expanded-parameter move.</summary>
        public static Task MoveAsync(this IMouse mouse, float x, float y, int? steps = default)
            => mouse.MoveAsync(x, y, steps.HasValue ? new MouseMoveOptions { Steps = steps } : null);

        /// <summary>Legacy expanded-parameter mouse down.</summary>
        public static Task DownAsync(this IMouse mouse, MouseButton button = default, int? clickCount = default)
            => mouse.DownAsync(new MouseDownOptions
            {
                Button = button,
                ClickCount = clickCount,
            });

        /// <summary>Legacy expanded-parameter mouse up.</summary>
        public static Task UpAsync(this IMouse mouse, MouseButton button = default, int? clickCount = default)
            => mouse.UpAsync(new MouseUpOptions
            {
                Button = button,
                ClickCount = clickCount,
            });

        /// <summary>Legacy expanded-parameter click.</summary>
        public static Task ClickAsync(
            this IMouse mouse,
            float x,
            float y,
            MouseButton button = default,
            int? clickCount = default,
            float? delay = default,
            int? steps = default)
            => mouse.ClickAsync(x, y, new MouseClickOptions
            {
                Button = button,
                ClickCount = clickCount,
                Delay = delay,
            });

        /// <summary>Legacy expanded-parameter double click.</summary>
        public static Task DblClickAsync(
            this IMouse mouse,
            float x,
            float y,
            MouseButton button = default,
            float? delay = default,
            int? steps = default)
            => mouse.DblClickAsync(x, y, new MouseDblClickOptions
            {
                Button = button,
                Delay = delay,
            });
    }
}
