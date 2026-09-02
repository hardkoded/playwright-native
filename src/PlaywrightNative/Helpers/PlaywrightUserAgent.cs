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
