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
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official Playwright WebAuthn page interceptor
    /// (<c>packages/injected/src/webAuthn.ts</c> <c>inject()</c>).
    /// </summary>
    internal static class WebAuthnInjectScript
    {
        /// <summary>
        /// Binding name used by the injected interceptor.
        /// </summary>
        internal const string BindingName = "__pwWebAuthnBinding";

        /// <summary>
        /// Page-side override of <c>navigator.credentials</c>. Sets
        /// <c>globalThis.__pwWebAuthnInstalled</c>.
        /// </summary>
        internal const string Source = @"(() => {
            if (globalThis.__pwWebAuthnInstalled)
                return;
            globalThis.__pwWebAuthnInstalled = true;

            const binding = globalThis.__pwWebAuthnBinding;
            if (!binding || !globalThis.navigator)
                return;
            if (!globalThis.navigator.credentials) {
                Object.defineProperty(globalThis.navigator, 'credentials', {
                    value: { create: async () => null, get: async () => null },
                    writable: true,
                    configurable: true,
                });
            }

            function toBase64Url(buf) {
                const bytes = buf instanceof ArrayBuffer ? new Uint8Array(buf) : new Uint8Array(buf.buffer, buf.byteOffset, buf.byteLength);
                let s = '';
                for (let i = 0; i < bytes.length; i++)
                    s += String.fromCharCode(bytes[i]);
                return globalThis.btoa(s).replace(/[+]/g, '-').replace(/[/]/g, '_').replace(/=/g, '');
            }

            function fromBase64Url(s) {
                let str = String(s).replace(/-/g, '+').replace(/_/g, '/');
                while (str.length % 4)
                    str += '=';
                const bin = globalThis.atob(str);
                const out = new Uint8Array(bin.length);
                for (let i = 0; i < bin.length; i++)
                    out[i] = bin.charCodeAt(i);
                return out.buffer;
            }

            const PublicKeyCredentialCtor = globalThis.PublicKeyCredential;
            const AuthAttestationResponseCtor = globalThis.AuthenticatorAttestationResponse;
            const AuthAssertionResponseCtor = globalThis.AuthenticatorAssertionResponse;

            function defineReadonly(target, props) {
                const keys = Object.keys(props);
                for (let i = 0; i < keys.length; i++) {
                    const k = keys[i];
                    Object.defineProperty(target, k, { value: props[k], enumerable: true, configurable: true });
                }
            }

            function makeAttestationResponse(clientDataJSON, attestationObject) {
                const proto = (AuthAttestationResponseCtor && AuthAttestationResponseCtor.prototype) || Object.prototype;
                const r = Object.create(proto);
                defineReadonly(r, { clientDataJSON: clientDataJSON, attestationObject: attestationObject });
                r.getTransports = () => ['internal'];
                r.getAuthenticatorData = () => attestationObject;
                r.getPublicKey = () => null;
                r.getPublicKeyAlgorithm = () => -7;
                return r;
            }

            function makeAssertionResponse(clientDataJSON, authenticatorData, signature, userHandle) {
                const proto = (AuthAssertionResponseCtor && AuthAssertionResponseCtor.prototype) || Object.prototype;
                const r = Object.create(proto);
                defineReadonly(r, {
                    clientDataJSON: clientDataJSON,
                    authenticatorData: authenticatorData,
                    signature: signature,
                    userHandle: userHandle,
                });
                return r;
            }

            function makePublicKeyCredential(id, response) {
                const proto = (PublicKeyCredentialCtor && PublicKeyCredentialCtor.prototype) || Object.prototype;
                const cred = Object.create(proto);
                defineReadonly(cred, {
                    id: id,
                    rawId: fromBase64Url(id),
                    type: 'public-key',
                    authenticatorAttachment: 'platform',
                    response: response,
                });
                cred.getClientExtensionResults = () => ({});
                cred.toJSON = () => ({ id: id, rawId: id, type: 'public-key', response: {} });
                return cred;
            }

            function toBuf(x) {
                if (!x)
                    return new ArrayBuffer(0);
                if (x instanceof ArrayBuffer)
                    return x;
                const out = new Uint8Array(x.byteLength);
                out.set(new Uint8Array(x.buffer, x.byteOffset, x.byteLength));
                return out.buffer;
            }

            function failure(name, message) {
                const Ctor = globalThis.DOMException || Error;
                throw new Ctor(message, name);
            }

            const origCreate = globalThis.navigator.credentials.create.bind(globalThis.navigator.credentials);
            const origGet = globalThis.navigator.credentials.get.bind(globalThis.navigator.credentials);

            globalThis.navigator.credentials.create = async function(options) {
                if (!options || !options.publicKey)
                    return origCreate(options);
                const pk = options.publicKey;
                const exclude = pk.excludeCredentials || [];
                const params = pk.pubKeyCredParams || [];
                const req = {
                    type: 'create',
                    origin: globalThis.location.origin,
                    challenge: toBase64Url(toBuf(pk.challenge)),
                    rp: { id: pk.rp && pk.rp.id, name: (pk.rp && pk.rp.name) || '' },
                    user: {
                        id: toBase64Url(toBuf(pk.user && pk.user.id)),
                        name: (pk.user && pk.user.name) || '',
                        displayName: (pk.user && pk.user.displayName) || '',
                    },
                    pubKeyCredParams: params.map(function(p) { return { type: p.type, alg: p.alg }; }),
                    excludeCredentials: exclude.map(function(c) { return { type: c.type, id: toBase64Url(toBuf(c.id)) }; }),
                    userVerification: pk.authenticatorSelection && pk.authenticatorSelection.userVerification,
                    residentKey: pk.authenticatorSelection && pk.authenticatorSelection.residentKey,
                };
                const result = await binding(req);
                if (!result.ok)
                    failure(result.name, result.message);
                const resp = makeAttestationResponse(fromBase64Url(result.clientDataJSON), fromBase64Url(result.attestationObject));
                return makePublicKeyCredential(result.id, resp);
            };

            globalThis.navigator.credentials.get = async function(options) {
                if (!options || !options.publicKey)
                    return origGet(options);
                const pk = options.publicKey;
                const allow = pk.allowCredentials || [];
                const req = {
                    type: 'get',
                    origin: globalThis.location.origin,
                    challenge: toBase64Url(toBuf(pk.challenge)),
                    rpId: pk.rpId || new URL(globalThis.location.origin).hostname,
                    allowCredentials: allow.map(function(c) { return { type: c.type, id: toBase64Url(toBuf(c.id)) }; }),
                    userVerification: pk.userVerification,
                };
                const result = await binding(req);
                if (!result.ok)
                    failure(result.name, result.message);
                const resp = makeAssertionResponse(
                    fromBase64Url(result.clientDataJSON),
                    fromBase64Url(result.authenticatorData),
                    fromBase64Url(result.signature),
                    result.userHandle ? fromBase64Url(result.userHandle) : null);
                return makePublicKeyCredential(result.id, resp);
            };

            if (PublicKeyCredentialCtor) {
                PublicKeyCredentialCtor.isUserVerifyingPlatformAuthenticatorAvailable = async function() { return true; };
                PublicKeyCredentialCtor.isConditionalMediationAvailable = async function() { return true; };
                if (typeof PublicKeyCredentialCtor.getClientCapabilities !== 'function') {
                    PublicKeyCredentialCtor.getClientCapabilities = async function() { return {}; };
                }
            }
        })();";
    }
}
