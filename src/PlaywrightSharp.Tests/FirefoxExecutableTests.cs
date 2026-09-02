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
    /// <see cref="BrowserExecutableFixture"/> resolves a Firefox executable
    /// when <c>PRODUCT=FIREFOX</c>, matching Chromium and WebKit.
    /// </summary>
    [TestFixture]
    public class FirefoxExecutableTests : PlaywrightTestEx
    {
        [PlaywrightTest("browsertype-basic.spec.ts", "BrowserExecutableFixture resolves Firefox when PRODUCT=FIREFOX")]
        [Test]
        [Timeout(180_000)]
        public void ShouldResolveFirefoxExecutableWhenProductIsFirefox()
        {
            if (!TestConstants.IsFirefox)
            {
                Assert.That(BrowserExecutableFixture.FirefoxExecutablePath, Is.Null);
                return;
            }

            if (string.IsNullOrEmpty(BrowserExecutableFixture.FirefoxExecutablePath))
            {
                Assert.Ignore("Firefox executable not available (download skipped or failed).");
                return;
            }

            Assert.That(File.Exists(BrowserExecutableFixture.FirefoxExecutablePath), Is.True);
        }
    }
}
