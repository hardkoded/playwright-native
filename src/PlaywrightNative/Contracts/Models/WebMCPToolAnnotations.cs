/*
 * Copyright (c) Microsoft Corporation.
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
    /// Hints a page provides about a <see cref="WebMCPTool"/>. Only hints the
    /// page set to <see langword="true"/> are reported.
    /// </summary>
    public sealed class WebMCPToolAnnotations
    {
        /// <summary>
        /// The tool does not modify any state.
        /// </summary>
        [JsonPropertyName("readOnly")]
        public bool? ReadOnly { get; set; }

        /// <summary>
        /// The tool output may contain third-party content.
        /// </summary>
        [JsonPropertyName("untrustedContent")]
        public bool? UntrustedContent { get; set; }

        /// <summary>
        /// The tool takes a consequential action, such as placing an order.
        /// </summary>
        [JsonPropertyName("consequential")]
        public bool? Consequential { get; set; }
    }
}
