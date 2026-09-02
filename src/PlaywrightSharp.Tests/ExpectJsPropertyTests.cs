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
    /// Expect ToHaveJSProperty and ToBeInViewport.
    /// </summary>
    [TestFixture]
    public class ExpectJsPropertyTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToHaveJSProperty waits until the property is set")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveJSPropertyShouldWaitUntilThePropertyIsSet()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">x</div>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#t")).ToHaveJSPropertyAsync("foo", 7, new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#t').foo = 7").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveJSProperty matches a string property")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveJSPropertyShouldMatchAStringProperty()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">x</div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#t")).ToHaveJSPropertyAsync("id", "t").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToBeInViewport waits until scrolled into view")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeInViewportShouldWaitUntilScrolledIntoView()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 400).ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"height:2000px\"></div><div id=\"t\">target</div>").ConfigureAwait(false);

            var before = await page.Locator("#t").BoundingBoxAsync().ConfigureAwait(false);
            Assert.That(before, Is.Not.Null);
            Assert.That(before.Y, Is.GreaterThan(400));
            await Assertions.Expect(page.Locator("#t")).Not.ToBeInViewportAsync(new() { Timeout = 2000 }).ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#t")).ToBeInViewportAsync(new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.Locator("#t").ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }
    }
}
