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
    /// Official <c>page.locator(otherLocator)</c> / <c>frame.locator(otherLocator)</c>.
    /// </summary>
    [TestFixture]
    public class PageLocatorLocatorTests : PageTestEx
    {
        [PlaywrightTest("locator-query.spec.ts", "Page.Locator(ILocator) finds the same match")]
        [Test]
        [Timeout(30_000)]
        public async Task PageLocatorShouldFindTheSameMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                    "<div id=\"out\"><button id=\"a\">A</button></div>" +
                    "<div id=\"in\"><button id=\"b\">B</button></div>")
                .ConfigureAwait(false);

            ILocator buttons = page.Locator(page.Locator("button"));
            Assert.That(await buttons.CountAsync().ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await page.Locator(page.Locator("#b")).GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("b"));
        }

        [PlaywrightTest("locator-query.spec.ts", "Page.Locator(ILocator) accepts a filtered locator")]
        [Test]
        [Timeout(30_000)]
        public async Task PageLocatorShouldAcceptAFilteredLocator()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                    "<button>Save</button><button>Cancel</button>")
                .ConfigureAwait(false);

            ILocator save = page.Locator(page.Locator("button").Filter("Save"));
            Assert.That(await save.CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That((await save.TextContentAsync().ConfigureAwait(false)).Trim(), Is.EqualTo("Save"));
        }

        [PlaywrightTest("locator-query.spec.ts", "Frame.Locator(ILocator) stays in that frame")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameLocatorShouldStayInThatFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"go\">Go</button>").ConfigureAwait(false);

            ILocator go = page.MainFrame.Locator(page.MainFrame.Locator("#go"));
            Assert.That(await go.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("go"));
        }

        [PlaywrightTest("locator-query.spec.ts", "Page.Locator(ILocator) rejects a locator from another page")]
        [Test]
        [Timeout(30_000)]
        public async Task PageLocatorShouldRejectAnotherPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IPage other = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);
            await other.SetContentAsync("<button>X</button>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.Throws<PlaywrightNativeException>(
                () => page.Locator(other.Locator("button")));

            Assert.That(ex.Message, Does.Contain("same frame"));
        }

        [PlaywrightTest("locator-query.spec.ts", "Page.Locator(ILocator) rejects null")]
        [Test]
        [Timeout(30_000)]
        public async Task PageLocatorShouldRejectNull()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.Throws<ArgumentNullException>(() => page.Locator((ILocator)null));
        }
    }
}
