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
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IPage.QuerySelectorAllAsync"/>.
    /// </summary>
    [TestFixture]
    public class QuerySelectorAllTests : PageTestEx
    {
        [PlaywrightTest("queryselector.spec.ts", "QuerySelectorAllAsync returns matching elements")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnMatchingElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>a</div><div>b</div><span>c</span>").ConfigureAwait(false);

            IReadOnlyList<IElementHandle> divs = await page.QuerySelectorAllAsync("div").ConfigureAwait(false);
            Assert.That(divs, Has.Exactly(2).Items);
            Assert.That(await divs[0].EvaluateAsync<string>("n => n.textContent").ConfigureAwait(false), Is.EqualTo("a"));
            Assert.That(await divs[1].EvaluateAsync<string>("n => n.textContent").ConfigureAwait(false), Is.EqualTo("b"));
        }

        [PlaywrightTest("queryselector.spec.ts", "QuerySelectorAllAsync is empty when nothing matches")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnEmptyWhenNothingMatches()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<p>only</p>").ConfigureAwait(false);

            IReadOnlyList<IElementHandle> missing = await page.QuerySelectorAllAsync(".nope").ConfigureAwait(false);
            Assert.That(missing, Is.Empty);
        }

        [PlaywrightTest("queryselector.spec.ts", "frame QuerySelectorAllAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldQueryOnMainFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<li>1</li><li>2</li><li>3</li>").ConfigureAwait(false);

            IReadOnlyList<IElementHandle> items = await page.MainFrame.QuerySelectorAllAsync("li").ConfigureAwait(false);
            Assert.That(items, Has.Exactly(3).Items);
            Assert.That(await items[2].EvaluateAsync<string>("n => n.textContent").ConfigureAwait(false), Is.EqualTo("3"));
        }
    }
}
