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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/chromium/bfcache.spec.ts</c> parity. Do not edit
    /// leftover page-history bfcache titles.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBfcacheParityTests : PageTestEx
    {
        private static readonly string[] EnableBfcacheArgs = { "--disable-back-forward-cache" };

        [SetUp]
        public void SkipNonChromium()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Official Chromium-only bfcache.spec.ts.");
            }
        }

        [PlaywrightTest("bfcache.spec.ts", "bindings should work after restoring from bfcache")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BindingsShouldWorkAfterRestoringFromBfcache()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync(new BrowserTypeLaunchOptions
            {
                IgnoreDefaultArgsList = EnableBfcacheArgs,
            }).ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.ExposeFunctionAsync("add", (int a, int b) => a + b).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/cached/bfcached.html").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("window.add(1, 2)").ConfigureAwait(false), Is.EqualTo(3));
            await page.SetContentAsync("<a href='about:blank'}>click me</a>").ConfigureAwait(false);
            await page.ClickAsync("a").ConfigureAwait(false);
            await page.GoBackAsync(WaitUntilState.Commit).ConfigureAwait(false);
            await page.EvaluateAsync("window.didShow").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("window.add(2, 3)").ConfigureAwait(false), Is.EqualTo(5));
            await page.CloseAsync().ConfigureAwait(false);
        }
    }
}
