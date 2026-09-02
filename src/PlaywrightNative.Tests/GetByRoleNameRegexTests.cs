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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>getByRole({ name })</c> regular-expression form.
    /// </summary>
    [TestFixture]
    public class GetByRoleNameRegexTests : PageTestEx
    {
        [PlaywrightTest("selectors-get-by.spec.ts", "Matches accessible name")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleShouldMatchNameRegex()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<button id=\"a\" aria-label=\"Save file\">x</button>" +
                "<button id=\"b\" aria-label=\"Close dialog\">y</button>").ConfigureAwait(false);

            ILocator save = page.GetByRole("button", nameRegex: new Regex("^Save"));
            Assert.That(await save.CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await save.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("a"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "IgnoreCase matches")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleNameRegexShouldHonorIgnoreCase()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"a\" aria-label=\"Save file\">x</button>").ConfigureAwait(false);

            Assert.That(
                await page.GetByRole("button", nameRegex: new Regex("SAVE FILE", RegexOptions.IgnoreCase)).CountAsync().ConfigureAwait(false),
                Is.EqualTo(1));
            Assert.That(
                await page.GetByRole("button", nameRegex: new Regex("SAVE FILE")).CountAsync().ConfigureAwait(false),
                Is.EqualTo(0));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "locator and frame honor nameRegex")]
        [Test]
        [Timeout(30_000)]
        public async Task LocatorAndFrameShouldHonorNameRegex()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<div id='host'><button id=\"a\" aria-label=\"Save file\">x</button></div>" +
                "<iframe></iframe>").ConfigureAwait(false);

            ILocator inner = page.Locator("#host").GetByRole("button", nameRegex: new Regex("^Save"));
            Assert.That(await inner.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("a"));

            IFrame frame = null;
            foreach (IFrame child in page.MainFrame.ChildFrames)
            {
                frame = child;
                break;
            }

            Assert.That(frame, Is.Not.Null);
            await frame.SetContentAsync("<button id=\"f\" aria-label=\"Save file\">x</button>").ConfigureAwait(false);
            Assert.That(
                await frame.GetByRole("button", nameRegex: new Regex("^Save")).GetAttributeAsync("id").ConfigureAwait(false),
                Is.EqualTo("f"));
        }
    }
}
