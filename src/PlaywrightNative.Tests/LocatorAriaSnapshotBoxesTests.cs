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
    /// Official <c>locator.ariaSnapshot({ boxes })</c>.
    /// </summary>
    [TestFixture]
    public class LocatorAriaSnapshotBoxesTests : PageTestEx
    {
        [PlaywrightTest("page-aria-snapshot.spec.ts", "Boxes appends box markers")]
        [Test]
        [Timeout(30_000)]
        public async Task BoxesShouldAppendBoxMarkers()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='go'>Go</button>").ConfigureAwait(false);

            string yaml = await page.Locator("#go")
                .AriaSnapshotAsync(new() { Boxes = true })
                .ConfigureAwait(false);
            Assert.That(yaml, Does.Contain("button"));
            Assert.That(yaml, Does.Match(new Regex(@"\[box=-?\d+,-?\d+,\d+,\d+\]")));
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "Default omits box markers")]
        [Test]
        [Timeout(30_000)]
        public async Task DefaultShouldOmitBoxMarkers()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='go'>Go</button>").ConfigureAwait(false);

            string yaml = await page.Locator("#go").AriaSnapshotAsync().ConfigureAwait(false);
            Assert.That(yaml, Does.Not.Contain("[box="));
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "Page AriaSnapshot boxes")]
        [Test]
        [Timeout(30_000)]
        public async Task PageAriaSnapshotShouldAppendBoxMarkers()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='go'>Go</button>").ConfigureAwait(false);

            string yaml = await page.AriaSnapshotAsync(new() { Boxes = true }).ConfigureAwait(false);
            Assert.That(yaml, Does.Contain("button"));
            Assert.That(yaml, Does.Match(new Regex(@"\[box=-?\d+,-?\d+,\d+,\d+\]")));
        }
    }
}
