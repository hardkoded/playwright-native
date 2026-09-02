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

namespace PlaywrightSharp.Firefox
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
