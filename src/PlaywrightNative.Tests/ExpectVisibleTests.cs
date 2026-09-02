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
    /// <see cref="Assertions.Expect(ILocator)"/> visibility and count.
    /// </summary>
    [TestFixture]
    public class ExpectVisibleTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToBeVisible waits until shown")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeVisibleShouldWaitUntilShown()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\" style=\"display:none\">x</div>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#t")).ToBeVisibleAsync(new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#t').style.display = 'block'").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToBeHidden matches a missing locator")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeHiddenShouldMatchAMissingLocator()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<p>only</p>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#gone")).ToBeHiddenAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveCount waits until the count matches")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveCountShouldWaitUntilTheCountMatches()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div class=\"x\">one</div>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator(".x")).ToHaveCountAsync(2, new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.body.insertAdjacentHTML('beforeend', '<div class=\"x\">two</div>')")
                .ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToBeVisible times out while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeVisibleShouldTimeoutWhileHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\" style=\"display:none\">x</div>").ConfigureAwait(false);

            TimeoutException ex = Assert.CatchAsync<TimeoutException>(
                () => Assertions.Expect(page.Locator("#t")).ToBeVisibleAsync(new() { Timeout = 200 }));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("expect.toBeVisible"));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }
    }
}
