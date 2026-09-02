/*
 * MIT License
 *
 * Copyright (c) 2020 Darío Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;

namespace PlaywrightSharp.WebKit
{
    /// <summary>Public <see cref="ITouchscreen"/> wrapping <see cref="Input.Touchscreen"/>.</summary>
    internal sealed partial class WebKitTouchscreen : ITouchscreen
    {
        private readonly Input.Touchscreen _touchscreen;

        internal WebKitTouchscreen(Input.Touchscreen touchscreen)
        {
            _touchscreen = touchscreen ?? throw new ArgumentNullException(nameof(touchscreen));
        }

        /// <inheritdoc/>
        public Task TapAsync(float x, float y) => _touchscreen.TapAsync(x, y);
    }
}
