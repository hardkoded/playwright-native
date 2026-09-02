/*
 * MIT License
 *
 * Copyright (c) 2020 Darío Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PlaywrightNative.Firefox
{
    /// <summary>
    /// Lightweight frame manager for <see cref="FFPage"/> — tracks the main frame
    /// and fires navigation events.
    /// </summary>
    internal class FFFrameManager
    {
        private readonly FFPage _page;

        /// <summary>
        /// Initializes a new instance of the <see cref="FFFrameManager"/> class.
        /// </summary>
        /// <param name="page">The owning page.</param>
        public FFFrameManager(FFPage page) => _page = page;

        /// <summary>
        /// Handles <c>Page.navigationCommitted</c> events.
        /// </summary>
        /// <param name="parameters">The event payload.</param>
        internal void OnNavigationCommitted(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            if (parameters.Value.TryGetProperty("url", out JsonElement urlEl)
                && urlEl.ValueKind == JsonValueKind.String)
            {
                _page.Url = urlEl.GetString();
            }
        }

        /// <summary>
        /// Handles <c>Page.frameAttached</c> events.
        /// </summary>
        /// <param name="parameters">The event payload.</param>
        internal void OnFrameAttached(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;
            string frameId = payload.TryGetProperty("frameId", out JsonElement idEl)
                ? idEl.GetString() : string.Empty;
            string parentFrameId = payload.TryGetProperty("parentFrameId", out JsonElement parentEl)
                ? parentEl.GetString() : string.Empty;

            if (!string.IsNullOrEmpty(frameId) && string.IsNullOrEmpty(parentFrameId))
            {
                _page.RememberMainFrameId(frameId, worldName: string.Empty);
            }
        }

        /// <summary>
        /// Handles <c>Page.frameDetached</c> events.
        /// </summary>
        /// <param name="parameters">The event payload.</param>
        internal void OnFrameDetached(JsonElement? parameters)
        {
        }
    }
}
