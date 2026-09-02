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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page.route(..., { times })</c>.
    /// </summary>
    [TestFixture]
    public class RouteTimesTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [SetUp]
        public void SkipFirefox()
        {
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("RouteAsync is Chromium/WebKit until Firefox interception is wired.");
            }
        }

        [PlaywrightTest("page-route.spec.ts", "RouteAsync times:1 is removed after one use")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldApplyTheRouteOnlyOnce()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/route-times.txt", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("from-server");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await page.RouteAsync(
                "**/route-times.txt",
                route => route.FulfillAsync(new() { Body = "mocked", ContentType = "text/plain", Status = 200 }),
                times: 1).ConfigureAwait(false);

            string first = await page.EvaluateAsync<string>("fetch('/route-times.txt').then(r => r.text())").ConfigureAwait(false);
            string second = await page.EvaluateAsync<string>("fetch('/route-times.txt').then(r => r.text())").ConfigureAwait(false);

            Assert.That(first, Is.EqualTo("mocked"));
            Assert.That(second, Is.EqualTo("from-server"));
        }

        [PlaywrightTest("page-route.spec.ts", "RouteAsync without times keeps intercepting")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldKeepInterceptingWhenTimesIsOmitted()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/route-times-unlimited.txt", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("from-server");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await page.RouteAsync(
                "**/route-times-unlimited.txt",
                route => route.FulfillAsync(new() { Body = "mocked", ContentType = "text/plain", Status = 200 })).ConfigureAwait(false);

            string first = await page.EvaluateAsync<string>("fetch('/route-times-unlimited.txt').then(r => r.text())").ConfigureAwait(false);
            string second = await page.EvaluateAsync<string>("fetch('/route-times-unlimited.txt').then(r => r.text())").ConfigureAwait(false);

            Assert.That(first, Is.EqualTo("mocked"));
            Assert.That(second, Is.EqualTo("mocked"));
        }
    }
}
