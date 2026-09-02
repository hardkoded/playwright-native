/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>locator.locator(otherLocator)</c>.
    /// </summary>
    [TestFixture]
    public class LocatorLocatorTests : PageTestEx
    {
        [PlaywrightTest("locator-query.spec.ts", "Locator(ILocator) stays inside the outer match")]
        [Test]
        [Timeout(30_000)]
        public async Task LocatorShouldStayInsideTheOuterMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<div id=\"out\"><button id=\"a\">A</button></div>" +
                "<div id=\"in\"><button id=\"b\">B</button></div>").ConfigureAwait(false);

            ILocator inner = page.Locator("#in").Locator(page.Locator("button"));
            Assert.That(await inner.CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await inner.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("b"));
        }

        [PlaywrightTest("locator-query.spec.ts", "Locator(ILocator) accepts a filtered inner locator")]
        [Test]
        [Timeout(30_000)]
        public async Task LocatorShouldAcceptAFilteredInnerLocator()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<section id=\"one\"><button>Save</button><button>Cancel</button></section>" +
                "<section id=\"two\"><button>Save</button></section>").ConfigureAwait(false);

            ILocator save = page.Locator("#one").Locator(page.Locator("button").Filter("Save"));
            Assert.That(await save.CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That((await save.TextContentAsync().ConfigureAwait(false)).Trim(), Is.EqualTo("Save"));
            Assert.That(await page.Locator("#two").Locator(page.Locator("button").Filter("Save")).CountAsync().ConfigureAwait(false), Is.EqualTo(1));
        }

        [PlaywrightTest("locator-query.spec.ts", "Locator(ILocator) rejects a locator from another page")]
        [Test]
        [Timeout(30_000)]
        public async Task LocatorShouldRejectAnotherPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IPage other = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);
            await other.SetContentAsync("<button>X</button>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.Throws<PlaywrightNativeException>(
                () => page.Locator("div").Locator(other.Locator("button")));

            Assert.That(ex.Message, Does.Contain("same frame"));
        }

        [PlaywrightTest("locator-query.spec.ts", "Locator(ILocator) rejects null")]
        [Test]
        [Timeout(30_000)]
        public async Task LocatorShouldRejectNull()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            Assert.Throws<ArgumentNullException>(() => page.Locator("div").Locator((ILocator)null));
        }
    }
}
