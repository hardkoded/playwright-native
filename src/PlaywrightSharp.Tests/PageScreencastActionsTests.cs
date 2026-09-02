/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>screencast.showActions</c>.
    /// </summary>
    [TestFixture]
    public class PageScreencastActionsTests : PageTestEx
    {
        [PlaywrightTest("screencast-actions.spec.ts", "showActions annotates click")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAnnotateClick()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"go\">Go</button>").ConfigureAwait(false);
            await page.Screencast.ShowActionsAsync(new() { Duration = 2000 }).ConfigureAwait(false);

            Task click = page.ClickAsync("#go");
            Assert.That(
                await page.Locator("[data-pw-screencast-action-title]").TextContentAsync().ConfigureAwait(false),
                Is.EqualTo("Click"));
            await click.ConfigureAwait(false);
        }

        [PlaywrightTest("screencast-actions.spec.ts", "disposing showActions stops annotations")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldStopAnnotatingWhenDisposed()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"go\">Go</button>").ConfigureAwait(false);

            await using (IAsyncDisposable actions = await page.Screencast.ShowActionsAsync(new() { Duration = 2000 }).ConfigureAwait(false))
            {
            }

            await page.ClickAsync("#go").ConfigureAwait(false);
            Assert.That(await page.Locator("[data-pw-screencast-action-title]").CountAsync().ConfigureAwait(false), Is.EqualTo(0));
        }
    }
}
