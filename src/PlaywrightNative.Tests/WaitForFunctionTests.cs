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
