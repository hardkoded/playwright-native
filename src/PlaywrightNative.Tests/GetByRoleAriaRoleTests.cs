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
    /// Official <c>getByRole(AriaRole)</c> on page, frame, locator, and frame locator.
    /// </summary>
    [TestFixture]
    public class GetByRoleAriaRoleTests : PageTestEx
    {
        [PlaywrightTest("selectors-get-by.spec.ts", "Page matches AriaRole.Button")]
        [Test]
        [Timeout(30_000)]
        public async Task PageGetByRoleShouldMatchAriaRole()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<button id=\"a\">Save</button>" +
                "<a id=\"b\" href=\"#\">Link</a>").ConfigureAwait(false);

            ILocator save = page.GetByRole(AriaRole.Button);
            Assert.That(await save.CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await save.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("a"));

            IElementHandle handle = await page.GetByRoleAsync(AriaRole.Button).ConfigureAwait(false);
            Assert.That(await handle.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("a"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "Locator is scoped")]
        [Test]
        [Timeout(30_000)]
        public async Task LocatorGetByRoleShouldStayInside()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<section id=\"one\"><button>A</button></section>" +
                "<section id=\"two\"><button>B</button></section>").ConfigureAwait(false);

            ILocator inner = page.Locator("#two").GetByRole(AriaRole.Button);
            Assert.That(await inner.CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That((await inner.TextContentAsync().ConfigureAwait(false)).Trim(), Is.EqualTo("B"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "Frame and frame locator honor AriaRole")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameAndFrameLocatorShouldHonorAriaRole()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<iframe></iframe>").ConfigureAwait(false);

            IFrame frame = null;
            foreach (IFrame child in page.MainFrame.ChildFrames)
            {
                frame = child;
                break;
            }

            Assert.That(frame, Is.Not.Null);
            await frame.SetContentAsync("<button id=\"f\">Save</button>").ConfigureAwait(false);

            Assert.That(
                await frame.GetByRole(AriaRole.Button).GetAttributeAsync("id").ConfigureAwait(false),
                Is.EqualTo("f"));

            IElementHandle handle = await frame.GetByRoleAsync(AriaRole.Button).ConfigureAwait(false);
            Assert.That(await handle.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("f"));

            Assert.That(
                await page.FrameLocator("iframe").GetByRole(AriaRole.Button).GetAttributeAsync("id").ConfigureAwait(false),
                Is.EqualTo("f"));
        }
    }
}
