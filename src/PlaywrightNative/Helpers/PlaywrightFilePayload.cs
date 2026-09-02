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
#pragma warning disable SA1204
using System.Collections.Generic;
using System.Linq;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// File payload with PlaywrightNative-only fields for WebKit directory uploads.
    /// </summary>
    internal sealed class PlaywrightFilePayload
    {
        /// <summary>File name.</summary>
        internal string Name { get; set; }

        /// <summary>MIME type.</summary>
        internal string MimeType { get; set; }

        /// <summary>Raw bytes.</summary>
        internal byte[] Buffer { get; set; }

        /// <summary>Optional <c>File.lastModified</c> override (Unix ms).</summary>
        internal long? LastModified { get; set; }

        /// <summary>Optional <c>File.webkitRelativePath</c>.</summary>
        internal string WebkitRelativePath { get; set; }

        /// <summary>Converts to the official <see cref="FilePayload"/> shape.</summary>
        internal FilePayload ToOfficial()
            => new FilePayload
            {
                Name = Name,
                MimeType = MimeType,
                Buffer = Buffer,
            };

        /// <summary>Converts from the official <see cref="FilePayload"/> shape.</summary>
        internal static PlaywrightFilePayload FromOfficial(FilePayload payload)
            => payload == null
                ? null
                : new PlaywrightFilePayload
                {
                    Name = payload.Name,
                    MimeType = payload.MimeType,
                    Buffer = payload.Buffer,
                };

        /// <summary>Converts a sequence of official payloads.</summary>
        internal static IEnumerable<PlaywrightFilePayload> FromOfficial(IEnumerable<FilePayload> payloads)
            => payloads?.Select(FromOfficial);

        /// <summary>Converts a sequence to official payloads.</summary>
        internal static IEnumerable<FilePayload> ToOfficial(IEnumerable<PlaywrightFilePayload> payloads)
            => payloads?.Select(p => p?.ToOfficial());
    }
}
