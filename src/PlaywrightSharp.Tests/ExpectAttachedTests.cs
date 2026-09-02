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
    /// Expect ToBeAttached and ToBeFocused.
    /// </summary>
    [TestFixture]
    public class ExpectAttachedTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToBeAttached waits until the element appears")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeAttachedShouldWaitUntilTheElementAppears()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#t")).ToBeAttachedAsync(new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('host').insertAdjacentHTML('beforeend', '<span id=\"t\">x</span>')")
                .ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToBeFocused waits until focus")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeFocusedShouldWaitUntilFocus()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"n\" /><input id=\"other\" />").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#n")).ToBeFocusedAsync(new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.Locator("#n").FocusAsync().ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "Not ToBeAttached matches a missing locator")]
        [Test]
        [Timeout(30_000)]
        public async Task NotToBeAttachedShouldMatchAMissingLocator()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<p>only</p>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#gone")).Not.ToBeAttachedAsync().ConfigureAwait(false);
        }
    }
}
