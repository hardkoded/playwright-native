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

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Result of classifying <c>setInputFiles</c> filesystem paths.
    /// </summary>
    internal sealed class ResolvedInputFilePaths
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResolvedInputFilePaths"/> class.
        /// </summary>
        /// <param name="isDirectory">Whether the caller passed a single directory.</param>
        /// <param name="absolutePaths">Absolute file paths (directory contents when <paramref name="isDirectory"/>).</param>
        /// <param name="payloads">In-page payloads, including last-modified and relative paths.</param>
        internal ResolvedInputFilePaths(bool isDirectory, string[] absolutePaths, PlaywrightFilePayload[] payloads)
        {
            IsDirectory = isDirectory;
            AbsolutePaths = absolutePaths ?? Array.Empty<string>();
            Payloads = payloads ?? Array.Empty<PlaywrightFilePayload>();
        }

        /// <summary>
        /// Gets a value indicating whether the caller passed a single directory.
        /// </summary>
        internal bool IsDirectory { get; }

        /// <summary>
        /// Gets absolute filesystem paths for native <c>DOM.setFileInputFiles</c>.
        /// </summary>
        internal string[] AbsolutePaths { get; }

        /// <summary>
        /// Gets payloads for the in-page <c>File</c> / <c>DataTransfer</c> path.
        /// </summary>
        internal PlaywrightFilePayload[] Payloads { get; }
    }
}
