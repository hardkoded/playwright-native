/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Loads official Playwright device descriptors into
    /// <see cref="BrowserContextOptions"/>.
    /// </summary>
    internal static class PlaywrightDevices
    {
        /// <summary>
        /// Parses the bundled <c>deviceDescriptorsSource.json</c>.
        /// </summary>
        /// <returns>A name-to-options map.</returns>
        internal static IReadOnlyDictionary<string, BrowserContextOptions> Load()
        {
            Assembly assembly = typeof(PlaywrightDevices).Assembly;
            using Stream stream = assembly.GetManifestResourceStream("PlaywrightNative.Helpers.deviceDescriptorsSource.json")
                ?? throw new PlaywrightNativeException("Bundled Playwright device descriptors are missing.");
            using JsonDocument document = JsonDocument.Parse(stream);
            Dictionary<string, BrowserContextOptions> devices = new(StringComparer.Ordinal);
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                devices[property.Name] = Parse(property.Value);
            }

            return devices;
        }

        private static BrowserContextOptions Parse(JsonElement descriptor)
        {
            BrowserContextOptions options = new();
            if (descriptor.TryGetProperty("userAgent", out JsonElement userAgent)
                && userAgent.ValueKind == JsonValueKind.String)
            {
                options.UserAgent = userAgent.GetString();
            }

            if (descriptor.TryGetProperty("viewport", out JsonElement viewport)
                && TryReadSize(viewport, out int viewWidth, out int viewHeight))
            {
                options.Viewport = new ViewportSize { Width = viewWidth, Height = viewHeight };
            }

            if (descriptor.TryGetProperty("screen", out JsonElement screen)
                && TryReadSize(screen, out int screenWidth, out int screenHeight))
            {
                options.ScreenSize = new ScreenSize { Width = screenWidth, Height = screenHeight };
            }

            if (descriptor.TryGetProperty("deviceScaleFactor", out JsonElement scale)
                && scale.TryGetSingle(out float deviceScaleFactor))
            {
                options.DeviceScaleFactor = deviceScaleFactor;
            }

            if (descriptor.TryGetProperty("isMobile", out JsonElement isMobile)
                && (isMobile.ValueKind == JsonValueKind.True || isMobile.ValueKind == JsonValueKind.False))
            {
                options.IsMobile = isMobile.GetBoolean();
            }

            if (descriptor.TryGetProperty("hasTouch", out JsonElement hasTouch)
                && (hasTouch.ValueKind == JsonValueKind.True || hasTouch.ValueKind == JsonValueKind.False))
            {
                options.HasTouch = hasTouch.GetBoolean();
            }

            if (descriptor.TryGetProperty("defaultBrowserType", out JsonElement defaultBrowserType)
                && defaultBrowserType.ValueKind == JsonValueKind.String)
            {
                options.DefaultBrowserType = defaultBrowserType.GetString();
            }

            return options;
        }

        private static bool TryReadSize(JsonElement size, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (size.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!size.TryGetProperty("width", out JsonElement widthEl)
                || !widthEl.TryGetInt32(out width))
            {
                return false;
            }

            if (!size.TryGetProperty("height", out JsonElement heightEl)
                || !heightEl.TryGetInt32(out height))
            {
                return false;
            }

            return true;
        }
    }
}
