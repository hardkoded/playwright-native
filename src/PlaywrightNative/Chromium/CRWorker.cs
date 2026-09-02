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
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Dedicated Web Worker attached to a Chromium page session.
    /// </summary>
    internal sealed class CRWorker
    {
        private readonly CRSession _session;
        private readonly object _contextLock = new();
        private TaskCompletionSource<CRExecutionContext> _contextTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CRExecutionContext _context;

        internal CRWorker(CRSession session, string sessionId, string url)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            Url = url ?? string.Empty;
            _session.MessageReceived += OnMessage;
        }

        internal event EventHandler Closed;

        internal event EventHandler<IConsoleMessage> Console;

        internal event EventHandler<PageErrorEventArgs> ExceptionThrown;

        internal string SessionId { get; }

        internal string Url { get; }

        internal CRSession Session => _session;

        internal Task EnableRuntimeAsync()
            => _session.SendAsync("Runtime.enable");

        internal Task ResumeDebuggerAsync()
            => _session.SendAsync("Runtime.runIfWaitingForDebugger");

        internal async Task InitializeAsync()
        {
            await EnableRuntimeAsync().ConfigureAwait(false);
            await ResumeDebuggerAsync().ConfigureAwait(false);
        }

        internal async Task<T> EvaluateAsync<T>(string expression)
        {
            CRExecutionContext context = await WaitForExecutionContextAsync().ConfigureAwait(false);
            return await context.EvaluateAsync<T>(expression).ConfigureAwait(false);
        }

        internal async Task<CRJSHandle> EvaluateHandleAsync(string expression)
        {
            CRExecutionContext context = await WaitForExecutionContextAsync().ConfigureAwait(false);
            JsonElement? handleValue = await context.EvaluateHandleAsync(expression).ConfigureAwait(false);
            string objectId = RemoteObject.GetObjectId(handleValue);
            return string.IsNullOrEmpty(objectId) ? null : new CRJSHandle(context, objectId);
        }

        internal void NotifyClosed()
        {
            _session.MessageReceived -= OnMessage;
            Closed?.Invoke(this, EventArgs.Empty);
            _session.Dispose();
        }

        private Task<CRExecutionContext> WaitForExecutionContextAsync()
        {
            lock (_contextLock)
            {
                return _contextTcs.Task;
            }
        }

        private void ResetExecutionContext()
        {
            lock (_contextLock)
            {
                _context = null;
                if (_contextTcs.Task.IsCompleted)
                {
                    _contextTcs = new TaskCompletionSource<CRExecutionContext>(TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }
        }

        private void OnMessage(string method, JsonElement? parameters)
        {
            if (method == "Runtime.consoleAPICalled" && parameters.HasValue)
            {
                Console?.Invoke(this, WorkerConsole.Parse(parameters.Value, remote => WrapConsoleRemote(remote, parameters.Value)));
                return;
            }

            if (method == "Runtime.exceptionThrown" && parameters.HasValue
                && parameters.Value.TryGetProperty("exceptionDetails", out JsonElement details))
            {
                ExceptionThrown?.Invoke(this, PageErrorText.FromExceptionDetails(details));
                return;
            }

            if (method == "Inspector.targetCrashed" || method == "Runtime.executionContextsCleared")
            {
                ResetExecutionContext();
                return;
            }

            if (method == "Runtime.executionContextDestroyed")
            {
                ResetExecutionContext();
                return;
            }

            if (method == "Inspector.targetReloadedAfterCrash")
            {
                // Official CRServiceWorker: resume after Chrome restarts the worker.
                _ = ResumeDebuggerAsync();
                return;
            }

            if (method != "Runtime.executionContextCreated" || !parameters.HasValue)
            {
                return;
            }

            if (!parameters.Value.TryGetProperty("context", out JsonElement context)
                || !context.TryGetProperty("id", out JsonElement idEl)
                || idEl.ValueKind != JsonValueKind.Number)
            {
                return;
            }

            CRExecutionContext created = new(_session, idEl.GetInt32());
            lock (_contextLock)
            {
                _context = created;
                _contextTcs.TrySetResult(created);
            }
        }

        private IJSHandle WrapConsoleRemote(JsonElement remote, JsonElement payload)
        {
            string objectId = RemoteObject.GetObjectId(remote);
            if (objectId == null)
            {
                return null;
            }

            CRExecutionContext context = _context;
            if (context == null)
            {
                int contextId = payload.TryGetProperty("executionContextId", out JsonElement ctxEl)
                    && ctxEl.TryGetInt32(out int cid)
                    ? cid
                    : 0;
                context = new CRExecutionContext(_session, contextId);
            }

            return new ChromiumJSHandle(new CRJSHandle(context, objectId, RemoteObject.HandlePreview(remote)));
        }
    }
}
