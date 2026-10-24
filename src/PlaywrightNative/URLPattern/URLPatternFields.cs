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
    /// A mutable URLPatternInit dictionary. A null value means the member does not exist.
    /// </summary>
    internal sealed class URLPatternFields
    {
        public string Protocol { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        public string Hostname { get; set; }

        public string Port { get; set; }

        public string Pathname { get; set; }

        public string Search { get; set; }

        public string Hash { get; set; }

        public string BaseURL { get; set; }

        public static URLPatternFields From(URLPatternInit init) => new()
        {
            Protocol = UrlCodePoints.ToUsvString(init.Protocol),
            Username = UrlCodePoints.ToUsvString(init.Username),
            Password = UrlCodePoints.ToUsvString(init.Password),
            Hostname = UrlCodePoints.ToUsvString(init.Hostname),
            Port = UrlCodePoints.ToUsvString(init.Port),
            Pathname = UrlCodePoints.ToUsvString(init.Pathname),
            Search = UrlCodePoints.ToUsvString(init.Search),
            Hash = UrlCodePoints.ToUsvString(init.Hash),
            BaseURL = UrlCodePoints.ToUsvString(init.BaseURL),
        };
    }
}
