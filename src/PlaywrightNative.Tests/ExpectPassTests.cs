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
    /// Expect ToPass.
    /// </summary>
    [TestFixture]
    public class ExpectPassTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToPass waits until the callback succeeds")]
        [Test]
        [Timeout(30_000)]
        public async Task ToPassShouldWaitUntilTheCallbackSucceeds()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">hello</div>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#t")).ToPassAsync(
                async () =>
                {
                    string text = await page.Locator("#t").TextContentAsync().ConfigureAwait(false);
                    if (text != "ready")
                    {
                        throw new InvalidOperationException(text);
                    }
                },
                timeout: 5000);
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#t').textContent = 'ready'").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "Not ToPass succeeds when the callback throws")]
        [Test]
        [Timeout(30_000)]
        public async Task NotToPassShouldSucceedWhenTheCallbackThrows()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">hello</div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#t")).Not.ToPassAsync(
                () => throw new InvalidOperationException("still failing"),
                timeout: 2000).ConfigureAwait(false);
        }
    }
}
