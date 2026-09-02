/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// CDP <c>Page.fileChooserOpened</c> payload.
    /// </summary>
    internal sealed class FileChooserOpenedEventArgs : EventArgs
    {
        internal FileChooserOpenedEventArgs(int backendNodeId, bool multiple, CRSession session = null)
        {
            BackendNodeId = backendNodeId;
            Multiple = multiple;
            Session = session;
        }

        internal int BackendNodeId { get; }

        internal bool Multiple { get; }

        internal CRSession Session { get; }
    }
}
