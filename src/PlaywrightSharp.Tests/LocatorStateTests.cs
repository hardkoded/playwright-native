/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Visibility and enabled queries on <see cref="ILocator"/>.
    /// </summary>
    [TestFixture]
    public class LocatorStateTests : PageTestEx
    {
        [PlaywrightTest("locator-convenience.spec.ts", "IsVisible and IsHidden do not wait")]
        [Test]
        [Timeout(30_000)]
        public async Task VisibleAndHiddenShouldNotWait()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"v\">shown</div><div id=\"h\" style=\"display:none\">hidden</div>").ConfigureAwait(false);

            Assert.That(await page.Locator("#v").IsVisibleAsync().ConfigureAwait(false), Is.True);
            Assert.That(await page.Locator("#v").IsHiddenAsync().ConfigureAwait(false), Is.False);
            Assert.That(await page.Locator("#h").IsVisibleAsync().ConfigureAwait(false), Is.False);
            Assert.That(await page.Locator("#h").IsHiddenAsync().ConfigureAwait(false), Is.True);
            Assert.That(await page.Locator("#missing").IsVisibleAsync().ConfigureAwait(false), Is.False);
            Assert.That(await page.Locator("#missing").IsHiddenAsync().ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("locator-convenience.spec.ts", "IsEnabled and IsDisabled")]
        [Test]
        [Timeout(30_000)]
        public async Task EnabledAndDisabledShouldReadTheControl()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"on\">Go</button><button id=\"off\" disabled>Stop</button>").ConfigureAwait(false);

            Assert.That(await page.Locator("#on").IsEnabledAsync().ConfigureAwait(false), Is.True);
            Assert.That(await page.Locator("#on").IsDisabledAsync().ConfigureAwait(false), Is.False);
            Assert.That(await page.Locator("#off").IsEnabledAsync().ConfigureAwait(false), Is.False);
            Assert.That(await page.Locator("#off").IsDisabledAsync().ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("locator-convenience.spec.ts", "IsEditable")]
        [Test]
        [Timeout(30_000)]
        public async Task EditableShouldReadTheControl()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"e\" /><input id=\"r\" readonly />").ConfigureAwait(false);

            Assert.That(await page.Locator("#e").IsEditableAsync().ConfigureAwait(false), Is.True);
            Assert.That(await page.Locator("#r").IsEditableAsync().ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("locator-convenience.spec.ts", "IsVisible is strict")]
        [Test]
        [Timeout(30_000)]
        public async Task VisibleShouldThrowWhenTwoMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div class=\"x\">a</div><div class=\"x\">b</div>").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Locator(".x").IsVisibleAsync());

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
