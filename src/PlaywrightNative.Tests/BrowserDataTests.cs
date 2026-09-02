// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.IO;
using NUnit.Framework;
using PlaywrightNative;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    [TestFixture]
    public class BrowserDataTests
    {
        [PlaywrightTest("browser.spec.ts", "Default revision returns pinned build")]
        [Test]
        public void DefaultRevisionReturnsPinnedBuild()
        {
            Assert.That(BrowserData.DefaultRevision(SupportedBrowser.Chromium), Is.EqualTo(BrowserData.ChromiumRevision));
            Assert.That(BrowserData.DefaultRevision(SupportedBrowser.Firefox), Is.EqualTo(BrowserData.FirefoxRevision));
            Assert.That(BrowserData.DefaultRevision(SupportedBrowser.Webkit), Is.EqualTo(BrowserData.WebkitRevision));
        }

        [PlaywrightTest("browser.spec.ts", "Short platform key maps every non unknown platform")]
        [Test]
        public void ShortPlatformKeyMapsEveryNonUnknownPlatform()
        {
            Assert.That(BrowserData.ShortPlatformKey(Platform.MacOS), Is.EqualTo("mac-x64"));
            Assert.That(BrowserData.ShortPlatformKey(Platform.MacOSArm64), Is.EqualTo("mac-arm64"));
            Assert.That(BrowserData.ShortPlatformKey(Platform.Linux), Is.EqualTo("linux-x64"));
            Assert.That(BrowserData.ShortPlatformKey(Platform.LinuxArm64), Is.EqualTo("linux-arm64"));
            Assert.That(BrowserData.ShortPlatformKey(Platform.Win64), Is.EqualTo("win-x64"));
        }

        [PlaywrightTest("browser.spec.ts", "Playwright platform key for chromium uses short format")]
        [Test]
        public void PlaywrightPlatformKeyForChromiumUsesShortFormat()
        {
            Assert.That(BrowserData.PlaywrightPlatformKey(SupportedBrowser.Chromium, Platform.MacOSArm64), Is.EqualTo("mac-arm64"));
            Assert.That(BrowserData.PlaywrightPlatformKey(SupportedBrowser.Chromium, Platform.Linux), Is.EqualTo("linux-x64"));
            Assert.That(BrowserData.PlaywrightPlatformKey(SupportedBrowser.Chromium, Platform.Win64), Is.EqualTo("win64"));
        }

        [PlaywrightTest("browser.spec.ts", "Playwright platform key for firefox on linux uses ubuntu versioned key")]
        [Test]
        public void PlaywrightPlatformKeyForFirefoxOnLinuxUsesUbuntuVersionedKey()
        {
            // Exact tag depends on /etc/os-release (22.04 vs 24.04); we only assert the shape.
            string x64 = BrowserData.PlaywrightPlatformKey(SupportedBrowser.Firefox, Platform.Linux);
            string arm = BrowserData.PlaywrightPlatformKey(SupportedBrowser.Firefox, Platform.LinuxArm64);
            Assert.That(x64, Does.Match("^ubuntu(22|24)\\.04-x64$"));
            Assert.That(arm, Does.Match("^ubuntu(22|24)\\.04-arm64$"));
        }

        [PlaywrightTest("browser.spec.ts", "Playwright platform key for webkit on mac includes major version")]
        [Test]
        public void PlaywrightPlatformKeyForWebkitOnMacIncludesMajorVersion()
        {
            // The exact label depends on the host macOS version, but the shape
            // is always mac{N}[-arm64] for a supported release.
            string key = BrowserData.PlaywrightPlatformKey(SupportedBrowser.Webkit, Platform.MacOSArm64);
            Assert.That(key, Does.StartWith("mac"));
            Assert.That(key, Does.EndWith("-arm64"));
        }

        [PlaywrightTest("browser.spec.ts", "Download urls uses provided hosts over mirrors")]
        [Test]
        public void DownloadUrlsUsesProvidedHostsOverMirrors()
        {
            string[] hosts = ["https://example.test/playwright"];
            string[] urls = BrowserData.DownloadUrls(SupportedBrowser.Chromium, "mac-arm64", "1219", hosts);
            Assert.That(urls, Has.Length.EqualTo(1));
            Assert.That(urls[0], Is.EqualTo("https://example.test/playwright/builds/chromium/1219/chromium-mac-arm64.zip"));
        }

        [PlaywrightTest("browser.spec.ts", "Download urls falls back to cdn mirrors")]
        [Test]
        public void DownloadUrlsFallsBackToCdnMirrors()
        {
            string[] urls = BrowserData.DownloadUrls(SupportedBrowser.Chromium, "mac-arm64", "1219", null);
            Assert.That(urls, Has.Length.EqualTo(2));
            Assert.That(urls[0], Does.StartWith("https://cdn.playwright.dev/"));
            Assert.That(urls[1], Does.StartWith("https://playwright.download.prss.microsoft.com/"));
            Assert.That(urls[0], Does.EndWith("chromium-mac-arm64.zip"));
        }

        [PlaywrightTest("browser.spec.ts", "Download urls throws for unsupported platform key")]
        [Test]
        public void DownloadUrlsThrowsForUnsupportedPlatformKey()
        {
            Assert.Throws<PlaywrightNativeException>(() =>
                BrowserData.DownloadUrls(SupportedBrowser.Chromium, "platform-that-does-not-exist", "1219", null));
        }

        [PlaywrightTest("browser.spec.ts", "Executable path on mac arm64chromium goes into app bundle")]
        [Test]
        public void ExecutablePathOnMacArm64ChromiumGoesIntoAppBundle()
        {
            string installDir = Path.Combine(Path.GetTempPath(), "chromium-1219");
            string path = BrowserData.ExecutablePath(SupportedBrowser.Chromium, Platform.MacOSArm64, installDir);
            Assert.That(path, Is.EqualTo(Path.Combine(installDir, "chrome-mac-arm64", "Google Chrome for Testing.app", "Contents", "MacOS", "Google Chrome for Testing")));
        }

        [PlaywrightTest("browser.spec.ts", "Executable path on windows firefox uses exe")]
        [Test]
        public void ExecutablePathOnWindowsFirefoxUsesExe()
        {
            string installDir = Path.Combine(Path.GetTempPath(), "firefox-1515");
            string path = BrowserData.ExecutablePath(SupportedBrowser.Firefox, Platform.Win64, installDir);
            Assert.That(path, Is.EqualTo(Path.Combine(installDir, "firefox", "firefox.exe")));
        }

        [PlaywrightTest("browser.spec.ts", "Resolve revision prefers explicit argument")]
        [Test]
        public void ResolveRevisionPrefersExplicitArgument()
        {
            string r = BrowserData.ResolveRevision(SupportedBrowser.Chromium, "mac-arm64", "999");
            Assert.That(r, Is.EqualTo("999"));
        }

        [PlaywrightTest("browser.spec.ts", "Resolve revision applies webkit mac14override")]
        [Test]
        public void ResolveRevisionAppliesWebkitMac14Override()
        {
            string r = BrowserData.ResolveRevision(SupportedBrowser.Webkit, "mac14", null);
            Assert.That(r, Is.EqualTo("2251"));
        }

        [PlaywrightTest("browser.spec.ts", "Resolve revision falls back to default")]
        [Test]
        public void ResolveRevisionFallsBackToDefault()
        {
            string r = BrowserData.ResolveRevision(SupportedBrowser.Webkit, "mac15-arm64", null);
            Assert.That(r, Is.EqualTo(BrowserData.WebkitRevision));
        }

        [PlaywrightTest("browser.spec.ts", "Installation dir combines cache browser build id")]
        [Test]
        public void InstallationDirCombinesCacheBrowserBuildId()
        {
            string dir = BrowserData.InstallationDir("/tmp/cache", SupportedBrowser.Chromium, "1219");
            Assert.That(dir, Is.EqualTo(Path.Combine("/tmp/cache", "chromium-1219")));
        }
    }
}
