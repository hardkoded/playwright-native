/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>tests/library/locator-highlight.spec.ts</c> parity for
    /// styled highlight, hide, and navigation. Node <c>test.skip</c> when
    /// <c>mode !== 'default'</c> is not applied (this gate is default).
    /// Official object <c>style</c> is
    /// <see cref="ILocator.HighlightAsync(IReadOnlyDictionary{string, string}, float?)"/>.
    /// Frozen-WebKit skips are not applied.
    /// </summary>
    [TestFixture]
    public class LibraryLocatorHighlightParityTests : PageTestEx
    {
        private static readonly string Prefix = TestConstants.ServerUrl.TrimEnd('/');

        [PlaywrightTest("locator-highlight.spec.ts", "highlight should accept a CSS string style")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task HighlightShouldAcceptACssStringStyle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);

            await page.GetByRole("button").HighlightAsync(style: "outline: 3px solid rgb(255, 0, 0); background-color: rgba(0, 255, 0, 0.25)").ConfigureAwait(false);

            ILocator highlight = page.Locator("x-pw-highlight");
            await Assertions.Expect(highlight).ToBeVisibleAsync().ConfigureAwait(false);
            JsonElement style = await highlight.EvaluateAsync<JsonElement>("el => ({ outline: el.style.outline, backgroundColor: el.style.backgroundColor })").ConfigureAwait(false);
            if (TestConstants.IsChromium)
            {
                Assert.That(style.GetProperty("outline").GetString(), Is.EqualTo("rgb(255, 0, 0) solid 3px"));
            }
            else
            {
                Assert.That(style.GetProperty("outline").GetString(), Is.EqualTo("3px solid rgb(255, 0, 0)"));
            }

            Assert.That(style.GetProperty("backgroundColor").GetString(), Is.EqualTo("rgba(0, 255, 0, 0.25)"));
        }

        [PlaywrightTest("locator-highlight.spec.ts", "highlight should accept an object style (JS only)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task HighlightShouldAcceptAnObjectStyleJsOnly()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);

            await page.GetByRole("button").HighlightAsync(style: new Dictionary<string, string>
            {
                ["outline"] = "2px dashed rgb(0, 0, 255)",
                ["backgroundColor"] = "rgba(255, 255, 0, 0.2)",
            }).ConfigureAwait(false);

            ILocator highlight = page.Locator("x-pw-highlight");
            await Assertions.Expect(highlight).ToBeVisibleAsync().ConfigureAwait(false);
            JsonElement style = await highlight.EvaluateAsync<JsonElement>("el => ({ outline: el.style.outline, backgroundColor: el.style.backgroundColor })").ConfigureAwait(false);
            if (TestConstants.IsChromium)
            {
                Assert.That(style.GetProperty("outline").GetString(), Is.EqualTo("rgb(0, 0, 255) dashed 2px"));
            }
            else
            {
                Assert.That(style.GetProperty("outline").GetString(), Is.EqualTo("2px dashed rgb(0, 0, 255)"));
            }

            Assert.That(style.GetProperty("backgroundColor").GetString(), Is.EqualTo("rgba(255, 255, 0, 0.2)"));
        }

        [PlaywrightTest("locator-highlight.spec.ts", "hideHighlight removes a styled highlight")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task HideHighlightRemovesAStyledHighlight()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);

            ILocator button = page.GetByRole("button");
            await button.HighlightAsync(style: "outline: 2px solid red").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("x-pw-highlight")).ToBeVisibleAsync().ConfigureAwait(false);

            await button.HideHighlightAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("x-pw-highlight")).ToHaveCountAsync(0).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-highlight.spec.ts", "highlight should survive navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task HighlightShouldSurviveNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button onclick=\"console.log(1)\">Clicker</button>").ConfigureAwait(false);

            await page.GetByRole("button").HighlightAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("x-pw-highlight")).ToHaveCountAsync(1).ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("x-pw-highlight")).ToHaveCountAsync(1).ConfigureAwait(false);

            await page.HideHighlightAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("x-pw-highlight")).ToHaveCountAsync(0).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-highlight.spec.ts", "Page.hideHighlight clears all locator highlights")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PageHideHighlightClearsAllLocatorHighlights()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button>One</button><button>Two</button>").ConfigureAwait(false);

            await page.GetByRole("button", name: "One").HighlightAsync().ConfigureAwait(false);
            await page.GetByRole("button", name: "Two").HighlightAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("x-pw-highlight")).ToHaveCountAsync(2).ConfigureAwait(false);

            await page.HideHighlightAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("x-pw-highlight")).ToHaveCountAsync(0).ConfigureAwait(false);
        }
    }
}
