/*
 * MIT License
 *
 * Copyright (c) 2020 Darío Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */

namespace PlaywrightNative
{
    /// <summary>
    /// Platform used by a BrowserFetcher.
    /// </summary>
    public enum Platform
    {
        /// <summary>Unknown / unsupported platform.</summary>
        Unknown,

        /// <summary>macOS on Intel (x64).</summary>
        MacOS,

        /// <summary>macOS on Apple Silicon (arm64).</summary>
        MacOSArm64,

        /// <summary>Linux x64 (defaults to the Ubuntu 22.04 archive).</summary>
        Linux,

        /// <summary>Linux arm64 (defaults to the Ubuntu 22.04 archive).</summary>
        LinuxArm64,

        /// <summary>Windows 64-bit.</summary>
        Win64,
    }
}
