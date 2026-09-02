/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Text.Json;
using System.Threading.Tasks;
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

        internal async Task InitializeAsync()
        {
            await _session.SendAsync("Runtime.enable").ConfigureAwait(false);
            try
            {
                await _session.SendAsync("Console.enable").ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                // Some WebKit builds expose worker logs only via Runtime.consoleAPICalled.
            }
        }

        internal Task<T> EvaluateAsync<T>(string expression)
            => _context.EvaluateAsync<T>(expression);

        internal async Task<IJSHandle> EvaluateHandleAsync(string expression)
        {
            JsonElement? handleValue = await _context.EvaluateHandleAsync(expression).ConfigureAwait(false);
            string objectId = RemoteObject.GetObjectId(handleValue);
            return string.IsNullOrEmpty(objectId) ? null : new WKJSHandle(_context, objectId);
        }

        internal void NotifyClosed()
        {
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
                ConsoleMessage added = WorkerConsole.ParseMessageAdded(parameters.Value);
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
