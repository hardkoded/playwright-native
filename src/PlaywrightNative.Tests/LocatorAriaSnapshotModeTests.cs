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
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>locator.ariaSnapshot({ mode })</c>.
    /// </summary>
    [TestFixture]
    public class LocatorAriaSnapshotModeTests : PageTestEx
    {
        [PlaywrightTest("page-aria-snapshot.spec.ts", "AI mode adds ref markers")]
        [Test]
        [Timeout(30_000)]
        public async Task AiModeShouldAddRefMarkers()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='go'>Go</button>").ConfigureAwait(false);

            string yaml = await page.Locator("#go")
                .AriaSnapshotAsync(new() { Mode = AriaSnapshotMode.Ai })
                .ConfigureAwait(false);
            Assert.That(yaml, Does.Contain("button"));
            Assert.That(yaml, Does.Contain("Go"));
            Assert.That(yaml, Does.Contain("[ref=e"));
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "Default mode omits ref markers")]
        [Test]
        [Timeout(30_000)]
        public async Task DefaultModeShouldOmitRefMarkers()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='go'>Go</button>").ConfigureAwait(false);

            string yaml = await page.Locator("#go")
                .AriaSnapshotAsync(new() { Mode = AriaSnapshotMode.Default })
                .ConfigureAwait(false);
            Assert.That(yaml, Does.Contain("button"));
            Assert.That(yaml, Does.Not.Contain("[ref="));
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "AI mode throws when nothing matches")]
        [Test]
        [Timeout(30_000)]
        public async Task AiModeShouldThrowWhenNothingMatches()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>empty</div>").ConfigureAwait(false);

            Stopwatch clock = Stopwatch.StartNew();
            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.Locator("#missing").AriaSnapshotAsync(new() { Mode = AriaSnapshotMode.Ai }));
            clock.Stop();

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("does not match any element"));
            Assert.That(clock.ElapsedMilliseconds, Is.LessThan(5_000));
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "AI mode includes iframe contents")]
        [Test]
        [Timeout(30_000)]
        public async Task AiModeShouldIncludeIframeContents()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                    "<h1>Hello</h1><iframe srcdoc='<h1>World</h1>'></iframe>")
                .ConfigureAwait(false);
            await page.Locator("iframe").ContentFrame.Locator("h1").WaitForAsync().ConfigureAwait(false);

            string yaml = await page.Locator("body")
                .AriaSnapshotAsync(new() { Mode = AriaSnapshotMode.Ai })
                .ConfigureAwait(false);
            Assert.That(yaml, Does.Contain("iframe"));
            Assert.That(yaml, Does.Contain("World"));
            Assert.That(yaml, Does.Contain("[ref=e"));
        }
    }
}
