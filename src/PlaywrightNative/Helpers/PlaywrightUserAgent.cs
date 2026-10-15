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
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>getUserAgent()</c>: the default User-Agent for global
    /// API request contexts and <c>browserType.connectOverCDP</c>
    /// discovery / WebSocket handshake headers.
    /// </summary>
    public static class PlaywrightUserAgent
    {
        /// <summary>
        /// Default User-Agent sent when the caller does not supply one, e.g.
        /// <c>Playwright/1.11.0 (arm64; macOS 15.0) csharp/10.0</c>.
        /// </summary>
        /// <returns>A Playwright User-Agent string.</returns>
        public static string GetUserAgent()
        {
            Version version = typeof(Playwright).Assembly.GetName().Version;
            string product = version == null
                ? "unknown"
                : version.ToString(3);
            string arch = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                Architecture.Arm64 => "arm64",
                Architecture.Arm => "arm",
                _ => RuntimeInformation.OSArchitecture.ToString(),
            };
            string osIdentifier = "unknown";
            string osVersion = "unknown";
            if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
            {
                Version os = Environment.OSVersion.Version;
                osIdentifier = OperatingSystem.IsWindows() ? "windows" : "macOS";
                osVersion = os.ToString(2);
            }
            else if (OperatingSystem.IsLinux())
            {
                // Linux distribution without /etc/os-release defaults to linux/unknown.
                osIdentifier = "linux";
                Dictionary<string, string> osRelease = ReadOSRelease();
                if (osRelease != null)
                {
                    osIdentifier = NonEmptyOr(osRelease.GetValueOrDefault("id"), "linux");
                    osVersion = NonEmptyOr(osRelease.GetValueOrDefault("version_id"), "unknown");
                }
            }

            string tokens = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"))
                ? string.Empty
                : " CI/1";

            // Official getEmbedderName(): playwright-dotnet runs the driver with
            // PW_LANG_NAME=csharp and PW_LANG_NAME_VERSION=<runtime major.minor>.
            string embedderVersion = Environment.Version.ToString(2);
            return "Playwright/" + product + " (" + arch + "; " + osIdentifier + " " + osVersion + ") csharp/"
                + embedderVersion + tokens;
        }

        private static Dictionary<string, string> ReadOSRelease()
        {
            string text;
            try
            {
                text = File.ReadAllText("/etc/os-release");
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }

            // Official parseOSReleaseText.
            Dictionary<string, string> fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in text.Split('\n'))
            {
                int separator = line.IndexOf('=', StringComparison.Ordinal);
                string name = separator >= 0 ? line.Substring(0, separator) : line;
                string value = separator >= 0 ? line.Substring(separator + 1).Trim() : string.Empty;
                if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                if (name.Length > 0)
                {
                    fields[name] = value;
                }
            }

            return fields;
        }

        private static string NonEmptyOr(string value, string fallback)
            => string.IsNullOrEmpty(value) ? fallback : value;
    }
}
