/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// One API-request hop recorded into a HAR session.
    /// </summary>
    internal sealed class ApiHarHop
    {
        internal string Method { get; set; }

        internal string Url { get; set; }

        internal string HttpVersion { get; set; } = "HTTP/1.1";

        internal IEnumerable<KeyValuePair<string, string>> RequestHeaders { get; set; }

        internal IEnumerable<KeyValuePair<string, string>> ResponseHeaders { get; set; }

        internal byte[] PostData { get; set; }

        internal byte[] ResponseBody { get; set; }

        internal int Status { get; set; }

        internal string StatusText { get; set; }

        internal DateTimeOffset Started { get; set; } = DateTimeOffset.UtcNow;

        internal RequestTimingResult Timing { get; set; }

        internal string ServerIpAddress { get; set; }

        internal int? ServerPort { get; set; }

        internal ResponseSecurityDetailsResult SecurityDetails { get; set; }
    }
}
