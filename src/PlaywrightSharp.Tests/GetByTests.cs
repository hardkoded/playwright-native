/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Direct-connection tests for locator-less <see cref="IPage.GetByRoleAsync"/>,
    /// <see cref="IPage.GetByTextAsync"/>, <see cref="IPage.GetByLabelAsync"/>,
    /// <see cref="IPage.GetByPlaceholderAsync"/>, <see cref="IPage.GetByAltTextAsync"/>,
    /// <see cref="IPage.GetByTitleAsync"/>, and <see cref="IPage.GetByTestIdAsync"/>.
    /// First-match subset of upstream <c>selectors-role</c> / <c>selectors-text</c> /
    /// <c>locator-get-by</c>.
    /// </summary>
    [TestFixture]
    public class GetByTests : PageTestEx
    {
        [PlaywrightTest("selectors-text.spec.ts", "should work")]
        [PlaywrightTest("selectors-text.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByTextShouldReturnInnermostMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>hello unique text</div>").ConfigureAwait(false);
            IElementHandle handle = await page.GetByTextAsync("unique text").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Does.Contain("unique text"));
        }

        [PlaywrightTest("selectors-text.spec.ts", "should work with exact")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByTextExactShouldMatchFullText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>Click me</div><div>Click me please</div>").ConfigureAwait(false);
            IElementHandle handle = await page.GetByTextAsync("Click me", exact: true).ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Is.EqualTo("Click me"));
        }

        [PlaywrightTest("selectors-role.spec.ts", "should work")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleShouldFindButton()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<button>Click me</button>").ConfigureAwait(false);
            IElementHandle handle = await page.GetByRoleAsync("button").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Is.EqualTo("Click me"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByRole should work")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleShouldFilterByAccessibleName()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<button>Save</button><button>Cancel</button>").ConfigureAwait(false);
            IElementHandle handle = await page.GetByRoleAsync("button", name: "Cancel").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Is.EqualTo("Cancel"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByLabel should work")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByLabelShouldFindControlForAttributeAndWrappingLabel()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<label for=\"pw\">Password</label><input id=\"pw\" value=\"secret\"/><label>Username <input id=\"user\" value=\"ada\"/></label>").ConfigureAwait(false);

            IElementHandle password = await page.GetByLabelAsync("Password").ConfigureAwait(false);
            Assert.That(password, Is.Not.Null);
            Assert.That(await password.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("pw"));

            IElementHandle username = await page.GetByLabelAsync("Username").ConfigureAwait(false);
            Assert.That(username, Is.Not.Null);
            Assert.That(await username.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("user"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByLabel should work with aria-label")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByLabelShouldFindAriaLabel()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input aria-label=\"Search query\" value=\"playwright\"/>").ConfigureAwait(false);
            IElementHandle handle = await page.GetByLabelAsync("Search query").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.GetAttributeAsync("aria-label").ConfigureAwait(false), Is.EqualTo("Search query"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByPlaceholder should work")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByPlaceholderShouldFindInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input placeholder=\"Email address\" value=\"a@b.c\"/>").ConfigureAwait(false);
            IElementHandle handle = await page.GetByPlaceholderAsync("Email").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.GetAttributeAsync("placeholder").ConfigureAwait(false), Is.EqualTo("Email address"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByAltText should work")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByAltTextShouldFindImage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<img alt=\"Playwright logo\" src=\"data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7\">").ConfigureAwait(false);
            IElementHandle handle = await page.GetByAltTextAsync("Playwright logo").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.GetAttributeAsync("alt").ConfigureAwait(false), Is.EqualTo("Playwright logo"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByTitle should work")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByTitleShouldFindElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<span title=\"Issue count\">25 issues</span>").ConfigureAwait(false);
            IElementHandle handle = await page.GetByTitleAsync("Issue count").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Is.EqualTo("25 issues"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByTestId should work")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByTestIdShouldFindElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div data-testid=\"directions\">North</div>").ConfigureAwait(false);
            IElementHandle handle = await page.GetByTestIdAsync("directions").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Is.EqualTo("North"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByRole checked should work")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleShouldFilterByCheckedState()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input type=\"checkbox\" id=\"off\"><input type=\"checkbox\" id=\"on\" checked>").ConfigureAwait(false);
            IElementHandle on = await page.GetByRoleAsync("checkbox", checkedState: true).ConfigureAwait(false);
            IElementHandle off = await page.GetByRoleAsync("checkbox", checkedState: false).ConfigureAwait(false);
            Assert.That(await on.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("on"));
            Assert.That(await off.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("off"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByRole disabled should work")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleShouldFilterByDisabledState()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<button id=\"go\">Go</button><button id=\"stop\" disabled>Stop</button>").ConfigureAwait(false);
            IElementHandle stopped = await page.GetByRoleAsync("button", disabled: true).ConfigureAwait(false);
            IElementHandle enabled = await page.GetByRoleAsync("button", disabled: false).ConfigureAwait(false);
            Assert.That(await stopped.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("stop"));
            Assert.That(await enabled.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("go"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByRole expanded should work")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleShouldFilterByExpandedState()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<button id=\"closed\" aria-expanded=\"false\">Closed</button><button id=\"open\" aria-expanded=\"true\">Open</button>").ConfigureAwait(false);
            IElementHandle opened = await page.GetByRoleAsync("button", expanded: true).ConfigureAwait(false);
            IElementHandle closed = await page.GetByRoleAsync("button", expanded: false).ConfigureAwait(false);
            Assert.That(await opened.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("open"));
            Assert.That(await closed.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("closed"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByRole includeHidden should work")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleShouldSkipHiddenWhenRequested()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<button id=\"hid\" style=\"display:none\">Hidden</button><button id=\"vis\">Visible</button>").ConfigureAwait(false);
            IElementHandle hidden = await page.GetByRoleAsync("button", name: "Hidden").ConfigureAwait(false);
            IElementHandle visible = await page.GetByRoleAsync("button", includeHidden: false).ConfigureAwait(false);
            Assert.That(await hidden.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("hid"));
            Assert.That(await visible.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("vis"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByRole level should work")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleShouldFilterByHeadingLevel()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<h1 id=\"one\">One</h1><h2 id=\"two\">Two</h2>").ConfigureAwait(false);
            IElementHandle first = await page.GetByRoleAsync("heading", level: 1).ConfigureAwait(false);
            IElementHandle second = await page.GetByRoleAsync("heading", level: 2).ConfigureAwait(false);
            Assert.That(await first.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("one"));
            Assert.That(await second.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("two"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByRole pressed should work")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleShouldFilterByPressedState()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<button id=\"up\" aria-pressed=\"false\">Up</button><button id=\"down\" aria-pressed=\"true\">Down</button>").ConfigureAwait(false);
            IElementHandle down = await page.GetByRoleAsync("button", pressed: true).ConfigureAwait(false);
            IElementHandle up = await page.GetByRoleAsync("button", pressed: false).ConfigureAwait(false);
            Assert.That(await down.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("down"));
            Assert.That(await up.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("up"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByRole selected should work")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleShouldFilterBySelectedState()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div role=\"tab\" id=\"idle\" aria-selected=\"false\">Idle</div><div role=\"tab\" id=\"cur\" aria-selected=\"true\">Current</div>").ConfigureAwait(false);
            IElementHandle current = await page.GetByRoleAsync("tab", selected: true).ConfigureAwait(false);
            IElementHandle idle = await page.GetByRoleAsync("tab", selected: false).ConfigureAwait(false);
            Assert.That(await current.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("cur"));
            Assert.That(await idle.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("idle"));
        }
    }
}
