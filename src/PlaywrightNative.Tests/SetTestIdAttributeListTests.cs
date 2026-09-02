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
    /// Official <c>selectors.setTestIdAttribute("a,b")</c> matches any listed attribute.
    /// </summary>
    [TestFixture]
    public class SetTestIdAttributeListTests : PageTestEx
    {
        [PlaywrightTest("selectors-get-by.spec.ts", "Matches either listed attribute")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByTestIdShouldMatchEitherListedAttribute()
        {
            Playwright.SetTestIdAttribute("data-pw,data-ti");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync(
                    "<div id=\"a\" data-pw=\"hello\">North</div>" +
                    "<div id=\"b\" data-ti=\"hello\">East</div>" +
                    "<div id=\"c\" data-testid=\"hello\">South</div>").ConfigureAwait(false);

                ILocator matches = page.GetByTestId("hello");
                Assert.That(await matches.CountAsync().ConfigureAwait(false), Is.EqualTo(2));
                Assert.That(await matches.First.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("a"));
                Assert.That(await matches.Last.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("b"));
            }
            finally
            {
                Playwright.SetTestIdAttribute("data-testid");
            }
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "Regex matches either listed attribute")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByTestIdRegexShouldMatchEitherListedAttribute()
        {
            Playwright.SetTestIdAttribute("data-pw, data-ti");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync(
                    "<div id=\"a\" data-pw=\"hero-1\">North</div>" +
                    "<div id=\"b\" data-ti=\"hero-2\">East</div>").ConfigureAwait(false);

                Assert.That(
                    await page.GetByTestId(new Regex("^hero-")).CountAsync().ConfigureAwait(false),
                    Is.EqualTo(2));
                Assert.That(
                    await page.Locator("body").GetByTestId(new Regex("2$")).GetAttributeAsync("id").ConfigureAwait(false),
                    Is.EqualTo("b"));
            }
            finally
            {
                Playwright.SetTestIdAttribute("data-testid");
            }
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "Frame honors the list")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameGetByTestIdShouldHonorAttributeList()
        {
            Playwright.SetTestIdAttribute("data-pw,data-ti");
            try
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
                await frame.SetContentAsync("<button id=\"f\" data-ti=\"go\">x</button>").ConfigureAwait(false);
                Assert.That(
                    await frame.GetByTestId("go").GetAttributeAsync("id").ConfigureAwait(false),
                    Is.EqualTo("f"));
            }
            finally
            {
                Playwright.SetTestIdAttribute("data-testid");
            }
        }
    }
}
