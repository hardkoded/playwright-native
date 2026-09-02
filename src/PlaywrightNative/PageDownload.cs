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
using System.IO;
using System.Threading.Tasks;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// In-process <see cref="IDownload"/> backed by a context downloads directory.
    /// </summary>
    internal sealed partial class PageDownload : IDownload
    {
        internal const string DownloadsDisabledError =
            "Pass { acceptDownloads: true } when you are creating your browser context";

        internal const string CanceledError = "canceled";

        private readonly string _downloadsDirectory;
        private readonly string _guid;
        private readonly Func<Task> _cancelAsync;
        private readonly TaskCompletionSource<string> _finishedTcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private string _suggestedFilename;
        private bool _deleted;
        private bool _eventFired;

        internal PageDownload(
            IPage page,
            string url,
            string suggestedFilename,
            string downloadsDirectory,
            string guid,
            Func<Task> cancelAsync = null,
            bool acceptDownloads = true)
        {
            Page = page;
            Url = url ?? string.Empty;
            _suggestedFilename = suggestedFilename ?? string.Empty;
            _downloadsDirectory = downloadsDirectory;
            _guid = guid ?? string.Empty;
            _cancelAsync = cancelAsync;
            if (!acceptDownloads)
            {
                MarkFailed(DownloadsDisabledError);
            }
        }

        /// <inheritdoc/>
        public IPage Page { get; }

        /// <inheritdoc/>
        public string Url { get; }

        /// <inheritdoc/>
        public string SuggestedFilename => _suggestedFilename;

        /// <inheritdoc/>
        public async Task<Stream> CreateReadStreamAsync()
        {
            string error = await WaitForFinishAsync().ConfigureAwait(false);
            if (error != null)
            {
                return null;
            }

            string path = await PathAsync().ConfigureAwait(false);
            return File.OpenRead(path);
        }

        /// <inheritdoc/>
        public async Task DeleteAsync()
        {
            string path = await PathAsync().ConfigureAwait(false);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            _deleted = true;
        }

        /// <inheritdoc/>
        public async Task CancelAsync()
        {
            if (!_finishedTcs.Task.IsCompleted)
            {
                MarkFailed(CanceledError);
            }

            if (_cancelAsync != null)
            {
                try
                {
                    await _cancelAsync().WithTimeout(() => Task.CompletedTask, 2_000).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                    // The download may have already finished or the browser closed.
                }
            }
        }

        /// <inheritdoc/>
        public Task<string> FailureAsync() => WaitForFinishAsync();

        /// <inheritdoc/>
        public async Task<string> PathAsync()
        {
            string error = await WaitForFinishAsync().ConfigureAwait(false);
            if (error != null)
            {
                throw new PlaywrightNativeException("download.path: " + error);
            }

            DateTime deadline = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < deadline)
            {
                string found = TryFindFile();
                if (found != null)
                {
                    return found;
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            throw new PlaywrightNativeException("Download finished but the file was not found.");
        }

        /// <inheritdoc/>
        public async Task SaveAsAsync(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("A destination path is required.", nameof(path));
            }

            if (_deleted)
            {
                throw new PlaywrightNativeException(
                    "Target page, context or browser has been closed");
            }

            string source;
            try
            {
                source = await PathAsync().ConfigureAwait(false);
            }
            catch (PlaywrightNativeException ex)
            {
                string message = ex.Message ?? string.Empty;
                if (message.StartsWith("download.path: ", StringComparison.Ordinal))
                {
                    throw new PlaywrightNativeException(
                        "download.saveAs: " + message.AsSpan("download.path: ".Length).ToString(),
                        ex);
                }

                throw new PlaywrightNativeException("download.saveAs: " + message, ex);
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(source, path, overwrite: true);
        }

        internal void SetSuggestedFilename(string suggestedFilename)
        {
            if (!string.IsNullOrEmpty(suggestedFilename))
            {
                _suggestedFilename = suggestedFilename;
            }
        }

        /// <summary>
        /// Official WebKit fires the Download event once, after the suggested filename arrives.
        /// </summary>
        /// <returns><see langword="true"/> the first time this is called.</returns>
        internal bool TryMarkEventFired()
        {
            if (_eventFired)
            {
                return false;
            }

            _eventFired = true;
            return true;
        }

        /// <summary>
        /// Official <c>artifact.deleteOnContextClose</c>: remove the file without
        /// waiting, and leave a user-provided downloads directory in place.
        /// </summary>
        internal void DeleteOnContextClose()
        {
            if (_deleted)
            {
                return;
            }

            _deleted = true;
            string found = TryFindFile();
            if (found != null)
            {
                try
                {
                    File.Delete(found);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            MarkFailed(CanceledError);
        }

        internal void MarkCompleted() => _finishedTcs.TrySetResult(null);

        internal void MarkFailed(string error)
        {
            if (string.IsNullOrEmpty(error)
                || string.Equals(error, "Download canceled.", StringComparison.OrdinalIgnoreCase)
                || string.Equals(error, "Download canceled", StringComparison.OrdinalIgnoreCase))
            {
                error = CanceledError;
            }

            _finishedTcs.TrySetResult(error);
        }

        private Task<string> WaitForFinishAsync() => _finishedTcs.Task;

        private string TryFindFile()
        {
            if (string.IsNullOrEmpty(_downloadsDirectory) || !Directory.Exists(_downloadsDirectory))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(_guid))
            {
                string guidPath = Path.Combine(_downloadsDirectory, _guid);
                if (FileExistsWithLength(guidPath))
                {
                    return guidPath;
                }
            }

            if (!string.IsNullOrEmpty(_suggestedFilename))
            {
                string namedPath = Path.Combine(_downloadsDirectory, _suggestedFilename);
                if (FileExistsWithLength(namedPath))
                {
                    return namedPath;
                }
            }

            try
            {
                foreach (string file in Directory.GetFiles(_downloadsDirectory))
                {
                    if (FileExistsWithLength(file))
                    {
                        return file;
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return null;
        }

        private bool FileExistsWithLength(string path)
        {
            try
            {
                return File.Exists(path) && new FileInfo(path).Length > 0;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
