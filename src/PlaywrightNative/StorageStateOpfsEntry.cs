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
using System.Text.Json.Serialization;

namespace PlaywrightNative
{
    /// <summary>
    /// One Origin Private File System entry in <see cref="StorageStateOrigin.Opfs"/>
    /// (official <c>OPFSEntry</c>).
    /// </summary>
    internal sealed record StorageStateOpfsEntry
    {
        /// <summary>
        /// Slash-separated path relative to the OPFS root.
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; init; }

        /// <summary>
        /// <c>file</c> or <c>directory</c>.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; init; }

        /// <summary>
        /// Base64 file contents. Omitted for directories.
        /// </summary>
        [JsonPropertyName("base64")]
        public string Base64 { get; init; }
    }
}
