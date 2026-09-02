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
    /// Represents a browser revision that has been (or will be) downloaded to the local cache.
    /// </summary>
    public class InstalledBrowser
    {
        /// <summary>Gets or sets the browser type.</summary>
        public SupportedBrowser Browser { get; set; }

        /// <summary>Gets or sets the build identifier (e.g. <c>"1515"</c> for Firefox).</summary>
        public string BuildId { get; set; }

        /// <summary>Gets or sets the platform this browser was downloaded for.</summary>
        public Platform Platform { get; set; }

        /// <summary>Gets the local directory that contains the extracted browser.</summary>
        public string InstallationDir { get; internal set; }

        /// <summary>
        /// Indicates whether executable bits have been applied (Unix only).
        /// <c>null</c> on Windows.
        /// </summary>
        public bool? PermissionsFixed { get; internal set; }

        /// <summary>Returns the full path to the browser executable.</summary>
        /// <returns>An absolute path to the binary inside <see cref="InstallationDir"/>.</returns>
        public string GetExecutablePath()
            => BrowserData.ExecutablePath(Browser, Platform, InstallationDir);
    }
}
