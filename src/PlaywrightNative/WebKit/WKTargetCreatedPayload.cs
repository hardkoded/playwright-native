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
    /// Payload of the WebKit <c>Target.targetCreated</c> event.
    /// </summary>
    internal sealed class WKTargetCreatedPayload
    {
        /// <summary>
        /// Gets or sets the descriptor of the target that was created.
        /// </summary>
        [JsonPropertyName("targetInfo")]
        public WKTargetInfo TargetInfo { get; set; }
    }
}
