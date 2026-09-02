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

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Represents a JavaScript dialog (alert, confirm, prompt, beforeunload) that the
    /// page is waiting on. Must be answered via <see cref="AcceptAsync"/> or
    /// <see cref="DismissAsync"/> — otherwise the page will block indefinitely.
    /// </summary>
    internal class CRDialog
    {
        private readonly CRSession _session;
        private bool _handled;

        /// <summary>
        /// Initializes a new instance of the <see cref="CRDialog"/> class.
        /// </summary>
        /// <param name="session">The CDP session to reply on.</param>
        /// <param name="type">Dialog type: "alert", "confirm", "prompt", or "beforeunload".</param>
        /// <param name="message">The dialog message text.</param>
        /// <param name="defaultValue">For prompt dialogs, the default input value.</param>
        public CRDialog(CRSession session, string type, string message, string defaultValue)
        {
            _session = session;
            Type = type ?? string.Empty;
            Message = message ?? string.Empty;
            DefaultValue = defaultValue ?? string.Empty;
        }

        /// <summary>Gets the dialog type (alert/confirm/prompt/beforeunload).</summary>
        internal string Type { get; }

        /// <summary>Gets the dialog message.</summary>
        internal string Message { get; }

        /// <summary>Gets the default prompt input value (empty for non-prompt dialogs).</summary>
        internal string DefaultValue { get; }

        /// <summary>
        /// Accepts the dialog. For prompt dialogs, supply <paramref name="promptText"/>.
        /// </summary>
        /// <param name="promptText">Text to return from a prompt dialog.</param>
        /// <returns>A task that completes when the reply has been sent.</returns>
        internal async Task AcceptAsync(string promptText = null)
        {
            if (_handled)
            {
                return;
            }

            _handled = true;
            await _session.SendAsync("Page.handleJavaScriptDialog", new
            {
                accept = true,
                promptText = promptText ?? string.Empty,
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Dismisses (cancels) the dialog.
        /// </summary>
        /// <returns>A task that completes when the reply has been sent.</returns>
        internal async Task DismissAsync()
        {
            if (_handled)
            {
                return;
            }

            _handled = true;
            await _session.SendAsync("Page.handleJavaScriptDialog", new
            {
                accept = false,
            }).ConfigureAwait(false);
        }
    }
}
