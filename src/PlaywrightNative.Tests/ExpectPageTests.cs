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
    /// Expect page ToHaveTitle and ToHaveURL.
    /// </summary>
    [TestFixture]
    public class ExpectPageTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToHaveTitle waits until the title changes")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveTitleShouldWaitUntilTheTitleChanges()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<html><head><title>Hello</title></head><body></body></html>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page).ToHaveTitleAsync("World", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.title = 'World'").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveURL waits until navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveURLShouldWaitUntilNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<div>one</div>").ConfigureAwait(false);
            await Assertions.Expect(page).ToHaveURLAsync("data:text/html", new() { Timeout = 5000 }).ConfigureAwait(false);
            await Assertions.Expect(page).Not.ToHaveURLAsync("expect-url-two", new() { Timeout = 2000 }).ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page).ToHaveURLAsync("expect-url-two", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.GoToAsync("data:text/html,<div>expect-url-two</div>").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveTitle and ToHaveURL match a regex")]
        [Test]
        [Timeout(30_000)]
        public async Task TitleAndUrlShouldMatchARegex()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("data:text/html,<title>Playwright</title><div>ok</div>").ConfigureAwait(false);

            await Assertions.Expect(page).ToHaveTitleAsync(new Regex("Play.*")).ConfigureAwait(false);
            await Assertions.Expect(page).ToHaveURLAsync(new Regex("^data:text/html")).ConfigureAwait(false);
            await Assertions.Expect(page).Not.ToHaveTitleAsync(new Regex("Nope"), new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToMatchAriaSnapshot contains the body")]
        [Test]
        [Timeout(30_000)]
        public async Task ToMatchAriaSnapshotShouldContainTheBody()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"go\">Go</button>").ConfigureAwait(false);

            await Assertions.Expect(page).ToMatchAriaSnapshotAsync("button", new() { Timeout = 5000 }).ConfigureAwait(false);
            await Assertions.Expect(page).ToMatchAriaSnapshotAsync("Go", new() { Timeout = 5000 }).ConfigureAwait(false);
        }
    }
}
