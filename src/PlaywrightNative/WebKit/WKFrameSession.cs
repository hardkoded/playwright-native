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
using Microsoft.Extensions.Logging;

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// Per-frame Console session used on WebKit revisions that emit frame targets
    /// (upstream <c>WKFrame</c> / <c>enableFrameSessions</c>). The page session has no
    /// Console domain on those builds; javascript errors and <c>console.*</c> arrive here.
    /// </summary>
    internal sealed class WKFrameSession : IDisposable
    {
        private readonly WKTargetSession _session;
        private readonly ILogger _logger;
        private readonly Action<JsonElement?> _onMessageAdded;
        private readonly Action<JsonElement?> _onRepeatCountUpdated;
        private readonly object _initGate = new();
        private Task _initializeTask;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="WKFrameSession"/> class.
        /// </summary>
        /// <param name="session">The inner frame target session.</param>
        /// <param name="logger">Optional logger.</param>
        /// <param name="onMessageAdded">Forwards <c>Console.messageAdded</c>.</param>
        /// <param name="onRepeatCountUpdated">Forwards <c>Console.messageRepeatCountUpdated</c>.</param>
        internal WKFrameSession(
            WKTargetSession session,
            ILogger logger,
            Action<JsonElement?> onMessageAdded,
            Action<JsonElement?> onRepeatCountUpdated)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _logger = logger;
            _onMessageAdded = onMessageAdded ?? throw new ArgumentNullException(nameof(onMessageAdded));
            _onRepeatCountUpdated = onRepeatCountUpdated ?? throw new ArgumentNullException(nameof(onRepeatCountUpdated));
            _session.MessageReceived += OnMessage;
        }

        /// <summary>
        /// Gets the frame target id.
        /// </summary>
        internal string TargetId => _session.TargetId;

        /// <summary>
        /// Gets the underlying target session used for inbound dispatch.
        /// </summary>
        internal WKTargetSession Session => _session;

        /// <summary>
        /// Gets a value indicating whether <c>Console.enable</c> has finished for this frame.
        /// </summary>
        internal bool IsInitialized
        {
            get
            {
                lock (_initGate)
                {
                    return _initializeTask != null && _initializeTask.IsCompleted;
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (_initGate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            _session.MessageReceived -= OnMessage;
            _session.Dispose();
        }

        /// <summary>
        /// Enables the Console domain and starts forwarding events to the page.
        /// Idempotent; concurrent callers share one initialize task.
        /// </summary>
        /// <returns>A task that completes when Console is enabled.</returns>
        internal Task InitializeAsync()
        {
            lock (_initGate)
            {
                if (_disposed)
                {
                    return Task.CompletedTask;
                }

                _initializeTask ??= InitializeCoreAsync();
                return _initializeTask;
            }
        }

        private async Task InitializeCoreAsync()
        {
            try
            {
                // Child frames inherit Console agent state from the main frame, but
                // upstream keeps enable uniform for every frame session.
                await _session.SendAsync("Console.enable").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Frame Console.enable failed for {TargetId}", _session.TargetId);
            }
        }

        private void OnMessage(string method, JsonElement? parameters)
        {
            switch (method)
            {
                case "Console.messageAdded":
                    _onMessageAdded(parameters);
                    break;
                case "Console.messageRepeatCountUpdated":
                    _onRepeatCountUpdated(parameters);
                    break;
            }
        }
    }
}
