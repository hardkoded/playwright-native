/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IRequest.ServiceWorker()"/>.
    /// </summary>
    [TestFixture]
    public class RequestServiceWorkerTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("page-network-request.spec.ts", "document request has no service worker")]
        [Test]
        [Timeout(30_000)]
        public async Task DocumentRequestShouldHaveNoServiceWorker()
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
            Assert.That(response.Request.ServiceWorker(), Is.Null);
        }

        [PlaywrightTest("page-network-request.spec.ts", "passthrough fetch is issued by the service worker")]
        [Test]
        [Timeout(30_000)]
        public async Task PassthroughFetchShouldBeIssuedByServiceWorker()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("IRequest.ServiceWorker() is Chromium-only.");
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
                    "  if (event.request.url.includes('passthrough')) {" +
                    "    event.respondWith(fetch(event.request));" +
                    "    return;" +
                    "  }" +
                    "  if (event.request.url.endsWith('.html') || event.request.url.endsWith('/sw.js')) return;" +
                    "});");
            });
            Server.SetRoute("/sw.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync(
                    "<script>window.activationPromise = new Promise(r => navigator.serviceWorker.oncontrollerchange = r);" +
                    "navigator.serviceWorker.register('/sw.js');</script>");
            });
            Server.SetRoute("/passthrough", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("from-net");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IWorker> swTask = context.WaitForServiceWorkerAsync();
            await page.GoToAsync(TestConstants.ServerUrl + "/sw.html").ConfigureAwait(false);
            IWorker worker = await swTask.ConfigureAwait(false);
            await page.EvaluateAsync<object>("window.activationPromise").ConfigureAwait(false);

            Task<IRequest> requestTask = context.WaitForEventAsync(
                BrowserContextEvent.Request,
                r => r.ServiceWorker() != null && r.Url != null && r.Url.Contains("/passthrough"));
            string text = await page.EvaluateAsync<string>("fetch('/passthrough').then(r => r.text())").ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);

            Assert.That(text, Is.EqualTo("from-net"));
            Assert.That(request.ServiceWorker(), Is.SameAs(worker));
        }
    }
}
