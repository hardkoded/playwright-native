/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page-history.spec.ts</c> parity for GoBack, GoForward, and Reload.
    /// </summary>
    [TestFixture]
    public class PageHistoryParityTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static async Task PollAsync(Func<bool> condition)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (!condition() && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.That(condition(), Is.True);
        }

        private static bool IsFileUrlUnsupported(Exception ex)
        {
            string message = ex?.Message ?? string.Empty;
            return message.Contains("file:", StringComparison.OrdinalIgnoreCase)
                || message.Contains("local resource", StringComparison.OrdinalIgnoreCase)
                || message.Contains("ERR_ACCESS_DENIED", StringComparison.OrdinalIgnoreCase)
                || message.Contains("ERR_UNKNOWN_URL_SCHEME", StringComparison.OrdinalIgnoreCase)
                || message.Contains("net::ERR", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Timeout", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHereConsole(IConsoleMessage message, string url)
        {
            if (message == null || string.IsNullOrEmpty(message.Text) || string.IsNullOrEmpty(url))
            {
                return false;
            }

            const string prefix = "here:";
            if (!message.Text.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            return string.Equals(message.Text.Substring(prefix.Length), url, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task ReloadAndSignalAsync(IPage page, TaskCompletionSource<bool> done)
        {
            try
            {
                await page.ReloadAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            done.TrySetResult(true);
        }

        private static async Task GoBackAndSignalAsync(IPage page, TaskCompletionSource<bool> done)
        {
            try
            {
                await page.GoBackAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            done.TrySetResult(true);
        }

        private static async Task GoForwardAndSignalAsync(IPage page, TaskCompletionSource<bool> done)
        {
            try
            {
                await page.GoForwardAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            done.TrySetResult(true);
        }

        private static async Task ClickAndIgnoreAsync(IPage page, string selector)
        {
            try
            {
                await page.ClickAsync(selector).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        private static async Task EvaluateAndIgnoreAsync(IPage page, string expression)
        {
            try
            {
                await page.EvaluateAsync(expression).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        private static async Task WaitForHelloSelectorAsync(IPage page)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            Exception last = null;
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    await page.WaitForSelectorAsync("text=hello", new() { Timeout = 500 }).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex) when (
                    ex is TimeoutException
                    || (ex is PlaywrightSharpException playwrightEx
                        && playwrightEx.Message != null
                        && playwrightEx.Message.Contains("Missing injected script", StringComparison.OrdinalIgnoreCase)))
                {
                    last = ex;
                    await Task.Delay(50).ConfigureAwait(false);
                }
            }

            if (last != null)
            {
                throw last;
            }

            await page.WaitForSelectorAsync("text=hello").ConfigureAwait(false);
        }

        [PlaywrightTest("page-history.spec.ts", "page.goBack should work")]
        [PlaywrightTest("page-history.spec.ts", "page.goBack should work @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task PageGoBackShouldWork()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(await page.GoBackAsync().ConfigureAwait(false), Is.Null);

            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/grid.html").ConfigureAwait(false);

            IResponse response = await page.GoBackAsync().ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Ok, Is.True);
            Assert.That(response.Url, Does.Contain(TestConstants.EmptyPage));

            response = await page.GoForwardAsync().ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Ok, Is.True);
            Assert.That(response.Url, Does.Contain("/grid.html"));

            response = await page.GoForwardAsync().ConfigureAwait(false);
            Assert.That(response, Is.Null);
        }

        [PlaywrightTest("page-history.spec.ts", "page.goBack should work with HistoryAPI")]
        [Test]
        [Timeout(30_000)]
        public async Task PageGoBackShouldWorkWithHistoryAPI()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync("history.pushState({}, '', '/first.html'); history.pushState({}, '', '/second.html');").ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(TestConstants.ServerUrl + "/second.html"));

            await page.GoBackAsync().ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(TestConstants.ServerUrl + "/first.html"));
            await page.GoBackAsync().ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(TestConstants.EmptyPage));
            await page.GoForwardAsync().ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(TestConstants.ServerUrl + "/first.html"));
        }

        [PlaywrightTest("page-history.spec.ts", "page.goBack should work for file urls")]
        [Test]
        [Timeout(30_000)]
        public async Task PageGoBackShouldWorkForFileUrls()
        {
            if (OperatingSystem.IsAndroid())
            {
                Assert.Ignore("No files on Android");
            }

            if (string.Equals(Environment.GetEnvironmentVariable("CHANNEL"), "webkit-wsl", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore("webkit-wsl");
            }

            EnsureServer();
            string assetPath = TestUtils.GetWebServerFile("consolelog.html");
            if (!File.Exists(assetPath))
            {
                Assert.Ignore("file URLs are unsupported here");
            }

            string url1 = new Uri(assetPath).AbsoluteUri;
            string url2 = TestConstants.ServerUrl + "/consolelog.html";

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            try
            {
                Task<IConsoleMessage> firstConsole = page.WaitForConsoleMessageAsync(
                    message => IsHereConsole(message, url1),
                    timeout: 8_000);
                await page.GoToAsync(url1, timeout: 8_000).ConfigureAwait(false);
                await firstConsole.ConfigureAwait(false);
            }
            catch (Exception ex) when (IsFileUrlUnsupported(ex) || ex is TimeoutException || ex is PlaywrightSharpException)
            {
                Assert.Ignore("file URLs are unsupported here");
            }

            await page.SetContentAsync($"<a href='{url2}'>url2</a>").ConfigureAwait(false);
            Assert.That(page.Url.ToLowerInvariant(), Is.EqualTo(url1.ToLowerInvariant()));

            await Task.WhenAll(
                page.WaitForConsoleMessageAsync(message => IsHereConsole(message, url2)),
                page.ClickAsync("a")).ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(url2));

            await Task.WhenAll(
                page.WaitForConsoleMessageAsync(message => IsHereConsole(message, url1)),
                page.GoBackAsync()).ConfigureAwait(false);
            Assert.That(page.Url.ToLowerInvariant(), Is.EqualTo(url1.ToLowerInvariant()));
            Assert.That(await page.EvaluateAsync<double>("window.scrollX").ConfigureAwait(false), Is.EqualTo(0));
            await page.ScreenshotAsync().ConfigureAwait(false);

            await Task.WhenAll(
                page.WaitForConsoleMessageAsync(message => IsHereConsole(message, url2)),
                page.GoForwardAsync()).ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(url2));
            Assert.That(await page.EvaluateAsync<double>("window.scrollX").ConfigureAwait(false), Is.EqualTo(0));
            await page.ScreenshotAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("page-history.spec.ts", "goBack/goForward should work with bfcache-able pages")]
        [Test]
        [Timeout(30_000)]
        public async Task GoBackGoForwardShouldWorkWithBfcacheAblePages()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(TestConstants.ServerUrl + "/cached/bfcached.html").ConfigureAwait(false);
            string href = TestConstants.ServerUrl + "/cached/bfcached.html?foo";
            await page.SetContentAsync("<a href=" + JsonSerializer.Serialize(href) + ">click me</a>").ConfigureAwait(false);
            await page.RunAndWaitForNavigationAsync(() => page.ClickAsync("a")).ConfigureAwait(false);

            IResponse response = await page.GoBackAsync().ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Url, Is.EqualTo(TestConstants.ServerUrl + "/cached/bfcached.html"));
            JsonElement didShow = await page.EvaluateAsync<JsonElement>("window.didShow").ConfigureAwait(false);
            Assert.That(didShow.GetProperty("persisted").GetBoolean(), Is.False);

            response = await page.GoForwardAsync().ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Url, Is.EqualTo(TestConstants.ServerUrl + "/cached/bfcached.html?foo"));
        }

        [PlaywrightTest("page-history.spec.ts", "page.reload should work")]
        [Test]
        [Timeout(30_000)]
        public async Task PageReloadShouldWork()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync("window['_foo'] = 10").ConfigureAwait(false);
            await page.ReloadAsync().ConfigureAwait(false);
            object foo = await page.EvaluateAsync<object>("window['_foo']").ConfigureAwait(false);
            Assert.That(foo, Is.Null);
        }

        [PlaywrightTest("page-history.spec.ts", "page.reload should work with data url")]
        [Test]
        [Timeout(30_000)]
        public async Task PageReloadShouldWorkWithDataUrl()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,hello").ConfigureAwait(false);
            Assert.That(await page.ContentAsync().ConfigureAwait(false), Does.Contain("hello"));
            Assert.That(await page.ReloadAsync().ConfigureAwait(false), Is.Null);
            Assert.That(await page.ContentAsync().ConfigureAwait(false), Does.Contain("hello"));
        }

        [PlaywrightTest("page-history.spec.ts", "page.reload during renderer-initiated navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task PageReloadDuringRendererInitiatedNavigation()
        {
            EnsureServer();
            Server.Reset();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);

                await page.GoToAsync(TestConstants.ServerUrl + "/one-style.html").ConfigureAwait(false);
                await page.SetContentAsync("<form method='POST' action='/post'>Form is here<input type='submit'></form>").ConfigureAwait(false);
                Server.SetRoute("/post", _ => Task.Delay(-1));

                TaskCompletionSource<bool> reloadFailed = new(TaskCreationOptions.RunContinuationsAsynchronously);
                EventHandler<IRequest> onRequest = null;
                onRequest = (_, __) =>
                {
                    page.Request -= onRequest;
                    _ = ReloadAndSignalAsync(page, reloadFailed);
                };
                page.Request += onRequest;

                Task clickTask = ClickAndIgnoreAsync(page, "input[type=submit]");
                await reloadFailed.Task.ConfigureAwait(false);
                await clickTask.ConfigureAwait(false);

                await WaitForHelloSelectorAsync(page).ConfigureAwait(false);
            }
            finally
            {
                Server.Reset();
            }
        }

        [PlaywrightTest("page-history.spec.ts", "page.reload should not resolve with same-document navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task PageReloadShouldNotResolveWithSameDocumentNavigation()
        {
            EnsureServer();
            Server.Reset();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);

                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await page.EvaluateAsync("1").ConfigureAwait(false);

                TaskCompletionSource<bool> arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
                TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
                async Task StallEmptyAsync(HttpContext http)
                {
                    arrived.TrySetResult(true);
                    await release.Task.ConfigureAwait(false);
                    http.Response.StatusCode = 200;
                    http.Response.ContentType = "text/html; charset=utf-8";
                    await http.Response.WriteAsync("hello").ConfigureAwait(false);
                }

                Server.SetRoute("/empty.html", StallEmptyAsync);
                _ = EvaluateAndIgnoreAsync(page, "window.history.pushState({}, '')");
                Task<IRequest> browserRequest = page.WaitForRequestAsync(request => request.Url.Contains("/empty.html", StringComparison.Ordinal), new() { Timeout = 15_000 });
                Task<IResponse> reloadPromise = page.ReloadAsync();
                _ = EvaluateAndIgnoreAsync(page, "window.history.pushState({}, '')");

                await Task.WhenAny(arrived.Task, browserRequest).ConfigureAwait(false);
                release.TrySetResult(true);
                await arrived.Task.ConfigureAwait(false);

                IResponse gotResponse = await reloadPromise.ConfigureAwait(false);
                Assert.That(gotResponse, Is.Not.Null);
                Assert.That(await gotResponse.TextAsync().ConfigureAwait(false), Is.EqualTo("hello"));
            }
            finally
            {
                Server.Reset();
            }
        }

        [PlaywrightTest("page-history.spec.ts", "page.reload should work with same origin redirect")]
        [Test]
        [Timeout(30_000)]
        public async Task PageReloadShouldWorkWithSameOriginRedirect()
        {
            EnsureServer();
            Server.Reset();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);

                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                Server.SetRedirect("/empty.html", TestConstants.ServerUrl + "/title.html");
                await page.ReloadAsync().ConfigureAwait(false);
                Assert.That(page.Url, Is.EqualTo(TestConstants.ServerUrl + "/title.html"));
            }
            finally
            {
                Server.Reset();
            }
        }

        [PlaywrightTest("page-history.spec.ts", "page.reload should work with cross-origin redirect")]
        [Test]
        [Timeout(30_000)]
        public async Task PageReloadShouldWorkWithCrossOriginRedirect()
        {
            EnsureServer();
            Server.Reset();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);

                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                Server.SetRedirect("/empty.html", TestConstants.CrossProcessHttpPrefix + "/title.html");
                await page.ReloadAsync().ConfigureAwait(false);
                Assert.That(page.Url, Is.EqualTo(TestConstants.CrossProcessHttpPrefix + "/title.html"));
            }
            finally
            {
                Server.Reset();
            }
        }

        [PlaywrightTest("page-history.spec.ts", "page.reload should work on a page with a hash")]
        [Test]
        [Timeout(30_000)]
        public async Task PageReloadShouldWorkOnAPageWithAHash()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(TestConstants.EmptyPage + "#hash").ConfigureAwait(false);
            await page.ReloadAsync().ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(TestConstants.EmptyPage + "#hash"));
        }

        [PlaywrightTest("page-history.spec.ts", "page.reload should work on a page with a hash at the end")]
        [Test]
        [Timeout(30_000)]
        public async Task PageReloadShouldWorkOnAPageWithAHashAtTheEnd()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(TestConstants.EmptyPage + "#").ConfigureAwait(false);
            await page.ReloadAsync().ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(TestConstants.EmptyPage + "#"));
        }

        [PlaywrightTest("page-history.spec.ts", "page.goBack during renderer-initiated navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task PageGoBackDuringRendererInitiatedNavigation()
        {
            EnsureServer();
            Server.Reset();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);

                await page.GoToAsync(TestConstants.ServerUrl + "/one-style.html").ConfigureAwait(false);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await page.SetContentAsync("<form method='POST' action='/post'>Form is here<input type='submit'></form>").ConfigureAwait(false);
                Server.SetRoute("/post", _ => Task.Delay(-1));

                TaskCompletionSource<bool> goBackFailed = new(TaskCreationOptions.RunContinuationsAsynchronously);
                EventHandler<IRequest> onRequest = null;
                onRequest = (_, __) =>
                {
                    page.Request -= onRequest;
                    _ = GoBackAndSignalAsync(page, goBackFailed);
                };
                page.Request += onRequest;

                Task clickTask = ClickAndIgnoreAsync(page, "input[type=submit]");
                await goBackFailed.Task.ConfigureAwait(false);
                await clickTask.ConfigureAwait(false);

                await WaitForHelloSelectorAsync(page).ConfigureAwait(false);
            }
            finally
            {
                Server.Reset();
            }
        }

        [PlaywrightTest("page-history.spec.ts", "page.goForward during renderer-initiated navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task PageGoForwardDuringRendererInitiatedNavigation()
        {
            EnsureServer();
            Server.Reset();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);

                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/one-style.html").ConfigureAwait(false);
                await page.GoBackAsync().ConfigureAwait(false);

                await page.SetContentAsync("<form method='POST' action='/post'>Form is here<input type='submit'></form>").ConfigureAwait(false);
                Server.SetRoute("/post", _ => Task.Delay(-1));

                TaskCompletionSource<bool> goForwardFailed = new(TaskCreationOptions.RunContinuationsAsynchronously);
                EventHandler<IRequest> onRequest = null;
                onRequest = (_, __) =>
                {
                    page.Request -= onRequest;
                    _ = GoForwardAndSignalAsync(page, goForwardFailed);
                };
                page.Request += onRequest;

                Task clickTask = ClickAndIgnoreAsync(page, "input[type=submit]");
                await goForwardFailed.Task.ConfigureAwait(false);
                await clickTask.ConfigureAwait(false);

                await WaitForHelloSelectorAsync(page).ConfigureAwait(false);
            }
            finally
            {
                Server.Reset();
            }
        }

        [PlaywrightTest("page-history.spec.ts", "regression test for issue 20791")]
        [Test]
        [Timeout(30_000)]
        public async Task RegressionTestForIssue20791()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("PW_CLOCK"), "frozen", StringComparison.Ordinal))
            {
                Assert.Ignore("PW_CLOCK=frozen");
            }

            EnsureServer();
            Server.Reset();
            try
            {
                Server.SetRoute("/iframe.html", http =>
                {
                    http.Response.ContentType = "text/html; charset=utf-8";
                    return http.Response.WriteAsync(@"
      <!doctype html>
      <script type=""text/javascript"">
        console.log(window.parent.foo);
      </script>
    ");
                });
                Server.SetRoute("/main.html", http =>
                {
                    http.Response.ContentType = "text/html; charset=utf-8";
                    return http.Response.WriteAsync(@"
      <!doctype html>
      <iframe id=myframe src=""about:blank""></iframe>
      <script type=""text/javascript"">
        setTimeout(() => window.foo = 'foo', 0);
        setTimeout(() => myframe.contentDocument.location.href = '" + TestConstants.ServerUrl + @"/iframe.html', 0);
      </script>
    ");
                });

                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);

                List<string> messages = new List<string>();
                page.Console += (_, msg) => messages.Add(msg.Text);
                await page.GoToAsync(TestConstants.ServerUrl + "/main.html").ConfigureAwait(false);
                await PollAsync(() => messages.Count == 1 && messages[0] == "foo").ConfigureAwait(false);
                Assert.That(messages, Is.EqualTo(new[] { "foo" }));
                await page.ReloadAsync().ConfigureAwait(false);
                await PollAsync(() => messages.Count == 2 && messages[0] == "foo" && messages[1] == "foo").ConfigureAwait(false);
                Assert.That(messages, Is.EqualTo(new[] { "foo", "foo" }));
            }
            finally
            {
                Server.Reset();
            }
        }

        [PlaywrightTest("page-history.spec.ts", "should reload proper page")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReloadProperPage()
        {
            EnsureServer();
            Server.Reset();
            try
            {
                int mainRequest = 0;
                int popupRequest = 0;
                Server.SetRoute("/main.html", http =>
                {
                    int n = System.Threading.Interlocked.Increment(ref mainRequest);
                    http.Response.ContentType = "text/html; charset=utf-8";
                    return http.Response.WriteAsync("<!doctype html><h1>main: " + n + "</h1>");
                });
                Server.SetRoute("/popup.html", http =>
                {
                    int n = System.Threading.Interlocked.Increment(ref popupRequest);
                    http.Response.ContentType = "text/html; charset=utf-8";
                    return http.Response.WriteAsync("<!doctype html><h1>popup: " + n + "</h1>");
                });

                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);

                await page.GoToAsync(TestConstants.ServerUrl + "/main.html").ConfigureAwait(false);
                Task<IPage> popupTask = page.WaitForPopupAsync();
                await page.EvaluateAsync<bool>("window.open('/popup.html'), true").ConfigureAwait(false);
                IPage popup = await popupTask.ConfigureAwait(false);
                await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("main: 1").ConfigureAwait(false);
                await Assertions.Expect(popup.Locator("h1")).ToHaveTextAsync("popup: 1").ConfigureAwait(false);

                await page.ReloadAsync().ConfigureAwait(false);
                await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("main: 2").ConfigureAwait(false);
                await Assertions.Expect(popup.Locator("h1")).ToHaveTextAsync("popup: 1").ConfigureAwait(false);

                await popup.ReloadAsync().ConfigureAwait(false);
                await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("main: 2").ConfigureAwait(false);
                await Assertions.Expect(popup.Locator("h1")).ToHaveTextAsync("popup: 2").ConfigureAwait(false);
            }
            finally
            {
                Server.Reset();
            }
        }
    }
}
