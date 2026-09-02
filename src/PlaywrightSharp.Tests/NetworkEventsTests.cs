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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Integration tests for page network events: Request, Response,
    /// RequestFinished, RequestFailed.
    /// </summary>
    [TestFixture]
    public class NetworkEventsTests : PageTestEx
    {
        [PlaywrightTest("page-event-network.spec.ts", "Request event should fire on navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task RequestEventShouldFireOnNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<IRequest> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.Request += (_, request) =>
            {
                if (request.Url.StartsWith("data:", StringComparison.Ordinal))
                {
                    tcs.TrySetResult(request);
                }
            };

            await page.GoToAsync("data:text/html,<div>request-event</div>").ConfigureAwait(false);

            using CancellationTokenSource cts = new(5_000);
            cts.Token.Register(() => tcs.TrySetCanceled());
            IRequest received = await tcs.Task.ConfigureAwait(false);

            Assert.That(received, Is.Not.Null);
            Assert.That(received.Url, Does.StartWith("data:text/html,"));
            Assert.That(received.Method, Is.EqualTo("GET"));
            Assert.That(received.IsNavigationRequest, Is.True);
        }

        [PlaywrightTest("page-event-network.spec.ts", "Response event should fire on navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ResponseEventShouldFireOnNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<IResponse> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.Response += (_, response) =>
            {
                if (response.Url.StartsWith("data:", StringComparison.Ordinal))
                {
                    tcs.TrySetResult(response);
                }
            };

            await page.GoToAsync("data:text/html,<div>response-event</div>").ConfigureAwait(false);

            using CancellationTokenSource cts = new(5_000);
            cts.Token.Register(() => tcs.TrySetCanceled());
            IResponse received = await tcs.Task.ConfigureAwait(false);

            Assert.That(received, Is.Not.Null);
            Assert.That(received.Status, Is.EqualTo(200));
            Assert.That(received.Ok, Is.True);
            Assert.That(received.Request, Is.Not.Null);
            Assert.That(received.Request.Url, Is.EqualTo(received.Url));
        }

        [PlaywrightTest("page-event-network.spec.ts", "Request finished should fire after response")]
        [Test]
        [Timeout(30_000)]
        public async Task RequestFinishedShouldFireAfterResponse()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<IRequest> requests = new();
            List<IRequest> finished = new();
            object gate = new();

            page.Request += (_, request) =>
            {
                lock (gate)
                {
                    requests.Add(request);
                }
            };
            page.RequestFinished += (_, request) =>
            {
                lock (gate)
                {
                    finished.Add(request);
                }
            };

            await page.GoToAsync("data:text/html,<div>request-finished</div>").ConfigureAwait(false);

            // Give the RequestFinished event time to arrive after the navigation completes.
            await Task.Delay(500).ConfigureAwait(false);

            lock (gate)
            {
                Assert.That(requests, Has.Count.GreaterThanOrEqualTo(1), "Expected at least one Request event");
                Assert.That(finished, Has.Count.GreaterThanOrEqualTo(1), "Expected at least one RequestFinished event");

                // Identity: the same IRequest instance is passed to both events.
                IRequest navRequest = requests.FirstOrDefault(r => r.Url.StartsWith("data:", StringComparison.Ordinal));
                IRequest navFinished = finished.FirstOrDefault(r => r.Url.StartsWith("data:", StringComparison.Ordinal));
                Assert.That(navRequest, Is.Not.Null);
                Assert.That(navFinished, Is.Not.Null);
                Assert.That(navFinished, Is.SameAs(navRequest), "Request and RequestFinished should receive the same IRequest instance");
            }
        }

        [PlaywrightTest("page-event-network.spec.ts", "Request failed should fire")]
        [Test]
        [Timeout(30_000)]
        public async Task RequestFailedShouldFire()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<IRequest> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.RequestFailed += (_, request) =>
            {
                if (request.Url.Contains("nonexistent.invalid", StringComparison.Ordinal))
                {
                    tcs.TrySetResult(request);
                }
            };

            // Navigate to a data URL that kicks off a subresource fetch to an
            // unresolvable hostname. The main navigation succeeds; the fetch fails
            // and RequestFailed fires for the subresource.
            await page.GoToAsync(
                "data:text/html,<script>fetch('http://nonexistent.invalid/x').catch(()=>{});</script>")
                .ConfigureAwait(false);

            using CancellationTokenSource cts = new(5_000);
            cts.Token.Register(() => tcs.TrySetCanceled());
            IRequest failed = await tcs.Task.ConfigureAwait(false);

            Assert.That(failed, Is.Not.Null);
            Assert.That(failed.Url, Does.Contain("nonexistent.invalid"));
            Assert.That(failed.Failure, Is.Not.Null.And.Not.Empty);
        }

    }
}
