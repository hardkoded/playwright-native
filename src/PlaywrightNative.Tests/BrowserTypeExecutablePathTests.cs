/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>browserType.executablePath()</c>.
    /// </summary>
    [TestFixture]
    public class BrowserTypeExecutablePathTests : PageTestEx
    {
        [PlaywrightTest("browsertype-basic.spec.ts", "Chromium ExecutablePath is a rooted chrome path")]
        [Test]
        public void ChromiumExecutablePathShouldBeARootedChromePath()
        {
            string path = BrowserTypeInfo.Chromium.ExecutablePath;
            Assert.That(path, Is.Not.Null.And.Not.Empty);
            Assert.That(Path.IsPathRooted(path), Is.True);
            Assert.That(path, Does.Contain("chrome").IgnoreCase);
        }

        [PlaywrightTest("browsertype-basic.spec.ts", "launched browser exposes BrowserType.ExecutablePath")]
        [Test]
        [Timeout(30_000)]
        public async Task LaunchedBrowserShouldExposeExecutablePath()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            string path = browser.BrowserType.ExecutablePath;
            Assert.That(path, Is.Not.Null.And.Not.Empty);
            Assert.That(Path.IsPathRooted(path), Is.True);
            if (TestConstants.IsWebKit)
            {
                Assert.That(path, Does.Contain("webkit").IgnoreCase);
            }
            else
            {
                Assert.That(path, Does.Contain("chrome").IgnoreCase);
            }
        }
    }
}
