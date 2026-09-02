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
    /// Official <c>GetByLabel(Regex)</c> / <c>GetByPlaceholder(Regex)</c>.
    /// </summary>
    [TestFixture]
    public class GetByLabelPlaceholderRegexTests : PageTestEx
    {
        [PlaywrightTest("selectors-get-by.spec.ts", "GetByLabel and GetByPlaceholder Regex")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByLabelAndPlaceholderRegexShouldResolve()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<label for=\"n\">Full name</label><input id=\"n\" placeholder=\"Your name\" />").ConfigureAwait(false);

            string labelId = await page.GetByLabel(new Regex("full", RegexOptions.IgnoreCase)).GetAttributeAsync("id").ConfigureAwait(false);
            string placeholderId = await page.GetByPlaceholder(new Regex("^Your")).GetAttributeAsync("id").ConfigureAwait(false);
            string locatorId = await page.Locator("body").GetByLabel(new Regex("name$")).GetAttributeAsync("id").ConfigureAwait(false);

            Assert.That(labelId, Is.EqualTo("n"));
            Assert.That(placeholderId, Is.EqualTo("n"));
            Assert.That(locatorId, Is.EqualTo("n"));
        }
    }
}
