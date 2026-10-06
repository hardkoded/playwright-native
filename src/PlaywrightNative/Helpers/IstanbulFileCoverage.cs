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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Coverage of one file. In a delta the maps are <see langword="null"/>
    /// unless it is the first report of the file.
    /// </summary>
    internal sealed class IstanbulFileCoverage
    {
        /// <summary>Gets or sets the file path.</summary>
        [JsonPropertyName("path")]
        public string Path { get; set; }

        /// <summary>Gets or sets the statement locations.</summary>
        [JsonPropertyName("statementMap")]
        public Dictionary<string, JsonElement> StatementMap { get; set; }

        /// <summary>Gets or sets the function mappings.</summary>
        [JsonPropertyName("fnMap")]
        public Dictionary<string, JsonElement> FnMap { get; set; }

        /// <summary>Gets or sets the branch mappings.</summary>
        [JsonPropertyName("branchMap")]
        public Dictionary<string, IstanbulBranchMapping> BranchMap { get; set; }

        /// <summary>Gets or sets the statement counters.</summary>
        [JsonPropertyName("s")]
        public Dictionary<string, long> S { get; set; } = new();

        /// <summary>Gets or sets the function counters.</summary>
        [JsonPropertyName("f")]
        public Dictionary<string, long> F { get; set; } = new();

        /// <summary>Gets or sets the branch counters.</summary>
        [JsonPropertyName("b")]
        public Dictionary<string, List<long>> B { get; set; } = new();
    }
}
