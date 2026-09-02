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
