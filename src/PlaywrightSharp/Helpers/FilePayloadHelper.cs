/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Reads local files into <see cref="FilePayload"/> and JSON for in-page assignment.
    /// </summary>
    internal static class FilePayloadHelper
    {
        /// <summary>
        /// Reads <paramref name="path"/> into a <see cref="FilePayload"/>.
        /// </summary>
        /// <param name="path">A filesystem path.</param>
        /// <returns>The payload.</returns>
        internal static PlaywrightFilePayload FromPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                throw new FileNotFoundException(SetInputFilesPathHelper.FormatEnoent(path ?? string.Empty), path);
            }

            DateTime utc = File.GetLastWriteTimeUtc(path);
            long lastModified = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            return new PlaywrightFilePayload
            {
                Name = Path.GetFileName(path),
                MimeType = MimeTypeFromPath(path),
                Buffer = File.ReadAllBytes(path),
                LastModified = lastModified,
            };
        }

        /// <summary>
        /// Serializes <paramref name="files"/> as JSON for <see cref="ElementStateScript.SetInputFilesFromJsonFunction"/>.
        /// </summary>
        /// <param name="files">Official file payloads. Null is treated as empty.</param>
        /// <returns>A JSON array string.</returns>
        internal static string ToJson(IEnumerable<Microsoft.Playwright.FilePayload> files)
            => ToJson(PlaywrightFilePayload.FromOfficial(files));

        /// <summary>
        /// Serializes <paramref name="files"/> as JSON for <see cref="ElementStateScript.SetInputFilesFromJsonFunction"/>.
        /// </summary>
        /// <param name="files">File payloads. Null is treated as empty.</param>
        /// <returns>A JSON array string.</returns>
        internal static string ToJson(IEnumerable<PlaywrightFilePayload> files)
        {
            List<object> list = new List<object>();
            if (files != null)
            {
                foreach (PlaywrightFilePayload file in files)
                {
                    list.Add(new
                    {
                        name = file?.Name ?? string.Empty,
                        mimeType = file?.MimeType ?? "application/octet-stream",
                        buffer = Convert.ToBase64String(file?.Buffer ?? Array.Empty<byte>()),
                        lastModified = file?.LastModified,
                        webkitRelativePath = file?.WebkitRelativePath,
                    });
                }
            }

            return JsonSerializer.Serialize(list);
        }

        /// <summary>
        /// Infers a MIME type from <paramref name="path"/>'s extension.
        /// Unknown extensions map to <c>application/octet-stream</c>.
        /// </summary>
        /// <param name="path">A filesystem path.</param>
        /// <returns>The inferred MIME type.</returns>
        internal static string MimeTypeFromPath(string path)
        {
            string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext))
            {
                return "application/octet-stream";
            }

            if (ext.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                return "text/plain";
            }

            if (ext.Equals(".html", StringComparison.OrdinalIgnoreCase) || ext.Equals(".htm", StringComparison.OrdinalIgnoreCase))
            {
                return "text/html";
            }

            if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                return "image/png";
            }

            return "application/octet-stream";
        }
    }
}
