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
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PlaywrightNative.Helpers
{
    /// <summary>A coverage report of one document, or a stash it left behind.</summary>
    internal sealed class IstanbulCoverageChunk
    {
        /// <summary>Gets or sets the coverage delta.</summary>
        [JsonPropertyName("data")]
        public Dictionary<string, IstanbulFileCoverage> Data { get; set; }

        /// <summary>Gets or sets the stash id, set only for stashes.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }
    }
}
