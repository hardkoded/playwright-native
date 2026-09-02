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
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IPage.WaitForRequestAsync"/>,
    /// <see cref="IPage.WaitForRequestFinishedAsync"/>,
    /// <see cref="IPage.WaitForRequestFailedAsync"/>, and
    /// <see cref="IPage.WaitForResponseAsync"/>. First-match subset of upstream
    /// <c>page-wait-for-request</c> / <c>page-wait-for-response</c>.
    /// </summary>
    [TestFixture]
    public class WaitForRequestTests : PageTestEx
    {
        [PlaywrightTest("page-wait-for-request.spec.ts", "should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForMatchingRequest()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> waitTask = page.WaitForRequestAsync("data:text/html*");
            await page.GoToAsync("data:text/html,wait-for-request-marker").ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);
            Assert.That(request, Is.Not.Null);
            Assert.That(request.Url, Does.StartWith("data:text/html"));
            Assert.That(request.Method, Is.EqualTo("GET"));
        }

        [PlaywrightTest("page-wait-for-request.spec.ts", "RequestsAsync returns recorded requests")]
        [Test]
        [Timeout(30_000)]
        public async Task RequestsAsyncShouldReturnRecordedRequests()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,wave342-requests").ConfigureAwait(false);

            IReadOnlyList<IRequest> requests = await page.RequestsAsync().ConfigureAwait(false);
            Assert.That(requests, Is.Not.Null);
            Assert.That(requests.Select(item => item.Url), Has.Some.StartWith("data:text/html"));
        }

        [PlaywrightTest("page-wait-for-request.spec.ts", "RunAndWaitForRequestAsync waits for GoTo")]
        [Test]
        [Timeout(30_000)]
        public async Task RunAndWaitForRequestAsyncShouldReturnTheRequest()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IRequest request = await page.RunAndWaitForRequestAsync(
                () => page.GoToAsync("data:text/html,run-wait-request")).ConfigureAwait(false);

            Assert.That(request, Is.Not.Null);
            Assert.That(request.Url, Does.StartWith("data:text/html"));
        }

        [PlaywrightTest("page-wait-for-request.spec.ts", "RunAndWaitForRequestFinishedAsync waits for GoTo")]
        [Test]
        [Timeout(30_000)]
        public async Task RunAndWaitForRequestFinishedAsyncShouldReturnTheRequest()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IRequest request = await page.RunAndWaitForRequestFinishedAsync(
                () => page.GoToAsync("data:text/html,run-wait-finished")).ConfigureAwait(false);

            Assert.That(request, Is.Not.Null);
            Assert.That(request.Url, Does.StartWith("data:text/html"));
        }

        [PlaywrightTest("page-wait-for-request.spec.ts", "should work with predicate")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForRequestPredicate()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> waitTask = page.WaitForRequestAsync(r => r.Url.Contains("wait-for-req-pred", StringComparison.Ordinal));
            await page.GoToAsync("data:text/html,wait-for-req-pred").ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);
            Assert.That(request.Url, Does.Contain("wait-for-req-pred"));
        }

        [PlaywrightTest("page-wait-for-response.spec.ts", "should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForMatchingResponse()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IResponse> waitTask = page.WaitForResponseAsync(new Regex("wait-for-resp"));
            await page.GoToAsync("data:text/html,wait-for-resp").ConfigureAwait(false);
            IResponse response = await waitTask.ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Url, Does.Contain("wait-for-resp"));
            Assert.That(response.Ok, Is.True);
        }

        [PlaywrightTest("page-wait-for-response.spec.ts", "RunAndWaitForResponseAsync waits for GoTo")]
        [Test]
        [Timeout(30_000)]
        public async Task RunAndWaitForResponseAsyncShouldReturnTheResponse()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.RunAndWaitForResponseAsync(
                () => page.GoToAsync("data:text/html,run-wait-response")).ConfigureAwait(false);

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Url, Does.Contain("run-wait-response"));
            Assert.That(response.Ok, Is.True);
        }

        [PlaywrightTest("page-wait-for-request.spec.ts", "should timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForRequest()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await page.WaitForRequestAsync("**/never-this-url", timeout: 200).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("page.waitForRequest"));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
        }

        [PlaywrightTest("page-wait-for-response.spec.ts", "should timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForResponse()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await page.WaitForResponseAsync("**/never-this-url", timeout: 200).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("page.waitForResponse"));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
        }

        [PlaywrightTest("page-event-request.spec.ts", "should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForMatchingFinishedRequest()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> waitTask = page.WaitForRequestFinishedAsync("data:text/html*");
            await page.GoToAsync("data:text/html,wait-for-req-finished").ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);
            Assert.That(request, Is.Not.Null);
            Assert.That(request.Url, Does.StartWith("data:text/html"));
            Assert.That(request.Method, Is.EqualTo("GET"));
        }

        [PlaywrightTest("page-event-request.spec.ts", "should work with regex")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForFinishedRequestRegex()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> waitTask = page.WaitForRequestFinishedAsync(new Regex("wait-for-req-fin-re"));
            await page.GoToAsync("data:text/html,wait-for-req-fin-re").ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);
            Assert.That(request.Url, Does.Contain("wait-for-req-fin-re"));
        }

        [PlaywrightTest("page-event-request.spec.ts", "should work with predicate")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForFinishedRequestPredicate()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> waitTask = page.WaitForRequestFinishedAsync(
                r => r.Url.Contains("wait-for-req-fin-pred", StringComparison.Ordinal));
            await page.GoToAsync("data:text/html,wait-for-req-fin-pred").ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);
            Assert.That(request.Url, Does.Contain("wait-for-req-fin-pred"));
        }

        [PlaywrightTest("page-event-request.spec.ts", "should timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForFinishedRequest()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await page.WaitForRequestFinishedAsync("**/never-this-url", timeout: 200).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("page.waitForEvent"));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
        }

        [PlaywrightTest("page-event-request.spec.ts", "should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForMatchingFailedRequest()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> waitTask = page.WaitForRequestFailedAsync("**nonexistent.invalid**");
            await page.GoToAsync(
                "data:text/html,<script>fetch('http://nonexistent.invalid/x').catch(()=>{});</script>")
                .ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);
            Assert.That(request, Is.Not.Null);
            Assert.That(request.Url, Does.Contain("nonexistent.invalid"));
            Assert.That(request.Failure, Is.Not.Null.And.Not.Empty);
        }

        [PlaywrightTest("page-event-request.spec.ts", "RunAndWaitForRequestFailedAsync waits for fetch")]
        [Test]
        [Timeout(30_000)]
        public async Task RunAndWaitForRequestFailedAsyncShouldReturnTheRequest()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IRequest request = await page.RunAndWaitForRequestFailedAsync(
                () => page.GoToAsync(
                    "data:text/html,<script>fetch('http://nonexistent.invalid/run-wait').catch(()=>{});</script>"))
                .ConfigureAwait(false);

            Assert.That(request, Is.Not.Null);
            Assert.That(request.Url, Does.Contain("nonexistent.invalid"));
        }

        [PlaywrightTest("page-event-request.spec.ts", "should work with regex")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForFailedRequestRegex()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> waitTask = page.WaitForRequestFailedAsync(new Regex("nonexistent\\.invalid"));
            await page.GoToAsync(
                "data:text/html,<script>fetch('http://nonexistent.invalid/regex').catch(()=>{});</script>")
                .ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);
            Assert.That(request.Url, Does.Contain("nonexistent.invalid"));
        }

        [PlaywrightTest("page-event-request.spec.ts", "should work with predicate")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForFailedRequestPredicate()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> waitTask = page.WaitForRequestFailedAsync(
                r => r.Url.Contains("nonexistent.invalid", StringComparison.Ordinal));
            await page.GoToAsync(
                "data:text/html,<script>fetch('http://nonexistent.invalid/pred').catch(()=>{});</script>")
                .ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);
            Assert.That(request.Url, Does.Contain("nonexistent.invalid"));
        }

        [PlaywrightTest("page-event-request.spec.ts", "should timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForFailedRequest()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await page.WaitForRequestFailedAsync("**/never-this-url", timeout: 200).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("page.waitForEvent"));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
        }
    }
}
