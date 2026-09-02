/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Expect ToHaveClass and ToHaveCSS.
    /// </summary>
    [TestFixture]
    public class ExpectClassCssTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToHaveClass waits until the class is added")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveClassShouldWaitUntilTheClassIsAdded()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\" class=\"a\">x</div>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#t")).ToContainClassAsync("ready", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#t').classList.add('ready')").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveCSS matches computed style")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveCSSShouldMatchComputedStyle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\" style=\"display:none\">x</div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#t")).ToHaveCSSAsync("display", "none").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#t")).ToHaveCSSAsync("display", "block", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#t').style.display = 'block'").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveId and ToHaveCSS match a regex")]
        [Test]
        [Timeout(30_000)]
        public async Task IdAndCssShouldMatchARegex()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"row-12\" style=\"display:none\">x</div>").ConfigureAwait(false);

            ILocatorAssertions expect = Assertions.Expect(page.Locator("#row-12"));
            await expect.ToHaveIdAsync(new Regex("row-\\d+")).ConfigureAwait(false);
            await expect.ToHaveCSSAsync("display", new Regex("^none$")).ConfigureAwait(false);
            await expect.Not.ToHaveCSSAsync("display", new Regex("^block$"), new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveClass matches a regex")]
        [Test]
        [Timeout(30_000)]
        public async Task ClassShouldMatchARegex()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\" class=\"a ready\">x</div>").ConfigureAwait(false);

            ILocatorAssertions expect = Assertions.Expect(page.Locator("#t"));
            await expect.ToHaveClassAsync(new Regex("ready")).ConfigureAwait(false);
            await expect.ToHaveClassAsync(new Regex("^a ready$")).ConfigureAwait(false);
            await expect.Not.ToHaveClassAsync(new Regex("^missing$"), new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveClass list matches each locator in order")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveClassListShouldMatchEachLocatorInOrder()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<ul id=\"w\">" +
                "<li class=\"a ready\">1</li>" +
                "<li class=\"b\">2</li>" +
                "<li class=\"c done\">3</li>" +
                "</ul>").ConfigureAwait(false);

            ILocatorAssertions expect = Assertions.Expect(page.Locator("#w li"));
            await expect.ToHaveClassAsync(new[] { "a ready", "b", "c done" }, new() { Timeout = 5000 }).ConfigureAwait(false);
            await expect.ToHaveClassAsync(new[] { new Regex("ready"), new Regex("^b$"), new Regex("done") }).ConfigureAwait(false);
            await expect.Not.ToHaveClassAsync(new[] { "a ready", "b" }, new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveCSS reads a pseudo-element")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveCSSShouldReadPseudoElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<style>#t { color: rgb(0, 0, 255); } #t::before { content: 'x'; color: rgb(255, 0, 0); }</style>" +
                "<div id=\"t\">n</div>").ConfigureAwait(false);

            ILocatorAssertions expect = Assertions.Expect(page.Locator("#t"));
            await expect.ToHaveCSSAsync("color", "rgb(0, 0, 255)").ConfigureAwait(false);
            await expect.ToHaveCSSAsync("color", "rgb(255, 0, 0)", new() { Pseudo = PseudoElement.Before }).ConfigureAwait(false);
            await expect.ToHaveCSSAsync("color", new Regex("255, 0, 0"), new() { Pseudo = PseudoElement.Before }).ConfigureAwait(false);
        }
    }
}
