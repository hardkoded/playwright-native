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
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.Helpers;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-set-extra-http-headers.spec.ts</c> parity for
    /// <see cref="IPage.SetExtraHttpHeadersAsync"/> and
    /// <see cref="IBrowserContext.SetExtraHttpHeadersAsync"/>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// Official .NET extra title <c>should override extra headers from browser context</c>
    /// is not in current upstream and is not ported.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageSetExtraHttpHeadersParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null && await FixtureReachableAsync(TestConstants.ServerUrl).ConfigureAwait(false))
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19745;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    string origin = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    Prefix = origin;
                    EmptyPage = origin + "/empty.html";
                    return;
                }
                catch (Exception)
                {
                }
            }

            Assert.Ignore("Test server is unavailable.");
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }
        }

        private static async Task<bool> FixtureReachableAsync(string prefix)
        {
            try
            {
                using System.Net.Http.HttpClient client = new System.Net.Http.HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(2),
                };
                System.Net.Http.HttpResponseMessage response = await client.GetAsync(prefix + "/empty.html").ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        [PlaywrightTest("page-set-extra-http-headers.spec.ts", "should work")]
        [PlaywrightTest("page-set-extra-http-headers.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            EnsureServer();
            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
            {
                ["foo"] = "bar",
            }).ConfigureAwait(false);

            Task<(string Foo, string Baz)> headerTask = Server.WaitForRequest(
                "/empty.html",
                request => (request.Headers["foo"].ToString(), request.Headers["baz"].ToString()));
            await Task.WhenAll(page.GoToAsync(EmptyPage), headerTask).ConfigureAwait(false);

            Assert.That(headerTask.Result.Foo, Is.EqualTo("bar"));
            Assert.That(headerTask.Result.Baz, Is.Empty);
        }

        [PlaywrightTest("page-set-extra-http-headers.spec.ts", "should work with redirects")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithRedirects()
        {
            EnsureServer();
            Server.Reset();
            Server.SetRedirect("/foo.html", "/empty.html");

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
            {
                ["foo"] = "bar",
            }).ConfigureAwait(false);

            Task<string> headerTask = Server.WaitForRequest(
                "/empty.html",
                request => request.Headers["foo"].ToString());
            await Task.WhenAll(page.GoToAsync(Prefix + "/foo.html"), headerTask).ConfigureAwait(false);

            Assert.That(headerTask.Result, Is.EqualTo("bar"));
        }

        [PlaywrightTest("page-set-extra-http-headers.spec.ts", "should work with extra headers from browser context")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithExtraHeadersFromBrowserContext()
        {
            EnsureServer();
            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.Context.SetExtraHttpHeadersAsync(new Dictionary<string, string>
            {
                ["foo"] = "bar",
            }).ConfigureAwait(false);

            Task<string> headerTask = Server.WaitForRequest(
                "/empty.html",
                request => request.Headers["foo"].ToString());
            await Task.WhenAll(page.GoToAsync(EmptyPage), headerTask).ConfigureAwait(false);

            Assert.That(headerTask.Result, Is.EqualTo("bar"));
        }

        [PlaywrightTest("page-set-extra-http-headers.spec.ts", "should throw for non-string header values")]
        [Test]
        [Timeout(30_000)]
        public void ShouldThrowForNonStringHeaderValues()
        {
            // Public IEnumerable<KeyValuePair<string, string>> cannot express
            // non-strings. Page and context SetExtraHttpHeadersAsync both go
            // through ExtraHttpHeaders; boxed values reproduce the upstream
            // @ts-expect-error cases (number on page, boolean on context).
            PlaywrightNativeException error1 = Assert.Throws<PlaywrightNativeException>(
                () => ExtraHttpHeaders.ToMap(new[] { new KeyValuePair<string, object>("foo", 1) }));
            Assert.That(error1.Message, Does.Contain("Expected value of header \"foo\" to be String, but \"number\" is found."));

            PlaywrightNativeException error2 = Assert.Throws<PlaywrightNativeException>(
                () => ExtraHttpHeaders.ToMap(new[] { new KeyValuePair<string, object>("foo", true) }));
            Assert.That(error2.Message, Does.Contain("Expected value of header \"foo\" to be String, but \"boolean\" is found."));
        }

        [PlaywrightTest("page-set-extra-http-headers.spec.ts", "should not duplicate referer header")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotDuplicateRefererHeader()
        {
            EnsureServer();
            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
            {
                ["referer"] = EmptyPage,
            }).ConfigureAwait(false);

            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);

            Dictionary<string, string> headers = HeaderMap.All(response.Request.Headers);
            Assert.That(headers["referer"], Is.EqualTo(EmptyPage));
        }
    }
}
