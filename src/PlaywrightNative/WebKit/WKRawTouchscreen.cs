/*
 * Copyright (c) 2020 Darío Kondratiuk
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
using System.Collections.Generic;
using System.Threading.Tasks;
using PlaywrightNative.Input;

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// WebKit raw touchscreen. Sends WIP <c>Input.dispatchTapEvent</c> on the page-proxy
    /// session. Mirrors upstream <c>wkInput.ts</c> <c>RawTouchscreenImpl</c>.
    /// </summary>
    internal sealed class WKRawTouchscreen : IRawTouchscreen
    {
        private readonly WKPage _page;

        /// <summary>
        /// Initializes a new instance of the <see cref="WKRawTouchscreen"/> class.
        /// </summary>
        /// <param name="page">The owning WebKit page.</param>
        public WKRawTouchscreen(WKPage page)
        {
            _page = page;
        }

        /// <inheritdoc/>
        public Task TapAsync(double x, double y, IReadOnlyCollection<Input.KeyboardModifier> modifiers)
        {
            return _page.Session.SendAsync("Input.dispatchTapEvent", new
            {
                x,
                y,
                modifiers = WKRawKeyboard.ToWebKitModifiersMask(modifiers),
            });
        }
    }
}
