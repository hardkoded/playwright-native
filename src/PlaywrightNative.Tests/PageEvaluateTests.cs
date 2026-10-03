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
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for promise-aware evaluation. WebKit's
    /// <c>Runtime.evaluate</c> does not honor <c>awaitPromise</c>, so the execution
    /// context unwraps promises by piping the result through <c>Runtime.callFunctionOn</c>.
    /// Mirrors upstream <c>page-evaluate.spec.ts</c> "should await promise".
    /// </summary>
    [TestFixture]
    public class PageEvaluateTests : PageTestEx
    {
        [PlaywrightTest("page-evaluate.spec.ts", "should await promise")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAwaitPromise()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);

            int result = await page.EvaluateAsync<int>("Promise.resolve(8 * 7)").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(56));
        }
        [PlaywrightTest("jshandle-properties.spec.ts", "should work")]
        [PlaywrightTest("jshandle-properties.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task GetPropertyAsyncShouldReturnNestedObjectHandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            IJSHandle handle = await page.EvaluateHandleAsync("({ nested: { value: 42 } })").ConfigureAwait(false);
            IJSHandle nested = await handle.GetPropertyAsync("nested").ConfigureAwait(false);
            Assert.That(nested, Is.Not.Null);
            int value = await nested.EvaluateAsync<int>("o => o.value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo(42));
            await nested.DisposeAsync().ConfigureAwait(false);
            await handle.DisposeAsync().ConfigureAwait(false);
        }
    }
}
