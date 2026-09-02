/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.IO;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/browsertype-basic.spec.ts</c> parity. All 3 titles
    /// are ported. Official skip: connectOverCDP guard is Firefox-only.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserTypeBasicParityTests : PageTestEx
    {
        [PlaywrightTest("browsertype-basic.spec.ts", "browserType.executablePath should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void BrowserTypeExecutablePathShouldWork()
        {
            IBrowserType browserType = CurrentBrowserType();
            Assert.That(File.Exists(browserType.ExecutablePath), Is.True);
        }

        [PlaywrightTest("browsertype-basic.spec.ts", "browserType.name should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void BrowserTypeNameShouldWork()
        {
            IBrowserType browserType = CurrentBrowserType();
            string expected = TestConstants.IsWebKit
                ? "webkit"
                : TestConstants.IsFirefox
                    ? "firefox"
                    : "chromium";
            Assert.That(browserType.Name, Is.EqualTo(expected));
        }

        [PlaywrightTest("browsertype-basic.spec.ts", "should throw when trying to connect with not-chromium")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowWhenTryingToConnectWithNotChromium()
        {
            if (TestConstants.IsChromium || TestConstants.IsWebKit)
            {
                Assert.Ignore("official skip: browserName === 'chromium' || browserName === 'webkit'");
            }

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => CurrentBrowserType().ConnectOverCDPAsync("ws://foo"));
            Assert.That(error.Message, Is.EqualTo("Connecting over CDP is only supported in Chromium and WebKit."));
        }

        private static IBrowserType CurrentBrowserType()
        {
            if (TestConstants.IsWebKit)
            {
                return BrowserTypeInfo.Webkit;
            }

            if (TestConstants.IsFirefox)
            {
                return BrowserTypeInfo.Firefox;
            }

            return BrowserTypeInfo.Chromium;
        }
    }
}
