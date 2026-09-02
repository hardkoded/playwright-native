/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for request/response AllHeaders and HeaderValue.
    /// </summary>
    [TestFixture]
    public class AllHeadersTests : PageTestEx
    {
        [PlaywrightTest("page-network-request.spec.ts", "request AllHeadersAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task RequestAllHeadersShouldIncludeHost()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IRequest request = response.Request;

            Dictionary<string, string> headers = await request.AllHeadersAsync().ConfigureAwait(false);
            Assert.That(headers, Is.Not.Null);
            Assert.That(headers, Has.Count.GreaterThan(0));
            bool found = headers.ContainsKey("user-agent")
                || headers.ContainsKey("accept")
                || headers.ContainsKey("host")
                || headers.ContainsKey("accept-language");
            Assert.That(found, Is.True);
        }

        [PlaywrightTest("page-network-request.spec.ts", "request HeaderValuesAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task RequestHeaderValuesShouldReturnMatchingValues()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IRequest request = response.Request;

            IReadOnlyList<string> values = await request.HeaderValuesAsync("user-agent").ConfigureAwait(false);
            if (values.Count == 0)
            {
                values = await request.HeaderValuesAsync("accept").ConfigureAwait(false);
            }

            Assert.That(values, Is.Not.Null);
            Assert.That(values, Has.Count.GreaterThan(0));
            Assert.That(values[0], Is.Not.Empty);
            IReadOnlyList<string> missing = await request.HeaderValuesAsync("x-missing-wave223").ConfigureAwait(false);
            Assert.That(missing, Is.Empty);
        }

        [PlaywrightTest("page-network-request.spec.ts", "response HeaderValueAsync content-type")]
        [Test]
        [Timeout(30_000)]
        public async Task ResponseHeaderValueShouldReturnContentType()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            string contentType = await response.HeaderValueAsync("content-type").ConfigureAwait(false);
            Assert.That(contentType, Does.Contain("text/html"));
            Assert.That(await response.HeaderValueAsync("x-missing-wave104").ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("page-network-request.spec.ts", "response HeaderValuesAsync content-type")]
        [Test]
        [Timeout(30_000)]
        public async Task ResponseHeaderValuesShouldReturnContentType()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            IReadOnlyList<string> values = await response.HeaderValuesAsync("content-type").ConfigureAwait(false);
            Assert.That(values, Is.Not.Null);
            Assert.That(values, Has.Count.GreaterThan(0));
            Assert.That(values[0], Does.Contain("text/html"));
            Assert.That(await response.HeaderValuesAsync("x-missing-wave224").ConfigureAwait(false), Is.Empty);
        }

        [PlaywrightTest("page-network-request.spec.ts", "response HeadersArrayAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task ResponseHeadersArrayShouldContainEntries()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            IReadOnlyList<Header> headers = await response.HeadersArrayAsync().ConfigureAwait(false);
            Assert.That(headers, Is.Not.Null);
            Assert.That(headers, Has.Count.GreaterThan(0));
            bool found = false;
            foreach (Header entry in headers)
            {
                if (string.Equals(entry.Name, "content-type", StringComparison.OrdinalIgnoreCase)
                    && entry.Value != null
                    && entry.Value.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }

            Assert.That(found, Is.True);
        }
    }
}
