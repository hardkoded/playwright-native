/*
 * Copyright (c) 2026 Dario Kondratiuk
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
using System.Collections.Generic;

namespace PlaywrightNative
{
    /// <summary>
    /// A WHATWG URL record (https://url.spec.whatwg.org/#concept-url). <see cref="System.Uri"/> does not follow
    /// the WHATWG rules, so URL pattern matching uses this record instead.
    /// </summary>
    internal sealed class UrlRecord
    {
        public string Scheme { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the serialized host, or null when the URL has no host.
        /// </summary>
        public string Host { get; set; }

        public int? Port { get; set; }

        public List<string> Path { get; set; } = [];

        /// <summary>
        /// Gets or sets the opaque path. It is null unless the URL has an opaque path.
        /// </summary>
        public string OpaquePath { get; set; }

        public string Query { get; set; }

        public string Fragment { get; set; }

        public bool IsSpecial => DefaultPort(Scheme) != null || Scheme == "file";

        public bool HasOpaquePath => OpaquePath != null;

        public bool IncludesCredentials => Username.Length > 0 || Password.Length > 0;

        /// <summary>
        /// Returns the default port of a special scheme, or null.
        /// </summary>
        public static int? DefaultPort(string scheme) => scheme switch
        {
            "ftp" => 21,
            "http" or "ws" => 80,
            "https" or "wss" => 443,
            _ => null,
        };

        public static bool IsSpecialScheme(string scheme) => DefaultPort(scheme) != null || scheme == "file";

        /// <summary>
        /// Creates the dummy URL "https://dummy.invalid/" that canonicalization runs against.
        /// </summary>
        public static UrlRecord CreateDummy() => new() { Scheme = "https", Host = "dummy.invalid", Path = [string.Empty] };

        /// <summary>
        /// The URL path serializer (https://url.spec.whatwg.org/#url-path-serializer).
        /// </summary>
        public string SerializePath() => HasOpaquePath ? OpaquePath : (Path.Count == 0 ? string.Empty : "/" + string.Join('/', Path));

        /// <summary>
        /// Shortens the path (https://url.spec.whatwg.org/#shorten-a-urls-path).
        /// </summary>
        public void ShortenPath()
        {
            if (Scheme == "file" && Path.Count == 1 && UrlParser.IsNormalizedWindowsDriveLetter(Path[0]))
            {
                return;
            }

            if (Path.Count > 0)
            {
                Path.RemoveAt(Path.Count - 1);
            }
        }
    }
}
