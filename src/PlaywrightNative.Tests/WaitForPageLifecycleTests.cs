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
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for page lifecycle wait helpers
    /// (<see cref="IPage.WaitForLoadAsync"/> and
    /// <see cref="IPage.WaitForDOMContentLoadedAsync"/>).
    /// </summary>
    [TestFixture]
    public class WaitForPageLifecycleTests : PageTestEx
    {
        [PlaywrightTest("page-event-load.spec.ts", "WaitForLoadAsync resolves on navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForLoadOnNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IPage> waitTask = page.WaitForLoadAsync();
            await page.GoToAsync("data:text/html,<div>wave186</div>").ConfigureAwait(false);
            IPage loaded = await waitTask.ConfigureAwait(false);
            Assert.That(loaded, Is.SameAs(page));
        }

        [PlaywrightTest("page-event-load.spec.ts", "WaitForLoadAsync times out")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForLoad()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await page.WaitForLoadAsync(timeout: 200).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("page.waitForEvent"));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "WaitForDOMContentLoadedAsync resolves on navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForDOMContentLoadedOnNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IPage> waitTask = page.WaitForDOMContentLoadedAsync();
            await page.GoToAsync("data:text/html,<div>wave187</div>").ConfigureAwait(false);
            IPage loaded = await waitTask.ConfigureAwait(false);
            Assert.That(loaded, Is.SameAs(page));
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "WaitForDOMContentLoadedAsync times out")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForDOMContentLoaded()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await page.WaitForDOMContentLoadedAsync(timeout: 200).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("page.waitForEvent"));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
        }

        [PlaywrightTest("page-event-pageerror.spec.ts", "WaitForPageErrorAsync resolves on uncaught exception")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForPageError()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<string> waitTask = page.WaitForPageErrorAsync();
            await page.GoToAsync("data:text/html,<script>throw new Error('wave188-boom');</script>").ConfigureAwait(false);
            string error = await waitTask.ConfigureAwait(false);
            Assert.That(error, Does.Contain("wave188-boom"));
        }

        [PlaywrightTest("page-event-pageerror.spec.ts", "WaitForPageErrorAsync times out")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForPageError()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await page.WaitForPageErrorAsync(timeout: 200).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("page.waitForEvent"));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
        }

        [PlaywrightTest("page-event-pageerror.spec.ts", "PageErrorsAsync returns recorded errors")]
        [Test]
        [Timeout(30_000)]
        public async Task PageErrorsAsyncShouldReturnRecordedErrors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<string> waitTask = page.WaitForPageErrorAsync();
            await page.GoToAsync("data:text/html,<script>throw new Error('wave341-boom');</script>").ConfigureAwait(false);
            await waitTask.ConfigureAwait(false);

            IReadOnlyList<string> errors = await page.PageErrorsAsync().ConfigureAwait(false);
            Assert.That(errors, Is.Not.Null);
            Assert.That(string.Join("\n", errors), Does.Contain("wave341-boom"));
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "WaitForFrameNavigatedAsync resolves on GoTo")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForFrameNavigated()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IFrame> waitTask = page.WaitForFrameNavigatedAsync(
                frame => frame.Url != null && frame.Url.Contains("wave189", StringComparison.Ordinal));
            await page.GoToAsync("data:text/html,wave189").ConfigureAwait(false);
            IFrame frame = await waitTask.ConfigureAwait(false);
            Assert.That(frame, Is.SameAs(page.MainFrame));
            Assert.That(frame.Url, Does.Contain("wave189"));
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "WaitForFrameNavigatedAsync times out")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForFrameNavigated()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await page.WaitForFrameNavigatedAsync(timeout: 200).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("page.waitForEvent"));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "WaitForFrameDetachedAsync resolves when an iframe is removed")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForFrameDetached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<iframe id=\"wave190\"></iframe>").ConfigureAwait(false);
            Assert.That(page.Frames.Count, Is.GreaterThan(1));

            Task<IFrame> waitTask = page.WaitForFrameDetachedAsync();
            await page.EvaluateAsync<object>("document.getElementById('wave190').remove()").ConfigureAwait(false);
            IFrame gone = await waitTask.ConfigureAwait(false);
            Assert.That(gone, Is.Not.Null);
            Assert.That(gone.IsDetached, Is.True);
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "WaitForFrameDetachedAsync times out")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForFrameDetached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await page.WaitForFrameDetachedAsync(timeout: 200).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("page.waitForEvent"));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "WaitForFrameAttachedAsync resolves when an iframe is added")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForFrameAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IFrame> waitTask = page.WaitForFrameAttachedAsync();
            await page.EvaluateAsync<object>(@"
                const iframe = document.createElement('iframe');
                iframe.id = 'wave208';
                iframe.src = 'about:blank';
                document.body.appendChild(iframe);
            ").ConfigureAwait(false);
            IFrame child = await waitTask.ConfigureAwait(false);
            Assert.That(child, Is.Not.Null);
            Assert.That(child.IsDetached, Is.False);
            Assert.That(page.Frames.Count, Is.GreaterThan(1));
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "WaitForFrameAttachedAsync times out")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForFrameAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await page.WaitForFrameAttachedAsync(timeout: 200).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("page.waitForEvent"));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
        }
    }
}
