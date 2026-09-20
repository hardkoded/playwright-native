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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Chromium
{
    /// <summary>Public <see cref="IWorker"/> wrapping <see cref="CRWorker"/>.</summary>
    internal sealed partial class ChromiumWorker : IWorker
    {
        private readonly CRWorker _worker;
        private readonly object _consoleGate = new();
        private readonly List<IConsoleMessage> _consoleMessages = new();
        private TaskCompletionSource<IConsoleMessage> _consoleWaiter;

        internal ChromiumWorker(CRWorker worker)
        {
            _worker = worker ?? throw new ArgumentNullException(nameof(worker));
            _worker.Closed += (_, _) => Close?.Invoke(this, this);
            _worker.Console += OnWorkerConsole;
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
        public async Task<IJSHandle> EvaluateHandleAsync(string expression, object arg = default)
        {
            string toEval = arg == null ? EvaluateWithArg.InvokeIfFunction(expression) : EvaluateWithArg.Wrap(expression, arg);
            CRJSHandle handle = await _worker.EvaluateHandleAsync(toEval).ConfigureAwait(false);
            return WrapHandle(handle);
        }

        /// <inheritdoc/>
        public Task<IWorker> WaitForCloseAsync(float? timeout = default)
            => WaitForEventHelper.WaitAsync<IWorker>(
                h => Close += h,
                h => Close -= h,
                _ => true,
                timeout,
                "worker.waitForEvent");

        /// <summary>
        /// Waits for a console message, including one that arrived before the
        /// caller subscribed. Service-worker startup logs fire during
        /// <c>Runtime.runIfWaitingForDebugger</c>, before
        /// <c>worker.WaitForConsoleMessageAsync</c> runs.
        /// </summary>
        /// <param name="timeout">Timeout in milliseconds, or <see langword="null"/> for the default.</param>
        /// <returns>The console message.</returns>
        internal async Task<IConsoleMessage> WaitForConsoleMessageAsync(float? timeout = default)
        {
            Task<IConsoleMessage> pending;
            lock (_consoleGate)
            {
                if (_consoleMessages.Count > 0)
                {
                    IConsoleMessage buffered = _consoleMessages[0];
                    _consoleMessages.RemoveAt(0);
                    return buffered;
                }

                _consoleWaiter = new TaskCompletionSource<IConsoleMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                pending = _consoleWaiter.Task;
            }

            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            if (timeoutMs == Timeout.Infinite)
            {
                return await pending.ConfigureAwait(false);
            }

            try
            {
                return await pending.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                lock (_consoleGate)
                {
                    if (_consoleWaiter != null && ReferenceEquals(_consoleWaiter.Task, pending))
                    {
                        _consoleWaiter = null;
                    }
                }

                throw new TimeoutException(
                    "worker.waitForEvent: Timeout " + timeoutMs.ToString(System.Globalization.CultureInfo.InvariantCulture) + "ms exceeded.");
            }
        }

        private IJSHandle WrapHandle(CRJSHandle handle)
            => handle == null ? null : new ChromiumJSHandle(handle);

        private void OnWorkerConsole(object sender, IConsoleMessage message)
        {
            if (message is ConsoleMessage consoleMessage)
            {
                consoleMessage.Worker = this;
            }

            TaskCompletionSource<IConsoleMessage> waiter;
            lock (_consoleGate)
            {
                waiter = _consoleWaiter;
                _consoleWaiter = null;
                if (waiter == null)
                {
                    _consoleMessages.Add(message);
                    if (_consoleMessages.Count > 200)
                    {
                        _consoleMessages.RemoveAt(0);
                    }
                }
            }

            waiter?.TrySetResult(message);
            Console?.Invoke(this, message);
        }
    }
}
