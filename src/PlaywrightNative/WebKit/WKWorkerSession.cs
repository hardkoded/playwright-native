/*
 * Copyright (c) 2020 Darío Kondratiuk
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

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// Inspector session for a dedicated Web Worker. Outbound commands are wrapped in
    /// <c>Worker.sendMessageToWorker</c> on the page's inner target; inbound messages
    /// arrive via <c>Worker.dispatchMessageFromWorker</c>.
    /// </summary>
    internal sealed class WKWorkerSession : WKTargetSession
    {
        private readonly WKTargetSession _pageSession;
        private readonly string _workerId;

        /// <summary>
        /// Initializes a new instance of the <see cref="WKWorkerSession"/> class.
        /// </summary>
        /// <param name="pageProxy">The page-proxy session (used only as the WKTargetSession parent).</param>
        /// <param name="pageSession">The page inner target that owns the Worker domain.</param>
        /// <param name="connection">The owning connection (shared message ids).</param>
        /// <param name="workerId">The WebKit worker id.</param>
        public WKWorkerSession(WKSession pageProxy, WKTargetSession pageSession, WKConnection connection, string workerId)
            : base(pageProxy, connection, workerId)
        {
            _pageSession = pageSession ?? throw new ArgumentNullException(nameof(pageSession));
            _workerId = workerId ?? throw new ArgumentNullException(nameof(workerId));
        }

        /// <inheritdoc/>
        internal override Task<JsonElement?> SendAsync(string method, object parameters = null)
        {
            if (IsDisposed)
            {
                return Task.FromException<JsonElement?>(
                    new TargetClosedException($"WKWorkerSession {_workerId} is closed"));
            }

            (int id, TaskCompletionSource<JsonElement?> tcs) = EnqueueCommand();
            string innerJson = CreateInnerMessage(id, method, parameters);
            _ = _pageSession.SendAsync(
                "Worker.sendMessageToWorker",
                new { workerId = _workerId, message = innerJson });
            return tcs.Task;
        }
    }
}
