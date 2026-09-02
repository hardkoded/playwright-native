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

namespace PlaywrightNative
{
    /// <summary>
    /// Response captured by <see cref="IRoute.FetchAsync"/>.
    /// </summary>
    public sealed class RouteFetchResult
    {
        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// HTTP status text.
        /// </summary>
        public string StatusText { get; set; }

        /// <summary>
        /// Final response URL after redirects.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Response headers. Names are lower-cased.
        /// </summary>
        public IReadOnlyDictionary<string, string> Headers { get; set; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Response body. Never <see langword="null"/>.
        /// </summary>
        public byte[] Body { get; set; } = Array.Empty<byte>();
    }
}
