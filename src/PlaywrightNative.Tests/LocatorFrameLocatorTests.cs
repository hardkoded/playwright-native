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
    /// <see cref="ILocator.FrameLocator"/> scopes iframe queries to a locator.
    /// </summary>
    [TestFixture]
    public class LocatorFrameLocatorTests : PageTestEx
    {
        [PlaywrightTest("locator-frame.spec.ts", "FrameLocator is scoped to the locator")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameLocatorShouldBeScopedToTheLocator()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.SetContentAsync(
                "<div id=\"wrap\"><iframe srcdoc=\"<button id='in'>In</button>\"></iframe></div>" +
                "<iframe srcdoc=\"<button id='out'>Out</button>\"></iframe>").ConfigureAwait(false);

            ILocator button = page.Locator("#wrap").FrameLocator("iframe").Locator("button");
            Assert.That(await button.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("in"));
            Assert.That(await button.TextContentAsync().ConfigureAwait(false), Is.EqualTo("In"));
        }

        [PlaywrightTest("locator-frame.spec.ts", "FrameLocator First narrows two iframes")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameLocatorFirstShouldNarrowTwoIframes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.SetContentAsync(
                "<section>" +
                "<iframe srcdoc=\"<button id='x'>A</button>\"></iframe>" +
                "<iframe srcdoc=\"<button id='y'>B</button>\"></iframe>" +
                "</section>").ConfigureAwait(false);

            ILocator first = page.Locator("section").FrameLocator("iframe").First.Locator("button");
            ILocator second = page.Locator("section").FrameLocator("iframe").Nth(1).Locator("button");
            Assert.That(await first.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("x"));
            Assert.That(await second.TextContentAsync().ConfigureAwait(false), Is.EqualTo("B"));
        }

        [PlaywrightTest("locator-frame.spec.ts", "FrameLocator is strict")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameLocatorShouldThrowWhenTwoIframesMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.SetContentAsync(
                "<section>" +
                "<iframe srcdoc=\"<button>A</button>\"></iframe>" +
                "<iframe srcdoc=\"<button>B</button>\"></iframe>" +
                "</section>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.Locator("section").FrameLocator("iframe").Locator("button").ClickAsync());

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
            Assert.That(ex.Message, Does.Contain("frame locator"));
        }
    }
}
