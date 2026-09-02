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
    /// Describe on <see cref="ILocator"/>.
    /// </summary>
    [TestFixture]
    public class LocatorDescribeTests : PageTestEx
    {
        [PlaywrightTest("locator-convenience.spec.ts", "Describe names strict errors")]
        [Test]
        [Timeout(30_000)]
        public async Task DescribeShouldNameStrictErrors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div class=\"x\"></div><div class=\"x\"></div>").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Locator(".x").Describe("cards").ClickAsync());

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation: cards resolved to 2 elements."));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "Describe survives First")]
        [Test]
        [Timeout(30_000)]
        public async Task DescribeShouldSurviveFirst()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"a\">A</button><button id=\"b\">B</button>").ConfigureAwait(false);

            await page.Locator("button").Describe("primary").First.ClickAsync().ConfigureAwait(false);
            string id = await page.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);
            Assert.That(id, Is.EqualTo("a"));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "Unnamed locator keeps the generic label")]
        [Test]
        [Timeout(30_000)]
        public async Task UnnamedLocatorShouldKeepGenericLabel()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div class=\"x\"></div><div class=\"x\"></div>").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Locator(".x").ClickAsync());

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation: locator resolved to 2 elements."));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "Description returns the describe label")]
        [Test]
        [Timeout(30_000)]
        public async Task DescriptionShouldReturnTheDescribeLabel()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button>Go</button>").ConfigureAwait(false);

            Assert.That(page.Locator("button").Description, Is.Null);
            Assert.That(page.Locator("button").Describe("primary").Description, Is.EqualTo("primary"));
            Assert.That(page.Locator("button").Describe("primary").First.Description, Is.EqualTo("primary"));
        }
    }
}
