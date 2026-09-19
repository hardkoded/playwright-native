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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for context-level network events.
    /// </summary>
    [TestFixture]
    public class ContextNetworkEventTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-network-event.spec.ts", "WaitForEvent Request on navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireRequestOnNavigation()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> waitTask = context.WaitForEventAsync(
                BrowserContextEvent.Request,
                request => request.Url.Contains("/empty.html", StringComparison.Ordinal));
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);

            Assert.That(request, Is.Not.Null);
            Assert.That(request.Url, Does.Contain("empty.html"));
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "WaitForRequestAsync matches a glob")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForMatchingRequest()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> waitTask = context.WaitForRequestAsync(TestConstants.EmptyPage);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);
            Assert.That(request, Is.Not.Null);
            Assert.That(request.Url, Is.EqualTo(TestConstants.EmptyPage));
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "RunAndWaitForRequestAsync returns the request")]
        [Test]
        [Timeout(30_000)]
        public async Task RunAndWaitForRequestAsyncShouldReturnTheRequest()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IRequest request = await context.RunAndWaitForRequestAsync(
                () => page.GoToAsync(TestConstants.EmptyPage))
                .ConfigureAwait(false);

            Assert.That(request, Is.Not.Null);
            Assert.That(request.Url, Is.EqualTo(TestConstants.EmptyPage));
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "WaitForRequestAsync times out")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForRequest()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await context.WaitForRequestAsync("**/never-this-url", timeout: 200).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "WaitForEvent Response on navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireResponseOnNavigation()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IResponse> waitTask = context.WaitForEventAsync(
                BrowserContextEvent.Response,
                response => response.Url.Contains("/empty.html", StringComparison.Ordinal));
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IResponse response = await waitTask.ConfigureAwait(false);

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Url, Does.Contain("empty.html"));
            Assert.That(response.Status, Is.EqualTo(200));
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "WaitForResponseAsync matches a glob")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForMatchingResponse()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IResponse> waitTask = context.WaitForResponseAsync(TestConstants.EmptyPage);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IResponse response = await waitTask.ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Url, Is.EqualTo(TestConstants.EmptyPage));
            Assert.That(response.Ok, Is.True);
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "RunAndWaitForResponseAsync returns the response")]
        [Test]
        [Timeout(30_000)]
        public async Task RunAndWaitForResponseAsyncShouldReturnTheResponse()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await context.RunAndWaitForResponseAsync(
                () => page.GoToAsync(TestConstants.EmptyPage))
                .ConfigureAwait(false);

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Url, Is.EqualTo(TestConstants.EmptyPage));
            Assert.That(response.Ok, Is.True);
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "WaitForResponseAsync times out")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForResponse()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await context.WaitForResponseAsync("**/never-this-url", timeout: 200).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "WaitForEvent RequestFinished on navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireRequestFinishedOnNavigation()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> waitTask = context.WaitForEventAsync(
                BrowserContextEvent.RequestFinished,
                request => request.Url.Contains("/empty.html", StringComparison.Ordinal));
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);

            Assert.That(request, Is.Not.Null);
            Assert.That(request.Url, Does.Contain("empty.html"));
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "WaitForRequestFinishedAsync matches a glob")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForMatchingFinishedRequest()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> waitTask = context.WaitForRequestFinishedAsync(TestConstants.EmptyPage);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);
            Assert.That(request, Is.Not.Null);
            Assert.That(request.Url, Is.EqualTo(TestConstants.EmptyPage));
            Assert.That(request.Method, Is.EqualTo("GET"));
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "RunAndWaitForRequestFinishedAsync returns the request")]
        [Test]
        [Timeout(30_000)]
        public async Task RunAndWaitForRequestFinishedAsyncShouldReturnTheRequest()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IRequest request = await context.RunAndWaitForRequestFinishedAsync(
                () => page.GoToAsync(TestConstants.EmptyPage))
                .ConfigureAwait(false);

            Assert.That(request, Is.Not.Null);
            Assert.That(request.Url, Is.EqualTo(TestConstants.EmptyPage));
            Assert.That(request.Method, Is.EqualTo("GET"));
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "WaitForRequestFinishedAsync matches a regex")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForFinishedRequestRegex()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> waitTask = context.WaitForRequestFinishedAsync(new Regex(@"empty\.html"));
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);
            Assert.That(request.Url, Does.Contain("empty.html"));
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "WaitForRequestFinishedAsync matches a predicate")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForFinishedRequestPredicate()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> waitTask = context.WaitForRequestFinishedAsync(
                r => r.Url.Contains("empty.html", StringComparison.Ordinal));
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);
            Assert.That(request.Url, Does.Contain("empty.html"));
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "WaitForRequestFinishedAsync times out")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForFinishedRequest()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await context.WaitForRequestFinishedAsync("**/never-this-url", timeout: 200).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("browserContext.waitForEvent"));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "WaitForRequestFailedAsync matches a glob")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForMatchingFailedRequest()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> waitTask = context.WaitForRequestFailedAsync("**nonexistent.invalid**");
            await page.GoToAsync(
                "data:text/html,<script>fetch('http://nonexistent.invalid/wave206').catch(()=>{});</script>")
                .ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);
            Assert.That(request, Is.Not.Null);
            Assert.That(request.Url, Does.Contain("nonexistent.invalid"));
            Assert.That(request.Failure, Is.Not.Null.And.Not.Empty);
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "RunAndWaitForRequestFailedAsync returns the request")]
        [Test]
        [Timeout(30_000)]
        public async Task RunAndWaitForRequestFailedAsyncShouldReturnTheRequest()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IRequest request = await context.RunAndWaitForRequestFailedAsync(
                () => page.GoToAsync(
                    "data:text/html,<script>fetch('http://nonexistent.invalid/wave313').catch(()=>{});</script>"))
                .ConfigureAwait(false);

            Assert.That(request, Is.Not.Null);
            Assert.That(request.Url, Does.Contain("nonexistent.invalid"));
            Assert.That(request.Failure, Is.Not.Null.And.Not.Empty);
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "WaitForRequestFailedAsync matches a regex")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForFailedRequestRegex()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> waitTask = context.WaitForRequestFailedAsync(new Regex("nonexistent\\.invalid"));
            await page.GoToAsync(
                "data:text/html,<script>fetch('http://nonexistent.invalid/wave206-re').catch(()=>{});</script>")
                .ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);
            Assert.That(request.Url, Does.Contain("nonexistent.invalid"));
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "WaitForRequestFailedAsync matches a predicate")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForFailedRequestPredicate()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> waitTask = context.WaitForRequestFailedAsync(
                r => r.Url.Contains("nonexistent.invalid", StringComparison.Ordinal));
            await page.GoToAsync(
                "data:text/html,<script>fetch('http://nonexistent.invalid/wave206-pred').catch(()=>{});</script>")
                .ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);
            Assert.That(request.Url, Does.Contain("nonexistent.invalid"));
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "WaitForRequestFailedAsync times out")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForFailedRequest()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await context.WaitForRequestFailedAsync("**/never-this-url", timeout: 200).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("browserContext.waitForEvent"));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
        }
    }
}
