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
using System;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Tracks the open JavaScript dialog so <see cref="IPage.DialogClosed"/>
    /// fires the same <see cref="IDialog"/> instance after accept, dismiss,
    /// auto-dismiss, or a browser-side close (<c>javascriptDialogClosed</c>).
    /// </summary>
    internal sealed class PageDialogTracker
    {
        private IDialog _open;
        private bool _closedEmitted;

        /// <summary>
        /// Playwright auto-dismisses when neither the page nor the context has
        /// a <c>Dialog</c> subscriber.
        /// </summary>
        /// <param name="pageDialog">The page <see cref="IPage.Dialog"/> event.</param>
        /// <param name="contextHasListeners">True when the owning context has subscribers.</param>
        /// <returns>True when the dialog should be dismissed automatically.</returns>
        internal static bool ShouldAutoDismiss(EventHandler<IDialog> pageDialog, bool contextHasListeners)
            => pageDialog == null && !contextHasListeners;

        /// <summary>
        /// Dismisses <paramref name="dialog"/> when nobody was listening at
        /// open time. Callers must pass the pre-emit listener snapshot:
        /// official <c>dialogDidOpen</c> decides auto-handle before handlers
        /// run, because <c>waitForEvent('dialog')</c> unsubscribes during emit.
        /// </summary>
        /// <param name="dialog">The open dialog.</param>
        /// <param name="pageDialog">
        /// The page <see cref="IPage.Dialog"/> event as it was before emit.
        /// </param>
        /// <param name="contextHasListeners">
        /// True when the owning context had subscribers before emit.
        /// </param>
        internal static void AutoDismissIfNeeded(
            IDialog dialog,
            EventHandler<IDialog> pageDialog,
            bool contextHasListeners)
        {
            if (dialog == null || !ShouldAutoDismiss(pageDialog, contextHasListeners))
            {
                return;
            }

            // Official page.ts: unhandled beforeunload is accepted so library
            // navigations are not aborted; other dialogs are dismissed.
            Task handle;
            if (dialog is TrackedDialog tracked)
            {
                handle = string.Equals(dialog.Type, DialogType.BeforeUnload, StringComparison.Ordinal)
                    ? tracked.AcceptSilentAsync()
                    : tracked.DismissSilentAsync();
            }
            else
            {
                handle = string.Equals(dialog.Type, DialogType.BeforeUnload, StringComparison.Ordinal)
                    ? dialog.AcceptAsync()
                    : dialog.DismissAsync();
            }

            _ = handle.ContinueWith(
                t => _ = t.Exception,
                default,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <summary>
        /// Wraps <paramref name="inner"/> so accept/dismiss emit
        /// <paramref name="emitClosed"/> once with the wrapper instance.
        /// </summary>
        /// <param name="inner">The browser-specific dialog.</param>
        /// <param name="emitClosed">Raised with the public dialog instance.</param>
        /// <returns>The instance pages should expose on <see cref="IPage.Dialog"/>.</returns>
        internal IDialog Wrap(IDialog inner, Action<IDialog> emitClosed)
        {
            TrackedDialog tracked = new TrackedDialog(inner, this, emitClosed);
            _open = tracked;
            _closedEmitted = false;
            return tracked;
        }

        /// <summary>
        /// Called when the browser reports the dialog closed out of band
        /// (<c>Page.javascriptDialogClosed</c>, <c>Dialog.javascriptDialogClosed</c>,
        /// <c>Page.dialogClosed</c>).
        /// </summary>
        /// <param name="emitClosed">Raised with the open dialog, if any.</param>
        internal void OnBrowserClosedDialog(Action<IDialog> emitClosed)
            => EmitClosed(emitClosed, _open);

        /// <summary>
        /// Emits <paramref name="emitClosed"/> once for <paramref name="dialog"/>.
        /// </summary>
        /// <param name="emitClosed">The page <c>DialogClosed</c> raise.</param>
        /// <param name="dialog">The dialog that closed.</param>
        internal void EmitClosed(Action<IDialog> emitClosed, IDialog dialog)
        {
            if (dialog == null || _closedEmitted)
            {
                return;
            }

            _closedEmitted = true;
            emitClosed(dialog);
        }

        private sealed class TrackedDialog : IDialog
        {
            private readonly IDialog _inner;
            private readonly PageDialogTracker _tracker;
            private readonly Action<IDialog> _emitClosed;

            internal TrackedDialog(IDialog inner, PageDialogTracker tracker, Action<IDialog> emitClosed)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
                _emitClosed = emitClosed ?? throw new ArgumentNullException(nameof(emitClosed));

                // Official DialogDispatcher: page is omitted until reportAsNew
                // (javascript: dialogs open during initialization).
                IPage page = inner.Page;
                if (page is IHasClientInitializedPage initialized && !initialized.IsClientInitialized)
                {
                    page = null;
                }

                Page = page;
            }

            public IPage Page { get; }

            public string DefaultValue => _inner.DefaultValue;

            public string Message => _inner.Message;

            public string Type => _inner.Type;

            public Task AcceptAsync(string promptText = default)
            {
                IBrowserContext context = Page?.Context;
                return ActionTrace.RunAsync(
                    context,
                    "Accept dialog",
                    "Dialog",
                    "accept",
                    () => AcceptSilentAsync(promptText));
            }

            public async Task DismissAsync()
            {
                await _inner.DismissAsync().ConfigureAwait(false);
                _tracker.EmitClosed(_emitClosed, this);
            }

            internal async Task AcceptSilentAsync(string promptText = default)
            {
                await _inner.AcceptAsync(promptText).ConfigureAwait(false);
                _tracker.EmitClosed(_emitClosed, this);
            }

            internal async Task DismissSilentAsync()
            {
                await _inner.DismissAsync().ConfigureAwait(false);
                _tracker.EmitClosed(_emitClosed, this);
            }
        }
    }
}
