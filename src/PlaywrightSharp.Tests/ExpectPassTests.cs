/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Expect ToPass.
    /// </summary>
    [TestFixture]
    public class ExpectPassTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToPass waits until the callback succeeds")]
        [Test]
        [Timeout(30_000)]
        public async Task ToPassShouldWaitUntilTheCallbackSucceeds()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">hello</div>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#t")).ToPassAsync(
                async () =>
                {
                    string text = await page.Locator("#t").TextContentAsync().ConfigureAwait(false);
                    if (text != "ready")
                    {
                        throw new InvalidOperationException(text);
                    }
                },
                timeout: 5000);
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#t').textContent = 'ready'").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "Not ToPass succeeds when the callback throws")]
        [Test]
        [Timeout(30_000)]
        public async Task NotToPassShouldSucceedWhenTheCallbackThrows()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">hello</div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#t")).Not.ToPassAsync(
                () => throw new InvalidOperationException("still failing"),
                timeout: 2000).ConfigureAwait(false);
        }
    }
}
