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
    /// Expect ToBeEmpty and ToContainText.
    /// </summary>
    [TestFixture]
    public class ExpectEmptyTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToBeEmpty waits until the node has no text")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeEmptyShouldWaitUntilTheNodeHasNoText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">hello</div>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#t")).ToBeEmptyAsync(new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#t').textContent = ''").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToBeEmpty matches an empty input")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeEmptyShouldMatchAnEmptyInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"n\" value=\"Ada\" />").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#n")).Not.ToBeEmptyAsync(new() { Timeout = 2000 }).ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#n")).ToBeEmptyAsync(new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#n').value = ''").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToContainText waits until the substring appears")]
        [Test]
        [Timeout(30_000)]
        public async Task ToContainTextShouldWaitUntilTheSubstringAppears()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">hello</div>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#t")).ToContainTextAsync("world", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#t').textContent = 'hello world'").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "Not ToContainText matches missing substring")]
        [Test]
        [Timeout(30_000)]
        public async Task NotToContainTextShouldMatchMissingSubstring()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">hello</div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#t")).Not.ToContainTextAsync("world", new() { Timeout = 2000 }).ConfigureAwait(false);
        }
    }
}
