/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
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
