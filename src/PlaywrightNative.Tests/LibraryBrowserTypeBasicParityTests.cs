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
using System.IO;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
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

            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
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
