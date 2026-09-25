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
using Microsoft.Playwright;
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
        private readonly string _publicDownloadsDirectory;
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
            bool acceptDownloads = true,
            string publicDownloadsDirectory = null)
        {
            Page = page;
            Url = url ?? string.Empty;
            _suggestedFilename = suggestedFilename ?? string.Empty;
            _downloadsDirectory = downloadsDirectory;
            _publicDownloadsDirectory = publicDownloadsDirectory;
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
                catch (PlaywrightException)
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
                throw new PlaywrightException("download.path: " + error);
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

            throw new PlaywrightException("Download finished but the file was not found.");
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
                throw new PlaywrightException(
                    "Target page, context or browser has been closed");
            }

            string source;
            try
            {
                source = await PathAsync().ConfigureAwait(false);
            }
            catch (PlaywrightException ex)
            {
                string message = ex.Message ?? string.Empty;
                if (message.StartsWith("download.path: ", StringComparison.Ordinal))
                {
                    throw new PlaywrightException(
                        "download.saveAs: " + message.AsSpan("download.path: ".Length).ToString(),
                        ex);
                }

                throw new PlaywrightException("download.saveAs: " + message, ex);
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
            TryDeleteFile(TryFindBrowserArtifact());
            TryDeletePromotedCopies();
            MarkFailed(CanceledError);
        }

        internal void MarkCompleted()
        {
            PromoteCompletedFile();
            _finishedTcs.TrySetResult(null);
        }

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

        /// <summary>
        /// Copies a finished Chromium <c>allowAndName</c> artifact from the
        /// browser-pending directory into the public downloads path so filesystem
        /// polls never observe a locked <c>.crdownload</c> (Windows CI).
        /// <see cref="PathAsync"/> still returns the browser artifact so Chromium
        /// page-close cleanup and <c>deleteOnContextClose</c> keep working.
        /// </summary>
        private void PromoteCompletedFile()
        {
            if (string.IsNullOrEmpty(_publicDownloadsDirectory)
                || string.IsNullOrEmpty(_downloadsDirectory)
                || string.Equals(_publicDownloadsDirectory, _downloadsDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string source = null;
            for (int findAttempt = 0; findAttempt < 50; findAttempt++)
            {
                source = TryFindFileInDirectory(_downloadsDirectory);
                if (source != null)
                {
                    break;
                }

                System.Threading.Thread.Sleep(20);
            }

            if (source == null)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(_publicDownloadsDirectory);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            // Keep the browser guid name when possible so public polls and
            // PathAsync consumers agree on the allowAndName artifact identity.
            // Fall back to the suggested filename for directory-poll tests that
            // only assert content (persistent acceptDownloads).
            string destName = !string.IsNullOrEmpty(_guid)
                ? _guid
                : (!string.IsNullOrEmpty(_suggestedFilename)
                    ? _suggestedFilename
                    : Path.GetFileName(source));
            if (string.IsNullOrEmpty(destName))
            {
                destName = "download";
            }

            string dest = Path.Combine(_publicDownloadsDirectory, destName);
            for (int attempt = 0; attempt < 50; attempt++)
            {
                string temp = null;
                try
                {
                    // Stage outside the public directory, then rename in. Directory
                    // polls (LaunchArtifactsDir) must never observe a mid-copy file
                    // that Windows still locks for ReadAllText.
                    temp = Path.Combine(
                        Path.GetTempPath(),
                        "pw-promote-" + Guid.NewGuid().ToString("N"));
                    File.Copy(source, temp, overwrite: true);
                    File.Move(temp, dest, overwrite: true);
                    temp = null;
                    if (!string.IsNullOrEmpty(_suggestedFilename)
                        && !string.Equals(destName, _suggestedFilename, StringComparison.OrdinalIgnoreCase))
                    {
                        // Also surface the human name so content-only directory
                        // polls (AcceptDownloads) find a finished file quickly.
                        string named = Path.Combine(_publicDownloadsDirectory, _suggestedFilename);
                        string namedTemp = Path.Combine(
                            Path.GetTempPath(),
                            "pw-promote-" + Guid.NewGuid().ToString("N"));
                        try
                        {
                            File.Copy(source, namedTemp, overwrite: true);
                            File.Move(namedTemp, named, overwrite: true);
                            namedTemp = null;
                        }
                        finally
                        {
                            if (namedTemp != null)
                            {
                                TryDeleteFile(namedTemp);
                            }
                        }
                    }

                    return;
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(20);
                }
                catch (UnauthorizedAccessException)
                {
                    System.Threading.Thread.Sleep(20);
                }
                finally
                {
                    if (temp != null)
                    {
                        TryDeleteFile(temp);
                    }
                }
            }
        }

        private string TryFindFile()
        {
            // Prefer the browser-pending artifact. Chromium deletes that file on
            // page close; PathAsync must track it so delete-on-close parity holds.
            // The public promoted copy is only for filesystem directory polls.
            string browserFile = TryFindBrowserArtifact();
            if (browserFile != null)
            {
                return browserFile;
            }

            return TryFindPromotedFile();
        }

        private string TryFindBrowserArtifact() => TryFindFileInDirectory(_downloadsDirectory);

        private string TryFindPromotedFile() => TryFindFileInDirectory(_publicDownloadsDirectory);

        private void TryDeletePromotedCopies()
        {
            if (string.IsNullOrEmpty(_publicDownloadsDirectory))
            {
                return;
            }

            TryDeleteFile(TryFindPromotedFile());
            if (!string.IsNullOrEmpty(_guid))
            {
                TryDeleteFile(Path.Combine(_publicDownloadsDirectory, _guid));
            }

            if (!string.IsNullOrEmpty(_suggestedFilename))
            {
                TryDeleteFile(Path.Combine(_publicDownloadsDirectory, _suggestedFilename));
            }
        }

        private void TryDeleteFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private string TryFindFileInDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(_guid))
            {
                string guidPath = Path.Combine(directory, _guid);
                if (FileExistsWithLength(guidPath))
                {
                    return guidPath;
                }
            }

            if (!string.IsNullOrEmpty(_suggestedFilename))
            {
                string namedPath = Path.Combine(directory, _suggestedFilename);
                if (FileExistsWithLength(namedPath))
                {
                    return namedPath;
                }
            }

            try
            {
                foreach (string file in Directory.GetFiles(directory))
                {
                    if (IsIncompleteDownloadPath(file))
                    {
                        continue;
                    }

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
            if (IsIncompleteDownloadPath(path))
            {
                return false;
            }

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

        private bool IsIncompleteDownloadPath(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".crdownload", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".tmp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".com.google.chrome.download", StringComparison.OrdinalIgnoreCase);
        }
    }
}
