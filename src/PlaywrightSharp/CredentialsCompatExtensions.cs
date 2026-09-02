/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
#pragma warning disable CA1062
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightSharp.Helpers;

namespace PlaywrightSharp
{
    /// <summary>
    /// Legacy helpers over official <see cref="ICredentials"/>.
    /// </summary>
    public static class CredentialsCompatExtensions
    {
        /// <summary>Legacy expanded-parameter create.</summary>
        public static Task<VirtualCredential> CreateAsync(
            this ICredentials credentials,
            string rpId,
            string id = default,
            string userHandle = default,
            string privateKey = default,
            string publicKey = default)
        {
            if (credentials is not ContextCredentials sharp)
            {
                throw new NotSupportedException("This credentials object does not support PlaywrightSharp extensions.");
            }

            return sharp.CreateAsync(rpId, id, userHandle, privateKey, publicKey);
        }

        /// <summary>Legacy get by rpId.</summary>
        public static Task<IReadOnlyList<VirtualCredential>> GetAsync(this ICredentials credentials, string rpId)
        {
            if (credentials is ContextCredentials sharp)
            {
                return sharp.GetAsync(rpId);
            }

            return credentials.GetAsync(new CredentialsGetOptions { RpId = rpId });
        }

        /// <summary>Legacy get with rpId and credential id.</summary>
        public static Task<IReadOnlyList<VirtualCredential>> GetAsync(
            this ICredentials credentials,
            string rpId,
            string id)
        {
            if (credentials is ContextCredentials sharp)
            {
                return sharp.GetAsync(rpId, id);
            }

            return credentials.GetAsync(new CredentialsGetOptions { RpId = rpId, Id = id });
        }
    }
}
