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
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>locator-element-handle.spec.ts</c> parity for
    /// <see cref="ILocator.ElementHandleAsync"/> and
    /// <see cref="ILocator.ElementHandlesAsync"/>.
    /// Official .NET also replaces <c>playground.html</c> with <c>setContent</c>
    /// before querying; assertions depend only on that document.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LocatorElementHandleParityTests : PageTestEx
    {
        [PlaywrightTest("locator-element-handle.spec.ts", "should query existing element")]
        [PlaywrightTest("locator-element-handle.spec.ts", "should query existing element @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldQueryExistingElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<html><body><div class=\"second\"><div class=\"inner\">A</div></div></body></html>").ConfigureAwait(false);
            ILocator html = page.Locator("html");
            ILocator second = html.Locator(".second");
            ILocator inner = second.Locator(".inner");
            string content = await page.EvaluateAsync<string>("e => e.textContent", await inner.ElementHandleAsync().ConfigureAwait(false)).ConfigureAwait(false);
            Assert.That(content, Is.EqualTo("A"));
        }

        [PlaywrightTest("locator-element-handle.spec.ts", "should query existing elements")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldQueryExistingElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<html><body><div>A</div><br/><div>B</div></body></html>").ConfigureAwait(false);
            ILocator html = page.Locator("html");
            IReadOnlyList<IElementHandle> elements = await html.Locator("div").ElementHandlesAsync().ConfigureAwait(false);
            Assert.That(elements.Count, Is.EqualTo(2));
            Task<string>[] promises = new Task<string>[elements.Count];
            for (int i = 0; i < elements.Count; i++)
            {
                promises[i] = page.EvaluateAsync<string>("e => e.textContent", elements[i]);
            }

            string[] texts = await Task.WhenAll(promises).ConfigureAwait(false);
            Assert.That(texts, Is.EqualTo(new[] { "A", "B" }));
        }

        [PlaywrightTest("locator-element-handle.spec.ts", "should return empty array for non-existing elements")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnEmptyArrayForNonExistingElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<html><body><span>A</span><br/><span>B</span></body></html>").ConfigureAwait(false);
            ILocator html = page.Locator("html");
            IReadOnlyList<IElementHandle> elements = await html.Locator("div").ElementHandlesAsync().ConfigureAwait(false);
            Assert.That(elements.Count, Is.EqualTo(0));
        }

        [PlaywrightTest("locator-element-handle.spec.ts", "xpath should query existing element")]
        [Test]
        [Timeout(30_000)]
        public async Task XpathShouldQueryExistingElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<html><body><div class=\"second\"><div class=\"inner\">A</div></div></body></html>").ConfigureAwait(false);
            ILocator html = page.Locator("html");
            ILocator second = html.Locator("xpath=./body/div[contains(@class, 'second')]");
            ILocator inner = second.Locator("xpath=./div[contains(@class, 'inner')]");
            IElementHandle handle = await inner.ElementHandleAsync().ConfigureAwait(false);
            string content = await page.EvaluateAsync<string>("e => e.textContent", handle).ConfigureAwait(false);
            Assert.That(content, Is.EqualTo("A"));
        }

        [PlaywrightTest("locator-element-handle.spec.ts", "xpath should return null for non-existing element")]
        [Test]
        [Timeout(30_000)]
        public async Task XpathShouldReturnNullForNonExistingElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<html><body><div class=\"second\"><div class=\"inner\">B</div></div></body></html>").ConfigureAwait(false);
            ILocator html = page.Locator("html");
            IReadOnlyList<IElementHandle> second = await html.Locator("xpath=/div[contains(@class, 'third')]").ElementHandlesAsync().ConfigureAwait(false);
            Assert.That(second, Is.Empty);
        }
    }
}
