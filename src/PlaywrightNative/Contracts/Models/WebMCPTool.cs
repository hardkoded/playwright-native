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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlaywrightNative
{
    /// <summary>
    /// A WebMCP tool registered by a frame.
    /// </summary>
    public sealed class WebMCPTool
    {
        /// <summary>
        /// Tool name, unique within the frame.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Tool description.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// JSON Schema of the tool input, when the page provides one.
        /// </summary>
        [JsonPropertyName("inputSchema")]
        public JsonElement? InputSchema { get; set; }

        /// <summary>
        /// Hints the page provides about the tool, or <see langword="null"/> when it provides none.
        /// </summary>
        [JsonPropertyName("annotations")]
        public WebMCPToolAnnotations Annotations { get; set; }
    }
}
