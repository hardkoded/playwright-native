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
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for locator-less <see cref="IFrame"/> getBy* helpers.
    /// </summary>
    [TestFixture]
    public class FrameGetByTests : PageTestEx
    {
        [PlaywrightTest("selectors-get-by.spec.ts", "GetByRole finds a button inside a child frame")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleShouldFindButtonInChildFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);
            await child.SetContentAsync("<button>Inside</button><button>Other</button>").ConfigureAwait(false);

            IElementHandle handle = await child.GetByRoleAsync("button", name: "Inside").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Is.EqualTo("Inside"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "GetByText finds text inside a child frame")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByTextShouldFindInnermostMatchInChildFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);
            await child.SetContentAsync("<div>hello unique frame text</div>").ConfigureAwait(false);

            IElementHandle handle = await child.GetByTextAsync("unique frame text").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Does.Contain("unique frame text"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "GetByLabel finds a control inside a child frame")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByLabelShouldFindControlInChildFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);
            await child.SetContentAsync("<label for=\"pw\">Password</label><input id=\"pw\" value=\"secret\"/>").ConfigureAwait(false);

            IElementHandle handle = await child.GetByLabelAsync("Password").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("pw"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "GetByPlaceholder finds an input inside a child frame")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByPlaceholderShouldFindInputInChildFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);
            await child.SetContentAsync("<input placeholder=\"Email address\" value=\"a@b.c\"/>").ConfigureAwait(false);

            IElementHandle handle = await child.GetByPlaceholderAsync("Email").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.GetAttributeAsync("placeholder").ConfigureAwait(false), Is.EqualTo("Email address"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "GetByAltText finds an image inside a child frame")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByAltTextShouldFindImageInChildFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);
            await child.SetContentAsync("<img alt=\"Playwright logo\" src=\"data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7\">").ConfigureAwait(false);

            IElementHandle handle = await child.GetByAltTextAsync("Playwright logo").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.GetAttributeAsync("alt").ConfigureAwait(false), Is.EqualTo("Playwright logo"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "GetByTitle finds an element inside a child frame")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByTitleShouldFindElementInChildFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);
            await child.SetContentAsync("<span title=\"Issue count\">25 issues</span>").ConfigureAwait(false);

            IElementHandle handle = await child.GetByTitleAsync("Issue count").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Is.EqualTo("25 issues"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "GetByTestId finds an element inside a child frame")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByTestIdShouldFindElementInChildFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);
            await child.SetContentAsync("<div data-testid=\"directions\">North</div>").ConfigureAwait(false);

            IElementHandle handle = await child.GetByTestIdAsync("directions").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Is.EqualTo("North"));
        }

        private static async Task<IFrame> AttachBlankChildFrameAsync(IPage page)
        {
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EvaluateAsync<bool>(@"
                const iframe = document.createElement('iframe');
                iframe.src = 'about:blank';
                document.body.appendChild(iframe);
                true
            ").ConfigureAwait(false);

            IFrame child = null;
            for (int i = 0; i < 50 && child == null; i++)
            {
                foreach (IFrame frame in page.MainFrame.ChildFrames)
                {
                    child = frame;
                    break;
                }

                if (child != null)
                {
                    break;
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            Assert.That(child, Is.Not.Null);

            for (int i = 0; i < 50; i++)
            {
                try
                {
                    if (!await child.EvaluateAsync<bool>("window === window.top").ConfigureAwait(false))
                    {
                        return child;
                    }
                }
                catch (PlaywrightNativeException)
                {
                    // Execution context is not ready yet.
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            throw new TimeoutException("Child frame execution context did not become ready.");
        }
    }
}
