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
    /// <see cref="ILocatorAssertions.Not"/> and enabled / checked expect matchers.
    /// </summary>
    [TestFixture]
    public class ExpectStateTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "Not ToBeVisible waits until hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task NotToBeVisibleShouldWaitUntilHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">x</div>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#t")).Not.ToBeVisibleAsync(new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#t').style.display = 'none'").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToBeEnabled waits until enabled")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeEnabledShouldWaitUntilEnabled()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"b\" disabled>Go</button>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#b")).ToBeEnabledAsync(new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#b').disabled = false").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToBeDisabled ToBeEditable ToBeChecked")]
        [Test]
        [Timeout(30_000)]
        public async Task StateMatchersShouldResolve()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<button id=\"b\" disabled>Go</button>" +
                "<input id=\"n\" value=\"hi\" />" +
                "<input id=\"c\" type=\"checkbox\" checked />").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#b")).ToBeDisabledAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#n")).ToBeEditableAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#n")).Not.ToBeDisabledAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#c")).ToBeCheckedAsync().ConfigureAwait(false);
        }
    }
}
