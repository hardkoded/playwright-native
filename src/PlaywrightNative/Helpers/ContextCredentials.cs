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
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>browserContext.credentials</c> virtual WebAuthn
    /// authenticator: in-memory registry plus a page-side interceptor.
    /// </summary>
    internal sealed partial class ContextCredentials : ICredentials
    {
        private readonly IBrowserContext _context;
        private readonly object _lock = new object();
        private readonly Dictionary<string, CredentialRecord> _registry = new Dictionary<string, CredentialRecord>(StringComparer.Ordinal);
        private bool _installed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContextCredentials"/> class.
        /// </summary>
        /// <param name="context">Owning browser context.</param>
        internal ContextCredentials(IBrowserContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <inheritdoc/>
        public async Task InstallAsync()
        {
            bool alreadyInstalled;
            lock (_lock)
            {
                alreadyInstalled = _installed;
                _installed = true;
            }

            if (!alreadyInstalled)
            {
                await _context.ExposeFunctionAsync<JsonElement, object>(
                    WebAuthnInjectScript.BindingName,
                    HandleBinding).ConfigureAwait(false);
                await _context.AddInitScriptAsync(WebAuthnInjectScript.Source).ConfigureAwait(false);
            }

            await EvaluateInjectAsync().ConfigureAwait(false);

            foreach (IPage page in _context.Pages)
            {
                await AttachCdpIfSupportedAsync(page).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<VirtualCredential> CreateAsync(string rpId, string id = default, string userHandle = default, string privateKey = default, string publicKey = default)
        {
            if (string.IsNullOrEmpty(rpId))
            {
                throw new ArgumentException("Relying party id must not be empty.", nameof(rpId));
            }

            VirtualCredential credential = VirtualCredentialFactory.Create(rpId, id, userHandle, privateKey, publicKey);
            CredentialRecord record = new CredentialRecord
            {
                Id = credential.Id,
                RpId = credential.RpId,
                UserHandle = credential.UserHandle,
                PrivateKey = credential.PrivateKey,
                PublicKey = credential.PublicKey,
                SignCount = 0,
                IsResident = true,
            };

            bool installed;
            lock (_lock)
            {
                _registry[record.Id] = record;
                installed = _installed;
            }

            if (installed)
            {
                foreach (IPage page in _context.Pages)
                {
                    await AddCdpCredentialAsync(page, credential).ConfigureAwait(false);
                }
            }

            return ToPublic(record);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<VirtualCredential>> GetAsync(string rpId = default, string id = default)
        {
            List<VirtualCredential> matches = new List<VirtualCredential>();
            lock (_lock)
            {
                foreach (CredentialRecord record in _registry.Values)
                {
                    if (!string.IsNullOrEmpty(rpId) && !string.Equals(record.RpId, rpId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(id) && !string.Equals(record.Id, id, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    matches.Add(ToPublic(record));
                }
            }

            return Task.FromResult<IReadOnlyList<VirtualCredential>>(matches);
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Credential id must not be empty.", nameof(id));
            }

            lock (_lock)
            {
                _registry.Remove(id);
            }

            foreach (IPage page in _context.Pages)
            {
                if (page is ISupportsVirtualAuthenticator authenticator)
                {
                    await authenticator.RemoveVirtualCredentialAsync(id).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Enables the virtual authenticator on <paramref name="page"/> when
        /// <see cref="InstallAsync"/> has already been called.
        /// </summary>
        /// <param name="page">A page in this context.</param>
        /// <returns>A task that completes when the page is attached, or immediately when not installed.</returns>
        internal async Task AttachIfInstalledAsync(IPage page)
        {
            bool installed;
            lock (_lock)
            {
                installed = _installed;
            }

            if (!installed)
            {
                return;
            }

            await EvaluateInjectOnPageAsync(page).ConfigureAwait(false);
            await AttachCdpIfSupportedAsync(page).ConfigureAwait(false);
        }

        private static VirtualCredential ToPublic(CredentialRecord record)
            => new VirtualCredential
            {
                Id = record.Id,
                RpId = record.RpId,
                UserHandle = record.UserHandle,
                PrivateKey = record.PrivateKey,
                PublicKey = record.PublicKey,
            };

        private static Dictionary<string, object> Fail(string name, string message)
            => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["name"] = name,
                ["message"] = message,
            };

        private static string ReadString(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty(name, out JsonElement value)
                || value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return value.GetString();
        }

        private static string HostnameOf(string origin)
        {
            if (!string.IsNullOrEmpty(origin) && Uri.TryCreate(origin, UriKind.Absolute, out Uri uri))
            {
                return uri.Host;
            }

            return origin;
        }

        private static IEnumerable<JsonElement> EnumerateArray(JsonElement parent, string name)
        {
            if (parent.ValueKind != JsonValueKind.Object
                || !parent.TryGetProperty(name, out JsonElement array)
                || array.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }

            foreach (JsonElement item in array.EnumerateArray())
            {
                yield return item;
            }
        }

        private static async Task EvaluateInjectOnPageAsync(IPage page)
        {
            if (page == null)
            {
                return;
            }

            IFrame[] frames;
            try
            {
                IReadOnlyCollection<IFrame> list = page.Frames;
                frames = new IFrame[list.Count];
                int index = 0;
                foreach (IFrame frame in list)
                {
                    frames[index++] = frame;
                }
            }
            catch (PlaywrightNativeException)
            {
                return;
            }

            foreach (IFrame frame in frames)
            {
                if (frame == null || frame.IsDetached)
                {
                    continue;
                }

                try
                {
                    await frame.EvaluateAsync(WebAuthnInjectScript.Source).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }
            }
        }

        private static Task AddCdpCredentialAsync(IPage page, VirtualCredential credential)
        {
            if (page is ISupportsVirtualAuthenticator authenticator)
            {
                return authenticator.AddVirtualCredentialAsync(credential);
            }

            return Task.CompletedTask;
        }

        private object HandleBinding(JsonElement payload)
        {
            try
            {
                if (payload.ValueKind != JsonValueKind.Object)
                {
                    return Fail("NotAllowedError", "Unknown WebAuthn request");
                }

                string type = ReadString(payload, "type");
                if (string.Equals(type, "create", StringComparison.Ordinal))
                {
                    return HandleCreate(payload);
                }

                if (string.Equals(type, "get", StringComparison.Ordinal))
                {
                    return HandleGet(payload);
                }

                return Fail("NotAllowedError", "Unknown WebAuthn request");
            }
            catch (Exception ex)
            {
                return Fail("NotAllowedError", ex.Message);
            }
        }

        private object HandleCreate(JsonElement req)
        {
            string origin = ReadString(req, "origin");
            string hostname = HostnameOf(origin);
            string rpId = hostname;
            if (req.TryGetProperty("rp", out JsonElement rp) && rp.ValueKind == JsonValueKind.Object)
            {
                string id = ReadString(rp, "id");
                if (!string.IsNullOrEmpty(id))
                {
                    rpId = id;
                }
            }

            string userHandle = null;
            if (req.TryGetProperty("user", out JsonElement user) && user.ValueKind == JsonValueKind.Object)
            {
                userHandle = ReadString(user, "id");
            }

            lock (_lock)
            {
                foreach (JsonElement desc in EnumerateArray(req, "excludeCredentials"))
                {
                    string excluded = ReadString(desc, "id");
                    if (!string.IsNullOrEmpty(excluded) && _registry.ContainsKey(excluded))
                    {
                        return Fail("InvalidStateError", "Credential excluded");
                    }
                }

                using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                string privateKey = VirtualCredentialFactory.ToBase64Url(ecdsa.ExportPkcs8PrivateKey());
                string publicKey = VirtualCredentialFactory.ToBase64Url(ecdsa.ExportSubjectPublicKeyInfo());
                byte[] credentialId = new byte[16];
                RandomNumberGenerator.Fill(credentialId);
                string credentialIdB64 = VirtualCredentialFactory.ToBase64Url(credentialId);
                string residentKey = ReadString(req, "residentKey");
                CredentialRecord record = new CredentialRecord
                {
                    Id = credentialIdB64,
                    RpId = rpId,
                    UserHandle = userHandle,
                    PrivateKey = privateKey,
                    PublicKey = publicKey,
                    SignCount = 0,
                    IsResident = string.Equals(residentKey, "required", StringComparison.Ordinal)
                        || string.Equals(residentKey, "preferred", StringComparison.Ordinal),
                };
                _registry[credentialIdB64] = record;

                byte[] clientDataJson = WebAuthnCbor.ClientDataJson(
                    "webauthn.create",
                    ReadString(req, "challenge"),
                    origin);
                byte[] cosePublicKey = WebAuthnCbor.EncodeCoseEs256PublicKey(null, ecdsa);
                byte[] authData = WebAuthnCbor.CreateAuthData(rpId, record.SignCount, credentialId, cosePublicKey);
                byte[] attestationObject = WebAuthnCbor.EncodeAttestationObjectNone(authData);
                return new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["ok"] = true,
                    ["id"] = credentialIdB64,
                    ["clientDataJSON"] = VirtualCredentialFactory.ToBase64Url(clientDataJson),
                    ["attestationObject"] = VirtualCredentialFactory.ToBase64Url(attestationObject),
                };
            }
        }

        private object HandleGet(JsonElement req)
        {
            string origin = ReadString(req, "origin");
            string rpId = ReadString(req, "rpId");
            if (string.IsNullOrEmpty(rpId))
            {
                rpId = HostnameOf(origin);
            }

            lock (_lock)
            {
                CredentialRecord candidate = null;
                bool hasAllow = false;
                foreach (JsonElement desc in EnumerateArray(req, "allowCredentials"))
                {
                    hasAllow = true;
                    string allowId = ReadString(desc, "id");
                    if (string.IsNullOrEmpty(allowId))
                    {
                        continue;
                    }

                    if (_registry.TryGetValue(allowId, out CredentialRecord match)
                        && string.Equals(match.RpId, rpId, StringComparison.Ordinal))
                    {
                        candidate = match;
                        break;
                    }
                }

                if (!hasAllow)
                {
                    foreach (CredentialRecord record in _registry.Values)
                    {
                        if (record.IsResident && string.Equals(record.RpId, rpId, StringComparison.Ordinal))
                        {
                            candidate = record;
                            break;
                        }
                    }
                }

                if (candidate == null)
                {
                    return Fail("NotAllowedError", "No matching credential");
                }

                candidate.SignCount += 1;
                byte[] clientDataJson = WebAuthnCbor.ClientDataJson(
                    "webauthn.get",
                    ReadString(req, "challenge"),
                    origin);
                byte[] authData = WebAuthnCbor.GetAuthData(rpId, candidate.SignCount);
                byte[] privateKey = VirtualCredentialFactory.FromBase64Url(candidate.PrivateKey);
                byte[] signature = WebAuthnCbor.SignAssertion(privateKey, authData, clientDataJson);
                return new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["ok"] = true,
                    ["id"] = candidate.Id,
                    ["clientDataJSON"] = VirtualCredentialFactory.ToBase64Url(clientDataJson),
                    ["authenticatorData"] = VirtualCredentialFactory.ToBase64Url(authData),
                    ["signature"] = VirtualCredentialFactory.ToBase64Url(signature),
                    ["userHandle"] = string.IsNullOrEmpty(candidate.UserHandle) ? null : candidate.UserHandle,
                };
            }
        }

        private async Task EvaluateInjectAsync()
        {
            foreach (IPage page in _context.Pages)
            {
                await EvaluateInjectOnPageAsync(page).ConfigureAwait(false);
            }
        }

        private async Task AttachCdpIfSupportedAsync(IPage page)
        {
            if (page is not ISupportsVirtualAuthenticator authenticator)
            {
                return;
            }

            await authenticator.EnableVirtualAuthenticatorAsync().ConfigureAwait(false);
            CredentialRecord[] snapshot;
            lock (_lock)
            {
                snapshot = new CredentialRecord[_registry.Count];
                _registry.Values.CopyTo(snapshot, 0);
            }

            foreach (CredentialRecord record in snapshot)
            {
                await authenticator.AddVirtualCredentialAsync(ToPublic(record)).ConfigureAwait(false);
            }
        }

        private sealed class CredentialRecord
        {
            internal string Id { get; set; }

            internal string RpId { get; set; }

            internal string UserHandle { get; set; }

            internal string PrivateKey { get; set; }

            internal string PublicKey { get; set; }

            internal uint SignCount { get; set; }

            internal bool IsResident { get; set; }
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task<VirtualCredential> ICredentials.CreateAsync(string rpId, CredentialsCreateOptions options) => Task.FromResult<VirtualCredential>(default!);

        Task<IReadOnlyList<VirtualCredential>> ICredentials.GetAsync(CredentialsGetOptions options) => Task.FromResult<IReadOnlyList<VirtualCredential>>(default!);
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
