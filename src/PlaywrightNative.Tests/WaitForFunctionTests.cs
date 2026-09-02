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
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IPage.WaitForFunctionAsync"/> and
    /// <see cref="IPage.WaitForTimeoutAsync"/>. Mirrors a first-match subset of
    /// upstream <c>page-wait-for-function.spec.ts</c>.
    /// </summary>
    [TestFixture]
    public class WaitForFunctionTests : PageTestEx
    {
        [PlaywrightTest("page-wait-for-function.spec.ts", "should work with waitForTimeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithWaitForTimeout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Stopwatch sw = Stopwatch.StartNew();
            await page.WaitForTimeoutAsync(300).ConfigureAwait(false);
            sw.Stop();
            Assert.That(sw.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(200));
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await page.WaitForFunctionAsync("false", options: new() { Timeout = 200 }).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("page.waitForFunction"));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should poll on interval")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldResolveWhenPredicateBecomesTruthy()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IJSHandle> waitTask = page.WaitForFunctionAsync("window.__FOO === 1");
            await page.EvaluateAsync("window.__FOO = 1").ConfigureAwait(false);
            IJSHandle handle = await waitTask.ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should work with raf polling")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithFunctionPredicate()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IJSHandle> waitTask = page.WaitForFunctionAsync("() => window.__FOO === 1");
            await page.EvaluateAsync("window.__FOO = 1").ConfigureAwait(false);
            IJSHandle handle = await waitTask.ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
        }
    }
}
