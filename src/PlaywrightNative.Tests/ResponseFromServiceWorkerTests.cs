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
    /// Direct-connection tests for <see cref="IResponse.FromServiceWorker"/>.
    /// </summary>
    [TestFixture]
    public class ResponseFromServiceWorkerTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("page-network-request.spec.ts", "document response is not from a service worker")]
        [Test]
        [Timeout(30_000)]
        public async Task DocumentResponseShouldNotBeFromServiceWorker()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            Assert.That(response, Is.Not.Null);
            Assert.That(response.FromServiceWorker, Is.False);
        }

        [PlaywrightTest("page-network-request.spec.ts", "fetch handled by a service worker")]
        [Test]
        [Timeout(30_000)]
        public async Task FetchResponseShouldBeFromServiceWorker()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("FromServiceWorker is reported on Chromium.");
                return;
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/sw.js", http =>
            {
                http.Response.ContentType = "application/javascript";
                return http.Response.WriteAsync(
                    "self.addEventListener('install', () => self.skipWaiting());" +
                    "self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));" +
                    "self.addEventListener('fetch', event => {" +
                    "  if (event.request.url.endsWith('.html') || event.request.url.endsWith('/sw.js')) return;" +
                    "  event.respondWith(new Response('from-sw', { status: 200, statusText: 'OK' }));" +
                    "});");
            });
            Server.SetRoute("/sw.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync(
                    "<script>window.activationPromise = new Promise(r => navigator.serviceWorker.oncontrollerchange = r);" +
                    "navigator.serviceWorker.register('/sw.js');</script>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IWorker> swTask = context.WaitForServiceWorkerAsync();
            await page.GoToAsync(TestConstants.ServerUrl + "/sw.html").ConfigureAwait(false);
            await swTask.ConfigureAwait(false);
            await page.EvaluateAsync<object>("window.activationPromise").ConfigureAwait(false);

            Task<IResponse> responseTask = page.WaitForResponseAsync(r => r.Url != null && r.Url.Contains("/foo.txt"));
            string text = await page.EvaluateAsync<string>("fetch('/foo.txt').then(r => r.text())").ConfigureAwait(false);
            IResponse response = await responseTask.ConfigureAwait(false);

            Assert.That(response.FromServiceWorker, Is.True);
            Assert.That(text, Is.EqualTo("from-sw"));
        }
    }
}
