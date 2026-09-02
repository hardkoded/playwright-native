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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>screencast.showOverlay</c>.
    /// </summary>
    [TestFixture]
    public class PageScreencastOverlayTests : PageTestEx
    {
        [PlaywrightTest("screencast-overlay.spec.ts", "showOverlay injects HTML")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldInjectOverlayHtml()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<body>page</body>").ConfigureAwait(false);

            await using IAsyncDisposable overlay = await page.Screencast.ShowOverlayAsync(
                "<div id=\"pw-ov-mark\">wave-631</div>").ConfigureAwait(false);

            Assert.That(await page.Locator("#pw-ov-mark").CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(
                await page.Locator("#pw-ov-mark").TextContentAsync().ConfigureAwait(false),
                Is.EqualTo("wave-631"));
        }

        [PlaywrightTest("screencast-overlay.spec.ts", "disposing showOverlay removes HTML")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRemoveOverlayWhenDisposed()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<body>page</body>").ConfigureAwait(false);

            await using (IAsyncDisposable overlay = await page.Screencast.ShowOverlayAsync(
                "<div id=\"pw-ov-mark\">wave-631</div>").ConfigureAwait(false))
            {
                Assert.That(await page.Locator("#pw-ov-mark").CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            }

            Assert.That(await page.Locator("#pw-ov-mark").CountAsync().ConfigureAwait(false), Is.EqualTo(0));
        }
    }
}
