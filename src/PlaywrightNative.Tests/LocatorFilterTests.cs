/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Filter, And, Or, and Has on <see cref="ILocator"/>.
    /// </summary>
    [TestFixture]
    public class LocatorFilterTests : PageTestEx
    {
        [PlaywrightTest("locator-query.spec.ts", "Filter hasText keeps the matching node")]
        [Test]
        [Timeout(30_000)]
        public async Task FilterShouldKeepTheMatchingText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"a\">Save</button><button id=\"b\">Cancel</button>").ConfigureAwait(false);

            await page.Locator("button").Filter("Save").ClickAsync().ConfigureAwait(false);

            string id = await page.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);
            Assert.That(id, Is.EqualTo("a"));
            Assert.That(await page.Locator("button").Filter("Save").CountAsync().ConfigureAwait(false), Is.EqualTo(1));
        }

        [PlaywrightTest("locator-query.spec.ts", "And intersects two locators")]
        [Test]
        [Timeout(30_000)]
        public async Task AndShouldIntersect()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<button class=\"primary\" id=\"a\">A</button>" +
                "<button id=\"b\">B</button>" +
                "<button class=\"primary extra\" id=\"c\">C</button>").ConfigureAwait(false);

            ILocator both = page.Locator("button").And(page.Locator(".primary"));
            Assert.That(await both.CountAsync().ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await both.First.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("a"));
            Assert.That(await both.Last.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("c"));
        }

        [PlaywrightTest("locator-query.spec.ts", "Or unions two locators")]
        [Test]
        [Timeout(30_000)]
        public async Task OrShouldUnion()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"a\">A</button><button id=\"b\">B</button><button id=\"c\">C</button>").ConfigureAwait(false);

            ILocator union = page.Locator("#a").Or(page.Locator("#c"));
            Assert.That(await union.CountAsync().ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await union.Nth(1).GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("c"));
        }

        [PlaywrightTest("locator-query.spec.ts", "Has keeps ancestors of a descendant")]
        [Test]
        [Timeout(30_000)]
        public async Task HasShouldKeepAncestors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<div class=\"card\" id=\"a\"><button>Go</button></div>" +
                "<div class=\"card\" id=\"b\"><span>No</span></div>").ConfigureAwait(false);

            ILocator withButton = page.Locator(".card").Has(page.Locator("button"));
            Assert.That(await withButton.CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await withButton.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("a"));
        }

        [PlaywrightTest("locator-query.spec.ts", "Filter remains strict")]
        [Test]
        [Timeout(30_000)]
        public async Task FilterShouldThrowWhenTwoMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button>Save now</button><button>Save later</button>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.Locator("button").Filter("Save").ClickAsync());

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
            Assert.That(await page.Locator("button").Filter("Save").CountAsync().ConfigureAwait(false), Is.EqualTo(2));
        }

        [PlaywrightTest("locator-query.spec.ts", "Filter visible keeps shown nodes")]
        [Test]
        [Timeout(30_000)]
        public async Task FilterVisibleShouldKeepShownNodes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<button id=\"a\">A</button>" +
                "<button id=\"b\" hidden>B</button>").ConfigureAwait(false);

            ILocator shown = page.Locator("button").Filter(true);
            ILocator hidden = page.Locator("button").Filter(false);
            Assert.That(await shown.CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await shown.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("a"));
            Assert.That(await hidden.CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await hidden.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("b"));
        }
    }
}
