/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
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
