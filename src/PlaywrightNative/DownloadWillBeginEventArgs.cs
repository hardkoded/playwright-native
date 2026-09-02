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

namespace PlaywrightNative
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
