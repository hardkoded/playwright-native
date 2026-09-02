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
    /// Expect ToHaveJSProperty and ToBeInViewport.
    /// </summary>
    [TestFixture]
    public class ExpectJsPropertyTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToHaveJSProperty waits until the property is set")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveJSPropertyShouldWaitUntilThePropertyIsSet()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">x</div>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#t")).ToHaveJSPropertyAsync("foo", 7, new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#t').foo = 7").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveJSProperty matches a string property")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveJSPropertyShouldMatchAStringProperty()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">x</div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#t")).ToHaveJSPropertyAsync("id", "t").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToBeInViewport waits until scrolled into view")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeInViewportShouldWaitUntilScrolledIntoView()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 400).ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"height:2000px\"></div><div id=\"t\">target</div>").ConfigureAwait(false);

            var before = await page.Locator("#t").BoundingBoxAsync().ConfigureAwait(false);
            Assert.That(before, Is.Not.Null);
            Assert.That(before.Y, Is.GreaterThan(400));
            await Assertions.Expect(page.Locator("#t")).Not.ToBeInViewportAsync(new() { Timeout = 2000 }).ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#t")).ToBeInViewportAsync(new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.Locator("#t").ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }
    }
}
