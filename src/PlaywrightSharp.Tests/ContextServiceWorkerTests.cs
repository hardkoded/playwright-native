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
    /// Direct-connection tests for <see cref="IBrowserContext.ServiceWorkers()"/>.
    /// </summary>
    [TestFixture]
    public class ContextServiceWorkerTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("browsercontext-service-worker-policy.spec.ts", "ServiceWorkers is empty before registration")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldStartWithNoServiceWorkers()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            Assert.That(context.ServiceWorkers(), Is.Empty);
        }

        [PlaywrightTest("browsercontext-service-worker-policy.spec.ts", "WaitForServiceWorker and evaluate")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportServiceWorkerAndEvaluate()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Service worker inspection is Chromium-only.");
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
                return http.Response.WriteAsync("self.addEventListener('install', () => self.skipWaiting());");
            });
            Server.SetRoute("/sw.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<script>navigator.serviceWorker.register('/sw.js');</script>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IWorker> waitTask = context.WaitForServiceWorkerAsync();
            await page.GoToAsync(TestConstants.ServerUrl + "/sw.html").ConfigureAwait(false);
            IWorker worker = await waitTask.ConfigureAwait(false);

            Assert.That(worker, Is.Not.Null);
            Assert.That(worker.Url, Does.Contain("/sw.js"));
            Assert.That(context.ServiceWorkers(), Has.Exactly(1).Items);
            Assert.That(context.ServiceWorkers(), Does.Contain(worker));

            int sum = await worker.EvaluateAsync<int>("1 + 1").ConfigureAwait(false);
            Assert.That(sum, Is.EqualTo(2));
        }

        [PlaywrightTest("browsercontext-service-worker-policy.spec.ts", "RunAndWaitForServiceWorkerAsync returns the worker")]
        [Test]
        [Timeout(30_000)]
        public async Task RunAndWaitForServiceWorkerAsyncShouldReturnTheWorker()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Service worker inspection is Chromium-only.");
                return;
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/sw-run.js", http =>
            {
                http.Response.ContentType = "application/javascript";
                return http.Response.WriteAsync("self.addEventListener('install', () => self.skipWaiting());");
            });
            Server.SetRoute("/sw-run.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<script>navigator.serviceWorker.register('/sw-run.js');</script>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IWorker worker = await context.RunAndWaitForServiceWorkerAsync(
                () => page.GoToAsync(TestConstants.ServerUrl + "/sw-run.html"))
                .ConfigureAwait(false);

            Assert.That(worker, Is.Not.Null);
            Assert.That(worker.Url, Does.Contain("/sw-run.js"));
        }

        [PlaywrightTest("browsercontext-service-worker-policy.spec.ts", "WaitForEvent ServiceWorker")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForServiceWorkerEvent()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Service worker inspection is Chromium-only.");
                return;
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/sw-event.js", http =>
            {
                http.Response.ContentType = "application/javascript";
                return http.Response.WriteAsync("// wave121");
            });
            Server.SetRoute("/sw-event.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<script>navigator.serviceWorker.register('/sw-event.js');</script>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IWorker> waitTask = context.WaitForEventAsync(BrowserContextEvent.ServiceWorker);
            await page.GoToAsync(TestConstants.ServerUrl + "/sw-event.html").ConfigureAwait(false);
            IWorker worker = await waitTask.ConfigureAwait(false);

            Assert.That(worker.Url, Does.Contain("/sw-event.js"));
        }

        [PlaywrightTest("browsercontext-service-worker-policy.spec.ts", "serviceWorkers Block rejects register")]
        [Test]
        [Timeout(30_000)]
        public async Task ServiceWorkersBlockShouldRejectRegister()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/sw-block.js", http =>
            {
                http.Response.ContentType = "application/javascript";
                return http.Response.WriteAsync("// blocked");
            });
            Server.SetRoute("/empty.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>empty</body></html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ServiceWorkers = ServiceWorkerPolicy.Block }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/empty.html").ConfigureAwait(false);

            Task<IConsoleMessage> consoleTask = page.WaitForEventAsync(
                PageEvent.Console,
                message => message.Text == "Service Worker registration blocked by Playwright");
            await page.EvaluateAsync<object>("(() => navigator.serviceWorker.register('/sw-block.js'))()").ConfigureAwait(false);
            IConsoleMessage console = await consoleTask.ConfigureAwait(false);

            Assert.That(console.Text, Is.EqualTo("Service Worker registration blocked by Playwright"));
            Assert.That(context.ServiceWorkers(), Is.Empty);
        }
    }
}
