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
    /// Official <c>frameLocator.locator(otherLocator)</c>.
    /// </summary>
    [TestFixture]
    public class FrameLocatorLocatorTests : PageTestEx
    {
        [PlaywrightTest("locator-frame.spec.ts", "FrameLocator.Locator(ILocator) finds the inner input")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameLocatorShouldFindTheInnerInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<div><input value=outer></div>" +
                "<iframe srcdoc=\"<div><input value=inner></div>\"></iframe>")
                .ConfigureAwait(false);

            ILocator inputLocator = page.Locator("input");
            Assert.That(await inputLocator.InputValueAsync().ConfigureAwait(false), Is.EqualTo("outer"));
            Assert.That(
                await page.FrameLocator("iframe").Locator(inputLocator).InputValueAsync().ConfigureAwait(false),
                Is.EqualTo("inner"));
        }

        [PlaywrightTest("locator-frame.spec.ts", "FrameLocator.Locator(ILocator) accepts a parent locator then a child selector")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameLocatorShouldAcceptAParentLocatorThenChildSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<div><input value=outer></div>" +
                "<iframe srcdoc=\"<div><input value=inner></div>\"></iframe>")
                .ConfigureAwait(false);

            ILocator divLocator = page.Locator("div");
            Assert.That(
                await page.FrameLocator("iframe").Locator(divLocator).Locator("input").InputValueAsync().ConfigureAwait(false),
                Is.EqualTo("inner"));
        }

        [PlaywrightTest("locator-frame.spec.ts", "FrameLocator.Locator(ILocator) accepts a filtered locator")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameLocatorShouldAcceptAFilteredLocator()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<button>outer</button>" +
                "<iframe srcdoc=\"<button>keep</button><button>drop</button>\"></iframe>")
                .ConfigureAwait(false);

            ILocator keep = page.FrameLocator("iframe").Locator(page.Locator("button").Filter("keep"));
            Assert.That(await keep.CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That((await keep.TextContentAsync().ConfigureAwait(false)).Trim(), Is.EqualTo("keep"));
        }

        [PlaywrightTest("locator-frame.spec.ts", "FrameLocator.Locator(ILocator) rejects a locator from another page")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameLocatorShouldRejectAnotherPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IPage other = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<iframe srcdoc=\"<input>\"></iframe>").ConfigureAwait(false);
            await other.SetContentAsync("<input value=x>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.Throws<PlaywrightNativeException>(
                () => page.FrameLocator("iframe").Locator(other.Locator("input")));

            Assert.That(ex.Message, Does.Contain("same frame"));
        }

        [PlaywrightTest("locator-frame.spec.ts", "FrameLocator.Locator(ILocator) rejects null")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameLocatorShouldRejectNull()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<iframe srcdoc=\"<input>\"></iframe>").ConfigureAwait(false);

            Assert.Throws<ArgumentNullException>(() => page.FrameLocator("iframe").Locator((ILocator)null));
        }
    }
}
