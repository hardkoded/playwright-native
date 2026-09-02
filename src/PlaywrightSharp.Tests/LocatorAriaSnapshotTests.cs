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
    /// AriaSnapshot on <see cref="ILocator"/>.
    /// </summary>
    [TestFixture]
    public class LocatorAriaSnapshotTests : PageTestEx
    {
        [PlaywrightTest("page-aria-snapshot.spec.ts", "AriaSnapshot includes the button")]
        [Test]
        [Timeout(30_000)]
        public async Task AriaSnapshotShouldIncludeTheButton()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='go'>Go</button>").ConfigureAwait(false);

            string yaml = await page.Locator("#go").AriaSnapshotAsync().ConfigureAwait(false);
            Assert.That(yaml, Does.Contain("button"));
            Assert.That(yaml, Does.Contain("Go"));
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "AriaSnapshot is strict")]
        [Test]
        [Timeout(30_000)]
        public async Task AriaSnapshotShouldThrowWhenTwoMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button>One</button><button>Two</button>").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Locator("button").AriaSnapshotAsync());

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
