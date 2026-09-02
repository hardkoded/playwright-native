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
using System.Threading.Tasks;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Chromium
{
    /// <summary>Public <see cref="IWorker"/> wrapping <see cref="CRWorker"/>.</summary>
    internal sealed partial class ChromiumWorker : IWorker
    {
        private readonly CRWorker _worker;

        internal ChromiumWorker(CRWorker worker)
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
        public async Task<IJSHandle> EvaluateHandleAsync(string expression, object arg = default)
        {
            string toEval = arg == null ? EvaluateWithArg.InvokeIfFunction(expression) : EvaluateWithArg.Wrap(expression, arg);
#pragma warning disable CA2000 // Ownership transfers to the returned ChromiumJSHandle
            CRJSHandle handle = await _worker.EvaluateHandleAsync(toEval).ConfigureAwait(false);
#pragma warning restore CA2000
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

        private IJSHandle WrapHandle(CRJSHandle handle)
            => handle == null ? null : new ChromiumJSHandle(handle);
    }
}
