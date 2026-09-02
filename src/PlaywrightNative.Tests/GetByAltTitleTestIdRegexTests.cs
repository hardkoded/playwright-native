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
    /// Official <c>GetByAltText</c> / <c>GetByTitle</c> / <c>GetByTestId(Regex)</c>.
    /// </summary>
    [TestFixture]
    public class GetByAltTitleTestIdRegexTests : PageTestEx
    {
        [PlaywrightTest("selectors-get-by.spec.ts", "GetByAltText GetByTitle GetByTestId Regex")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByAltTitleAndTestIdRegexShouldResolve()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<img id=\"logo\" alt=\"Company logo\" title=\"Acme Inc\" data-testid=\"hero-logo\" />").ConfigureAwait(false);

            string altId = await page.GetByAltText(new Regex("company", RegexOptions.IgnoreCase)).GetAttributeAsync("id").ConfigureAwait(false);
            string titleId = await page.GetByTitle(new Regex("^Acme")).GetAttributeAsync("id").ConfigureAwait(false);
            string testId = await page.GetByTestId(new Regex("hero-")).GetAttributeAsync("id").ConfigureAwait(false);
            string locatorId = await page.Locator("body").GetByTestId(new Regex("logo$")).GetAttributeAsync("id").ConfigureAwait(false);

            Assert.That(altId, Is.EqualTo("logo"));
            Assert.That(titleId, Is.EqualTo("logo"));
            Assert.That(testId, Is.EqualTo("logo"));
            Assert.That(locatorId, Is.EqualTo("logo"));
        }
    }
}
