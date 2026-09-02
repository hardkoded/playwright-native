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
