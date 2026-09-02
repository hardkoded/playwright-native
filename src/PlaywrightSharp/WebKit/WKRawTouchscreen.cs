/*
 * MIT License
 *
 * Copyright (c) 2020 Darío Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Threading.Tasks;
using PlaywrightSharp.Input;

namespace PlaywrightSharp.WebKit
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
