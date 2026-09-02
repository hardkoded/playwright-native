// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
using System;
using System.Collections.Generic;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Official <see cref="IPlaywright"/> entry returned by
    /// <see cref="Playwright.CreateAsync"/>.
    /// </summary>
    internal sealed class PlaywrightImpl : IPlaywright
    {
        private static readonly IReadOnlyDictionary<string, Microsoft.Playwright.BrowserNewContextOptions> OfficialDevices =
            LoadOfficialDevices();

        /// <inheritdoc/>
        public IBrowserType Chromium => BrowserTypeInfo.Chromium;

        /// <inheritdoc/>
        public IBrowserType Firefox => BrowserTypeInfo.Firefox;

        /// <inheritdoc/>
        public IBrowserType Webkit => BrowserTypeInfo.Webkit;

        /// <inheritdoc/>
        public IAPIRequest APIRequest => Helpers.APIRequest.Instance;

        /// <inheritdoc/>
        public ISelectors Selectors => Helpers.Selectors.Instance;

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, Microsoft.Playwright.BrowserNewContextOptions> Devices => OfficialDevices;

        /// <inheritdoc/>
        public IBrowserType this[string browserType]
        {
            get
            {
                if (string.Equals(browserType, Microsoft.Playwright.BrowserType.Chromium, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(browserType, "chromium", StringComparison.OrdinalIgnoreCase))
                {
                    return Chromium;
                }

                if (string.Equals(browserType, Microsoft.Playwright.BrowserType.Firefox, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(browserType, "firefox", StringComparison.OrdinalIgnoreCase))
                {
                    return Firefox;
                }

                if (string.Equals(browserType, Microsoft.Playwright.BrowserType.Webkit, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(browserType, "webkit", StringComparison.OrdinalIgnoreCase))
                {
                    return Webkit;
                }

                throw new PlaywrightNativeException($"Unknown browser type: {browserType}");
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Direct protocol stacks do not own a driver process. Dispose is a
            // no-op so callers can keep the official `using var playwright = ...`
            // pattern from microsoft/playwright-dotnet.
        }

        private static IReadOnlyDictionary<string, Microsoft.Playwright.BrowserNewContextOptions> LoadOfficialDevices()
        {
            Dictionary<string, Microsoft.Playwright.BrowserNewContextOptions> devices = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, BrowserContextOptions> pair in Playwright.Devices)
            {
                devices[pair.Key] = MicrosoftOptionsBridge.ToBrowserNewContextOptions(pair.Value);
            }

            return devices;
        }
    }
}
