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
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// Dedicated Web Worker attached to a WebKit page via the Worker domain.
    /// </summary>
    internal sealed class WKWorker
    {
        private readonly WKWorkerSession _session;
        private readonly WKExecutionContext _context;
        private readonly TaskCompletionSource<bool> _readyTcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal WKWorker(WKWorkerSession session, string workerId, string url)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            WorkerId = workerId ?? throw new ArgumentNullException(nameof(workerId));
            Url = url ?? string.Empty;
            _context = new WKExecutionContext(_session, contextId: null);
            _session.MessageReceived += OnMessage;
        }

        internal event EventHandler Closed;

        internal event EventHandler<IConsoleMessage> Console;

        internal event EventHandler<PageErrorEventArgs> ExceptionThrown;

        internal string WorkerId { get; }

        internal string Url { get; }

        internal WKWorkerSession Session => _session;

        /// <summary>
        /// Completes after <c>Runtime.enable</c> / <c>Console.enable</c> /
        /// <c>Worker.initialized</c> finish so the worker script has been allowed to run.
        /// </summary>
        internal Task Ready => _readyTcs.Task;

        internal async Task InitializeAsync()
        {
            await _session.SendAsync("Runtime.enable").ConfigureAwait(false);
            try
            {
                await _session.SendAsync("Console.enable").ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                // Some WebKit builds expose worker logs only via Runtime.consoleAPICalled.
            }
        }

        /// <summary>
        /// Marks the worker ready for evaluation after protocol init completes (or fails).
        /// </summary>
        internal void MarkReady()
            => _readyTcs.TrySetResult(true);

        internal async Task<T> EvaluateAsync<T>(string expression)
        {
            await Ready.ConfigureAwait(false);
            return await _context.EvaluateAsync<T>(expression).ConfigureAwait(false);
        }

        internal async Task<IJSHandle> EvaluateHandleAsync(string expression)
        {
            await Ready.ConfigureAwait(false);
            JsonElement? handleValue = await _context.EvaluateHandleAsync(expression).ConfigureAwait(false);
            string objectId = RemoteObject.GetObjectId(handleValue);
            return string.IsNullOrEmpty(objectId) ? null : new WKJSHandle(_context, objectId);
        }

        internal void NotifyClosed()
        {
            _readyTcs.TrySetResult(true);
            _session.MessageReceived -= OnMessage;
            Closed?.Invoke(this, EventArgs.Empty);
            _session.Dispose();
        }

        private void OnMessage(string method, JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            if (method == "Runtime.consoleAPICalled")
            {
                Console?.Invoke(this, WorkerConsole.Parse(parameters.Value, WrapConsoleRemote));
                return;
            }

            if (method == "Console.messageAdded")
            {
                // Match page Console: WebKit reports uncaught worker exceptions as
                // Console.messageAdded with level=error and source=javascript. Upstream
                // page mapping raises pageerror; without this, macOS only gets a console
                // message and ShouldReportErrors times out on PageError.
                if (parameters.Value.TryGetProperty("message", out JsonElement message)
                    && message.ValueKind == JsonValueKind.Object)
                {
                    string level = message.TryGetProperty("level", out JsonElement levelEl)
                        ? levelEl.GetString()
                        : string.Empty;
                    string source = message.TryGetProperty("source", out JsonElement sourceEl)
                        ? sourceEl.GetString()
                        : string.Empty;
                    if (string.Equals(level, "error", StringComparison.Ordinal)
                        && string.Equals(source, "javascript", StringComparison.Ordinal))
                    {
                        string protocolText = message.TryGetProperty("text", out JsonElement rawTextEl)
                            ? rawTextEl.GetString() ?? string.Empty
                            : string.Empty;
                        ExceptionThrown?.Invoke(this, PageErrorText.FromWebKitConsole(protocolText, message));
                        return;
                    }
                }

                ConsoleMessage added = WorkerConsole.ParseMessageAdded(parameters.Value, WrapConsoleRemote);
                if (added != null)
                {
                    Console?.Invoke(this, added);
                }

                return;
            }

            if (method == "Runtime.exceptionThrown"
                && parameters.Value.TryGetProperty("exceptionDetails", out JsonElement details))
            {
                ExceptionThrown?.Invoke(this, PageErrorText.FromExceptionDetails(details));
            }
        }

        private IJSHandle WrapConsoleRemote(JsonElement remote)
        {
            string objectId = RemoteObject.GetObjectId(remote);
            if (objectId == null)
            {
                return null;
            }

            return new WKJSHandle(_context, objectId, page: null, preview: RemoteObject.HandlePreview(remote));
        }
    }
}
