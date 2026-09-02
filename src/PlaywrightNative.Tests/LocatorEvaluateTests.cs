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
    /// Evaluate, EvaluateAll, and BoundingBox on <see cref="ILocator"/>.
    /// </summary>
    [TestFixture]
    public class LocatorEvaluateTests : PageTestEx
    {
        [PlaywrightTest("locator-evaluate.spec.ts", "Evaluate reads a unique match")]
        [Test]
        [Timeout(30_000)]
        public async Task EvaluateShouldReadAUniqueMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"only\">Go</button>").ConfigureAwait(false);

            string id = await page.Locator("button").EvaluateAsync<string>("el => el.id").ConfigureAwait(false);
            Assert.That(id, Is.EqualTo("only"));
        }

        [PlaywrightTest("locator-evaluate.spec.ts", "EvaluateAll maps each match")]
        [Test]
        [Timeout(30_000)]
        public async Task EvaluateAllShouldMapEachMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"a\"></div><div id=\"b\"></div>").ConfigureAwait(false);

            string[] ids = await page.Locator("div").EvaluateAllAsync<string[]>("els => els.map(el => el.id)").ConfigureAwait(false);
            Assert.That(ids, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(await page.Locator("section").EvaluateAllAsync<string[]>("els => els.map(el => el.id)").ConfigureAwait(false), Is.Empty);
        }

        [PlaywrightTest("locator-evaluate.spec.ts", "BoundingBox returns the box")]
        [Test]
        [Timeout(30_000)]
        public async Task BoundingBoxShouldReturnTheBox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(400, 300).ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"d\" style=\"width:80px;height:50px;margin:0\"></div>").ConfigureAwait(false);

            var box = await page.Locator("#d").BoundingBoxAsync().ConfigureAwait(false);
            Assert.That(box, Is.Not.Null);
            Assert.That(box.Width, Is.EqualTo(80).Within(1));
            Assert.That(box.Height, Is.EqualTo(50).Within(1));
        }

        [PlaywrightTest("locator-evaluate.spec.ts", "Evaluate is strict")]
        [Test]
        [Timeout(30_000)]
        public async Task EvaluateShouldThrowWhenTwoMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div class=\"x\"></div><div class=\"x\"></div>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.Locator(".x").EvaluateAsync<string>("el => el.id"));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
