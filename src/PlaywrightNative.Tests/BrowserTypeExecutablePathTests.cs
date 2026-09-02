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
