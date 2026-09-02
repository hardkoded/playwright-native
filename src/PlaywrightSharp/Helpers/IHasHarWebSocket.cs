/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Page WebSocket that exposes HAR handshake details.
    /// </summary>
    internal interface IHasHarWebSocket
    {
        /// <summary>
        /// Handshake headers, status, and wall-clock baseline.
        /// </summary>
        HarWebSocketState Har { get; }
    }
}
