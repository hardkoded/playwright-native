/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;

namespace PlaywrightSharp.Chromium
{
    /// <summary>Public <see cref="IDialog"/> wrapping <see cref="CRDialog"/>.</summary>
    internal sealed partial class ChromiumDialog : IDialog
    {
        private readonly CRDialog _crDialog;

        internal ChromiumDialog(CRDialog crDialog, IPage page)
        {
            _crDialog = crDialog ?? throw new ArgumentNullException(nameof(crDialog));
            Page = page;
        }

        /// <inheritdoc/>
        public IPage Page { get; }

        /// <inheritdoc/>
        public string Type => _crDialog.Type;

        /// <inheritdoc/>
        public string Message => _crDialog.Message;

        /// <inheritdoc/>
        public string DefaultValue => _crDialog.DefaultValue;

        /// <inheritdoc/>
        public Task AcceptAsync(string promptText = default) => _crDialog.AcceptAsync(promptText);

        /// <inheritdoc/>
        public Task DismissAsync() => _crDialog.DismissAsync();
    }
}
