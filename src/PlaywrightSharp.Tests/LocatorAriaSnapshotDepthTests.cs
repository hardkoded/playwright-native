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
    /// Official <c>locator.ariaSnapshot({ depth })</c>.
    /// </summary>
    [TestFixture]
    public class LocatorAriaSnapshotDepthTests : PageTestEx
    {
        [PlaywrightTest("page-aria-snapshot.spec.ts", "Depth 1 omits nested lists")]
        [Test]
        [Timeout(30_000)]
        public async Task DepthOneShouldOmitNestedLists()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                    "<div role='menubar' id='root'>" +
                    "<div role='menu'><div role='menuitem'>alpha" +
                    "<div role='menu'><div role='menuitem'>omega</div></div>" +
                    "</div></div></div>")
                .ConfigureAwait(false);

            string limited = await page.Locator("#root")
                .AriaSnapshotAsync(new() { Depth = 1 })
                .ConfigureAwait(false);
            Assert.That(limited, Does.Not.Contain("omega"));

            string full = await page.Locator("#root").AriaSnapshotAsync().ConfigureAwait(false);
            Assert.That(full, Does.Contain("omega"));
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "Depth 0 is only the root")]
        [Test]
        [Timeout(30_000)]
        public async Task DepthZeroShouldBeOnlyTheRoot()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                    "<div role='menubar' id='root'>" +
                    "<div role='menu'><div role='menuitem'>alpha" +
                    "<div role='menu'><div role='menuitem'>omega</div></div>" +
                    "</div></div></div>")
                .ConfigureAwait(false);

            string yaml = await page.Locator("#root")
                .AriaSnapshotAsync(new() { Depth = 0 })
                .ConfigureAwait(false);
            Assert.That(yaml, Does.Contain("menu"));
            Assert.That(yaml, Does.Not.Contain("omega"));
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "Page AriaSnapshot honors depth")]
        [Test]
        [Timeout(30_000)]
        public async Task PageAriaSnapshotShouldHonorDepth()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button>Go</button>").ConfigureAwait(false);

            string rootOnly = await page.AriaSnapshotAsync(new() { Depth = 0 }).ConfigureAwait(false);
            Assert.That(rootOnly, Does.Not.Contain("button"));

            string yaml = await page.AriaSnapshotAsync().ConfigureAwait(false);
            Assert.That(yaml, Does.Contain("button"));
        }
    }
}
