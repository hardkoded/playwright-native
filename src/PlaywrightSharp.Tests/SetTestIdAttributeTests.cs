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
    /// Official <c>selectors.setTestIdAttribute</c>.
    /// </summary>
    [TestFixture]
    public class SetTestIdAttributeTests : PageTestEx
    {
        [PlaywrightTest("selectors-get-by.spec.ts", "SetTestIdAttribute changes GetByTestIdAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task SetTestIdAttributeShouldChangeGetByTestId()
        {
            Playwright.SetTestIdAttribute("data-pw");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync("<div data-pw=\"hello\">North</div><div data-testid=\"hello\">South</div>").ConfigureAwait(false);

                IElementHandle handle = await page.GetByTestIdAsync("hello").ConfigureAwait(false);
                Assert.That(handle, Is.Not.Null);
                Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Is.EqualTo("North"));
            }
            finally
            {
                Playwright.SetTestIdAttribute("data-testid");
            }
        }
    }
}
