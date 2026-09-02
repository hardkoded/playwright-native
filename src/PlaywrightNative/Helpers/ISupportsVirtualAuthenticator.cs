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
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Chromium page that can host a CDP <c>WebAuthn</c> virtual authenticator.
    /// </summary>
    internal interface ISupportsVirtualAuthenticator
    {
        /// <summary>
        /// Enables the WebAuthn domain and adds a resident CTAP2 authenticator.
        /// </summary>
        /// <returns>A task that completes when the authenticator is ready.</returns>
        Task EnableVirtualAuthenticatorAsync();

        /// <summary>
        /// Adds <paramref name="credential"/> to the virtual authenticator.
        /// </summary>
        /// <param name="credential">A credential created by <see cref="ICredentials.CreateAsync"/>.</param>
        /// <returns>A task that completes when the credential has been added.</returns>
        Task AddVirtualCredentialAsync(VirtualCredential credential);

        /// <summary>
        /// Removes the credential with <paramref name="id"/> from the authenticator.
        /// </summary>
        /// <param name="id">Base64url credential id.</param>
        /// <returns>A task that completes when the credential has been removed.</returns>
        Task RemoveVirtualCredentialAsync(string id);
    }
}
