/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
