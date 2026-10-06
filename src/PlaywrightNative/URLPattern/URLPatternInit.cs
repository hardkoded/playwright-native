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
    /// The separate URL components of a <see cref="URLPattern"/>, or of a URL to test against one.
    /// Mirrors the JavaScript <c>URLPatternInit</c> dictionary. A null property is absent.
    /// </summary>
    public sealed class URLPatternInit
    {
        /// <summary>
        /// Gets the protocol, for example <c>https</c>. A trailing <c>:</c> is removed.
        /// </summary>
        public string Protocol { get; init; }

        /// <summary>
        /// Gets the username.
        /// </summary>
        public string Username { get; init; }

        /// <summary>
        /// Gets the password.
        /// </summary>
        public string Password { get; init; }

        /// <summary>
        /// Gets the hostname, for example <c>*.example.com</c>.
        /// </summary>
        public string Hostname { get; init; }

        /// <summary>
        /// Gets the port.
        /// </summary>
        public string Port { get; init; }

        /// <summary>
        /// Gets the pathname, for example <c>/books/:id</c>.
        /// </summary>
        public string Pathname { get; init; }

        /// <summary>
        /// Gets the search (query). A leading <c>?</c> is removed.
        /// </summary>
        public string Search { get; init; }

        /// <summary>
        /// Gets the hash (fragment). A leading <c>#</c> is removed.
        /// </summary>
        public string Hash { get; init; }

        /// <summary>
        /// Gets the base URL. It supplies the components that are not given, and resolves a relative pathname.
        /// </summary>
        public string BaseURL { get; init; }
    }
}
