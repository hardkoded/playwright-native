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
    /// A virtual WebAuthn passkey in <see cref="StorageState.Credentials"/>
    /// (official <c>SetVirtualCredential</c>).
    /// </summary>
    internal sealed record StorageStateCredential
    {
        /// <summary>
        /// Credential id (base64url).
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; init; }

        /// <summary>
        /// Relying party id.
        /// </summary>
        [JsonPropertyName("rpId")]
        public string RpId { get; init; }

        /// <summary>
        /// User handle (base64url).
        /// </summary>
        [JsonPropertyName("userHandle")]
        public string UserHandle { get; init; }

        /// <summary>
        /// Private key (base64url DER PKCS#8).
        /// </summary>
        [JsonPropertyName("privateKey")]
        public string PrivateKey { get; init; }

        /// <summary>
        /// Public key (base64url DER SPKI).
        /// </summary>
        [JsonPropertyName("publicKey")]
        public string PublicKey { get; init; }

        /// <summary>
        /// Signature counter. Storage state saved by older versions has no
        /// value, and the counter then starts from zero.
        /// </summary>
        [JsonPropertyName("signCount")]
        public long? SignCount { get; init; }
    }
}
