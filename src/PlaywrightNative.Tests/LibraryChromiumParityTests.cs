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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/chromium/chromium.spec.ts</c> parity. Do not edit leftover
    /// <c>ContextServiceWorkerTests</c> or
    /// <c>LaunchPersistentServiceWorkersTests</c>.
    /// Skipped: <c>should intercept service worker update requests</c> (official
    /// <c>test.fixme</c> / https://github.com/microsoft/playwright/issues/14711);
    /// <c>should refuse WebUI pages that crash Edge</c> (Edge-only);
    /// <c>should navigate to WebUI pages that work in an isolated context</c> and
    /// <c>should navigate to any WebUI page in a persistent context</c> (official
    /// headless skip).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryChromiumParityTests : PageTestEx
    {
        private static readonly string[] UserAgentWithSpacesArgs = { "--user-agent=I am Foo" };
        private static readonly string[] RemoteDebuggingPortArgs = { "--remote-debugging-port=9222" };
        private static readonly string[] RemoteDebuggingPortAltArgs = { "--remote-debugging-port=9223" };

        private static SimpleServer Server => TestServerSetup.Server;

        [SetUp]
        public void SkipNonChromium()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Official Chromium-only chromium.spec.ts.");
            }
        }

        [TearDown]
        public void ResetServer()
        {
            Server?.Reset();
        }

        [PlaywrightTest("chromium.spec.ts", "should create a worker from a service worker")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCreateAWorkerFromAServiceWorker()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            Task<IWorker> workerTask = page.Context.WaitForServiceWorkerAsync();
            await page.GoToAsync(TestConstants.ServerUrl + "/serviceworkers/empty/sw.html").ConfigureAwait(false);
            IWorker worker = await workerTask.ConfigureAwait(false);
            string text = await worker.EvaluateAsync<string>("() => self.toString()").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("[object ServiceWorkerGlobalScope]"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("chromium.spec.ts", "should create a worker from service worker with noop routing")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCreateAWorkerFromServiceWorkerWithNoopRouting()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await context.RouteAsync("**", route => route.ContinueAsync()).ConfigureAwait(false);
            Task<IWorker> workerTask = context.WaitForServiceWorkerAsync();
            await page.GoToAsync(TestConstants.ServerUrl + "/serviceworkers/empty/sw.html").ConfigureAwait(false);
            IWorker worker = await workerTask.ConfigureAwait(false);
            string text = await worker.EvaluateAsync<string>("() => self.toString()").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("[object ServiceWorkerGlobalScope]"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("chromium.spec.ts", "should emit new service worker on update")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmitNewServiceWorkerOnUpdate()
        {
            EnsureServer();
            int version = 0;
            Server.SetRoute("/worker.js", async ctx =>
            {
                ctx.Response.ContentType = "text/javascript";
                await ctx.Response.WriteAsync("self.PW_VERSION = " + version + ";").ConfigureAwait(false);
                version++;
            });
            Server.SetRoute("/home", async ctx =>
            {
                await ctx.Response.WriteAsync(
                    "<!DOCTYPE html><html><body>" +
                    "<button id=\"update\" disabled>update service worker</button>" +
                    "<script>" +
                    "const updateBtn = document.getElementById('update');" +
                    "updateBtn.addEventListener('click', evt => { evt.preventDefault(); registration.then(r => r.update()); });" +
                    "const registration = new Promise(r => navigator.serviceWorker.register('/worker.js').then(r));" +
                    "registration.then(() => updateBtn.disabled = false);" +
                    "</script></body></html>").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IWorker> swTask = context.WaitForServiceWorkerAsync();
            await page.GoToAsync(TestConstants.ServerUrl + "/home").ConfigureAwait(false);
            IWorker sw = await swTask.ConfigureAwait(false);
            await PollEqualAsync(() => sw.EvaluateAsync<int>("() => self['PW_VERSION']"), 0).ConfigureAwait(false);

            Task<IWorker> updatedTask = context.WaitForServiceWorkerAsync();
            await page.ClickAsync("#update").ConfigureAwait(false);
            IWorker updated = await updatedTask.ConfigureAwait(false);
            await PollEqualAsync(() => updated.EvaluateAsync<int>("() => self['PW_VERSION']"), 1).ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("chromium.spec.ts", "httpCredentials")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task HttpCredentials()
        {
            EnsureServer();
            Server.SetAuth("/serviceworkers/fetch/sw.html", "user", "pass");
            Server.SetAuth("/empty.html", "user", "pass");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                HttpCredentials = new HttpCredentials { Username = "user", Password = "pass" },
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IWorker> workerTask = context.WaitForServiceWorkerAsync();
            await page.GoToAsync(TestConstants.ServerUrl + "/serviceworkers/fetch/sw.html").ConfigureAwait(false);
            IWorker worker = await workerTask.ConfigureAwait(false);
            await page.EvaluateAsync("() => window['activationPromise']").ConfigureAwait(false);
            int status = await worker.EvaluateAsync<int>("() => fetch('/empty.html').then(r => r.status)").ConfigureAwait(false);
            Assert.That(status, Is.EqualTo(200));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("chromium.spec.ts", "serviceWorkers() should return current workers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ServiceWorkersShouldReturnCurrentWorkers()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            IBrowserContext context = page.Context;
            Task<IWorker> worker1Task = context.WaitForServiceWorkerAsync();
            await page.GoToAsync(TestConstants.ServerUrl + "/serviceworkers/empty/sw.html").ConfigureAwait(false);
            IWorker worker1 = await worker1Task.ConfigureAwait(false);
            Assert.That(context.ServiceWorkers().Count, Is.EqualTo(1));

            Task<IWorker> worker2Task = context.WaitForServiceWorkerAsync();
            await page.GoToAsync(TestConstants.CrossProcessHttpPrefix + "/serviceworkers/empty/sw.html").ConfigureAwait(false);
            IWorker worker2 = await worker2Task.ConfigureAwait(false);
            IReadOnlyCollection<IWorker> workers = context.ServiceWorkers();
            Assert.That(workers.Count, Is.EqualTo(2));
            Assert.That(workers, Does.Contain(worker1));
            Assert.That(workers, Does.Contain(worker2));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("chromium.spec.ts", "should not create a worker from a shared worker")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCreateAWorkerFromASharedWorker()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            bool serviceWorkerCreated = false;
            await page.EvaluateAsync("() => new SharedWorker('data:text/javascript,console.log(\"hi\")')").ConfigureAwait(false);
            Assert.That(serviceWorkerCreated, Is.False);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("chromium.spec.ts", "Page.route should work with intervention headers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PageRouteShouldWorkWithInterventionHeaders()
        {
            EnsureServer();
            Server.SetRoute("/intervention", async ctx =>
            {
                await ctx.Response.WriteAsync(
                    "<script>document.write('<script src=\"" + TestConstants.CrossProcessHttpPrefix +
                    "/intervention.js\">' + '</scr' + 'ipt>');</script>").ConfigureAwait(false);
            });
            Server.SetRedirect("/intervention.js", "/redirect.js");
            Task<string> interventionTask = Server.WaitForRequest(
                "/redirect.js",
                r => r.Headers["intervention"].ToString());
            Server.SetRoute("/redirect.js", async ctx =>
            {
                await ctx.Response.WriteAsync("console.log(1);").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.RouteAsync("*", route => route.ContinueAsync()).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/intervention").ConfigureAwait(false);
            string intervention = await interventionTask.ConfigureAwait(false);
            Assert.That(intervention, Does.Contain("feature/5718547946799104"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("chromium.spec.ts", "should close service worker together with the context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCloseServiceWorkerTogetherWithTheContext()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IWorker> workerTask = context.WaitForServiceWorkerAsync();
            await page.GoToAsync(TestConstants.ServerUrl + "/serviceworkers/empty/sw.html").ConfigureAwait(false);
            IWorker worker = await workerTask.ConfigureAwait(false);
            List<string> messages = new();
            context.Close += (_, _) => messages.Add("context");
            worker.Close += (_, _) => messages.Add("worker");
            await context.CloseAsync().ConfigureAwait(false);
            Assert.That(string.Join("|", messages), Is.EqualTo("worker|context"));
        }

        [PlaywrightTest("chromium.spec.ts", "should pass args with spaces")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPassArgsWithSpaces()
        {
            string userDataDir = CreateUserDataDir();
            try
            {
                IBrowserContext context = await LaunchPersistentAsync(
                    userDataDir,
                    new BrowserTypeLaunchPersistentContextOptions { Args = UserAgentWithSpacesArgs }).ConfigureAwait(false);
                try
                {
                    IPage page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync().ConfigureAwait(false);
                    string userAgent = await page.EvaluateAsync<string>("() => navigator.userAgent").ConfigureAwait(false);
                    Assert.That(userAgent, Is.EqualTo("I am Foo"));
                }
                finally
                {
                    await context.CloseAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                TryDeleteDirectory(userDataDir);
            }
        }

        [PlaywrightTest("chromium.spec.ts", "serviceWorker(), and fromServiceWorker() work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ServiceWorkerAndFromServiceWorkerWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IWorker> workerTask = context.WaitForServiceWorkerAsync();
            Task<IRequest> htmlTask = context.WaitForRequestAsync(r => r.Url.EndsWith("/sw.html", StringComparison.Ordinal));
            Task<IRequest> mainTask = context.WaitForRequestAsync(r => r.Url.EndsWith("/sw.js", StringComparison.Ordinal));
            Task<IRequest> inWorkerTask = context.WaitForRequestAsync(r => r.Url.EndsWith("/request-from-within-worker.txt", StringComparison.Ordinal));
            await page.GoToAsync(TestConstants.ServerUrl + "/serviceworkers/fetch/sw.html").ConfigureAwait(false);
            IWorker worker = await workerTask.ConfigureAwait(false);
            IRequest html = await htmlTask.ConfigureAwait(false);
            IRequest main = await mainTask.ConfigureAwait(false);
            IRequest inWorker = await inWorkerTask.ConfigureAwait(false);

            Assert.That(html.Frame, Is.Not.Null);
            Assert.That(html.ServiceWorker(), Is.Null);
            Assert.That((await html.ResponseAsync().ConfigureAwait(false)).FromServiceWorker, Is.False);

            Assert.Throws<PlaywrightNativeException>(() => _ = main.Frame);
            Assert.That(main.ServiceWorker(), Is.SameAs(worker));
            Assert.That((await main.ResponseAsync().ConfigureAwait(false)).FromServiceWorker, Is.False);

            Assert.Throws<PlaywrightNativeException>(() => _ = inWorker.Frame);
            Assert.That(inWorker.ServiceWorker(), Is.SameAs(worker));
            Assert.That((await inWorker.ResponseAsync().ConfigureAwait(false)).FromServiceWorker, Is.False);

            await page.EvaluateAsync("() => window['activationPromise']").ConfigureAwait(false);
            Task<IRequest> innerSwTask = context.WaitForRequestAsync(
                r => r.Url.EndsWith("/inner.txt", StringComparison.Ordinal) && r.ServiceWorker() != null);
            Task<IRequest> innerPageTask = context.WaitForRequestAsync(
                r => r.Url.EndsWith("/inner.txt", StringComparison.Ordinal) && r.ServiceWorker() == null);
            await page.EvaluateAsync("() => fetch('/inner.txt')").ConfigureAwait(false);
            IRequest innerSw = await innerSwTask.ConfigureAwait(false);
            IRequest innerPage = await innerPageTask.ConfigureAwait(false);
            Assert.That(innerPage.ServiceWorker(), Is.Null);
            Assert.That((await innerPage.ResponseAsync().ConfigureAwait(false)).FromServiceWorker, Is.True);
            Assert.That(innerSw.ServiceWorker(), Is.SameAs(worker));
            Assert.That((await innerSw.ResponseAsync().ConfigureAwait(false)).FromServiceWorker, Is.False);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("chromium.spec.ts", "should intercept service worker requests (main and within)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInterceptServiceWorkerRequestsMainAndWithin()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await context.RouteAsync("**/request-from-within-worker", route => route.FulfillAsync(new() { ContentType = "application/json", Status = 200, Body = "\"intercepted!\"" })).ConfigureAwait(false);
            await context.RouteAsync("**/sw.js", route => route.FulfillAsync(new() { ContentType = "text/javascript", Status = 200, Body = "self.contentPromise = new Promise(res => fetch('/request-from-within-worker').then(r => r.json()).then(res));" })).ConfigureAwait(false);

            Task<IWorker> swTask = context.WaitForServiceWorkerAsync();
            Task<IResponse> withinTask = context.WaitForResponseAsync(r => r.Url.EndsWith("/request-from-within-worker", StringComparison.Ordinal));
            Task<IRequest> swJsTask = context.WaitForRequestAsync(r => r.Url.EndsWith("sw.js", StringComparison.Ordinal) && r.ServiceWorker() != null);
            Task<IResponse> swJsResponseTask = context.WaitForResponseAsync(r => r.Url.EndsWith("sw.js", StringComparison.Ordinal) && !r.FromServiceWorker);
            await page.GoToAsync(TestConstants.ServerUrl + "/serviceworkers/empty/sw.html").ConfigureAwait(false);
            IWorker sw = await swTask.ConfigureAwait(false);
            await withinTask.ConfigureAwait(false);
            await swJsTask.ConfigureAwait(false);
            await swJsResponseTask.ConfigureAwait(false);
            string value = await sw.EvaluateAsync<string>("() => self['contentPromise']").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("intercepted!"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("chromium.spec.ts", "should report failure (due to content-type) of main service worker request")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportFailureDueToContentTypeOfMainServiceWorkerRequest()
        {
            EnsureServer();
            Server.SetRoute("/serviceworkers/fetch/sw.js", async ctx =>
            {
                ctx.Response.ContentType = "text/html";
                await ctx.Response.WriteAsync("console.log('hi from sw');").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task serverTask = Server.WaitForRequest("/serviceworkers/fetch/sw.js");
            Task<IRequest> mainTask = context.WaitForRequestAsync(r => r.Url.EndsWith("sw.js", StringComparison.Ordinal));
            await page.GoToAsync(TestConstants.ServerUrl + "/serviceworkers/fetch/sw.html").ConfigureAwait(false);
            await serverTask.ConfigureAwait(false);
            IRequest main = await mainTask.ConfigureAwait(false);
            await main.ResponseAsync().ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("chromium.spec.ts", "should report failure (due to redirect) of main service worker request")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportFailureDueToRedirectOfMainServiceWorkerRequest()
        {
            EnsureServer();
            Server.SetRedirect("/serviceworkers/empty/sw.js", "/dev/null");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task serverTask = Server.WaitForRequest("/serviceworkers/empty/sw.js");
            Task<IRequest> mainTask = context.WaitForRequestAsync(r => r.Url.EndsWith("sw.js", StringComparison.Ordinal));
            await page.GoToAsync(TestConstants.ServerUrl + "/serviceworkers/empty/sw.html").ConfigureAwait(false);
            await serverTask.ConfigureAwait(false);
            IRequest main = await mainTask.ConfigureAwait(false);
            IResponse resp = await main.ResponseAsync().ConfigureAwait(false);
            Assert.That(resp.Status, Is.EqualTo(302));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("chromium.spec.ts", "should intercept service worker importScripts")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInterceptServiceWorkerImportScripts()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await context.RouteAsync("**/import.js", route => route.FulfillAsync(new() { ContentType = "text/javascript", Status = 200, Body = "self.exportedValue = 47;" })).ConfigureAwait(false);
            await context.RouteAsync("**/sw.js", route => route.FulfillAsync(new() { ContentType = "text/javascript", Status = 200, Body = "importScripts('/import.js');\nself.importedValue = self.exportedValue;" })).ConfigureAwait(false);

            Task<IWorker> swTask = context.WaitForServiceWorkerAsync();
            Task<IResponse> importTask = context.WaitForResponseAsync(r => r.Url.EndsWith("/import.js", StringComparison.Ordinal));
            await page.GoToAsync(TestConstants.ServerUrl + "/serviceworkers/empty/sw.html").ConfigureAwait(false);
            IWorker sw = await swTask.ConfigureAwait(false);
            await importTask.ConfigureAwait(false);
            int value = await sw.EvaluateAsync<int>("() => self['importedValue']").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo(47));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("chromium.spec.ts", "should report intercepted service worker requests in HAR")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportInterceptedServiceWorkerRequestsInHar()
        {
            string harPath = Path.Combine(Path.GetTempPath(), "pwsharp-wave904-" + Guid.NewGuid().ToString("N") + ".har");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync(new() { RecordHarPath = harPath }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await context.RouteAsync("**/request-from-within-worker", route => route.FulfillAsync(
                contentType: "application/json",
                headers: new Dictionary<string, string> { ["x-pw-test"] = "request-within-worker" },
                status: 200,
                body: "\"intercepted!\"")).ConfigureAwait(false);
            await context.RouteAsync("**/sw.js", route => route.FulfillAsync(
                contentType: "text/javascript",
                headers: new Dictionary<string, string> { ["x-pw-test"] = "intercepted-main" },
                status: 200,
                body: "self.contentPromise = new Promise(res => fetch('/request-from-within-worker').then(r => r.json()).then(res));")).ConfigureAwait(false);

            Task<IWorker> swTask = context.WaitForServiceWorkerAsync();
            Task<IResponse> withinTask = context.WaitForResponseAsync(r => r.Url.EndsWith("/request-from-within-worker", StringComparison.Ordinal));
            await page.GoToAsync(TestConstants.ServerUrl + "/serviceworkers/empty/sw.html").ConfigureAwait(false);
            IWorker sw = await swTask.ConfigureAwait(false);
            await withinTask.ConfigureAwait(false);
            string value = await sw.EvaluateAsync<string>("() => self['contentPromise']").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("intercepted!"));
            await context.CloseAsync().ConfigureAwait(false);

            JsonElement log = ReadHarLog(harPath);
            JsonElement[] swEntries = log.GetProperty("entries")
                .EnumerateArray()
                .Where(e => e.GetProperty("request").GetProperty("url").GetString().EndsWith("sw.js", StringComparison.Ordinal))
                .ToArray();
            Assert.That(swEntries.Length, Is.EqualTo(1));
            AssertHeader(swEntries[0], "x-pw-test", "intercepted-main");

            JsonElement[] reqEntries = log.GetProperty("entries")
                .EnumerateArray()
                .Where(e => e.GetProperty("request").GetProperty("url").GetString().EndsWith("request-from-within-worker", StringComparison.Ordinal))
                .ToArray();
            Assert.That(reqEntries.Length, Is.EqualTo(1));
            AssertHeader(reqEntries[0], "x-pw-test", "request-within-worker");
            Assert.That(reqEntries[0].GetProperty("response").GetProperty("content").GetProperty("text").GetString(), Is.EqualTo("\"intercepted!\""));
            TryDelete(harPath);
        }

        [PlaywrightTest("chromium.spec.ts", "should intercept only serviceworker request, not page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInterceptOnlyServiceworkerRequestNotPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await context.RouteAsync("**/data.json", async route =>
            {
                if (route.Request.ServiceWorker() != null)
                {
                    await route.FulfillAsync(new() { ContentType = "text/plain", Status = 200, Body = "from sw" }).ConfigureAwait(false);
                }
                else
                {
                    await route.ContinueAsync().ConfigureAwait(false);
                }
            }).ConfigureAwait(false);

            Task<IWorker> swTask = context.WaitForServiceWorkerAsync();
            await page.GoToAsync(TestConstants.ServerUrl + "/serviceworkers/fetch/sw.html").ConfigureAwait(false);
            IWorker sw = await swTask.ConfigureAwait(false);
            await page.EvaluateAsync("() => window['activationPromise']").ConfigureAwait(false);
            string response = await page.EvaluateAsync<string>("() => fetch('/data.json').then(r => r.text())").ConfigureAwait(false);
            JsonElement intercepted = await sw.EvaluateAsync<JsonElement>("() => self['intercepted']").ConfigureAwait(false);
            string url = intercepted[0].GetString();
            Assert.That(url, Does.Match(new Regex(@"/data\.json$")));
            Assert.That(response, Is.EqualTo("from sw"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("chromium.spec.ts", "should produce network events, routing, and annotations for Service Worker")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldProduceNetworkEventsRoutingAndAnnotationsForServiceWorker()
        {
            await RunServiceWorkerNetworkTableAsync(advanced: false).ConfigureAwait(false);
        }

        [PlaywrightTest("chromium.spec.ts", "should produce network events, routing, and annotations for Service Worker (advanced)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldProduceNetworkEventsRoutingAndAnnotationsForServiceWorkerAdvanced()
        {
            await RunServiceWorkerNetworkTableAsync(advanced: true).ConfigureAwait(false);
        }

        [PlaywrightTest("chromium.spec.ts", "should intercept service worker update requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldInterceptServiceWorkerUpdateRequests()
        {
            Assert.Ignore("fixme: https://github.com/microsoft/playwright/issues/14711");
        }

        [PlaywrightTest("chromium.spec.ts", "setOffline")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SetOffline()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IWorker> workerTask = context.WaitForServiceWorkerAsync();
            await page.GoToAsync(TestConstants.ServerUrl + "/serviceworkers/fetch/sw.html").ConfigureAwait(false);
            IWorker worker = await workerTask.ConfigureAwait(false);
            await page.EvaluateAsync("() => window['activationPromise']").ConfigureAwait(false);
            await context.SetOfflineAsync(true).ConfigureAwait(false);
            Task<IRequest> requestTask = context.WaitForRequestAsync(
                r => r.Url.EndsWith("/inner.txt", StringComparison.Ordinal) && r.ServiceWorker() != null);
            Task<string> errorTask = worker.EvaluateAsync<string>("() => fetch('/inner.txt').catch(e => `REJECTED: ${e}`)");
            await requestTask.ConfigureAwait(false);
            string error = await errorTask.ConfigureAwait(false);
            Assert.That(error, Does.Match(new Regex("REJECTED.*Failed to fetch")));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("chromium.spec.ts", "setExtraHTTPHeaders")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SetExtraHttpHeaders()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IWorker> workerTask = context.WaitForServiceWorkerAsync();
            await page.GoToAsync(TestConstants.ServerUrl + "/serviceworkers/fetch/sw.html").ConfigureAwait(false);
            IWorker worker = await workerTask.ConfigureAwait(false);
            await page.EvaluateAsync("() => window['activationPromise']").ConfigureAwait(false);
            await context.SetExtraHttpHeadersAsync(new Dictionary<string, string> { ["x-custom-header"] = "custom!" }).ConfigureAwait(false);
            Task<string> requestPromise = Server.WaitForRequest("/inner.txt", r => r.Headers["x-custom-header"].ToString());
            await worker.EvaluateAsync<object>("() => fetch('/inner.txt')").ConfigureAwait(false);
            string header = await requestPromise.ConfigureAwait(false);
            Assert.That(header, Is.EqualTo("custom!"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("chromium.spec.ts", "should throw when connecting twice to an already running persistent context (--remote-debugging-port)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowWhenConnectingTwiceToAnAlreadyRunningPersistentContextRemoteDebuggingPort()
        {
            string userDataDir = CreateUserDataDir();
            IBrowserContext browser = await LaunchPersistentAsync(
                userDataDir,
                new BrowserTypeLaunchPersistentContextOptions { Args = RemoteDebuggingPortArgs }).ConfigureAwait(false);
            try
            {
                Exception error = await CatchAsync(() => LaunchPersistentAsync(
                    userDataDir,
                    new BrowserTypeLaunchPersistentContextOptions { Args = RemoteDebuggingPortAltArgs })).ConfigureAwait(false);
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Does.Contain("This usually means that the profile is already in use by another instance of Chromium."));
            }
            finally
            {
                await browser.CloseAsync().ConfigureAwait(false);
                TryDeleteDirectory(userDataDir);
            }
        }

        [PlaywrightTest("chromium.spec.ts", "should throw when connecting twice to an already running persistent context (--remote-debugging-pipe)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowWhenConnectingTwiceToAnAlreadyRunningPersistentContextRemoteDebuggingPipe()
        {
            string userDataDir = CreateUserDataDir();
            IBrowserContext browser = await LaunchPersistentAsync(userDataDir).ConfigureAwait(false);
            try
            {
                Exception error = await CatchAsync(() => LaunchPersistentAsync(userDataDir)).ConfigureAwait(false);
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Does.Contain("This usually means that the profile is already in use by another instance of Chromium."));
            }
            finally
            {
                await browser.CloseAsync().ConfigureAwait(false);
                TryDeleteDirectory(userDataDir);
            }
        }

        [PlaywrightTest("chromium.spec.ts", "PLAYWRIGHT_DISABLE_SERVICE_WORKER_NETWORK")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PlaywrightDisableServiceWorkerNetwork()
        {
            string previous = Environment.GetEnvironmentVariable("PLAYWRIGHT_DISABLE_SERVICE_WORKER_NETWORK");
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DISABLE_SERVICE_WORKER_NETWORK", "1");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                List<string> urls = new();
                context.Request += (_, r) =>
                {
                    Assert.That(r.ServiceWorker(), Is.Null);
                    urls.Add(r.Url);
                };

                Task<IWorker> workerTask = context.WaitForServiceWorkerAsync();
                await page.GoToAsync(TestConstants.ServerUrl + "/serviceworkers/fetch/sw.html").ConfigureAwait(false);
                await workerTask.ConfigureAwait(false);
                Assert.That(urls, Is.EqualTo(new[]
                {
                    TestConstants.ServerUrl + "/serviceworkers/fetch/sw.html",
                    TestConstants.ServerUrl + "/serviceworkers/fetch/style.css",
                }));

                await page.EvaluateAsync("() => window['activationPromise']").ConfigureAwait(false);
                await page.EvaluateAsync("() => fetch('./inner.txt')").ConfigureAwait(false);
                Assert.That(urls, Is.EqualTo(new[]
                {
                    TestConstants.ServerUrl + "/serviceworkers/fetch/sw.html",
                    TestConstants.ServerUrl + "/serviceworkers/fetch/style.css",
                    TestConstants.ServerUrl + "/serviceworkers/fetch/inner.txt",
                }));
                await context.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PLAYWRIGHT_DISABLE_SERVICE_WORKER_NETWORK", previous);
            }
        }

        [PlaywrightTest("chromium.spec.ts", "should emit console messages from service worker")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmitConsoleMessagesFromServiceWorker()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage emptyPage = await context.NewPageAsync().ConfigureAwait(false);
            await emptyPage.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            List<IConsoleMessage> emptyPageMessages = new();
            emptyPage.Console += (_, message) => emptyPageMessages.Add(message);

            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IWorker> workerTask = context.WaitForServiceWorkerAsync();
            await page.GoToAsync(TestConstants.ServerUrl + "/serviceworkers/empty/sw.html").ConfigureAwait(false);
            IWorker worker = await workerTask.ConfigureAwait(false);

            Task<IConsoleMessage> workerConsole = worker.WaitForConsoleMessageAsync();
            Task<IConsoleMessage> pageConsole = page.WaitForConsoleMessageAsync();
            await worker.EvaluateAsync<object>(
                "() => console.log('hello from service worker', { i: 'am', am: 1, complex: { yup: true } })").ConfigureAwait(false);
            IConsoleMessage message = await workerConsole.ConfigureAwait(false);
            IConsoleMessage pageMessage = await pageConsole.ConfigureAwait(false);
            Assert.That(message, Is.SameAs(pageMessage));
            Assert.That(emptyPageMessages, Is.Empty);

            await page.CloseAsync().ConfigureAwait(false);
            Assert.That(message.Text, Does.Contain("hello from service worker"));
            Assert.That(message.Type, Is.EqualTo("log"));
            IReadOnlyList<IJSHandle> args = new List<IJSHandle>(message.Args);
            Assert.That(args.Count, Is.EqualTo(2));
            Assert.That(await args[0].JsonValueAsync<string>().ConfigureAwait(false), Is.EqualTo("hello from service worker"));
            JsonElement arg1 = await args[1].JsonValueAsync<JsonElement>().ConfigureAwait(false);
            Assert.That(arg1.GetProperty("i").GetString(), Is.EqualTo("am"));
            Assert.That(arg1.GetProperty("am").GetInt32(), Is.EqualTo(1));
            Assert.That(arg1.GetProperty("complex").GetProperty("yup").GetBoolean(), Is.True);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("chromium.spec.ts", "should capture console.log from ServiceWorker start")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCaptureConsoleLogFromServiceWorkerStart()
        {
            EnsureServer();
            Server.SetRoute("/serviceworkers/empty/sw.js", async ctx =>
            {
                ctx.Response.ContentType = "text/javascript";
                await ctx.Response.WriteAsync("console.log('Hello from the first line of sw.js');").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IWorker> workerTask = context.WaitForServiceWorkerAsync();
            await page.GoToAsync(TestConstants.ServerUrl + "/serviceworkers/empty/sw.html").ConfigureAwait(false);
            IWorker worker = await workerTask.ConfigureAwait(false);
            IConsoleMessage consoleMessage = await worker.WaitForConsoleMessageAsync().ConfigureAwait(false);
            Assert.That(consoleMessage.Text, Is.EqualTo("Hello from the first line of sw.js"));
            Assert.That(consoleMessage.Type, Is.EqualTo("log"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("chromium.spec.ts", "should refuse WebUI pages that crash the browser in an isolated context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRefuseWebUiPagesThatCrashTheBrowserInAnIsolatedContext()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            string[] urls =
            {
                "chrome://apps",
                "chrome://extensions",
                "chrome://help",
                "chrome://history",
                "chrome://password-manager",
                "chrome://settings",
                "chrome://extensions/",
                "chrome://settings/help",
                "chrome://SETTINGS",
                "chrome:settings",
                "chrome:///settings",
                "view-source:chrome://settings",
            };
            foreach (string url in urls)
            {
                string message = await GotoErrorAsync(page, url).ConfigureAwait(false);
                Assert.That(message, Does.Contain("Cannot navigate to \"" + url + "\""));
            }

            Assert.That(browser.IsConnected, Is.True);
            Assert.That(await page.EvaluateAsync<int>("() => 1 + 1").ConfigureAwait(false), Is.EqualTo(2));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("chromium.spec.ts", "should refuse WebUI pages that crash Edge in an isolated context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldRefuseWebUiPagesThatCrashEdgeInAnIsolatedContext()
        {
            Assert.Ignore("Edge has its own list of pages disallowed in InPrivate");
        }

        [PlaywrightTest("chromium.spec.ts", "should navigate to WebUI pages that work in an isolated context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldNavigateToWebUiPagesThatWorkInAnIsolatedContext()
        {
            Assert.Ignore("WebUI pages are not available in headless");
        }

        [PlaywrightTest("chromium.spec.ts", "should navigate to any WebUI page in a persistent context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldNavigateToAnyWebUiPageInAPersistentContext()
        {
            Assert.Ignore("WebUI pages are not available in headless");
        }

        [PlaywrightTest("chromium.spec.ts", "should fire dialogclosed event when dialog is closed out of band")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFireDialogclosedEventWhenDialogIsClosedOutOfBand()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            ICDPSession client = await page.Context.NewCDPSessionAsync(page).ConfigureAwait(false);
            await client.SendAsync("Page.enable").ConfigureAwait(false);
            Task<IDialog> dialogTask = page.WaitForEventAsync(PageEvent.Dialog);
            Task<IDialog> closedTask = page.WaitForDialogClosedAsync();
            Task evaluateTask = page.EvaluateAsync("() => alert('yo')");
            IDialog dialog = await dialogTask.ConfigureAwait(false);
            await client.SendAsync("Page.handleJavaScriptDialog", new { accept = true }).ConfigureAwait(false);
            Assert.That(await closedTask.ConfigureAwait(false), Is.SameAs(dialog));
            await evaluateTask.ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        private static SimpleServer EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            return Server;
        }

        private static string CreateUserDataDir()
        {
            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-wave904-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(userDataDir);
            return userDataDir;
        }

        private static Task<IBrowserContext> LaunchPersistentAsync(
            string userDataDir,
            BrowserTypeLaunchPersistentContextOptions options = null)
        {
            options ??= new BrowserTypeLaunchPersistentContextOptions();
            options.Headless = true;
            if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
            {
                Assert.Ignore("Chromium executable not available (download skipped or failed).");
            }

            options.ExecutablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            return Playwright.Chromium.LaunchPersistentContextAsync(userDataDir, options);
        }

        private static async Task<Exception> CatchAsync(Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private static async Task PollEqualAsync<T>(Func<Task<T>> getValue, T expected)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            T last = default;
            while (DateTime.UtcNow < deadline)
            {
                last = await getValue().ConfigureAwait(false);
                if (Equals(last, expected))
                {
                    return;
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            Assert.That(last, Is.EqualTo(expected));
        }

        private static async Task<string> GotoErrorAsync(IPage page, string url)
        {
            try
            {
                await page.GoToAsync(url).ConfigureAwait(false);
                return string.Empty;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static JsonElement ReadHarLog(string harPath)
        {
            string json = File.ReadAllText(harPath);
            if (harPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using ZipArchive archive = ZipFile.OpenRead(harPath);
                using Stream stream = archive.Entries[0].Open();
                using StreamReader reader = new StreamReader(stream);
                json = reader.ReadToEnd();
            }

            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement.GetProperty("log").Clone();
        }

        private static void AssertHeader(JsonElement entry, string name, string value)
        {
            JsonElement[] headers = entry.GetProperty("response").GetProperty("headers")
                .EnumerateArray()
                .Where(h => string.Equals(h.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.That(headers.Length, Is.EqualTo(1));
            Assert.That(headers[0].GetProperty("value").GetString(), Is.EqualTo(value));
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }

        private static async Task RunServiceWorkerNetworkTableAsync(bool advanced)
        {
            EnsureServer();
            string scriptName = advanced ? "complex-service-worker.js" : "transparent-service-worker.js";
            Server.SetRoute("/index.html", async ctx =>
            {
                await ctx.Response.WriteAsync(
                    "<script>window.registrationPromise = navigator.serviceWorker.register('/" + scriptName + "');</script>").ConfigureAwait(false);
            });
            if (advanced)
            {
                Server.SetRoute("/complex-service-worker.js", async ctx =>
                {
                    ctx.Response.ContentType = "text/javascript";
                    await ctx.Response.WriteAsync(
                        "self.addEventListener(\"install\", function (event) {" +
                        "  event.waitUntil(caches.open(\"v1\").then(function (cache) { return cache.add(\"/addressbook.json\"); }));" +
                        "});" +
                        "self.addEventListener(\"fetch\", (event) => {" +
                        "  event.respondWith((async () => {" +
                        "    let response = await caches.match(event.request);" +
                        "    if (response) return response;" +
                        "    if (event.request.url.endsWith(\"foo\")) return fetch(\"./bar\");" +
                        "    if (event.request.url.endsWith(\"tracker.js\"))" +
                        "      return new Response('console.log(\"no trackers!\")', { status: 200, headers: { \"Content-Type\": \"text/javascript\" } });" +
                        "    return fetch(event.request);" +
                        "  })());" +
                        "});" +
                        "self.addEventListener(\"activate\", (event) => { event.waitUntil(clients.claim()); });").ConfigureAwait(false);
                });
                Server.SetRoute("/addressbook.json", async ctx =>
                {
                    await ctx.Response.WriteAsync("{}").ConfigureAwait(false);
                });
            }
            else
            {
                Server.SetRoute("/transparent-service-worker.js", async ctx =>
                {
                    ctx.Response.ContentType = "text/javascript";
                    await ctx.Response.WriteAsync(
                        "self.addEventListener(\"fetch\", (event) => {" +
                        "  const responsePromise = fetch(event.request);" +
                        "  event.respondWith(responsePromise);" +
                        "});" +
                        "self.addEventListener(\"activate\", (event) => { event.waitUntil(clients.claim()); });").ConfigureAwait(false);
                });
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            List<IRequest> routed = new();
            await context.RouteAsync("**", async route =>
            {
                routed.Add(route.Request);
                await route.ContinueAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
            await page.RouteAsync("**", async route =>
            {
                routed.Add(route.Request);
                await route.ContinueAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
            List<(string Scope, IRequest Request)> requests = new();
            page.Request += (_, r) => requests.Add(("page", r));
            context.Request += (_, r) => requests.Add(("context", r));

            Task<IWorker> swTask = context.WaitForServiceWorkerAsync();
            await page.GoToAsync(TestConstants.ServerUrl + "/index.html").ConfigureAwait(false);
            IWorker sw = await swTask.ConfigureAwait(false);
            await PollEqualAsync(
                () => sw.EvaluateAsync<string>("() => self.registration.active && self.registration.active.state"),
                "activated").ConfigureAwait(false);

            if (advanced)
            {
                await page.EvaluateAsync("() => fetch('/addressbook.json')").ConfigureAwait(false);
                await page.EvaluateAsync("() => fetch('/foo')").ConfigureAwait(false);
                await page.EvaluateAsync("() => fetch('/tracker.js')").ConfigureAwait(false);
                await page.EvaluateAsync("() => fetch('/fallthrough.txt')").ConfigureAwait(false);
            }
            else
            {
                await page.EvaluateAsync("() => fetch('/data.json')").ConfigureAwait(false);
            }

            List<string> rows = new()
            {
                "| Event                             | Owner            | URL                            | Routed | [`method: Response.fromServiceWorker`] |",
            };
            foreach ((string scope, IRequest request) in requests)
            {
                rows.Add(await FormatRequestAsync(scope, request, routed).ConfigureAwait(false));
            }

            string[] expected = advanced
                ? new[]
                {
                    "| Event                             | Owner            | URL                            | Routed | [`method: Response.fromServiceWorker`] |",
                    "| [`event: BrowserContext.request`] | [Frame]          | index.html                     | Yes    |                                        |",
                    "| [`event: Page.request`]           | [Frame]          | index.html                     | Yes    |                                        |",
                    "| [`event: BrowserContext.request`] | Service [Worker] | complex-service-worker.js      | Yes    |                                        |",
                    "| [`event: BrowserContext.request`] | Service [Worker] | addressbook.json               | Yes    |                                        |",
                    "| [`event: BrowserContext.request`] | [Frame]          | addressbook.json               |        | Yes                                    |",
                    "| [`event: Page.request`]           | [Frame]          | addressbook.json               |        | Yes                                    |",
                    "| [`event: BrowserContext.request`] | Service [Worker] | bar                            | Yes    |                                        |",
                    "| [`event: BrowserContext.request`] | [Frame]          | foo                            |        | Yes                                    |",
                    "| [`event: Page.request`]           | [Frame]          | foo                            |        | Yes                                    |",
                    "| [`event: BrowserContext.request`] | [Frame]          | tracker.js                     |        | Yes                                    |",
                    "| [`event: Page.request`]           | [Frame]          | tracker.js                     |        | Yes                                    |",
                    "| [`event: BrowserContext.request`] | Service [Worker] | fallthrough.txt                | Yes    |                                        |",
                    "| [`event: BrowserContext.request`] | [Frame]          | fallthrough.txt                |        | Yes                                    |",
                    "| [`event: Page.request`]           | [Frame]          | fallthrough.txt                |        | Yes                                    |",
                }
                : new[]
                {
                    "| Event                             | Owner            | URL                            | Routed | [`method: Response.fromServiceWorker`] |",
                    "| [`event: BrowserContext.request`] | [Frame]          | index.html                     | Yes    |                                        |",
                    "| [`event: Page.request`]           | [Frame]          | index.html                     | Yes    |                                        |",
                    "| [`event: BrowserContext.request`] | Service [Worker] | transparent-service-worker.js  | Yes    |                                        |",
                    "| [`event: BrowserContext.request`] | Service [Worker] | data.json                      | Yes    |                                        |",
                    "| [`event: BrowserContext.request`] | [Frame]          | data.json                      |        | Yes                                    |",
                    "| [`event: Page.request`]           | [Frame]          | data.json                      |        | Yes                                    |",
                };
            Assert.That(rows, Is.EqualTo(expected));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private static async Task<string> FormatRequestAsync(string scope, IRequest request, List<IRequest> routed)
        {
            string owner = request.ServiceWorker() != null ? "Service [Worker]" : "[Frame]".PadRight("Service [Worker]".Length);
            string url = request.Url.Split('/').Last().PadRight(30);
            string routedCell = (routed.Contains(request) ? "Yes" : string.Empty).PadRight("Routed".Length);
            IResponse response = await request.ResponseAsync().ConfigureAwait(false);
            string fromSw = ((response != null && response.FromServiceWorker) ? "Yes" : string.Empty)
                .PadRight("[`method: Response.fromServiceWorker`]".Length);
            string ev = scope == "page"
                ? "[`event: Page.request`]".PadRight("[`event: BrowserContext.request`]".Length)
                : "[`event: BrowserContext.request`]";
            return "| " + ev + " | " + owner + " | " + url + " | " + routedCell + " | " + fromSw + " |";
        }
    }
}
