/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
#pragma warning disable CA1062
using System;
using System.Threading.Tasks;

namespace PlaywrightSharp
{
    /// <summary>
    /// Legacy launch helpers over official <see cref="IBrowserType"/>.
    /// </summary>
    public static class BrowserTypeCompatExtensions
    {
        /// <summary>Launch with PlaywrightSharp <see cref="BrowserTypeLaunchOptions"/>.</summary>
        public static Task<IBrowser> LaunchAsync(this IBrowserType browserType, BrowserTypeLaunchOptions options = default)
        {
            if (browserType is BrowserTypeInfo info)
            {
                return info.LaunchAsync(options);
            }

            throw new NotSupportedException("Launch with PlaywrightSharp options requires a PlaywrightSharp browser type.");
        }

        /// <summary>Launch persistent context with PlaywrightSharp options.</summary>
        public static Task<IBrowserContext> LaunchPersistentContextAsync(
            this IBrowserType browserType,
            string userDataDir,
            BrowserTypeLaunchOptions options = default)
        {
            if (browserType is BrowserTypeInfo info)
            {
                return info.LaunchPersistentContextAsync(userDataDir, options);
            }

            throw new NotSupportedException("Launch with PlaywrightSharp options requires a PlaywrightSharp browser type.");
        }
    }
}
