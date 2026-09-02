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
