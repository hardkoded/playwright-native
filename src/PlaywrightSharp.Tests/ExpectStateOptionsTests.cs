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
    /// Expect ToBeEnabled / ToBeVisible / ToBeEditable option flags.
    /// </summary>
    [TestFixture]
    public class ExpectStateOptionsTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToBeEnabled enabled false matches disabled")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeEnabledEnabledFalseShouldMatchDisabled()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"b\" disabled>Go</button>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#b")).ToBeEnabledAsync(new() { Enabled = false }).ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#b")).ToBeEnabledAsync(new() { Timeout = 5000, Enabled = true });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#b').disabled = false").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToBeVisible visible false matches hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeVisibleVisibleFalseShouldMatchHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\" style=\"display:none\">x</div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#t")).ToBeVisibleAsync(new() { Visible = false }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#gone")).ToBeVisibleAsync(new() { Visible = false }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToBeEditable editable false matches readonly")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeEditableEditableFalseShouldMatchReadonly()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"n\" readonly value=\"hi\" />").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#n")).ToBeEditableAsync(new() { Editable = false }).ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#n")).ToBeEditableAsync(new() { Timeout = 5000, Editable = true });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#n').readOnly = false").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }
    }
}
