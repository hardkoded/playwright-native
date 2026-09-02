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
    /// Official <c>locator.normalize()</c>.
    /// </summary>
    [TestFixture]
    public class LocatorNormalizeTests : PageTestEx
    {
        [PlaywrightTest("locator-query.spec.ts", "Normalize prefers test id")]
        [Test]
        [Timeout(30_000)]
        public async Task NormalizeShouldPreferTestId()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"old\" data-testid=\"save\">Save</button>").ConfigureAwait(false);

            ILocator normalized = await page.Locator("#old").NormalizeAsync().ConfigureAwait(false);
            await page.EvalOnSelectorAsync<bool>("#old", "el => { el.removeAttribute('id'); return true; }").ConfigureAwait(false);

            Assert.That(await normalized.CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That((await normalized.TextContentAsync().ConfigureAwait(false)).Trim(), Is.EqualTo("Save"));
        }

        [PlaywrightTest("locator-query.spec.ts", "Normalize prefers alt text")]
        [Test]
        [Timeout(30_000)]
        public async Task NormalizeShouldPreferAltText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<img class=\"x\" alt=\"Cat\" />").ConfigureAwait(false);

            ILocator normalized = await page.Locator(".x").NormalizeAsync().ConfigureAwait(false);
            await page.EvalOnSelectorAsync<bool>("img", "el => { el.removeAttribute('class'); return true; }").ConfigureAwait(false);

            Assert.That(await normalized.CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await normalized.GetAttributeAsync("alt").ConfigureAwait(false), Is.EqualTo("Cat"));
        }

        [PlaywrightTest("locator-query.spec.ts", "Normalize is strict")]
        [Test]
        [Timeout(30_000)]
        public async Task NormalizeShouldThrowWhenTwoMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div class=\"x\"></div><div class=\"x\"></div>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.Locator(".x").NormalizeAsync());

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
