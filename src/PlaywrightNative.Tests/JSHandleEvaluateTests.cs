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
    /// Official <c>jshandle-evaluate.spec.ts</c>.
    /// </summary>
    [TestFixture]
    public class JSHandleEvaluateTests : PageTestEx
    {
        [PlaywrightTest("jshandle-evaluate.spec.ts", "should work with function")]
        [PlaywrightTest("jshandle-evaluate.spec.ts", "should work with function @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithFunction()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle windowHandle = await page.EvaluateHandleAsync("() => { window.foo = [1, 2]; return window; }").ConfigureAwait(false);
            int[] result = await windowHandle.EvaluateAsync<int[]>("w => w.foo").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(new[] { 1, 2 }));
        }

        [PlaywrightTest("jshandle-evaluate.spec.ts", "should work with expression")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithExpression()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle windowHandle = await page.EvaluateHandleAsync("() => { window.foo = [1, 2]; return window; }").ConfigureAwait(false);
            int[] result = await windowHandle.EvaluateAsync<int[]>("window.foo").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(new[] { 1, 2 }));
        }
    }
}
