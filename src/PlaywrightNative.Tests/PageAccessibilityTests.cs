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
    /// Direct-connection tests for <see cref="IPage.AriaSnapshotAsync"/>.
    /// </summary>
    [TestFixture]
    public class PageAccessibilityTests : PageTestEx
    {
        [PlaywrightTest("page-basic.spec.ts", "Element AriaSnapshot includes the button")]
        [Test]
        [Timeout(30_000)]
        public async Task ElementAriaSnapshotShouldIncludeTheButton()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='go'>Go</button>").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("#go").ConfigureAwait(false);

            string yaml = await button.AriaSnapshotAsync().ConfigureAwait(false);
            Assert.That(yaml, Does.Contain("button"));
            Assert.That(yaml, Does.Contain("Go"));
        }

        [PlaywrightTest("page-basic.spec.ts", "Frame AriaSnapshot includes the button")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameAriaSnapshotShouldIncludeTheButton()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='go'>Go</button>").ConfigureAwait(false);

            string yaml = await page.MainFrame.AriaSnapshotAsync("#go").ConfigureAwait(false);
            Assert.That(yaml, Does.Contain("button"));
            Assert.That(yaml, Does.Contain("Go"));
        }

        [PlaywrightTest("page-basic.spec.ts", "Page AriaSnapshot includes the button")]
        [Test]
        [Timeout(30_000)]
        public async Task PageAriaSnapshotShouldIncludeTheButton()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='go'>Go</button>").ConfigureAwait(false);

            string yaml = await page.AriaSnapshotAsync("#go").ConfigureAwait(false);
            Assert.That(yaml, Does.Contain("button"));
            Assert.That(yaml, Does.Contain("Go"));
        }
    }
}
