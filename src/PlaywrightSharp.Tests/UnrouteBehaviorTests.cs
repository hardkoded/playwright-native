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
    /// Official <c>UnrouteBehavior</c> on <see cref="IPage"/>.
    /// </summary>
    [TestFixture]
    public class UnrouteBehaviorTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        private static Task WriteServerHtmlAsync(HttpContext http)
        {
            http.Response.ContentType = "text/html";
            return http.Response.WriteAsync("<html><body>from-server</body></html>");
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "UnrouteBehavior exposes official values")]
        [Test]
        public void UnrouteBehaviorShouldExposeOfficialValues()
        {
            Assert.That((int)UnrouteBehavior.Undefined, Is.EqualTo(0));
            Assert.That(UnrouteBehavior.Wait, Is.Not.EqualTo(UnrouteBehavior.Undefined));
            Assert.That(UnrouteBehavior.IgnoreErrors, Is.Not.EqualTo(UnrouteBehavior.Wait));
            Assert.That(UnrouteBehavior.Default, Is.Not.EqualTo(UnrouteBehavior.Wait));
            Assert.That(UnrouteBehavior.Default, Is.Not.EqualTo(UnrouteBehavior.IgnoreErrors));
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "page UnrouteAllAsync wait waits for pending handlers")]
        [Test]
        [Timeout(30_000)]
        public async Task PageUnrouteAllWaitShouldWaitForPendingHandlers()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/unroute-wait.html", WriteServerHtmlAsync);

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);

            await page.RouteAsync("**/unroute-wait.html", async route =>
            {
                started.TrySetResult(true);
                await release.Task.ConfigureAwait(false);
                await route.FulfillAsync(new() { Body = "<html><body>from-wait</body></html>", ContentType = "text/html", Status = 200 }).ConfigureAwait(false);
            }).ConfigureAwait(false);

            Task<IResponse> navigation = page.GoToAsync(TestConstants.ServerUrl + "/unroute-wait.html");
            await started.Task.ConfigureAwait(false);

            Task unroute = page.UnrouteAllAsync(UnrouteBehavior.Wait);
            await Task.Delay(300).ConfigureAwait(false);
            Assert.That(unroute.IsCompleted, Is.False);

            release.TrySetResult(true);
            await unroute.ConfigureAwait(false);
            await navigation.ConfigureAwait(false);
            string body = (await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false)) ?? string.Empty;
            Assert.That(body, Is.EqualTo("from-wait"));
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "page UnrouteAllAsync default does not wait")]
        [Test]
        [Timeout(30_000)]
        public async Task PageUnrouteAllDefaultShouldNotWaitForPendingHandlers()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/unroute-default.html", WriteServerHtmlAsync);

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);

            await page.RouteAsync("**/unroute-default.html", async route =>
            {
                started.TrySetResult(true);
                await release.Task.ConfigureAwait(false);
                await route.FulfillAsync(new() { Body = "<html><body>from-default</body></html>", ContentType = "text/html", Status = 200 }).ConfigureAwait(false);
            }).ConfigureAwait(false);

            Task<IResponse> navigation = page.GoToAsync(TestConstants.ServerUrl + "/unroute-default.html");
            await started.Task.ConfigureAwait(false);

            await page.UnrouteAllAsync(UnrouteBehavior.Default).ConfigureAwait(false);
            Assert.That(release.Task.IsCompleted, Is.False);

            release.TrySetResult(true);
            await navigation.ConfigureAwait(false);
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "page UnrouteAllAsync ignoreErrors does not wait")]
        [Test]
        [Timeout(30_000)]
        public async Task PageUnrouteAllIgnoreErrorsShouldNotWaitAndSwallow()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/unroute-ignore.html", WriteServerHtmlAsync);

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);

            await page.RouteAsync("**/unroute-ignore.html", async route =>
            {
                started.TrySetResult(true);
                await release.Task.ConfigureAwait(false);
                throw new InvalidOperationException("handler boom");
            }).ConfigureAwait(false);

            Task<IResponse> navigation = page.GoToAsync(TestConstants.ServerUrl + "/unroute-ignore.html");
            await started.Task.ConfigureAwait(false);

            Task unroute = page.UnrouteAllAsync(UnrouteBehavior.IgnoreErrors);
            await Task.Delay(300).ConfigureAwait(false);
            await unroute.ConfigureAwait(false);
            Assert.That(unroute.IsCompleted, Is.True);

            release.TrySetResult(true);
            try
            {
                await navigation.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "page UnrouteAsync wait waits for pending handlers")]
        [Test]
        [Timeout(30_000)]
        public async Task PageUnrouteWaitShouldWaitForPendingHandlers()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/unroute-one.html", WriteServerHtmlAsync);

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);

            await page.RouteAsync("**/unroute-one.html", async route =>
            {
                started.TrySetResult(true);
                await release.Task.ConfigureAwait(false);
                await route.FulfillAsync(new() { Body = "<html><body>from-one</body></html>", ContentType = "text/html", Status = 200 }).ConfigureAwait(false);
            }).ConfigureAwait(false);

            Task<IResponse> navigation = page.GoToAsync(TestConstants.ServerUrl + "/unroute-one.html");
            await started.Task.ConfigureAwait(false);

            Task unroute = page.UnrouteAsync("**/unroute-one.html", behavior: UnrouteBehavior.Wait);
            await Task.Delay(300).ConfigureAwait(false);
            Assert.That(unroute.IsCompleted, Is.False);

            release.TrySetResult(true);
            await unroute.ConfigureAwait(false);
            await navigation.ConfigureAwait(false);
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "context UnrouteAllAsync wait waits for pending handlers")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextUnrouteAllWaitShouldWaitForPendingHandlers()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/ctx-unroute-wait.html", WriteServerHtmlAsync);

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);

            await context.RouteAsync("**/ctx-unroute-wait.html", async route =>
            {
                started.TrySetResult(true);
                await release.Task.ConfigureAwait(false);
                await route.FulfillAsync(new() { Body = "<html><body>from-ctx-wait</body></html>", ContentType = "text/html", Status = 200 }).ConfigureAwait(false);
            }).ConfigureAwait(false);

            Task<IResponse> navigation = page.GoToAsync(TestConstants.ServerUrl + "/ctx-unroute-wait.html");
            await started.Task.ConfigureAwait(false);

            Task unroute = context.UnrouteAllAsync(UnrouteBehavior.Wait);
            await Task.Delay(300).ConfigureAwait(false);
            Assert.That(unroute.IsCompleted, Is.False);

            release.TrySetResult(true);
            await unroute.ConfigureAwait(false);
            await navigation.ConfigureAwait(false);
            string body = (await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false)) ?? string.Empty;
            Assert.That(body, Is.EqualTo("from-ctx-wait"));
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "context UnrouteAllAsync default does not wait")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextUnrouteAllDefaultShouldNotWaitForPendingHandlers()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/ctx-unroute-default.html", WriteServerHtmlAsync);

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);

            await context.RouteAsync("**/ctx-unroute-default.html", async route =>
            {
                started.TrySetResult(true);
                await release.Task.ConfigureAwait(false);
                await route.FulfillAsync(new() { Body = "<html><body>from-ctx-default</body></html>", ContentType = "text/html", Status = 200 }).ConfigureAwait(false);
            }).ConfigureAwait(false);

            Task<IResponse> navigation = page.GoToAsync(TestConstants.ServerUrl + "/ctx-unroute-default.html");
            await started.Task.ConfigureAwait(false);

            await context.UnrouteAllAsync(UnrouteBehavior.Default).ConfigureAwait(false);
            Assert.That(release.Task.IsCompleted, Is.False);

            release.TrySetResult(true);
            await navigation.ConfigureAwait(false);
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "context UnrouteAsync wait waits for pending handlers")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextUnrouteWaitShouldWaitForPendingHandlers()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/ctx-unroute-one.html", WriteServerHtmlAsync);

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);

            await context.RouteAsync("**/ctx-unroute-one.html", async route =>
            {
                started.TrySetResult(true);
                await release.Task.ConfigureAwait(false);
                await route.FulfillAsync(new() { Body = "<html><body>from-ctx-one</body></html>", ContentType = "text/html", Status = 200 }).ConfigureAwait(false);
            }).ConfigureAwait(false);

            Task<IResponse> navigation = page.GoToAsync(TestConstants.ServerUrl + "/ctx-unroute-one.html");
            await started.Task.ConfigureAwait(false);

            Task unroute = context.UnrouteAsync("**/ctx-unroute-one.html", behavior: UnrouteBehavior.Wait);
            await Task.Delay(300).ConfigureAwait(false);
            Assert.That(unroute.IsCompleted, Is.False);

            release.TrySetResult(true);
            await unroute.ConfigureAwait(false);
            await navigation.ConfigureAwait(false);
        }
    }
}
