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
using System;
using System.Security.Cryptography;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Builds official <see cref="VirtualCredential"/> values (ECDSA P-256).
    /// </summary>
    internal static class VirtualCredentialFactory
    {
        /// <summary>
        /// Creates a credential, generating any omitted fields.
        /// </summary>
        /// <param name="rpId">Relying party id.</param>
        /// <param name="id">Optional base64url credential id.</param>
        /// <param name="userHandle">Optional base64url user handle.</param>
        /// <param name="privateKey">Optional base64url PKCS#8 private key.</param>
        /// <param name="publicKey">Optional base64url SPKI public key.</param>
        /// <returns>A complete credential.</returns>
        internal static VirtualCredential Create(string rpId, string id, string userHandle, string privateKey, string publicKey)
        {
            if (string.IsNullOrEmpty(privateKey) || string.IsNullOrEmpty(publicKey))
            {
                using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                privateKey = ToBase64Url(ecdsa.ExportPkcs8PrivateKey());
                publicKey = ToBase64Url(ecdsa.ExportSubjectPublicKeyInfo());
            }

            return new VirtualCredential
            {
                RpId = rpId,
                Id = string.IsNullOrEmpty(id) ? RandomBase64Url(32) : id,
                UserHandle = string.IsNullOrEmpty(userHandle) ? RandomBase64Url(32) : userHandle,
                PrivateKey = privateKey,
                PublicKey = publicKey,
            };
        }

        /// <summary>
        /// Converts a base64url string to standard base64 for CDP.
        /// </summary>
        /// <param name="base64Url">A base64url payload.</param>
        /// <returns>Standard base64, or <see langword="null"/> when empty.</returns>
        internal static string ToCdpBase64(string base64Url)
        {
            byte[] bytes = FromBase64Url(base64Url);
            return bytes == null ? null : Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Decodes a base64url payload.
        /// </summary>
        /// <param name="base64Url">A base64url string.</param>
        /// <returns>The bytes, or <see langword="null"/> when empty.</returns>
        internal static byte[] FromBase64Url(string base64Url)
        {
            if (string.IsNullOrEmpty(base64Url))
            {
                return null;
            }

            string padded = base64Url.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2:
                    padded += "==";
                    break;
                case 3:
                    padded += "=";
                    break;
            }

            return Convert.FromBase64String(padded);
        }

        /// <summary>
        /// Encodes bytes as unpadded base64url.
        /// </summary>
        /// <param name="bytes">The payload.</param>
        /// <returns>A base64url string.</returns>
        internal static string ToBase64Url(byte[] bytes)
            => Convert.ToBase64String(bytes ?? Array.Empty<byte>()).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        /// <summary>
        /// Returns <paramref name="byteCount"/> random bytes as base64url.
        /// </summary>
        /// <param name="byteCount">How many random bytes to generate.</param>
        /// <returns>A base64url string.</returns>
        internal static string RandomBase64Url(int byteCount)
        {
            byte[] bytes = new byte[byteCount];
            RandomNumberGenerator.Fill(bytes);
            return ToBase64Url(bytes);
        }
    }
}
