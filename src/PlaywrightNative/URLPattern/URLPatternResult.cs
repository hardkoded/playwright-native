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
namespace PlaywrightNative
{
    /// <summary>
    /// The result of <see cref="URLPattern.Exec(string, string)"/>. Mirrors the JavaScript <c>URLPatternResult</c>
    /// dictionary, without its <c>inputs</c> member.
    /// </summary>
    public sealed class URLPatternResult
    {
        internal URLPatternResult(
            URLPatternComponentResult protocol,
            URLPatternComponentResult username,
            URLPatternComponentResult password,
            URLPatternComponentResult hostname,
            URLPatternComponentResult port,
            URLPatternComponentResult pathname,
            URLPatternComponentResult search,
            URLPatternComponentResult hash)
        {
            Protocol = protocol;
            Username = username;
            Password = password;
            Hostname = hostname;
            Port = port;
            Pathname = pathname;
            Search = search;
            Hash = hash;
        }

        /// <summary>
        /// Gets the protocol match.
        /// </summary>
        public URLPatternComponentResult Protocol { get; }

        /// <summary>
        /// Gets the username match.
        /// </summary>
        public URLPatternComponentResult Username { get; }

        /// <summary>
        /// Gets the password match.
        /// </summary>
        public URLPatternComponentResult Password { get; }

        /// <summary>
        /// Gets the hostname match.
        /// </summary>
        public URLPatternComponentResult Hostname { get; }

        /// <summary>
        /// Gets the port match.
        /// </summary>
        public URLPatternComponentResult Port { get; }

        /// <summary>
        /// Gets the pathname match.
        /// </summary>
        public URLPatternComponentResult Pathname { get; }

        /// <summary>
        /// Gets the search match.
        /// </summary>
        public URLPatternComponentResult Search { get; }

        /// <summary>
        /// Gets the hash match.
        /// </summary>
        public URLPatternComponentResult Hash { get; }
    }
}
