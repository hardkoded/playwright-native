/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;

namespace PlaywrightSharp
{
    /// <summary>
    /// Browser-emitted download started before the artifact is fully written.
    /// </summary>
    internal sealed class DownloadWillBeginEventArgs : EventArgs
    {
        internal DownloadWillBeginEventArgs(string guid, string url, string suggestedFilename)
        {
            Guid = guid ?? string.Empty;
            Url = url ?? string.Empty;
            SuggestedFilename = suggestedFilename ?? string.Empty;
        }

        internal string Guid { get; }

        internal string Url { get; }

        internal string SuggestedFilename { get; }
    }
}
