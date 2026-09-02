/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Text.Json;

namespace PlaywrightSharp.Chromium
{
    /// <summary>
    /// Subscription handle returned by <see cref="CRCDPSession.Event"/>.
    /// </summary>
    internal sealed class CRCDPSessionEvent : ICDPSessionEvent
    {
        internal CRCDPSessionEvent(string eventName)
        {
            EventName = eventName;
        }

        /// <inheritdoc/>
        public event EventHandler<JsonElement?> OnEvent;

        /// <inheritdoc/>
        public string EventName { get; }

        internal void Raise(JsonElement? parameters) => OnEvent?.Invoke(this, parameters);
    }
}
