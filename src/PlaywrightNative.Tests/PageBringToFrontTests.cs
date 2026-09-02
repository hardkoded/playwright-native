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
    /// Direct-connection tests for <see cref="IPage.BringToFrontAsync"/>.
    /// </summary>
    [TestFixture]
    public class PageBringToFrontTests : PageTestEx
    {
        [PlaywrightTest("page-basic.spec.ts", "switches visibility between two pages")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSwitchVisibilityBetweenTwoPages()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page1 = await context.NewPageAsync().ConfigureAwait(false);
            await page1.SetContentAsync("<div>one</div>").ConfigureAwait(false);
            IPage page2 = await context.NewPageAsync().ConfigureAwait(false);
            await page2.SetContentAsync("<div>two</div>").ConfigureAwait(false);

            await page1.BringToFrontAsync().ConfigureAwait(false);
            Assert.That(await page1.EvaluateAsync<string>("document.visibilityState").ConfigureAwait(false), Is.EqualTo("visible"));
            Assert.That(await page1.EvaluateAsync<string>("document.querySelector('div').textContent").ConfigureAwait(false), Is.EqualTo("one"));
            if (TestConstants.IsChromium)
            {
                Assert.That(await page2.EvaluateAsync<string>("document.visibilityState").ConfigureAwait(false), Is.EqualTo("hidden"));
            }

            await page2.BringToFrontAsync().ConfigureAwait(false);
            Assert.That(await page2.EvaluateAsync<string>("document.visibilityState").ConfigureAwait(false), Is.EqualTo("visible"));
            Assert.That(await page2.EvaluateAsync<string>("document.querySelector('div').textContent").ConfigureAwait(false), Is.EqualTo("two"));
            if (TestConstants.IsChromium)
            {
                Assert.That(await page1.EvaluateAsync<string>("document.visibilityState").ConfigureAwait(false), Is.EqualTo("hidden"));
            }
        }

        [PlaywrightTest("page-basic.spec.ts", "is a no-op when already in front")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldStayVisibleWhenAlreadyInFront()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>front</div>").ConfigureAwait(false);

            await page.BringToFrontAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.visibilityState").ConfigureAwait(false), Is.EqualTo("visible"));
        }
    }
}
