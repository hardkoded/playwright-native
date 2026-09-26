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
    /// <summary>Public <see cref="IMouse"/> wrapping <see cref="Input.Mouse"/>.</summary>
    internal sealed partial class ChromiumMouse : IMouse
    {
        private readonly Input.Mouse _mouse;
        private readonly IBrowserContext _context;

        internal ChromiumMouse(Input.Mouse mouse, IBrowserContext context)
        {
            _mouse = mouse ?? throw new ArgumentNullException(nameof(mouse));
            _context = context;
        }

        /// <inheritdoc/>
        public Task ClickAsync(float x, float y, MouseButton button = default, int? clickCount = null, float? delay = null, int? steps = null)
            => _mouse.ClickAsync(
                x,
                y,
                ToInputMouseButton(button),
                clickCount ?? 1,
                delay.HasValue ? (int)delay.Value : 0,
                steps ?? 1);

        /// <inheritdoc/>
        public Task DblClickAsync(float x, float y, MouseButton button = default, float? delay = null, int? steps = null)
            => Helpers.ActionTrace.RunAsync(_context, "Double click", "Mouse", "dblclick", () => _mouse.DoubleClickAsync(
                x,
                y,
                ToInputMouseButton(button),
                delay.HasValue ? (int)delay.Value : 0,
                steps ?? 1));

        /// <inheritdoc/>
        public Task DownAsync(MouseButton button = default, int? clickCount = null)
            => _mouse.DownAsync(ToInputMouseButton(button), clickCount ?? 1);

        /// <inheritdoc/>
        public Task MoveAsync(float x, float y, int? steps = null)
            => Helpers.ActionTrace.RunAsync(_context, "Mouse move", "Mouse", "move", () => _mouse.MoveAsync(x, y, steps ?? 1));

        /// <inheritdoc/>
        public Task UpAsync(MouseButton button = default, int? clickCount = null)
            => _mouse.UpAsync(ToInputMouseButton(button), clickCount ?? 1);

        /// <inheritdoc/>
        public Task WheelAsync(float deltaX, float deltaY)
            => _mouse.WheelAsync(deltaX, deltaY);

        private static Input.MouseButton ToInputMouseButton(MouseButton button)
            => button switch
            {
                MouseButton.Right => Input.MouseButton.Right,
                MouseButton.Middle => Input.MouseButton.Middle,
                _ => Input.MouseButton.Left,
            };

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        // Official IMouse is options-only. Compat extensions (MoveAsync(x,y,steps), …)
        // call these; stubs here made every options-path click a silent no-op.
        Task IMouse.ClickAsync(float x, float y, MouseClickOptions options)
            => ClickAsync(x, y, options?.Button ?? default, options?.ClickCount, options?.Delay);

        Task IMouse.DblClickAsync(float x, float y, MouseDblClickOptions options)
            => DblClickAsync(x, y, options?.Button ?? default, options?.Delay);

        Task IMouse.DownAsync(MouseDownOptions options)
            => DownAsync(options?.Button ?? default, options?.ClickCount);

        Task IMouse.MoveAsync(float x, float y, MouseMoveOptions options)
            => MoveAsync(x, y, options?.Steps);

        Task IMouse.UpAsync(MouseUpOptions options)
            => UpAsync(options?.Button ?? default, options?.ClickCount);
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
