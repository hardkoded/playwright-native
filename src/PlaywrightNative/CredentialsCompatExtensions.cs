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
#pragma warning disable CA1062
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
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
                throw new NotSupportedException("This credentials object does not support PlaywrightNative extensions.");
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
