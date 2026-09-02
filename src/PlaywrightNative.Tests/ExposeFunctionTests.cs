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
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Integration tests: page.ExposeFunctionAsync including
    /// overloads that accept page-side arguments.
    /// </summary>
    [TestFixture]
    public class ExposeFunctionTests : PageTestEx
    {
        [PlaywrightTest("page-expose-function.spec.ts", "ExposeBindingAction")]
        [Test]
        [Timeout(30_000)]
        public async Task ExposeBindingAction()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            bool fired = false;
            Action callback = () => fired = true;

            await page.ExposeBindingAsync("markBound", callback).ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EvaluateAsync<object>("window.markBound()").ConfigureAwait(false);

            Assert.That(fired, Is.True);
        }

        [PlaywrightTest("page-expose-function.spec.ts", "ExposeFunctionAction")]
        [Test]
        [Timeout(30_000)]
        public async Task ExposeFunctionAction()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            bool fired = false;
            Action callback = () => fired = true;

            await page.ExposeFunctionAsync("markFired", callback).ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EvaluateAsync<object>("window.markFired()").ConfigureAwait(false);

            Assert.That(fired, Is.True);
        }

        [PlaywrightTest("page-expose-function.spec.ts", "ExposeFunctionWithReturnValue")]
        [Test]
        [Timeout(30_000)]
        public async Task ExposeFunctionWithReturnValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Func<int> callback = () => 42;

            await page.ExposeFunctionAsync("getFortyTwo", callback).ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            int result = await page.EvaluateAsync<int>("window.getFortyTwo()").ConfigureAwait(false);

            Assert.That(result, Is.EqualTo(42));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "ExposeFunctionWithArgs")]
        [Test]
        [Timeout(30_000)]
        public async Task ExposeFunctionWithArgs()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.ExposeFunctionAsync("add", (int a, int b) => a + b).ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            int result = await page.EvaluateAsync<int>("window.add(2, 3)").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(5));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "ExposeFunctionActionWithArg")]
        [Test]
        [Timeout(30_000)]
        public async Task ExposeFunctionActionWithArg()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            string captured = null;
            await page.ExposeFunctionAsync("capture", (string value) => { captured = value; }).ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EvaluateAsync<object>("window.capture('wave-42')").ConfigureAwait(false);
            Assert.That(captured, Is.EqualTo("wave-42"));
        }
    }
}
