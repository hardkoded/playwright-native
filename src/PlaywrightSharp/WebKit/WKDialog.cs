/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;

namespace PlaywrightSharp.WebKit
{
    /// <summary>
    /// WebKit JavaScript dialog. Replies via the page-proxy <c>Dialog</c> domain
    /// (upstream <c>wkPage._onDialog</c>).
    /// </summary>
    internal sealed partial class WKDialog : IDialog
    {
        private readonly WKSession _pageProxySession;
        private bool _handled;

        /// <summary>
        /// Initializes a new instance of the <see cref="WKDialog"/> class.
        /// </summary>
        /// <param name="pageProxySession">The page-proxy session.</param>
        /// <param name="type">Dialog type (<c>alert</c>, <c>confirm</c>, <c>prompt</c>, <c>beforeunload</c>).</param>
        /// <param name="message">Dialog message text.</param>
        /// <param name="defaultValue">Default prompt value.</param>
        /// <param name="page">The page that opened the dialog.</param>
        internal WKDialog(WKSession pageProxySession, string type, string message, string defaultValue, IPage page)
        {
            _pageProxySession = pageProxySession;
            Type = type ?? string.Empty;
            Message = message ?? string.Empty;
            DefaultValue = defaultValue ?? string.Empty;
            Page = page;
        }

        /// <inheritdoc/>
        public IPage Page { get; }

        /// <inheritdoc/>
        public string DefaultValue { get; }

        /// <inheritdoc/>
        public string Message { get; }

        /// <inheritdoc/>
        public string Type { get; }

        /// <inheritdoc/>
        public async Task AcceptAsync(string promptText = default)
        {
            if (_handled)
            {
                return;
            }

            _handled = true;
            await _pageProxySession.SendAsync("Dialog.handleJavaScriptDialog", new
            {
                accept = true,
                promptText = promptText ?? string.Empty,
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task DismissAsync()
        {
            if (_handled)
            {
                return;
            }

            _handled = true;
            await _pageProxySession.SendAsync("Dialog.handleJavaScriptDialog", new
            {
                accept = false,
            }).ConfigureAwait(false);
        }
    }
}
