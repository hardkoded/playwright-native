/*
 * MIT License
 *
 * Copyright (c) 2020 Darío Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
