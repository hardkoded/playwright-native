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
using System.IO;
using System.Threading.Tasks;
using PlaywrightNative.Chromium;
using PlaywrightNative.WebKit;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Sets file-input paths through the browser protocol
    /// (<c>DOM.setFileInputFiles</c>) so Chrome/WebKit read the files from disk.
    /// Avoids reconstructing multi-megabyte payloads via page evaluate.
    /// </summary>
    internal static class FileChooserSetFilesHelper
    {
        /// <summary>
        /// Assigns local filesystem paths to <paramref name="element"/> without
        /// reading file bytes into the Playwright process.
        /// </summary>
        /// <param name="element">The file input that opened the chooser.</param>
        /// <param name="files">Filesystem paths. An empty sequence clears the input.</param>
        /// <returns>A task that completes when the protocol command finishes.</returns>
        internal static async Task SetFromPathsAsync(IElementHandle element, IEnumerable<string> files)
        {
            if (element == null)
            {
                throw new PlaywrightNativeException("File chooser has no element handle.");
            }

            List<string> paths = new List<string>();
            if (files != null)
            {
                foreach (string path in files)
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        continue;
                    }

                    if (!File.Exists(path))
                    {
                        throw new FileNotFoundException("File not found: " + path, path);
                    }

                    paths.Add(path);
                }
            }

            if (paths.Count == 0)
            {
                await element.SetInputFilesAsync(
                    Array.Empty<FilePayload>(),
                    noWaitAfter: null,
                    timeout: null,
                    force: true).ConfigureAwait(false);
                return;
            }

            if (element is ChromiumElementHandle chromium)
            {
                await chromium.SetFileInputFilesFromPathsAsync(paths).ConfigureAwait(false);
                return;
            }

            if (element is WKElementHandle webkit)
            {
                await webkit.SetFileInputFilesFromPathsAsync(paths).ConfigureAwait(false);
                return;
            }

            await element.SetInputFilesAsync(paths).ConfigureAwait(false);
        }
    }
}
