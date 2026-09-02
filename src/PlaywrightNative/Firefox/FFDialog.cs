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
    /// Represents a JavaScript dialog in Firefox.
    /// </summary>
    internal class FFDialog
    {
        private readonly FFSession _session;
        private readonly JsonElement _payload;

        /// <summary>
        /// Initializes a new instance of the <see cref="FFDialog"/> class.
        /// </summary>
        /// <param name="session">The Juggler page session.</param>
        /// <param name="payload">The <c>Page.dialogOpened</c> payload.</param>
        public FFDialog(FFSession session, JsonElement payload)
        {
            _session = session;
            _payload = payload;

            Type = payload.TryGetProperty("type", out JsonElement typeEl) ? typeEl.GetString() : "alert";
            Message = payload.TryGetProperty("message", out JsonElement msgEl) ? msgEl.GetString() : string.Empty;
            DefaultValue = payload.TryGetProperty("defaultValue", out JsonElement defEl) ? defEl.GetString() : string.Empty;
        }

        /// <summary>Gets the default prompt input value (empty for non-prompt dialogs).</summary>
        internal string DefaultValue { get; }

        /// <summary>Gets the dialog type (alert, confirm, prompt, beforeunload).</summary>
        internal string Type { get; }

        /// <summary>Gets the dialog message.</summary>
        internal string Message { get; }

        /// <summary>
        /// Accepts the dialog.
        /// </summary>
        /// <param name="promptText">Optional text for prompt dialogs.</param>
        internal Task AcceptAsync(string promptText = null)
            => _session.SendAsync("Page.handleDialog", new
            {
                accept = true,
                userText = promptText,
            });

        /// <summary>
        /// Dismisses the dialog.
        /// </summary>
        internal Task DismissAsync()
            => _session.SendAsync("Page.handleDialog", new { accept = false });
    }
}
