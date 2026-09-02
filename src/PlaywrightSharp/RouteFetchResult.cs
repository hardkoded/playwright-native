/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;

namespace PlaywrightSharp
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
