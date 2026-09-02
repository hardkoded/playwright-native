/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>locator.elementHandles()</c>.
    /// </summary>
    [TestFixture]
    public class LocatorElementHandlesTests : PageTestEx
    {
        [PlaywrightTest("locator-convenience.spec.ts", "elementHandles should return matching elements")]
        [Test]
        [Timeout(30_000)]
        public async Task ElementHandlesShouldReturnMatchingElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button>one</button><button>two</button>").ConfigureAwait(false);

            IReadOnlyList<IElementHandle> handles = await page.Locator("button").ElementHandlesAsync().ConfigureAwait(false);
            Assert.That(handles, Has.Count.EqualTo(2));
            Assert.That(await handles[0].TextContentAsync().ConfigureAwait(false), Is.EqualTo("one"));
            Assert.That(await handles[1].TextContentAsync().ConfigureAwait(false), Is.EqualTo("two"));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "elementHandles should return empty list when nothing matches")]
        [Test]
        [Timeout(30_000)]
        public async Task ElementHandlesShouldReturnEmptyWhenNothingMatches()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>none</div>").ConfigureAwait(false);

            IReadOnlyList<IElementHandle> handles = await page.Locator("button").ElementHandlesAsync().ConfigureAwait(false);
            Assert.That(handles, Is.Empty);
        }
    }
}
