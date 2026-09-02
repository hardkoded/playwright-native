/*
 * MIT License
 *
 * Copyright (c) 2020 Darío Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace PlaywrightSharp.WebKit
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
