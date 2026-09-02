/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page-autowaiting-basic.spec.ts</c> parity for click
    /// navigation auto-wait and collapsed action/expect call logs.
    /// Skipped (Node-only internals):
    /// <c>should report navigation in the log when clicking anchor</c> uses
    /// <c>__testHookAfterPointerAction</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageAutowaitingBasicParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null && await FixtureReachableAsync(TestConstants.ServerUrl).ConfigureAwait(false))
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19784;
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
                    CrossProcessPrefix = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
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

        [PlaywrightTest("page-autowaiting-basic.spec.ts", "should await navigation when clicking anchor")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAwaitNavigationWhenClickingAnchor()
        {
            EnsureServer();
            Server.Reset();
            List<string> messages = InitServer(Server);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a id=\"anchor\" href=\"" + EmptyPage + "\">empty.html</a>")
                .ConfigureAwait(false);
            await Task.WhenAll(
                ClickThenAsync(page, "a", messages, "click"),
                WaitFrameNavigatedThenAsync(page, messages, "navigated")).ConfigureAwait(false);
            Assert.That(string.Join("|", messages), Is.EqualTo("route|navigated|click"));
        }

        [PlaywrightTest("page-autowaiting-basic.spec.ts", "should not stall on JS navigation link")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotStallOnJSNavigationLink()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"javascript:console.log(1)\">console.log</a>")
                .ConfigureAwait(false);
            await page.ClickAsync("a").ConfigureAwait(false);
        }

        [PlaywrightTest("page-autowaiting-basic.spec.ts", "should await cross-process navigation when clicking anchor")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAwaitCrossProcessNavigationWhenClickingAnchor()
        {
            EnsureServer();
            Server.Reset();
            List<string> messages = InitServer(Server);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + CrossProcessPrefix + "/empty.html\">empty.html</a>")
                .ConfigureAwait(false);
            await Task.WhenAll(
                ClickThenAsync(page, "a", messages, "click"),
                WaitFrameNavigatedThenAsync(page, messages, "navigated")).ConfigureAwait(false);
            Assert.That(string.Join("|", messages), Is.EqualTo("route|navigated|click"));
        }

        [PlaywrightTest("page-autowaiting-basic.spec.ts", "should await form-get on click")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAwaitFormGetOnClick()
        {
            EnsureServer();
            Server.Reset();
            List<string> messages = new List<string>();
            Server.SetRoute("/empty.html?foo=bar", async http =>
            {
                messages.Add("route");
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("<link rel='stylesheet' href='./one-style.css'>")
                    .ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<form action=\"" + EmptyPage + "\" method=\"get\">\n" +
                "      <input name=\"foo\" value=\"bar\">\n" +
                "      <input type=\"submit\" value=\"Submit\">\n" +
                "    </form>").ConfigureAwait(false);
            await Task.WhenAll(
                ClickThenAsync(page, "input[type=submit]", messages, "click"),
                WaitFrameNavigatedThenAsync(page, messages, "navigated")).ConfigureAwait(false);
            Assert.That(string.Join("|", messages), Is.EqualTo("route|navigated|click"));
        }

        [PlaywrightTest("page-autowaiting-basic.spec.ts", "should await form-post on click")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAwaitFormPostOnClick()
        {
            EnsureServer();
            Server.Reset();
            List<string> messages = InitServer(Server);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<form action=\"" + EmptyPage + "\" method=\"post\">\n" +
                "      <input name=\"foo\" value=\"bar\">\n" +
                "      <input type=\"submit\" value=\"Submit\">\n" +
                "    </form>").ConfigureAwait(false);
            await Task.WhenAll(
                ClickThenAsync(page, "input[type=submit]", messages, "click"),
                WaitFrameNavigatedThenAsync(page, messages, "navigated")).ConfigureAwait(false);
            Assert.That(string.Join("|", messages), Is.EqualTo("route|navigated|click"));
        }

        [PlaywrightTest("page-autowaiting-basic.spec.ts", "should work with noWaitAfter: true")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithNoWaitAfterTrue()
        {
            EnsureServer();
            Server.Reset();
            Server.SetRoute("/empty.html", _ => Task.Delay(Timeout.Infinite));
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a id=\"anchor\" href=\"" + EmptyPage + "\">empty.html</a>")
                .ConfigureAwait(false);
            await page.ClickAsync("a", new() { NoWaitAfter = true }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-autowaiting-basic.spec.ts", "should work with dblclick without noWaitAfter when navigation is stalled")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithDblclickWithoutNoWaitAfterWhenNavigationIsStalled()
        {
            EnsureServer();
            Server.Reset();
            Server.SetRoute("/empty.html", _ => Task.Delay(Timeout.Infinite));
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a id=\"anchor\" href=\"" + EmptyPage + "\">empty.html</a>")
                .ConfigureAwait(false);
            await page.DblClickAsync("a").ConfigureAwait(false);
        }

        [PlaywrightTest("page-autowaiting-basic.spec.ts", "should work with waitForLoadState(load)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithWaitForLoadStateLoad()
        {
            EnsureServer();
            Server.Reset();
            List<string> messages = InitServer(Server);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a id=\"anchor\" href=\"" + EmptyPage + "\">empty.html</a>")
                .ConfigureAwait(false);
            await Task.WhenAll(
                ClickThenLoadAsync(page, messages),
                WaitLoadThenAsync(page, messages)).ConfigureAwait(false);
            Assert.That(string.Join("|", messages), Is.EqualTo("route|load|clickload"));
        }

        [PlaywrightTest("page-autowaiting-basic.spec.ts", "should work with goto following click")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithGotoFollowingClick()
        {
            EnsureServer();
            Server.Reset();
            Server.SetRoute("/login.html", async http =>
            {
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("You are logged in").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<form action=\"" + Prefix + "/login.html\" method=\"get\">\n" +
                "      <input type=\"text\">\n" +
                "      <input type=\"submit\" value=\"Submit\">\n" +
                "    </form>").ConfigureAwait(false);
            await page.FillAsync("input[type=text]", "admin").ConfigureAwait(false);
            await page.ClickAsync("input[type=submit]").ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
        }

        [PlaywrightTest("page-autowaiting-basic.spec.ts", "should report and collapse log in action")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportAndCollapseLogInAction()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id='checkbox' type='checkbox' style=\"visibility: hidden\"></input>")
                .ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => page.Locator("input").ClickAsync(new() { Timeout = 5000 }));
            string message = error.Message;
            Assert.That(message, Does.Contain("Call log:"));
            Assert.That(message, Does.Match(new Regex(@"\d+ × waiting for")));
            string log = message.Substring(message.IndexOf("Call log:", StringComparison.Ordinal));
            string[] logLines = log.Split('\n');
            Assert.That(logLines.Length, Is.LessThan(30));
        }

        [PlaywrightTest("page-autowaiting-basic.spec.ts", "should report and collapse log in expect")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportAndCollapseLogInExpect()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id='checkbox' type='checkbox' style=\"visibility: hidden\"></input>")
                .ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => Assertions.Expect(page.Locator("input")).ToBeVisibleAsync(new() { Timeout = 5000 }));
            string message = error.Message;
            Assert.That(message, Does.Contain("Call log:"));
            Assert.That(message, Does.Match(new Regex(@"\d+ × locator resolved to")));
        }

        private static List<string> InitServer(SimpleServer server)
        {
            List<string> messages = new List<string>();
            server.SetRoute("/empty.html", async http =>
            {
                messages.Add("route");
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("<link rel='stylesheet' href='./one-style.css'>")
                    .ConfigureAwait(false);
            });
            return messages;
        }

        private static async Task ClickThenAsync(IPage page, string selector, List<string> messages, string label)
        {
            await page.ClickAsync(selector).ConfigureAwait(false);
            lock (messages)
            {
                messages.Add(label);
            }
        }

        private static async Task WaitFrameNavigatedThenAsync(IPage page, List<string> messages, string label)
        {
            await page.WaitForEventAsync(PageEvent.FrameNavigated).ConfigureAwait(false);
            lock (messages)
            {
                messages.Add(label);
            }
        }

        private static async Task ClickThenLoadAsync(IPage page, List<string> messages)
        {
            await page.ClickAsync("a").ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.Load).ConfigureAwait(false);
            lock (messages)
            {
                messages.Add("clickload");
            }
        }

        private static async Task WaitLoadThenAsync(IPage page, List<string> messages)
        {
            await page.WaitForEventAsync(PageEvent.Load).ConfigureAwait(false);
            lock (messages)
            {
                messages.Add("load");
            }
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
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
    }
}
