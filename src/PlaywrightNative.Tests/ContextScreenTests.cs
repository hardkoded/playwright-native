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
    /// NewContext screenSize applied to pages as window.screen.
    /// </summary>
    [TestFixture]
    public class ContextScreenTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-viewport.spec.ts", "screenSize is applied")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextScreenSizeShouldApplyToPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 400, Height = 300 }, ScreenSize = new ScreenSize { Width = 800, Height = 600 } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await AssertScreenAsync(page, 800, 600).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("window.innerWidth").ConfigureAwait(false), Is.EqualTo(400));
            Assert.That(await page.EvaluateAsync<int>("window.innerHeight").ConfigureAwait(false), Is.EqualTo(300));
        }

        [PlaywrightTest("browsercontext-viewport.spec.ts", "options bag screenSize")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextOptionsBagShouldApplyScreenSize()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                Viewport = new ViewportSize { Width = 500, Height = 400 },
                ScreenSize = new ScreenSize { Width = 1024, Height = 768 },
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await AssertScreenAsync(page, 1024, 768).ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport.spec.ts", "NewPage applies screenSize")]
        [Test]
        [Timeout(30_000)]
        public async Task NewPageShouldApplyScreenSize()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync(new() { ViewportSize = new ViewportSize { Width = 320, Height = 240 }, ScreenSize = new ScreenSize { Width = 1920, Height = 1080 } }).ConfigureAwait(false);

            await AssertScreenAsync(page, 1920, 1080).ConfigureAwait(false);
        }

        private static async Task AssertScreenAsync(IPage page, int width, int height)
        {
            Assert.That(await page.EvaluateAsync<int>("window.screen.width").ConfigureAwait(false), Is.EqualTo(width));
            Assert.That(await page.EvaluateAsync<int>("window.screen.height").ConfigureAwait(false), Is.EqualTo(height));
        }
    }
}
