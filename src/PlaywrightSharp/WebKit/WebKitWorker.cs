/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using PlaywrightSharp.Helpers;

namespace PlaywrightSharp.WebKit
{
    /// <summary>Public <see cref="IWorker"/> wrapping <see cref="WKWorker"/>.</summary>
    internal sealed partial class WebKitWorker : IWorker
    {
        private readonly WKWorker _worker;

        internal WebKitWorker(WKWorker worker)
        {
            _worker = worker ?? throw new ArgumentNullException(nameof(worker));
            _worker.Closed += (_, _) => Close?.Invoke(this, this);
            _worker.Console += (_, message) =>
            {
                if (message is ConsoleMessage consoleMessage)
                {
                    consoleMessage.Worker = this;
                }

                Console?.Invoke(this, message);
            };
        }

        /// <inheritdoc/>
        public event EventHandler<IWorker> Close;

        /// <inheritdoc/>
        public event EventHandler<IConsoleMessage> Console;

        /// <inheritdoc/>
        public string Url => _worker.Url;

        /// <inheritdoc/>
        public Task<T> EvaluateAsync<T>(string expression, object arg = default)
        {
            string toEval = arg == null ? EvaluateWithArg.InvokeIfFunction(expression) : EvaluateWithArg.Wrap(expression, arg);
            return _worker.EvaluateAsync<T>(toEval);
        }

        /// <inheritdoc/>
        public Task<IJSHandle> EvaluateHandleAsync(string expression, object arg = default)
        {
            string toEval = arg == null ? EvaluateWithArg.InvokeIfFunction(expression) : EvaluateWithArg.Wrap(expression, arg);
            return _worker.EvaluateHandleAsync(toEval);
        }

        /// <inheritdoc/>
        public Task<IWorker> WaitForCloseAsync(float? timeout = default)
            => WaitForEventHelper.WaitAsync<IWorker>(
                h => Close += h,
                h => Close -= h,
                _ => true,
                timeout,
                "worker.waitForEvent");
    }
}
