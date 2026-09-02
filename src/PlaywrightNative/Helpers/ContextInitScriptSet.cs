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
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official context <c>addInitScript</c>: install on existing pages, apply to
    /// pages created later (including popups), and dispose by removing each install.
    /// </summary>
    internal sealed class ContextInitScriptSet
    {
        private readonly List<Entry> _entries = new List<Entry>();

        /// <summary>
        /// Registers an installer and applies it to <paramref name="pages"/>.
        /// </summary>
        /// <param name="pages">Pages that already exist in the context.</param>
        /// <param name="install">Installs the script on one page.</param>
        /// <param name="currentDocumentSource">
        /// Resolved source to evaluate on an already-created document, or <see langword="null"/>.
        /// </param>
        /// <param name="isCallback">Whether this entry is an <c>exposeFunctions</c> script.</param>
        /// <returns>A disposable that removes the script from every page.</returns>
        internal async Task<IAsyncDisposable> AddAsync(
            IEnumerable<IPage> pages,
            Func<IPage, Task<IAsyncDisposable>> install,
            string currentDocumentSource = null,
            bool isCallback = false)
        {
            if (install == null)
            {
                throw new ArgumentNullException(nameof(install));
            }

            Entry entry = new Entry(install, currentDocumentSource, isCallback);
            lock (_entries)
            {
                _entries.Add(entry);
            }

            if (pages != null)
            {
                foreach (IPage page in pages)
                {
                    await entry.ApplyAsync(page).ConfigureAwait(false);
                }
            }

            return AddInitScriptHelper.CreateDisposable(async () =>
            {
                lock (_entries)
                {
                    _entries.Remove(entry);
                }

                await entry.DisposeAsync().ConfigureAwait(false);
            });
        }

        /// <summary>
        /// Applies every registered script to a newly created page (before resume).
        /// </summary>
        /// <param name="page">The new page.</param>
        /// <returns>A task that completes when every script has been installed.</returns>
        internal Task ApplyAllAsync(IPage page)
            => ApplyAllAsync(page, callbacks: null);

        /// <summary>
        /// Applies registered scripts, optionally only callbacks or only string scripts.
        /// </summary>
        /// <param name="page">The new page.</param>
        /// <param name="callbacks">
        /// <see langword="true"/> for exposeFunctions entries,
        /// <see langword="false"/> for string scripts,
        /// <see langword="null"/> for every entry.
        /// </param>
        /// <returns>A task that completes when matching scripts have been installed.</returns>
        internal async Task ApplyAllAsync(IPage page, bool? callbacks)
        {
            Entry[] snapshot;
            lock (_entries)
            {
                snapshot = _entries.ToArray();
            }

            foreach (Entry entry in snapshot)
            {
                if (callbacks.HasValue && entry.IsCallback != callbacks.Value)
                {
                    continue;
                }

                await entry.ApplyAsync(page).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Runs string init scripts on the current document (NewPage about:blank
        /// is created before <c>addScriptToEvaluateOnNewDocument</c> can apply).
        /// </summary>
        /// <param name="page">The page whose current document should run the scripts.</param>
        /// <returns>A task that completes when evaluation has been attempted.</returns>
        internal async Task EvaluateOnCurrentAsync(IPage page)
        {
            if (page == null)
            {
                return;
            }

            Entry[] snapshot;
            lock (_entries)
            {
                snapshot = _entries.ToArray();
            }

            foreach (Entry entry in snapshot)
            {
                if (string.IsNullOrEmpty(entry.CurrentDocumentSource))
                {
                    continue;
                }

                try
                {
                    await page.EvaluateAsync(entry.CurrentDocumentSource).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }
            }
        }

        /// <summary>
        /// Official context <c>addInitScript</c> plus <c>exposeFunctions</c>.
        /// </summary>
        /// <param name="pages">Pages that already exist.</param>
        /// <param name="script">Inline script or function.</param>
        /// <param name="scriptPath">Filesystem path, or <see langword="null"/>.</param>
        /// <param name="arg">Optional argument passed to a function script.</param>
        /// <param name="exposeFunctions">Official <c>exposeFunctions</c>.</param>
        /// <returns>A disposable that removes the script and its callbacks.</returns>
        internal Task<IAsyncDisposable> AddResolvedAsync(
            IEnumerable<IPage> pages,
            string script,
            string scriptPath,
            object arg,
            bool exposeFunctions)
        {
            if (exposeFunctions)
            {
                if (!EvaluateWithArg.IsFunction(script))
                {
                    throw new PlaywrightNativeException(EvaluateCallbacks.InitScriptRequiresFunction);
                }

                return AddAsync(
                    pages,
                    page => EvaluateCallbacks.AddInitScriptTargetAsync(page, script, arg, true, runOnCurrentDocument: true),
                    isCallback: true);
            }

            if (string.IsNullOrEmpty(script) && !string.IsNullOrEmpty(scriptPath))
            {
                script = PathIo.ReadText(scriptPath);
            }

            if (arg != null && !string.IsNullOrEmpty(script))
            {
                script = EvaluateWithArg.Wrap(script, EvaluateCallbacks.DropFunctions(arg), throwOnFunctions: false);
            }

            if (string.IsNullOrEmpty(script))
            {
                throw new PlaywrightNativeException(AddInitScriptHelper.MissingOptionsMessage);
            }

            string captured = script;
            string current = AddInitScriptHelper.Resolve(captured, null);
            return AddAsync(pages, page => page.AddInitScriptAsync(captured), current);
        }

        private sealed class Entry
        {
            private readonly Func<IPage, Task<IAsyncDisposable>> _install;
            private readonly List<IAsyncDisposable> _installed = new List<IAsyncDisposable>();
            private bool _disposed;

            internal Entry(Func<IPage, Task<IAsyncDisposable>> install, string currentDocumentSource, bool isCallback)
            {
                _install = install;
                CurrentDocumentSource = currentDocumentSource;
                IsCallback = isCallback;
            }

            internal string CurrentDocumentSource { get; }

            internal bool IsCallback { get; }

            internal async Task ApplyAsync(IPage page)
            {
                if (page == null || _disposed)
                {
                    return;
                }

                IAsyncDisposable installed = await _install(page).ConfigureAwait(false);
                bool disposeNow = false;
                lock (_installed)
                {
                    if (_disposed)
                    {
                        disposeNow = true;
                    }
                    else
                    {
                        _installed.Add(installed);
                    }
                }

                if (disposeNow && installed != null)
                {
                    await installed.DisposeAsync().ConfigureAwait(false);
                }
            }

            internal async Task DisposeAsync()
            {
                _disposed = true;
                List<IAsyncDisposable> copy;
                lock (_installed)
                {
                    copy = new List<IAsyncDisposable>(_installed);
                    _installed.Clear();
                }

                foreach (IAsyncDisposable disposable in copy)
                {
                    if (disposable == null)
                    {
                        continue;
                    }

                    await disposable.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }
}
