/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
#pragma warning disable SA1204
using System.Collections.Generic;
using System.Linq;
using Microsoft.Playwright;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// File payload with PlaywrightSharp-only fields for WebKit directory uploads.
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
