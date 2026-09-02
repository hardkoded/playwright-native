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
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Loads official <see cref="ClientCertificate"/> values and matches them
    /// to a request origin.
    /// </summary>
    internal static class ClientCertificateHelper
    {
        internal const string MissingMaterialMessage =
            "None of cert, key, passphrase or pfx is specified";

        internal const string PfxConflictMessage =
            "pfx is specified together with cert, key or passphrase";

        internal const string MacVerifyFailureMessage = "mac verify failure";

        internal const string UnsupportedTlsCertificateMessage =
            "Unsupported TLS certificate";

        internal const string SelfSignedCertificateMessage = "self-signed certificate";

        internal const string TlsDisconnectedMessage =
            "Client network socket disconnected before secure TLS connection was established";

        internal const string FailedToLoadPrefix = "Failed to load client certificate: ";

        /// <summary>
        /// Returns a snapshot of <paramref name="certificates"/>, or
        /// <see langword="null"/> when none were provided.
        /// </summary>
        /// <param name="certificates">Caller certificates, or <see langword="null"/>.</param>
        /// <returns>A copied list, or <see langword="null"/>.</returns>
        internal static IReadOnlyList<ClientCertificate> Snapshot(IEnumerable<ClientCertificate> certificates)
        {
            if (certificates == null)
            {
                return null;
            }

            List<ClientCertificate> copy = new List<ClientCertificate>();
            foreach (ClientCertificate certificate in certificates)
            {
                if (certificate != null)
                {
                    copy.Add(certificate);
                }
            }

            return copy.Count == 0 ? null : copy;
        }

        /// <summary>
        /// Returns <see langword="true"/> when at least one certificate bag
        /// was provided.
        /// </summary>
        /// <param name="certificates">Caller certificates, or <see langword="null"/>.</param>
        /// <returns>Whether a client-certificate proxy should start.</returns>
        internal static bool HasAny(IEnumerable<ClientCertificate> certificates)
        {
            if (certificates == null)
            {
                return false;
            }

            foreach (ClientCertificate certificate in certificates)
            {
                if (certificate != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Official <c>verifyClientCertificates</c>. Path fields count as the
        /// corresponding buffer after the Node client reads them.
        /// </summary>
        /// <param name="certificates">Configured certificates, or <see langword="null"/>.</param>
        internal static void Verify(IEnumerable<ClientCertificate> certificates)
        {
            if (certificates == null)
            {
                return;
            }

            foreach (ClientCertificate certificate in certificates)
            {
                if (certificate == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(certificate.Origin))
                {
                    throw new PlaywrightNativeException("clientCertificates.origin is required");
                }

                bool hasCert = HasBytes(certificate.Cert) || !string.IsNullOrEmpty(certificate.CertPath);
                bool hasKey = HasBytes(certificate.Key) || !string.IsNullOrEmpty(certificate.KeyPath);
                bool hasPfx = HasBytes(certificate.Pfx) || !string.IsNullOrEmpty(certificate.PfxPath);
                bool hasPassphrase = !string.IsNullOrEmpty(certificate.Passphrase);
                if (!hasCert && !hasKey && !hasPfx && !hasPassphrase)
                {
                    throw new PlaywrightNativeException(MissingMaterialMessage);
                }

                if (hasCert && !hasKey)
                {
                    throw new PlaywrightNativeException("cert is specified without key");
                }

                if (!hasCert && hasKey)
                {
                    throw new PlaywrightNativeException("key is specified without cert");
                }

                if (hasPfx && (hasCert || hasKey))
                {
                    throw new PlaywrightNativeException(PfxConflictMessage);
                }
            }
        }

        /// <summary>
        /// Official <c>normalizeOrigin</c>: <c>new URL(origin).origin</c>.
        /// </summary>
        /// <param name="origin">Configured or request origin.</param>
        /// <returns>Scheme, host, and non-default port.</returns>
        internal static string NormalizeOrigin(string origin)
        {
            if (string.IsNullOrEmpty(origin))
            {
                return origin;
            }

            if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri uri))
            {
                return origin;
            }

            return uri.GetLeftPart(UriPartial.Authority);
        }

        /// <summary>
        /// Rewrites OpenSSL / .NET load failures to official strings.
        /// </summary>
        /// <param name="ex">The load failure.</param>
        /// <param name="forBrowser">
        /// When <see langword="true"/>, prefix
        /// <c>Failed to load client certificate:</c>.
        /// </param>
        /// <returns>A <see cref="PlaywrightNativeException"/>.</returns>
        internal static PlaywrightNativeException RewriteLoadException(Exception ex, bool forBrowser)
        {
            string rewritten = RewriteLoadMessage(ex);
            if (forBrowser && !rewritten.StartsWith(FailedToLoadPrefix, StringComparison.Ordinal))
            {
                rewritten = FailedToLoadPrefix + rewritten;
            }

            return new PlaywrightNativeException(rewritten, ex);
        }

        /// <summary>
        /// Rewrites outbound TLS failures to official Node strings.
        /// </summary>
        /// <param name="ex">The TLS failure.</param>
        /// <returns>Official error text.</returns>
        internal static string RewriteTlsMessage(Exception ex)
        {
            if (ex == null)
            {
                return TlsDisconnectedMessage;
            }

            string message = FlattenMessage(ex);
            if (IsMacVerifyFailure(message))
            {
                return MacVerifyFailureMessage;
            }

            if (IsUnsupportedCertificate(message, ex))
            {
                return UnsupportedTlsCertificateMessage;
            }

            if (IsSelfSigned(message, ex))
            {
                return SelfSignedCertificateMessage;
            }

            if (IsHandshakeDisconnect(message, ex))
            {
                return TlsDisconnectedMessage;
            }

            return string.IsNullOrEmpty(ex.Message) ? TlsDisconnectedMessage : FirstLine(ex.Message);
        }

        /// <summary>
        /// Loads the first certificate whose <see cref="ClientCertificate.Origin"/>
        /// matches <paramref name="requestUrl"/>.
        /// </summary>
        /// <param name="certificates">Configured certificates, or <see langword="null"/>.</param>
        /// <param name="requestUrl">Absolute request URL.</param>
        /// <returns>The matching certificate, or <see langword="null"/>.</returns>
        internal static X509Certificate2 LoadMatching(IEnumerable<ClientCertificate> certificates, string requestUrl)
        {
            if (certificates == null || string.IsNullOrEmpty(requestUrl))
            {
                return null;
            }

            foreach (ClientCertificate certificate in certificates)
            {
                if (certificate == null || !MatchesOrigin(certificate.Origin, requestUrl))
                {
                    continue;
                }

                try
                {
                    return Load(certificate);
                }
                catch (PlaywrightNativeException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw RewriteLoadException(ex, forBrowser: false);
                }
            }

            return null;
        }

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="origin"/> is empty
        /// or equals the origin of <paramref name="requestUrl"/>.
        /// </summary>
        /// <param name="origin">Configured origin, or <see langword="null"/>.</param>
        /// <param name="requestUrl">Absolute request URL.</param>
        /// <returns>Whether the certificate may be used.</returns>
        internal static bool MatchesOrigin(string origin, string requestUrl)
        {
            if (string.IsNullOrEmpty(origin))
            {
                return true;
            }

            if (string.IsNullOrEmpty(requestUrl)
                || !Uri.TryCreate(requestUrl, UriKind.Absolute, out Uri request)
                || !Uri.TryCreate(origin, UriKind.Absolute, out Uri configured))
            {
                return false;
            }

            return string.Equals(request.Scheme, configured.Scheme, StringComparison.OrdinalIgnoreCase)
                && string.Equals(request.Host, configured.Host, StringComparison.OrdinalIgnoreCase)
                && EffectivePort(request) == EffectivePort(configured);
        }

        /// <summary>
        /// Loads a PEM or PFX client certificate.
        /// </summary>
        /// <param name="certificate">The official option bag.</param>
        /// <returns>A certificate that includes the private key.</returns>
        internal static X509Certificate2 Load(ClientCertificate certificate)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            try
            {
                if (certificate.Pfx != null || !string.IsNullOrEmpty(certificate.PfxPath))
                {
                    byte[] pfx = certificate.Pfx ?? File.ReadAllBytes(certificate.PfxPath);
                    ThrowIfLegacyPfx(pfx);
                    return Normalize(LoadPkcs12(pfx, certificate.Passphrase ?? string.Empty));
                }

                string certPem = ReadPem(certificate.Cert, certificate.CertPath, "cert");
                string keyPem = ReadPem(certificate.Key, certificate.KeyPath, "key");
                X509Certificate2 loaded = string.IsNullOrEmpty(certificate.Passphrase)
                    ? X509Certificate2.CreateFromPem(certPem, keyPem)
                    : X509Certificate2.CreateFromEncryptedPem(certPem, keyPem, certificate.Passphrase);
                return Normalize(loaded);
            }
            catch (PlaywrightNativeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw RewriteLoadException(ex, forBrowser: false);
            }
        }

        /// <summary>
        /// Loads every certificate and rewrites failures the way official
        /// <c>_initSecureContexts</c> does at context creation.
        /// </summary>
        /// <param name="certificates">Configured certificates.</param>
        /// <returns>Origin to certificate map.</returns>
        internal static Dictionary<string, X509Certificate2> LoadAllForBrowser(
            IEnumerable<ClientCertificate> certificates)
        {
            Dictionary<string, X509Certificate2> map = new(StringComparer.Ordinal);
            if (certificates == null)
            {
                return map;
            }

            foreach (ClientCertificate certificate in certificates)
            {
                if (certificate == null)
                {
                    continue;
                }

                string origin = NormalizeOrigin(certificate.Origin);
                try
                {
                    map[origin] = Load(certificate);
                }
                catch (PlaywrightNativeException ex)
                {
                    throw new PlaywrightNativeException(
                        FailedToLoadPrefix + StripFailedPrefix(ex.Message),
                        ex);
                }
                catch (Exception ex)
                {
                    throw RewriteLoadException(ex, forBrowser: true);
                }
            }

            return map;
        }

        /// <summary>
        /// Loads a PKCS#12 blob with an exportable private key.
        /// </summary>
        /// <param name="pfx">PKCS#12 bytes.</param>
        /// <param name="password">Archive password, or empty.</param>
        /// <returns>The loaded certificate.</returns>
        internal static X509Certificate2 LoadPkcs12(byte[] pfx, string password)
        {
#if NET9_0_OR_GREATER
            return X509CertificateLoader.LoadPkcs12(pfx, password, X509KeyStorageFlags.Exportable);
#else
#pragma warning disable SYSLIB0057 // Obsolete ctor is the netstandard2.1 path.
            return new X509Certificate2(pfx, password, X509KeyStorageFlags.Exportable);
#pragma warning restore SYSLIB0057
#endif
        }

        private static string ReadPem(byte[] bytes, string path, string kind)
        {
            if (bytes != null && bytes.Length > 0)
            {
                return Encoding.UTF8.GetString(bytes);
            }

            if (string.IsNullOrEmpty(path))
            {
                throw new PlaywrightNativeException(
                    "Client certificate must provide " + kind + " bytes or path, or a PFX.");
            }

            return File.ReadAllText(path);
        }

        private static X509Certificate2 Normalize(X509Certificate2 certificate)
        {
            byte[] pfx = certificate.Export(X509ContentType.Pfx);
            certificate.Dispose();
            return LoadPkcs12(pfx, string.Empty);
        }

        private static int EffectivePort(Uri uri)
        {
            if (!uri.IsDefaultPort)
            {
                return uri.Port;
            }

            return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                ? 443
                : 80;
        }

        private static bool HasBytes(byte[] bytes) => bytes != null && bytes.Length > 0;

        private static string StripFailedPrefix(string message)
        {
            if (!string.IsNullOrEmpty(message)
                && message.StartsWith(FailedToLoadPrefix, StringComparison.Ordinal))
            {
                return message.Substring(FailedToLoadPrefix.Length);
            }

            return message;
        }

        private static string RewriteLoadMessage(Exception ex)
        {
            string message = FlattenMessage(ex);
            if (IsMacVerifyFailure(message))
            {
                return MacVerifyFailureMessage;
            }

            if (IsUnsupportedCertificate(message, ex))
            {
                return UnsupportedTlsCertificateMessage;
            }

            return FirstLine(ex?.Message) ?? "Failed to load client certificate";
        }

        private static void ThrowIfLegacyPfx(byte[] pfx)
        {
            if (ContainsLegacyPbe(pfx))
            {
                throw new PlaywrightNativeException(UnsupportedTlsCertificateMessage);
            }
        }

        private static bool ContainsLegacyPbe(byte[] pfx)
        {
            if (pfx == null || pfx.Length == 0)
            {
                return false;
            }

            // pbeWithSHA1And40BitRC2-CBC (1.2.840.113549.1.12.1.6) and
            // RC2-CBC (1.2.840.113549.3.2) used by official cert-legacy.pfx.
            return ContainsOid(pfx, new byte[] { 0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x0c, 0x01, 0x06 })
                || ContainsOid(pfx, new byte[] { 0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x03, 0x02 });
        }

        private static bool ContainsOid(byte[] data, byte[] oid)
        {
            for (int i = 0; i + oid.Length <= data.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < oid.Length; j++)
                {
                    if (data[i + j] != oid[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsMacVerifyFailure(string message)
            => ContainsAny(
                message,
                "mac verify failure",
                "mac verification",
                "network password is not correct",
                "password is not correct",
                "the specified network password",
                "invalid password",
                "password was incorrect",
                "provided password",
                "password may be incorrect");

        private static bool IsUnsupportedCertificate(string message, Exception ex)
            => ex is CryptographicException
                && ContainsAny(
                    message,
                    "unsupported",
                    "err_crypto_unsupported",
                    "rc2",
                    "legacy",
                    "not supported",
                    "algorithm");

        private static bool IsSelfSigned(string message, Exception ex)
            => ContainsAny(
                    message,
                    "untrustedroot",
                    "self-signed",
                    "self signed",
                    "remote certificate is invalid",
                    "certificate chain",
                    "not trusted",
                    "untrusted root",
                    "SslPolicyErrors");

        private static bool IsHandshakeDisconnect(string message, Exception ex)
            => ex is IOException
                || ContainsAny(
                    message,
                    "forcibly closed",
                    "connection reset",
                    "transport stream",
                    "unexpected packet",
                    "disconnected",
                    "handshake failed");

        private static bool ContainsAny(string message, params string[] tokens)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            foreach (string token in tokens)
            {
                if (message.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string FlattenMessage(Exception ex)
        {
            if (ex == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new();
            for (Exception current = ex; current != null; current = current.InnerException)
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(current.Message);
            }

            return builder.ToString();
        }

        private static string FirstLine(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return message;
            }

            int end = message.IndexOf('\n');
            return end < 0 ? message.Trim() : message.Substring(0, end).Trim();
        }
    }
}
