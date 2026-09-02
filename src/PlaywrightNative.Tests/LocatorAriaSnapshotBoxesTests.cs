/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>locator.ariaSnapshot({ boxes })</c>.
    /// </summary>
    [TestFixture]
    public class LocatorAriaSnapshotBoxesTests : PageTestEx
    {
        [PlaywrightTest("page-aria-snapshot.spec.ts", "Boxes appends box markers")]
        [Test]
        [Timeout(30_000)]
        public async Task BoxesShouldAppendBoxMarkers()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='go'>Go</button>").ConfigureAwait(false);

            string yaml = await page.Locator("#go")
                .AriaSnapshotAsync(new() { Boxes = true })
                .ConfigureAwait(false);
            Assert.That(yaml, Does.Contain("button"));
            Assert.That(yaml, Does.Match(new Regex(@"\[box=-?\d+,-?\d+,\d+,\d+\]")));
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "Default omits box markers")]
        [Test]
        [Timeout(30_000)]
        public async Task DefaultShouldOmitBoxMarkers()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='go'>Go</button>").ConfigureAwait(false);

            string yaml = await page.Locator("#go").AriaSnapshotAsync().ConfigureAwait(false);
            Assert.That(yaml, Does.Not.Contain("[box="));
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "Page AriaSnapshot boxes")]
        [Test]
        [Timeout(30_000)]
        public async Task PageAriaSnapshotShouldAppendBoxMarkers()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='go'>Go</button>").ConfigureAwait(false);

            string yaml = await page.AriaSnapshotAsync(new() { Boxes = true }).ConfigureAwait(false);
            Assert.That(yaml, Does.Contain("button"));
            Assert.That(yaml, Does.Match(new Regex(@"\[box=-?\d+,-?\d+,\d+,\d+\]")));
        }
    }
}
