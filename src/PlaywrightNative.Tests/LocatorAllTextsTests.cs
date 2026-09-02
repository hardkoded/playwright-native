/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// AllInnerTexts, AllTextContents, and EvaluateHandle on <see cref="ILocator"/>.
    /// </summary>
    [TestFixture]
    public class LocatorAllTextsTests : PageTestEx
    {
        [PlaywrightTest("locator-convenience.spec.ts", "AllInnerTexts returns each innerText")]
        [Test]
        [Timeout(30_000)]
        public async Task AllInnerTextsShouldReturnEachInnerText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>one</div><div>two</div>").ConfigureAwait(false);

            IReadOnlyList<string> texts = await page.Locator("div").AllInnerTextsAsync().ConfigureAwait(false);
            Assert.That(texts, Is.EqualTo(new[] { "one", "two" }));
            Assert.That(await page.Locator("span").AllInnerTextsAsync().ConfigureAwait(false), Is.Empty);
        }

        [PlaywrightTest("locator-convenience.spec.ts", "AllTextContents returns each textContent")]
        [Test]
        [Timeout(30_000)]
        public async Task AllTextContentsShouldReturnEachTextContent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>a<span>b</span></div><div></div>").ConfigureAwait(false);

            IReadOnlyList<string> texts = await page.Locator("div").AllTextContentsAsync().ConfigureAwait(false);
            Assert.That(texts, Is.EqualTo(new[] { "ab", string.Empty }));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "EvaluateHandle returns a handle")]
        [Test]
        [Timeout(30_000)]
        public async Task EvaluateHandleShouldReturnAHandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"only\">Go</button>").ConfigureAwait(false);

            IJSHandle handle = await page.Locator("button").EvaluateHandleAsync("el => el").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            string id = await handle.EvaluateAsync<string>("el => el.id").ConfigureAwait(false);
            Assert.That(id, Is.EqualTo("only"));
            await handle.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("locator-convenience.spec.ts", "EvaluateHandle is strict")]
        [Test]
        [Timeout(30_000)]
        public async Task EvaluateHandleShouldThrowWhenTwoMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div class=\"x\"></div><div class=\"x\"></div>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.Locator(".x").EvaluateHandleAsync("el => el"));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
