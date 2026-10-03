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
using Microsoft.Playwright;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>strictSelectors</c> on <see cref="BrowserContextOptions"/>.
    /// </summary>
    [TestFixture]
    public class StrictSelectorsTests : PageTestEx
    {
        [PlaywrightTest("page-strict.spec.ts", "should fail page.$ in strict mode")]
        [Test]
        [Timeout(30_000)]
        public async Task QuerySelectorShouldFailInStrictMode()
        {
            // Upstream resolves the strict option the same way for every
            // selector-based call (FrameSelectors._parseSelector): explicit
            // options.strict wins, otherwise it falls back to
            // context.strictSelectors. page.$ is not exempt.
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                StrictSelectors = true,
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><button>one</button><button>two</button></div>").ConfigureAwait(false);

            PlaywrightException ex = Assert.CatchAsync<PlaywrightException>(
                () => page.QuerySelectorAsync("button"));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
