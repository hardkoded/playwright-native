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
    /// HasNot and HasNotText on <see cref="ILocator"/>.
    /// </summary>
    [TestFixture]
    public class LocatorHasNotTests : PageTestEx
    {
        [PlaywrightTest("locator-query.spec.ts", "HasNot drops ancestors of a descendant")]
        [Test]
        [Timeout(30_000)]
        public async Task HasNotShouldDropMatchingAncestors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<div class=\"card\" id=\"a\"><button>Go</button></div>" +
                "<div class=\"card\" id=\"b\"><span>No</span></div>").ConfigureAwait(false);

            ILocator withoutButton = page.Locator(".card").HasNot(page.Locator("button"));
            Assert.That(await withoutButton.CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await withoutButton.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("b"));
        }

        [PlaywrightTest("locator-query.spec.ts", "HasNotText drops matching text")]
        [Test]
        [Timeout(30_000)]
        public async Task HasNotTextShouldDropMatchingText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"a\">Save</button><button id=\"b\">Cancel</button>").ConfigureAwait(false);

            ILocator kept = page.Locator("button").HasNotText("Save");
            Assert.That(await kept.CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await kept.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("b"));
        }
    }
}
