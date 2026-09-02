/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Official file-payload / clipboard-like <see cref="IPage.DropAsync"/>.
    /// </summary>
    internal static class PageDropHelper
    {
        /// <summary>
        /// JavaScript function <c>(el, json) => boolean</c> that builds a
        /// <c>DataTransfer</c> from files and MIME data, then dispatches
        /// <c>dragenter</c>, <c>dragover</c>, and <c>drop</c>. Throws when
        /// <c>dragover</c> is not cancelled.
        /// </summary>
        internal const string DropFunction = @"(el, json) => {
    if (!el || !el.isConnected) {
        throw new Error('Node is detached from document');
    }
    const spec = typeof json === 'string' ? JSON.parse(json) : (json || {});
    const files = spec.files || [];
    const data = spec.data || [];
    if (files.length === 0 && data.length === 0) {
        throw new Error('At least one of ""files"" or ""data"" must be provided');
    }
    const dt = new DataTransfer();
    for (let i = 0; i < files.length; i++) {
        const f = files[i];
        const binary = atob(f.buffer || '');
        const bytes = new Uint8Array(binary.length);
        for (let j = 0; j < binary.length; j++) {
            bytes[j] = binary.charCodeAt(j);
        }
        dt.items.add(new File([bytes], f.name, { type: f.mimeType || 'application/octet-stream' }));
    }
    for (let i = 0; i < data.length; i++) {
        dt.setData(data[i].type, data[i].value);
    }
    const rect = el.getBoundingClientRect();
    const clientX = spec.x != null ? rect.left + spec.x : rect.left + (rect.width / 2);
    const clientY = spec.y != null ? rect.top + spec.y : rect.top + (rect.height / 2);
    const init = { bubbles: true, cancelable: true, composed: true, dataTransfer: dt, clientX: clientX, clientY: clientY };
    el.dispatchEvent(new DragEvent('dragenter', init));
    const over = new DragEvent('dragover', init);
    const accepted = el.dispatchEvent(over) === false || over.defaultPrevented;
    if (!accepted) {
        el.dispatchEvent(new DragEvent('dragleave', init));
        throw new Error('Drop target did not accept the drop');
    }
    el.dispatchEvent(new DragEvent('drop', init));
    return true;
}";

        /// <summary>
        /// Drops <paramref name="payload"/> onto the element matching
        /// <paramref name="selector"/>.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="selector">Drop-target selector.</param>
        /// <param name="payload">Files and/or MIME data.</param>
        /// <param name="position">Optional offset inside the target box.</param>
        /// <param name="timeout">Selector wait timeout.</param>
        /// <param name="strict">When set, the selector honors official <c>strict</c>.</param>
        /// <returns>A task that completes when the drop events have been dispatched.</returns>
        internal static async Task RunAsync(
            IPage page,
            string selector,
            DropPayload payload,
            Position position,
            float? timeout,
            bool? strict)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (string.IsNullOrEmpty(selector))
            {
                throw new ArgumentException("Selector must not be null or empty.", nameof(selector));
            }

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            List<object> files = new List<object>();
            AppendFiles(files, payload.Files);
            if (payload.FilePaths != null)
            {
                foreach (string path in payload.FilePaths)
                {
                    AppendFile(files, FilePayloadHelper.FromPath(path).ToOfficial());
                }
            }

            List<object> data = new List<object>();
            if (payload.Data != null)
            {
                foreach (KeyValuePair<string, string> entry in payload.Data)
                {
                    data.Add(new
                    {
                        type = entry.Key ?? string.Empty,
                        value = entry.Value ?? string.Empty,
                    });
                }
            }

            if (files.Count == 0 && data.Count == 0)
            {
                throw new PlaywrightSharpException("At least one of \"files\" or \"data\" must be provided");
            }

            IElementHandle handle = await page.WaitForSelectorAsync(
                selector,
                WaitForSelectorState.Visible,
                timeout,
                strict).ConfigureAwait(false);
            if (handle == null)
            {
                throw new PlaywrightSharpException("Could not resolve drop selector '" + selector + "'");
            }

            object spec = new
            {
                files,
                data,
                x = position != null ? (float?)position.X : null,
                y = position != null ? (float?)position.Y : null,
            };
            string specJson = JsonSerializer.Serialize(spec);
            await handle.EvaluateAsync<bool>(DropFunction, specJson).ConfigureAwait(false);
        }

        private static void AppendFiles(List<object> files, IEnumerable<FilePayload> payloads)
        {
            if (payloads == null)
            {
                return;
            }

            foreach (FilePayload file in payloads)
            {
                AppendFile(files, file);
            }
        }

        private static void AppendFile(List<object> files, FilePayload file)
        {
            files.Add(new
            {
                name = file?.Name ?? string.Empty,
                mimeType = file?.MimeType ?? "application/octet-stream",
                buffer = Convert.ToBase64String(file?.Buffer ?? Array.Empty<byte>()),
            });
        }
    }
}
