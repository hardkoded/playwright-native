// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
using System.IO;
using NUnit.Framework;
using PlaywrightNative;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    [TestFixture]
    public class InstalledBrowserTests
    {
        [PlaywrightTest("browsers-path.spec.ts", "Get executable path chromium mac arm64returns app bundle binary")]
        [Test]
        public void GetExecutablePathChromiumMacArm64ReturnsAppBundleBinary()
        {
            string installDir = Path.Combine(Path.GetTempPath(), "chromium-1219");
            InstalledBrowser installed = new()
            {
                Browser = SupportedBrowser.Chromium,
                BuildId = "1219",
                Platform = Platform.MacOSArm64,
                InstallationDir = installDir,
            };

            string actual = installed.GetExecutablePath();
            Assert.That(actual, Is.EqualTo(Path.Combine(installDir, "chrome-mac-arm64", "Google Chrome for Testing.app", "Contents", "MacOS", "Google Chrome for Testing")));
        }

        [PlaywrightTest("browsers-path.spec.ts", "Get executable path firefox linux returns binary")]
        [Test]
        public void GetExecutablePathFirefoxLinuxReturnsBinary()
        {
            string installDir = "/tmp/firefox-1515";
            InstalledBrowser installed = new()
            {
                Browser = SupportedBrowser.Firefox,
                BuildId = "1515",
                Platform = Platform.Linux,
                InstallationDir = installDir,
            };

            Assert.That(installed.GetExecutablePath(), Is.EqualTo(Path.Combine(installDir, "firefox", "firefox")));
        }
    }
}
