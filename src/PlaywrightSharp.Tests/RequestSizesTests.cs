/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IRequest.GetSizesAsync"/>.
    /// </summary>
    [TestFixture]
    public class RequestSizesTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("page-network-request.spec.ts", "GetSizesAsync after navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportSizesAfterNavigation()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> waitTask = page.WaitForEventAsync(
                PageEvent.RequestFinished,
                request => request.Url.Contains("/empty.html", StringComparison.Ordinal));
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);

            RequestSizesResult sizes = await request.GetSizesAsync().ConfigureAwait(false);
            Assert.That(sizes, Is.Not.Null);
            Assert.That(sizes.RequestBodySize, Is.EqualTo(0));
            Assert.That(sizes.RequestHeadersSize, Is.GreaterThan(0));
            Assert.That(sizes.ResponseHeadersSize, Is.GreaterThan(0));
            Assert.That(sizes.ResponseBodySize, Is.GreaterThanOrEqualTo(0));
        }

        [PlaywrightTest("page-network-request.spec.ts", "GetSizesAsync reports POST body")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportPostBodySize()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/direct-sizes-post", httpContext =>
            {
                httpContext.Response.ContentType = "text/plain";
                return httpContext.Response.WriteAsync("ok");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            Task<IRequest> waitTask = page.WaitForEventAsync(
                PageEvent.RequestFinished,
                request => request.Url.Contains("/direct-sizes-post", StringComparison.Ordinal)
                    && string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase));
            await page.EvaluateAsync(
                "fetch('/direct-sizes-post', { method: 'POST', body: 'wave90' })").ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);

            RequestSizesResult sizes = await request.GetSizesAsync().ConfigureAwait(false);
            Assert.That(sizes.RequestBodySize, Is.EqualTo(6));
            Assert.That(sizes.RequestHeadersSize, Is.GreaterThan(0));
        }

        [PlaywrightTest("page-network-request.spec.ts", "SizesAsync aliases GetSizesAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task SizesAsyncShouldAliasGetSizesAsync()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> waitTask = page.WaitForEventAsync(
                PageEvent.RequestFinished,
                request => request.Url.Contains("/empty.html", StringComparison.Ordinal));
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);

            RequestSizesResult viaAlias = await request.SizesAsync().ConfigureAwait(false);
            RequestSizesResult viaGet = await request.GetSizesAsync().ConfigureAwait(false);
            Assert.That(viaAlias, Is.Not.Null);
            Assert.That(viaAlias.RequestBodySize, Is.EqualTo(viaGet.RequestBodySize));
            Assert.That(viaAlias.RequestHeadersSize, Is.EqualTo(viaGet.RequestHeadersSize));
            Assert.That(viaAlias.ResponseHeadersSize, Is.EqualTo(viaGet.ResponseHeadersSize));
            Assert.That(viaAlias.ResponseBodySize, Is.EqualTo(viaGet.ResponseBodySize));
        }

        [PlaywrightTest("page-network-request.spec.ts", "ResponseAsync aliases GetResponseAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task ResponseAsyncShouldAliasGetResponseAsync()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse navigation = await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IRequest request = navigation.Request;

            IResponse viaGet = await request.GetResponseAsync().ConfigureAwait(false);
            IResponse viaAlias = await request.ResponseAsync().ConfigureAwait(false);
            Assert.That(viaGet, Is.Not.Null);
            Assert.That(viaAlias, Is.Not.Null);
            Assert.That(viaAlias.Url, Is.EqualTo(viaGet.Url));
            Assert.That(viaAlias.Status, Is.EqualTo(viaGet.Status));
        }

        [PlaywrightTest("page-network-request.spec.ts", "ExistingResponse returns the received response")]
        [Test]
        [Timeout(30_000)]
        public async Task ExistingResponseShouldReturnReceivedResponse()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse navigation = await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IRequest request = navigation.Request;
            IResponse existing = request.ExistingResponse;

            Assert.That(existing, Is.Not.Null);
            Assert.That(existing.Url, Is.EqualTo(navigation.Url));
            Assert.That(existing.Status, Is.EqualTo(navigation.Status));
        }
    }
}
