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
    /// Official <c>library/role-utils.spec.ts</c> titles that use public
    /// locator APIs. Skipped (Node-only <c>__injectedScript</c> / WPT):
    /// name-and-role internals, hidden/aria-hidden cases that call
    /// <c>getNameAndRole</c>, and the injected-script suite.
    /// </summary>
    [TestFixture]
    public class LibraryRoleUtilsParityTests : PageTestEx
    {
        [PlaywrightTest("role-utils.spec.ts", "display:contents should be visible when contents are visible")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DisplayContentsShouldBeVisibleWhenContentsAreVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <button style='display: contents;'>yo</button>
  ").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("button")).ToHaveCountAsync(1).ConfigureAwait(false);
        }
    }
}
