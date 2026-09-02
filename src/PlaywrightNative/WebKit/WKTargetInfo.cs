/*
 * Copyright (c) 2020 Darío Kondratiuk
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

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// Describes a WebKit target reported under a page proxy.
    /// </summary>
    internal sealed class WKTargetInfo
    {
        /// <summary>
        /// Gets or sets the target identifier.
        /// </summary>
        [JsonPropertyName("targetId")]
        public string TargetId { get; set; }

        /// <summary>
        /// Gets or sets the target type — <c>page</c> or <c>frame</c>.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is a provisional target spawned
        /// for a cross-process navigation that has not yet committed.
        /// </summary>
        [JsonPropertyName("isProvisional")]
        public bool IsProvisional { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the target was created paused and
        /// must be resumed via <c>Target.resume</c>.
        /// </summary>
        [JsonPropertyName("isPaused")]
        public bool IsPaused { get; set; }

        /// <summary>
        /// Gets or sets the target URL when the protocol reports one (workers).
        /// </summary>
        [JsonPropertyName("url")]
        public string Url { get; set; }
    }
}
