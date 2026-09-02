/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Public <see cref="ICDPSession"/> wrapper around a child <see cref="CRSession"/>.
    /// </summary>
    internal sealed partial class CRCDPSession : ICDPSession
    {
        private readonly CRSession _session;
        private readonly CRSession _rootSession;
        private readonly Dictionary<string, CRCDPSessionEvent> _eventSubscriptions = new(StringComparer.Ordinal);
        private bool _detached;

        internal CRCDPSession(CRSession session, CRSession rootSession)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _rootSession = rootSession ?? throw new ArgumentNullException(nameof(rootSession));
            _session.MessageReceived += OnMessageReceived;
            _session.Closed += OnSessionClosed;
        }

        /// <inheritdoc/>
        public event EventHandler<ICDPSession> Close;

        /// <inheritdoc/>
        public Task<JsonElement?> SendAsync(string method, object args = null)
            => _session.SendAsync(method, args);

        /// <inheritdoc/>
        public ICDPSessionEvent Event(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                throw new ArgumentException("eventName must be non-empty.", nameof(eventName));
            }

            if (!_eventSubscriptions.TryGetValue(eventName, out CRCDPSessionEvent subscription))
            {
                subscription = new CRCDPSessionEvent(eventName);
                _eventSubscriptions[eventName] = subscription;
            }

            return subscription;
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await DetachAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task DetachAsync()
        {
            if (_session.IsClosed)
            {
                throw new TargetClosedException(DriverMessages.BrowserOrContextClosedExceptionMessage);
            }

            if (_detached)
            {
                return;
            }

            _detached = true;
            try
            {
                await _rootSession.SendAsync("Target.detachFromTarget", new
                {
                    sessionId = _session.SessionId,
                }).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                // Target already gone.
            }

            _session.Dispose();
        }

        /// <summary>
        /// Official: page close detaches user CDP sessions so later
        /// <see cref="DetachAsync"/> / <see cref="SendAsync"/> throw.
        /// </summary>
        internal void NotifyTargetClosed()
        {
            _session.Dispose();
        }

        private void OnMessageReceived(string method, JsonElement? parameters)
        {
            if (_eventSubscriptions.TryGetValue(method, out CRCDPSessionEvent subscription))
            {
                subscription.Raise(parameters);
            }
        }

        private void OnSessionClosed(object sender, EventArgs e)
        {
            _session.MessageReceived -= OnMessageReceived;
            _session.Closed -= OnSessionClosed;
            _detached = true;
            Close?.Invoke(this, this);
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task<JsonElement?> ICDPSession.SendAsync(string method, Dictionary<string, object> args)
            => SendAsync(method, (object)args);
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
