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
    /// Official <c>GetByText(Regex)</c>.
    /// </summary>
    [TestFixture]
    public class GetByTextRegexTests : PageTestEx
    {
        [PlaywrightTest("selectors-get-by.spec.ts", "GetByText Regex matches on page and locator")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByTextRegexShouldMatchOnPageAndLocator()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"root\"><p id=\"p\">Hello world</p></div>").ConfigureAwait(false);

            string pageText = await page.GetByText(new Regex("hello", RegexOptions.IgnoreCase)).TextContentAsync().ConfigureAwait(false);
            string locatorText = await page.Locator("#root").GetByText(new Regex("^Hello")).TextContentAsync().ConfigureAwait(false);
            string frameText = await page.MainFrame.GetByText(new Regex("world$")).TextContentAsync().ConfigureAwait(false);

            Assert.That(pageText, Does.Contain("Hello world"));
            Assert.That(locatorText, Does.Contain("Hello world"));
            Assert.That(frameText, Does.Contain("Hello world"));
        }
    }
}
