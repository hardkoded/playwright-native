/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>getUserAgent()</c> for <c>browserType.connectOverCDP</c>
    /// discovery and WebSocket handshake headers.
    /// </summary>
    public static class PlaywrightUserAgent
    {
        /// <summary>
        /// Default User-Agent sent when the caller does not supply one.
        /// </summary>
        /// <returns>A PlaywrightNative User-Agent string.</returns>
        public static string GetUserAgent()
        {
            Version version = typeof(Playwright).Assembly.GetName().Version;
            string product = version == null
                ? "1.0.0"
                : version.ToString(3);
            string arch = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                Architecture.Arm64 => "arm64",
                Architecture.Arm => "arm",
                _ => RuntimeInformation.OSArchitecture.ToString(),
            };
            string platform = OperatingSystem.IsWindows()
                ? "windows"
                : OperatingSystem.IsMacOS()
                    ? "macOS"
                    : "linux";
            return "PlaywrightNative/" + product + " (" + arch + "; " + platform + ")";
        }
    }
}
