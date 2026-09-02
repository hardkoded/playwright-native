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
using System.Threading.Tasks;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy launch helpers over official <see cref="IBrowserType"/>.
    /// </summary>
    public static class BrowserTypeCompatExtensions
    {
        /// <summary>Launch with PlaywrightNative <see cref="BrowserTypeLaunchOptions"/>.</summary>
        public static Task<IBrowser> LaunchAsync(this IBrowserType browserType, BrowserTypeLaunchOptions options = default)
        {
            if (browserType is BrowserTypeInfo info)
            {
                return info.LaunchAsync(options);
            }

            throw new NotSupportedException("Launch with PlaywrightNative options requires a PlaywrightNative browser type.");
        }

        /// <summary>Launch persistent context with PlaywrightNative options.</summary>
        public static Task<IBrowserContext> LaunchPersistentContextAsync(
            this IBrowserType browserType,
            string userDataDir,
            BrowserTypeLaunchOptions options = default)
        {
            if (browserType is BrowserTypeInfo info)
            {
                return info.LaunchPersistentContextAsync(userDataDir, options);
            }

            throw new NotSupportedException("Launch with PlaywrightNative options requires a PlaywrightNative browser type.");
        }
    }
}
