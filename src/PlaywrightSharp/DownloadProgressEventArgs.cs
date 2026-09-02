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
    /// Browser-emitted download progress or terminal state.
    /// </summary>
    internal sealed class DownloadProgressEventArgs : EventArgs
    {
        internal DownloadProgressEventArgs(string guid, string state, string error)
        {
            Guid = guid ?? string.Empty;
            State = state ?? string.Empty;
            Error = error;
        }

        internal string Guid { get; }

        internal string State { get; }

        internal string Error { get; }
    }
}
