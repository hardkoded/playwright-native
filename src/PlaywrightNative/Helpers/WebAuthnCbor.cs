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
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official Playwright WebAuthn CBOR / COSE / attestation helpers
    /// from <c>packages/playwright-core/src/server/credentials.ts</c>.
    /// </summary>
    internal static class WebAuthnCbor
    {
        private static readonly byte[] AuthenticatorAaguid = new byte[16];

        /// <summary>
        /// Encodes an ES256 COSE public key (kty=EC2, alg=-7, crv=P-256).
        /// </summary>
        /// <param name="publicKeySpki">SPKI DER public key, or null to use <paramref name="ecdsa"/>.</param>
        /// <param name="ecdsa">An ECDSA P-256 key.</param>
        /// <returns>CBOR-encoded COSE key bytes.</returns>
        internal static byte[] EncodeCoseEs256PublicKey(byte[] publicKeySpki, ECDsa ecdsa)
        {
            ECParameters parameters;
            if (ecdsa != null)
            {
                parameters = ecdsa.ExportParameters(includePrivateParameters: false);
            }
            else
            {
                using ECDsa imported = ECDsa.Create();
                imported.ImportSubjectPublicKeyInfo(publicKeySpki, out _);
                parameters = imported.ExportParameters(includePrivateParameters: false);
            }

            byte[] x = parameters.Q.X ?? Array.Empty<byte>();
            byte[] y = parameters.Q.Y ?? Array.Empty<byte>();
            return Map(new[]
            {
                (Uint(1), Uint(2)),
                (Uint(3), Nint(-7)),
                (Nint(-1), Uint(1)),
                (Nint(-2), Bytes(x)),
                (Nint(-3), Bytes(y)),
            });
        }

        /// <summary>
        /// Encodes a packed <c>none</c> attestation object.
        /// </summary>
        /// <param name="authData">Authenticator data.</param>
        /// <returns>CBOR attestation object.</returns>
        internal static byte[] EncodeAttestationObjectNone(byte[] authData)
            => Map(new[]
            {
                (Text("fmt"), Text("none")),
                (Text("attStmt"), Map(Array.Empty<(byte[], byte[])>())),
                (Text("authData"), Bytes(authData)),
            });

        /// <summary>
        /// Builds authenticator data for a create ceremony (UP|UV|AT).
        /// </summary>
        /// <param name="rpId">Relying party id.</param>
        /// <param name="signCount">Signature counter.</param>
        /// <param name="credentialId">Raw credential id.</param>
        /// <param name="cosePublicKey">COSE public key.</param>
        /// <returns>Authenticator data.</returns>
        internal static byte[] CreateAuthData(string rpId, uint signCount, byte[] credentialId, byte[] cosePublicKey)
        {
            byte[] rpIdHash = HashSha256(Encoding.UTF8.GetBytes(rpId ?? string.Empty));
            byte[] flags = { 0x01 | 0x04 | 0x40 };
            byte[] signCountBuf = U32ToBytes(signCount);
            byte[] credId = credentialId ?? Array.Empty<byte>();
            byte[] credIdLen = { (byte)((credId.Length >> 8) & 0xff), (byte)(credId.Length & 0xff) };
            return Concat(rpIdHash, flags, signCountBuf, AuthenticatorAaguid, credIdLen, credId, cosePublicKey);
        }

        /// <summary>
        /// Builds authenticator data for a get ceremony (UP|UV).
        /// </summary>
        /// <param name="rpId">Relying party id.</param>
        /// <param name="signCount">Signature counter after increment.</param>
        /// <returns>Authenticator data.</returns>
        internal static byte[] GetAuthData(string rpId, uint signCount)
        {
            byte[] rpIdHash = HashSha256(Encoding.UTF8.GetBytes(rpId ?? string.Empty));
            byte[] flags = { 0x01 | 0x04 };
            return Concat(rpIdHash, flags, U32ToBytes(signCount));
        }

        /// <summary>
        /// Signs <paramref name="authData"/> || SHA-256(<paramref name="clientDataJson"/>)
        /// with the PKCS#8 private key (DER ECDSA).
        /// </summary>
        /// <param name="privateKeyPkcs8">PKCS#8 DER private key.</param>
        /// <param name="authData">Authenticator data.</param>
        /// <param name="clientDataJson">Client data JSON bytes.</param>
        /// <returns>DER ECDSA signature.</returns>
        internal static byte[] SignAssertion(byte[] privateKeyPkcs8, byte[] authData, byte[] clientDataJson)
        {
            byte[] clientDataHash = HashSha256(clientDataJson);
            byte[] toSign = Concat(authData, clientDataHash);
            using ECDsa ecdsa = ECDsa.Create();
            ecdsa.ImportPkcs8PrivateKey(privateKeyPkcs8, out _);
            byte[] ieee = ecdsa.SignData(toSign, HashAlgorithmName.SHA256);
            return IeeeP1363ToDer(ieee);
        }

        /// <summary>
        /// Builds official clientDataJSON bytes.
        /// </summary>
        /// <param name="type">webauthn.create or webauthn.get.</param>
        /// <param name="challenge">Base64url challenge.</param>
        /// <param name="origin">Page origin.</param>
        /// <returns>UTF-8 JSON bytes.</returns>
        internal static byte[] ClientDataJson(string type, string challenge, string origin)
        {
            string json = "{\"type\":\"" + Escape(type) + "\",\"challenge\":\"" + Escape(challenge)
                + "\",\"origin\":\"" + Escape(origin) + "\",\"crossOrigin\":false}";
            return Encoding.UTF8.GetBytes(json);
        }

        /// <summary>
        /// Concatenates byte arrays.
        /// </summary>
        /// <param name="parts">Parts to concatenate.</param>
        /// <returns>The concatenated buffer.</returns>
        internal static byte[] Concat(params byte[][] parts)
        {
            int length = 0;
            foreach (byte[] part in parts)
            {
                length += part?.Length ?? 0;
            }

            byte[] result = new byte[length];
            int offset = 0;
            foreach (byte[] part in parts)
            {
                if (part == null || part.Length == 0)
                {
                    continue;
                }

                Buffer.BlockCopy(part, 0, result, offset, part.Length);
                offset += part.Length;
            }

            return result;
        }

        private static byte[] U32ToBytes(uint value)
            => new[]
            {
                (byte)((value >> 24) & 0xff),
                (byte)((value >> 16) & 0xff),
                (byte)((value >> 8) & 0xff),
                (byte)(value & 0xff),
            };

        private static byte[] Head(int major, int value)
        {
            int m = major << 5;
            if (value < 24)
            {
                return new[] { (byte)(m | value) };
            }

            if (value < 0x100)
            {
                return new[] { (byte)(m | 24), (byte)value };
            }

            if (value < 0x10000)
            {
                return new[] { (byte)(m | 25), (byte)((value >> 8) & 0xff), (byte)(value & 0xff) };
            }

            return new[]
            {
                (byte)(m | 26),
                (byte)((value >> 24) & 0xff),
                (byte)((value >> 16) & 0xff),
                (byte)((value >> 8) & 0xff),
                (byte)(value & 0xff),
            };
        }

        private static byte[] Uint(int value) => Head(0, value);

        private static byte[] Nint(int value) => Head(1, -1 - value);

        private static byte[] Bytes(byte[] value)
        {
            byte[] payload = value ?? Array.Empty<byte>();
            return Concat(Head(2, payload.Length), payload);
        }

        private static byte[] Text(string value)
        {
            byte[] payload = Encoding.UTF8.GetBytes(value ?? string.Empty);
            return Concat(Head(3, payload.Length), payload);
        }

        private static byte[] Map(IReadOnlyList<(byte[] Key, byte[] Value)> entries)
        {
            List<byte[]> parts = new List<byte[]> { Head(5, entries?.Count ?? 0) };
            if (entries != null)
            {
                foreach ((byte[] key, byte[] value) in entries)
                {
                    parts.Add(key);
                    parts.Add(value);
                }
            }

            return Concat(parts.ToArray());
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static byte[] HashSha256(byte[] data)
        {
#if NET5_0_OR_GREATER
            return SHA256.HashData(data ?? Array.Empty<byte>());
#else
            using SHA256 sha = SHA256.Create();
            return sha.ComputeHash(data ?? Array.Empty<byte>());
#endif
        }

        private static byte[] IeeeP1363ToDer(byte[] ieee)
        {
            if (ieee == null || ieee.Length < 2 || (ieee.Length % 2) != 0)
            {
                return ieee ?? Array.Empty<byte>();
            }

            int half = ieee.Length / 2;
            byte[] r = DerInteger(ieee, 0, half);
            byte[] s = DerInteger(ieee, half, half);
            byte[] body = Concat(r, s);
            return Concat(new[] { (byte)0x30, (byte)body.Length }, body);
        }

        private static byte[] DerInteger(byte[] ieee, int offset, int length)
        {
            int start = offset;
            int end = offset + length;
            while (start < end - 1 && ieee[start] == 0)
            {
                start++;
            }

            bool leadingZero = (ieee[start] & 0x80) != 0;
            int intLength = (end - start) + (leadingZero ? 1 : 0);
            byte[] result = new byte[2 + intLength];
            result[0] = 0x02;
            result[1] = (byte)intLength;
            int dest = 2;
            if (leadingZero)
            {
                result[dest++] = 0;
            }

            Buffer.BlockCopy(ieee, start, result, dest, end - start);
            return result;
        }
    }
}
