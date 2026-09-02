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
    /// Official <c>screencast.showActions</c>.
    /// </summary>
    [TestFixture]
    public class PageScreencastActionsTests : PageTestEx
    {
        [PlaywrightTest("screencast-actions.spec.ts", "showActions annotates click")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAnnotateClick()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"go\">Go</button>").ConfigureAwait(false);
            await page.Screencast.ShowActionsAsync(new() { Duration = 2000 }).ConfigureAwait(false);

            Task click = page.ClickAsync("#go");
            Assert.That(
                await page.Locator("[data-pw-screencast-action-title]").TextContentAsync().ConfigureAwait(false),
                Is.EqualTo("Click"));
            await click.ConfigureAwait(false);
        }

        [PlaywrightTest("screencast-actions.spec.ts", "disposing showActions stops annotations")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldStopAnnotatingWhenDisposed()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"go\">Go</button>").ConfigureAwait(false);

            await using (IAsyncDisposable actions = await page.Screencast.ShowActionsAsync(new() { Duration = 2000 }).ConfigureAwait(false))
            {
            }

            await page.ClickAsync("#go").ConfigureAwait(false);
            Assert.That(await page.Locator("[data-pw-screencast-action-title]").CountAsync().ConfigureAwait(false), Is.EqualTo(0));
        }
    }
}
