/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Expect ToBeChecked checked and indeterminate options.
    /// </summary>
    [TestFixture]
    public class ExpectCheckedOptionsTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToBeChecked checked false matches unchecked")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeCheckedCheckedFalseShouldMatchUnchecked()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" />").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#c")).ToBeCheckedAsync(new() { Checked = false }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#c")).Not.ToBeCheckedAsync(new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToBeChecked checked false waits until unchecked")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeCheckedCheckedFalseShouldWaitUntilUnchecked()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" checked />").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#c")).ToBeCheckedAsync(new() { Timeout = 5000, Checked = false });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#c').checked = false").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToBeChecked indeterminate matches mixed")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeCheckedIndeterminateShouldMatchMixed()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" />").ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#c').indeterminate = true").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#c")).ToBeCheckedAsync(new() { Indeterminate = true }).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#c').indeterminate = false").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#c")).Not.ToBeCheckedAsync(new() { Indeterminate = true, Timeout = 2000 })
                .ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToBeChecked rejects checked with indeterminate")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeCheckedShouldRejectCheckedWithIndeterminate()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" />").ConfigureAwait(false);

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => Assertions.Expect(page.Locator("#c")).ToBeCheckedAsync(new() { Checked = false, Indeterminate = true }));
            Assert.That(ex.Message, Does.Contain("indeterminate and checked"));
        }
    }
}
