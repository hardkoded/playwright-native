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
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Process-wide custom selector engines for <see cref="Playwright.Selectors"/>.
    /// </summary>
    internal sealed partial class Selectors : ISelectors
    {
        /// <summary>
        /// Shared registry used by <see cref="Playwright.Selectors"/>.
        /// </summary>
        internal static readonly Selectors Instance = new Selectors();

        /// <inheritdoc/>
        public async Task RegisterAsync(string name, string script = default, string path = default, bool contentScript = default)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Selector engine name must not be empty.", nameof(name));
            }

            if (!NameIsValid(name))
            {
                throw new PlaywrightNativeException("selectors.register: Selector engine name may only contain [a-zA-Z0-9_] characters");
            }

            string source = script;
            if (string.IsNullOrEmpty(source))
            {
                if (string.IsNullOrEmpty(path))
                {
                    throw new ArgumentException("Either script or path must be provided.", nameof(script));
                }

                source = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            }

            CustomSelectors.Register(name, source, contentScript);

            static bool NameIsValid(string value)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    bool ok = (c >= 'a' && c <= 'z')
                        || (c >= 'A' && c <= 'Z')
                        || (c >= '0' && c <= '9')
                        || c == '_';
                    if (!ok)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <inheritdoc/>
        public void SetTestIdAttribute(string attributeName)
            => Playwright.SetTestIdAttribute(attributeName);

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task ISelectors.RegisterAsync(string name, SelectorsRegisterOptions options)
            => RegisterAsync(
                name,
                options?.Script,
                options?.Path,
                options?.ContentScript == true);
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
