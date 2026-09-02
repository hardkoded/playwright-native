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
    /// NewContext javaScriptEnabled and ignoreHTTPSErrors applied to pages.
    /// </summary>
    [TestFixture]
    public class ContextScriptHttpsTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-csp.spec.ts", "javaScriptEnabled false skips page scripts")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextJavaScriptDisabledShouldSkipPageScripts()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { JavaScriptEnabled = false }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<script>window.__wave58 = 58;</script><div id=\"d\">ok</div>").ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<string>("typeof window.__wave58").ConfigureAwait(false), Is.EqualTo("undefined"));
            Assert.That(await page.EvaluateAsync<string>("document.getElementById('d').textContent").ConfigureAwait(false), Is.EqualTo("ok"));
        }

        [PlaywrightTest("browsercontext-csp.spec.ts", "options bag javaScriptEnabled")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextOptionsBagShouldDisableJavaScript()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                JavaScriptEnabled = false,
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<script>window.__wave58 = 58;</script>").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("typeof window.__wave58").ConfigureAwait(false), Is.EqualTo("undefined"));
        }

        [PlaywrightTest("browsercontext-csp.spec.ts", "ignoreHTTPSErrors allows self-signed")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextIgnoreHttpsErrorsShouldAllowSelfSigned()
        {
            if (TestServerSetup.HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync($"{TestConstants.HttpsPrefix}/empty.html").ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Ok, Is.True);
        }
    }
}
