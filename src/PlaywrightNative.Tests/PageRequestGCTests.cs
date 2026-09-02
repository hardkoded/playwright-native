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
    /// Direct-connection tests for <see cref="IPage.RequestGCAsync"/>.
    /// </summary>
    [TestFixture]
    public class PageRequestGCTests : PageTestEx
    {
        [PlaywrightTest("page-request-gc.spec.ts", "reachable WeakRef survives requestGC")]
        [Test]
        [Timeout(30_000)]
        public async Task ReachableObjectSurvivesRequestGC()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EvaluateAsync(
                "globalThis.objectToDestroy = { hello: 'world' }; globalThis.weakRef = new WeakRef(globalThis.objectToDestroy);").ConfigureAwait(false);

            await page.RequestGCAsync().ConfigureAwait(false);

            bool alive = await page.EvaluateAsync<bool>(
                "globalThis.weakRef.deref() !== undefined").ConfigureAwait(false);
            Assert.That(alive, Is.True);
        }

        [PlaywrightTest("page-request-gc.spec.ts", "unreachable WeakRef is collected")]
        [Test]
        [Timeout(30_000)]
        public async Task UnreachableObjectIsCollected()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EvaluateAsync(
                "globalThis.objectToDestroy = { hello: 'world' }; globalThis.weakRef = new WeakRef(globalThis.objectToDestroy);").ConfigureAwait(false);
            await page.EvaluateAsync("globalThis.objectToDestroy = null").ConfigureAwait(false);
            await page.RequestGCAsync().ConfigureAwait(false);

            bool collected = await page.EvaluateAsync<bool>(
                "globalThis.weakRef.deref() === undefined").ConfigureAwait(false);
            Assert.That(collected, Is.True);
        }
    }
}
