/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
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
    }
}
