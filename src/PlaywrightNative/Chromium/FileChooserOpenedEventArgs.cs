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
