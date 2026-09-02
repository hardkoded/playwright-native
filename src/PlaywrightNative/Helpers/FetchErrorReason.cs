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

namespace PlaywrightNative.Helpers
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
