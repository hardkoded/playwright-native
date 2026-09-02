/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>locator-list.spec.ts</c> parity for <see cref="ILocator.AllAsync"/>.
    /// </summary>
    [TestFixture]
    public class LocatorListParityTests : PageTestEx
    {
        [PlaywrightTest("locator-list.spec.ts", "locator.all should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task LocatorAllShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><p>A</p><p>B</p><p>C</p></div>").ConfigureAwait(false);
            List<string> texts = new();
            foreach (ILocator paragraph in await page.Locator("div >> p").AllAsync().ConfigureAwait(false))
            {
                texts.Add(await paragraph.TextContentAsync().ConfigureAwait(false));
            }

            Assert.That(texts, Is.EqualTo(new[] { "A", "B", "C" }));
        }
    }
}
