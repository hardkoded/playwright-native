/*
 * Copyright (c) 2020 Darío Kondratiuk
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
