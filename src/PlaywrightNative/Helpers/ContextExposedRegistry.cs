/*
 * Copyright (c) Microsoft Corporation.
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
    /// Official <c>browserContext.exposeFunction</c> / <c>exposeBinding</c>
    /// registry: duplicate checks, install on current and future pages,
    /// and dispose.
    /// </summary>
    internal sealed class ContextExposedRegistry : IHasExposedFunctionNames
    {
        private readonly HashSet<string> _names = new(StringComparer.Ordinal);
        private readonly List<Func<IPage, Task>> _installers = new();
        private readonly object _lock = new object();

        /// <summary>
        /// Installers applied to pages created after registration.
        /// </summary>
        internal IReadOnlyList<Func<IPage, Task>> Installers
        {
            get
            {
                lock (_lock)
                {
                    return _installers.ToArray();
                }
            }
        }

        /// <inheritdoc/>
        public bool HasExposedFunction(string name)
        {
            lock (_lock)
            {
                return _names.Contains(name);
            }
        }

        /// <summary>
        /// Registers <paramref name="name"/> on every current page and on
        /// future pages until the returned disposable is disposed.
        /// </summary>
        /// <param name="name">The JS global name.</param>
        /// <param name="install">Installs the binding on one page.</param>
        /// <param name="pages">Pages already in the context.</param>
        /// <param name="pageHasName">True when that page already exposed <paramref name="name"/>.</param>
        /// <returns>A disposable that unregisters the binding.</returns>
        internal async Task<IAsyncDisposable> RegisterAsync(
            string name,
            Func<IPage, Task<IAsyncDisposable>> install,
            IEnumerable<IPage> pages,
            Func<IPage, bool> pageHasName)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("name must be non-empty", nameof(name));
            }

            if (install == null)
            {
                throw new ArgumentNullException(nameof(install));
            }

            List<IPage> existing = pages == null ? new List<IPage>() : new List<IPage>(pages);

            lock (_lock)
            {
                if (_names.Contains(name))
                {
                    throw new PlaywrightNativeException(PageBindingScript.AlreadyRegistered(name));
                }
            }

            foreach (IPage page in existing)
            {
                if (pageHasName != null && pageHasName(page))
                {
                    throw new PlaywrightNativeException(PageBindingScript.AlreadyRegisteredInOneOfThePages(name));
                }
            }

            List<IAsyncDisposable> installed = new List<IAsyncDisposable>();

            async Task InstallOnAsync(IPage page)
            {
                IAsyncDisposable disposable = await install(page).ConfigureAwait(false);
                lock (installed)
                {
                    installed.Add(disposable);
                }
            }

            Func<IPage, Task> installer = InstallOnAsync;
            lock (_lock)
            {
                _names.Add(name);
                _installers.Add(installer);
            }

            foreach (IPage page in existing)
            {
                await InstallOnAsync(page).ConfigureAwait(false);
            }

            return AddInitScriptHelper.CreateDisposable(async () =>
            {
                lock (_lock)
                {
                    _names.Remove(name);
                    _installers.Remove(installer);
                }

                IAsyncDisposable[] copy;
                lock (installed)
                {
                    copy = installed.ToArray();
                    installed.Clear();
                }

                foreach (IAsyncDisposable disposable in copy)
                {
                    await disposable.DisposeAsync().ConfigureAwait(false);
                }
            });
        }
    }
}
