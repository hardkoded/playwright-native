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
    /// Official <c>locator.waitForFunction()</c>.
    /// </summary>
    [TestFixture]
    public class LocatorWaitForFunctionTests : PageTestEx
    {
        [PlaywrightTest("locator-wait-for-function.spec.ts", "Waits until the element predicate is truthy")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForFunctionShouldResolveWhenPredicateBecomesTruthy()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"s\">wait</div>").ConfigureAwait(false);

            Task<IJSHandle> waitTask = ((Locator)page.Locator("#s")).WaitForFunctionAsync("el => el.textContent === 'ready'");
            await page.EvaluateAsync("document.getElementById('s').textContent = 'ready'").ConfigureAwait(false);
            IJSHandle handle = await waitTask.ConfigureAwait(false);

            Assert.That(handle, Is.Not.Null);
        }

        [PlaywrightTest("locator-wait-for-function.spec.ts", "Passes arg as the second parameter")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForFunctionShouldPassArgToPredicate()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"s\">wait</div>").ConfigureAwait(false);

            Task<IJSHandle> waitTask = ((Locator)page.Locator("#s")).WaitForFunctionAsync(
                "(el, expected) => el.textContent === expected",
                "ready");
            await page.EvaluateAsync("document.getElementById('s').textContent = 'ready'").ConfigureAwait(false);
            IJSHandle handle = await waitTask.ConfigureAwait(false);

            Assert.That(handle, Is.Not.Null);
        }

        [PlaywrightTest("locator-wait-for-function.spec.ts", "Re-resolves after the node is replaced")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForFunctionShouldReResolveReplacedNode()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"s\">wait</div>").ConfigureAwait(false);

            Task<IJSHandle> waitTask = ((Locator)page.Locator("#s")).WaitForFunctionAsync("el => el.textContent === 'ready'");
            await page.EvaluateAsync(@"(() => {
                const n = document.createElement('div');
                n.id = 's';
                n.textContent = 'ready';
                document.getElementById('s').replaceWith(n);
            })()").ConfigureAwait(false);
            IJSHandle handle = await waitTask.ConfigureAwait(false);

            Assert.That(handle, Is.Not.Null);
        }

        [PlaywrightTest("locator-wait-for-function.spec.ts", "Times out when the element is missing")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForFunctionShouldTimeoutWhenMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await ((Locator)page.Locator("#missing")).WaitForFunctionAsync("el => true", options: new() { Timeout = 200 }).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("locator.waitForFunction"));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
        }

        [PlaywrightTest("locator-wait-for-function.spec.ts", "Times out when the predicate stays falsy")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForFunctionShouldTimeoutWhenFalsy()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"s\">wait</div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await ((Locator)page.Locator("#s")).WaitForFunctionAsync("el => false", options: new() { Timeout = 200 }).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("locator.waitForFunction"));
        }

        [PlaywrightTest("locator-wait-for-function.spec.ts", "Is strict")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForFunctionShouldThrowWhenTwoMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div class=\"x\"></div><div class=\"x\"></div>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => ((Locator)page.Locator(".x")).WaitForFunctionAsync("el => true", options: new() { Timeout = 2000 }));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
