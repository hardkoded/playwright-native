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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PlaywrightNative.Firefox
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
