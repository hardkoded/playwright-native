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
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/browser.spec.ts</c> parity. Six portable titles.
    /// Skip Node-only <c>newContext should not leave a context upon failure</c>
    /// (<c>toImpl</c> / <c>__testHookBeforeSetStorageState</c>).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserParityTests : PageTestEx
    {
        private IBrowser _browser;

        [SetUp]
        public async Task SetUpAsync()
        {
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }

            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }
        }

        [PlaywrightTest("browser.spec.ts", "should return browserType")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldReturnBrowserType()
        {
            IBrowserType expected = TestConstants.IsWebKit
                ? BrowserTypeInfo.Webkit
                : TestConstants.IsFirefox
                    ? BrowserTypeInfo.Firefox
                    : BrowserTypeInfo.Chromium;
            Assert.That(_browser.BrowserType, Is.SameAs(expected));
        }

        [PlaywrightTest("browser.spec.ts", "should create new page @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCreateNewPage()
        {
            IPage page1 = await _browser.NewPageAsync().ConfigureAwait(false);
            Assert.That(_browser.Contexts.Count, Is.EqualTo(1));

            IPage page2 = await _browser.NewPageAsync().ConfigureAwait(false);
            Assert.That(_browser.Contexts.Count, Is.EqualTo(2));

            await page1.CloseAsync().ConfigureAwait(false);
            Assert.That(_browser.Contexts.Count, Is.EqualTo(1));

            await page2.CloseAsync().ConfigureAwait(false);
            Assert.That(_browser.Contexts.Count, Is.EqualTo(0));
        }

        [PlaywrightTest("browser.spec.ts", "should throw upon second create new page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowUponSecondCreateNewPage()
        {
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.Context.NewPageAsync());
            await page.CloseAsync().ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("Please use browser.newContext()"));
        }

        [PlaywrightTest("browser.spec.ts", "version should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void VersionShouldWork()
        {
            string version = _browser.Version;
            if (TestConstants.IsChromium)
            {
                Assert.That(Regex.IsMatch(version, @"^\d+\.\d+\.\d+\.\d+$"), Is.True);
            }
            else
            {
                Assert.That(Regex.IsMatch(version, @"^\d+\.\d+"), Is.True);
            }
        }

        [PlaywrightTest("browser.spec.ts", "should dispatch page.on(close) upon browser.close and reject evaluate")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDispatchPageOnCloseUponBrowserCloseAndRejectEvaluate()
        {
            IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            bool closed = false;
            page.Close += (_, _) => closed = true;
            Task evaluateTask = page.EvaluateAsync("() => new Promise(() => {})");
            await browser.CloseAsync().ConfigureAwait(false);
            Assert.That(closed, Is.True);
            Exception error = Assert.CatchAsync(() => evaluateTask);
            Assert.That(error.Message, Does.Contain("Target page, context or browser has been closed"));
        }

        [PlaywrightTest("browser.spec.ts", "should fire context event on newContext")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFireContextEventOnNewContext()
        {
            List<IBrowserContext> events = new List<IBrowserContext>();
            _browser.Context += (_, ctx) => events.Add(ctx);
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Assert.That(events, Is.EqualTo(new[] { context }));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private static async Task DisposeQuietlyAsync(IAsyncDisposable disposable)
        {
            try
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }
    }
}
