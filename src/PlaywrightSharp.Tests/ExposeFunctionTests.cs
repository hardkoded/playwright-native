/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
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
