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
    /// Official <c>page.ariaSnapshot()</c> (no selector).
    /// </summary>
    [TestFixture]
    public class PageAriaSnapshotTests : PageTestEx
    {
        [PlaywrightTest("page-aria-snapshot.spec.ts", "Page AriaSnapshot includes the button")]
        [Test]
        [Timeout(30_000)]
        public async Task PageAriaSnapshotShouldIncludeTheButton()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='go'>Go</button>").ConfigureAwait(false);

            string yaml = await page.AriaSnapshotAsync().ConfigureAwait(false);
            Assert.That(yaml, Does.Contain("button"));
            Assert.That(yaml, Does.Contain("Go"));
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "Page AriaSnapshot AI mode adds refs")]
        [Test]
        [Timeout(30_000)]
        public async Task PageAriaSnapshotAiModeShouldAddRefs()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='go'>Go</button>").ConfigureAwait(false);

            string yaml = await page.AriaSnapshotAsync(new() { Mode = AriaSnapshotMode.Ai }).ConfigureAwait(false);
            Assert.That(yaml, Does.Contain("button"));
            Assert.That(yaml, Does.Contain("[ref=e"));
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "Page AriaSnapshot selector form still works")]
        [Test]
        [Timeout(30_000)]
        public async Task PageAriaSnapshotSelectorFormShouldStillWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='go'>Go</button>").ConfigureAwait(false);

            string yaml = await page.AriaSnapshotAsync("#go").ConfigureAwait(false);
            Assert.That(yaml, Does.Contain("button"));
            Assert.That(yaml, Does.Contain("Go"));
            Assert.That(yaml, Does.Not.Contain("[ref="));
        }
    }
}
