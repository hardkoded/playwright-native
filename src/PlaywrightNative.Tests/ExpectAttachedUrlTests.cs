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
    /// Expect ToBeAttached(attached) and ToHaveURL(ignoreCase).
    /// </summary>
    [TestFixture]
    public class ExpectAttachedUrlTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToBeAttached attached false waits until detached")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeAttachedAttachedFalseShouldWaitUntilDetached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">x</div>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#t")).ToBeAttachedAsync(new() { Timeout = 5000, Attached = false });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.getElementById('t').remove()").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToBeAttached attached false matches a missing locator")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeAttachedAttachedFalseShouldMatchAMissingLocator()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<p>only</p>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#gone")).ToBeAttachedAsync(new() { Attached = false }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("p")).ToBeAttachedAsync(new() { Attached = true }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveURL ignoreCase matches mixed case")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveURLIgnoreCaseShouldMatchMixedCase()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("data:text/html,<div>HELLO-URL</div>").ConfigureAwait(false);

            await Assertions.Expect(page).ToHaveURLAsync("hello-url", new() { IgnoreCase = true }).ConfigureAwait(false);
            await Assertions.Expect(page).ToHaveURLAsync(new Regex("hello-url"), new() { IgnoreCase = true }).ConfigureAwait(false);
            await Assertions.Expect(page).Not.ToHaveURLAsync("hello-url", new() { Timeout = 2000 }).ConfigureAwait(false);
        }
    }
}
