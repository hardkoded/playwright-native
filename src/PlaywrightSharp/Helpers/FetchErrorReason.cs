/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Maps Playwright <see cref="RequestAbortErrorCode"/> values onto Chromium
    /// <c>Fetch.failRequest</c> <c>errorReason</c> strings.
    /// </summary>
    internal static class FetchErrorReason
    {
        /// <summary>
        /// Returns the CDP <c>Network.ErrorReason</c> for <paramref name="errorCode"/>.
        /// </summary>
        /// <param name="errorCode">A Playwright abort error code, or <see langword="null"/>.</param>
        /// <returns>The protocol reason. Defaults to <c>Failed</c>.</returns>
        internal static string ToProtocol(string errorCode)
        {
            if (string.IsNullOrEmpty(errorCode))
            {
                return "Failed";
            }

            string normalized = errorCode.Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
            return normalized switch
            {
                "ABORTED" => "Aborted",
                "ACCESSDENIED" => "AccessDenied",
                "ADDRESSUNREACHABLE" => "AddressUnreachable",
                "BLOCKEDBYCLIENT" => "BlockedByClient",
                "BLOCKEDBYRESPONSE" => "BlockedByResponse",
                "CONNECTIONABORTED" => "ConnectionAborted",
                "CONNECTIONCLOSED" => "ConnectionClosed",
                "CONNECTIONFAILED" => "ConnectionFailed",
                "CONNECTIONREFUSED" => "ConnectionRefused",
                "CONNECTIONRESET" => "ConnectionReset",
                "INTERNETDISCONNECTED" => "InternetDisconnected",
                "NAMENOTRESOLVED" => "NameNotResolved",
                "TIMEDOUT" => "TimedOut",
                "FAILED" => "Failed",
                _ => errorCode,
            };
        }
    }
}
