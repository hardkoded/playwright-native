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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PlaywrightSharp.Firefox
{
    /// <summary>Public <see cref="IDialog"/> wrapping <see cref="FFDialog"/>.</summary>
    internal sealed partial class FirefoxDialog : IDialog
    {
        private readonly FFDialog _dialog;

        internal FirefoxDialog(FFDialog dialog, IPage page)
        {
            _dialog = dialog;
            Page = page;
        }

        /// <inheritdoc/>
        public IPage Page { get; }

        /// <inheritdoc/>
        public string DefaultValue => _dialog.DefaultValue ?? string.Empty;

        /// <inheritdoc/>
        public string Message => _dialog.Message;

        /// <inheritdoc/>
        public string Type => _dialog.Type;

        /// <inheritdoc/>
        public Task AcceptAsync(string promptText = null) => _dialog.AcceptAsync(promptText);

        /// <inheritdoc/>
        public Task DismissAsync() => _dialog.DismissAsync();
    }
}
