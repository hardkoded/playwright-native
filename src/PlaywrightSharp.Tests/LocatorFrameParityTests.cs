/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>locator-frame.spec.ts</c> parity for <see cref="IPage.FrameLocator"/>,
    /// <see cref="ILocator.FrameLocator"/>, <see cref="ILocator.ContentFrame"/>, and
    /// <see cref="IFrameLocator.Owner"/>.
    /// Android <c>fixme</c> on <c>should wait for frame to go</c> is Android-only and is not applied.
    /// Firefox <c>fixme</c> on <c>should work with COEP/COOP/CORP isolated iframe</c> is not applied
    /// on the Chromium/WebKit gate.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LocatorFrameParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static async Task RouteIframeAsync(IPage page)
        {
            await page.RouteAsync("**/empty.html", route => route.FulfillAsync(new() { Body = "<iframe src=\"iframe.html\" name=\"frame1\"></iframe>", ContentType = "text/html" })).ConfigureAwait(false);
            await page.RouteAsync("**/iframe.html", route => route.FulfillAsync(new() { Body = @"
        <html>
          <div>
            <button data-testid=""buttonId"">Hello iframe</button>
            <iframe src=""iframe-2.html""></iframe>
          </div>
          <span>1</span>
          <span>2</span>
          <label for=target>Name</label><input id=target type=text placeholder=Placeholder title=Title alt=Alternative>
        </html>", ContentType = "text/html" })).ConfigureAwait(false);
            await page.RouteAsync("**/iframe-2.html", route => route.FulfillAsync(new() { Body = "<html><button>Hello nested iframe</button></html>", ContentType = "text/html" })).ConfigureAwait(false);
        }

        private static async Task RouteAmbiguousAsync(IPage page)
        {
            await page.RouteAsync("**/empty.html", route => route.FulfillAsync(new() { Body = "<iframe src=\"iframe-1.html\"></iframe>\n             <iframe src=\"iframe-2.html\"></iframe>\n             <iframe src=\"iframe-3.html\"></iframe>", ContentType = "text/html" })).ConfigureAwait(false);
            await page.RouteAsync("**/iframe-*", async route =>
            {
                string path = new Uri(route.Request.Url).AbsolutePath.TrimStart('/');
                await route.FulfillAsync(new() { Body = "<html><button>Hello from " + path + "</button></html>", ContentType = "text/html" }).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private static Task WriteHtmlAsync(HttpContext http, string body)
        {
            http.Response.StatusCode = 200;
            http.Response.ContentType = "text/html; charset=utf-8";
            return http.Response.WriteAsync(body);
        }

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19437;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    server.SetRoute("/empty.html", http => WriteHtmlAsync(http, "<html><body></body></html>"));
                    server.SetRoute("/iframe.html", http => WriteHtmlAsync(
                        http,
                        "<html><div><button data-testid=\"buttonId\">Hello iframe</button><iframe src=\"iframe-2.html\"></iframe></div><span>1</span><span>2</span><label for=target>Name</label><input id=target type=text placeholder=Placeholder title=Title alt=Alternative></html>"));
                    server.SetRoute("/iframe-2.html", http => WriteHtmlAsync(http, "<html><button>Hello nested iframe</button></html>"));
                    server.SetRoute("/iframe-1.html", http => WriteHtmlAsync(http, "<html><button>Hello from iframe-1.html</button></html>"));
                    server.SetRoute("/iframe-3.html", http => WriteHtmlAsync(http, "<html><button>Hello from iframe-3.html</button></html>"));
                    server.SetRoute("/btn.html", http => WriteHtmlAsync(http, "<button onclick=\"window.__clicked=true\">Click target</button>"));
                    _ownedServer = server;
                    string origin = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
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

        [PlaywrightTest("locator-frame.spec.ts", "should work for iframe")]
        [PlaywrightTest("locator-frame.spec.ts", "should work for iframe @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForIframe()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator button = page.FrameLocator("iframe").Locator("button");
            await button.WaitForAsync().ConfigureAwait(false);
            Assert.That(await button.InnerTextAsync().ConfigureAwait(false), Is.EqualTo("Hello iframe"));
            await Assertions.Expect(button).ToHaveTextAsync("Hello iframe").ConfigureAwait(false);
            await button.ClickAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("locator-frame.spec.ts", "should work for nested iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForNestedIframe()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator button = page.FrameLocator("iframe").FrameLocator("iframe").Locator("button");
            await button.WaitForAsync().ConfigureAwait(false);
            Assert.That(await button.InnerTextAsync().ConfigureAwait(false), Is.EqualTo("Hello nested iframe"));
            await Assertions.Expect(button).ToHaveTextAsync("Hello nested iframe").ConfigureAwait(false);
            await button.ClickAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("locator-frame.spec.ts", "should work for $ and $$")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForDollarAndDollarDollar()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator locator = page.FrameLocator("iframe").Locator("button");
            await Assertions.Expect(locator).ToHaveTextAsync("Hello iframe").ConfigureAwait(false);
            ILocator spans = page.FrameLocator("iframe").Locator("span");
            await Assertions.Expect(spans).ToHaveCountAsync(2).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-frame.spec.ts", "should wait for frame")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Exception error = null;
            try
            {
                await page.Locator("body").FrameLocator("iframe").Locator("span").ClickAsync(new() { Timeout = 1000 }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("waiting for locator('body').locator('iframe').contentFrame()"));
        }

        [PlaywrightTest("locator-frame.spec.ts", "should wait for frame 2")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForFrame2()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            async Task NavigateLaterAsync()
            {
                await Task.Delay(300).ConfigureAwait(false);
                try
                {
                    await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            }

            _ = NavigateLaterAsync();
            await page.FrameLocator("iframe").Locator("button").ClickAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("locator-frame.spec.ts", "should wait for frame to go")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForFrameToGo()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            async Task RemoveLaterAsync()
            {
                await Task.Delay(300).ConfigureAwait(false);
                try
                {
                    await page.EvalOnSelectorAsync<object>("iframe", "e => e.remove()").ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            }

            _ = RemoveLaterAsync();
            await Assertions.Expect(page.FrameLocator("iframe").Locator("button")).ToBeHiddenAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("locator-frame.spec.ts", "should not wait for frame")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotWaitForFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Assertions.Expect(page.FrameLocator("iframe").Locator("span")).ToBeHiddenAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("locator-frame.spec.ts", "should not wait for frame 2")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotWaitForFrame2()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Assertions.Expect(page.FrameLocator("iframe").Locator("span")).Not.ToBeVisibleAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("locator-frame.spec.ts", "should not wait for frame 3")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotWaitForFrame3()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Assertions.Expect(page.FrameLocator("iframe").Locator("span")).ToHaveCountAsync(0).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-frame.spec.ts", "should click in lazy iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickInLazyIframe()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.RouteAsync("**/iframe.html", route => route.FulfillAsync(new() { Body = "<html><button>Hello iframe</button></html>", ContentType = "text/html" })).ConfigureAwait(false);

            // empty pge
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            string iframeUrl = new Uri(new Uri(EmptyPage), "iframe.html").AbsoluteUri;
            string iframeUrlJson = System.Text.Json.JsonSerializer.Serialize(iframeUrl);
            await page.EvaluateAsync<object>(
                "(() => { setTimeout(() => { const iframe = document.createElement('iframe'); document.body.appendChild(iframe); setTimeout(() => { document.querySelector('iframe').src = " +
                iframeUrlJson +
                "; }, 500); }, 500); })()").ConfigureAwait(false);

            ILocator button = page.FrameLocator("iframe").Locator("button");
            Task click = button.ClickAsync();
            Task<string> innerText = button.InnerTextAsync();
            Task expect = Assertions.Expect(button).ToHaveTextAsync("Hello iframe");
            await Task.WhenAll(click, innerText, expect).ConfigureAwait(false);
            Assert.That(innerText.Result, Is.EqualTo("Hello iframe"));
        }

        [PlaywrightTest("locator-frame.spec.ts", "waitFor should survive frame reattach")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForShouldSurviveFrameReattach()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator button = page.FrameLocator("iframe").Locator("button", new() { HasText = "Hello nested iframe" });
            Task promise = button.WaitForAsync();
            await page.Locator("iframe").EvaluateAsync<object>("e => e.remove()").ConfigureAwait(false);
            await page.EvaluateAsync<object>(@"(() => {
    const iframe = document.createElement('iframe');
    iframe.src = 'iframe-2.html';
    document.body.appendChild(iframe);
})()").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
        }

        [PlaywrightTest("locator-frame.spec.ts", "click should survive frame reattach")]
        [Test]
        [Timeout(30_000)]
        public async Task ClickShouldSurviveFrameReattach()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator button = page.FrameLocator("iframe").Locator("button", new() { HasText = "Hello nested iframe" });
            Task promise = button.ClickAsync();
            await page.Locator("iframe").EvaluateAsync<object>("e => e.remove()").ConfigureAwait(false);
            await page.EvaluateAsync<object>(@"(() => {
                const iframe = document.createElement('iframe');
                iframe.src = 'iframe-2.html';
                document.body.appendChild(iframe);
            })()").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
        }

        [PlaywrightTest("locator-frame.spec.ts", "click should survive iframe navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ClickShouldSurviveIframeNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator button = page.FrameLocator("iframe").Locator("button", new() { HasText = "Hello nested iframe" });
            Task promise = button.ClickAsync();
            _ = page.Locator("iframe").EvaluateAsync<object>("e => { e.src = 'iframe-2.html'; }");
            await promise.ConfigureAwait(false);
        }

        [PlaywrightTest("locator-frame.spec.ts", "should non work for non-frame")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNonWorkForNonFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);
            ILocator button = page.FrameLocator("div").Locator("button");
            Exception error = null;
            try
            {
                await button.WaitForAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("<div></div>"));
            Assert.That(error.Message, Does.Contain("<iframe> was expected"));
        }

        [PlaywrightTest("locator-frame.spec.ts", "locator.frameLocator should work for iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task LocatorFrameLocatorShouldWorkForIframe()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator button = page.Locator("body").FrameLocator("iframe").Locator("button");
            await button.WaitForAsync().ConfigureAwait(false);
            Assert.That(await button.InnerTextAsync().ConfigureAwait(false), Is.EqualTo("Hello iframe"));
            await Assertions.Expect(button).ToHaveTextAsync("Hello iframe").ConfigureAwait(false);
            await button.ClickAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("locator-frame.spec.ts", "locator.frameLocator should throw on ambiguity")]
        [Test]
        [Timeout(30_000)]
        public async Task LocatorFrameLocatorShouldThrowOnAmbiguity()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteAmbiguousAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator button = page.Locator("body").FrameLocator("iframe").Locator("button");
            Exception error = null;
            try
            {
                await button.WaitForAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Error: strict mode violation: locator('body').locator('iframe') resolved to 3 elements"));
        }

        [PlaywrightTest("locator-frame.spec.ts", "locator.frameLocator should not throw on first/last/nth")]
        [Test]
        [Timeout(30_000)]
        public async Task LocatorFrameLocatorShouldNotThrowOnFirstLastNth()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteAmbiguousAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator button1 = page.Locator("body").FrameLocator("iframe").First.Locator("button");
            await Assertions.Expect(button1).ToHaveTextAsync("Hello from iframe-1.html").ConfigureAwait(false);
            ILocator button2 = page.Locator("body").FrameLocator("iframe").Nth(1).Locator("button");
            await Assertions.Expect(button2).ToHaveTextAsync("Hello from iframe-2.html").ConfigureAwait(false);
            ILocator button3 = page.Locator("body").FrameLocator("iframe").Last.Locator("button");
            await Assertions.Expect(button3).ToHaveTextAsync("Hello from iframe-3.html").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-frame.spec.ts", "getBy coverage")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByCoverage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator button1 = page.FrameLocator("iframe").GetByRole("button");
            ILocator button2 = page.FrameLocator("iframe").GetByText("Hello");
            ILocator button3 = page.FrameLocator("iframe").GetByTestId("buttonId");
            await Assertions.Expect(button1).ToHaveTextAsync("Hello iframe").ConfigureAwait(false);
            await Assertions.Expect(button2).ToHaveTextAsync("Hello iframe").ConfigureAwait(false);
            await Assertions.Expect(button3).ToHaveTextAsync("Hello iframe").ConfigureAwait(false);
            ILocator input1 = page.FrameLocator("iframe").GetByLabel("Name");
            await Assertions.Expect(input1).ToHaveValueAsync(string.Empty).ConfigureAwait(false);
            ILocator input2 = page.FrameLocator("iframe").GetByPlaceholder("Placeholder");
            await Assertions.Expect(input2).ToHaveValueAsync(string.Empty).ConfigureAwait(false);
            ILocator input3 = page.FrameLocator("iframe").GetByAltText("Alternative");
            await Assertions.Expect(input3).ToHaveValueAsync(string.Empty).ConfigureAwait(false);
            ILocator input4 = page.FrameLocator("iframe").GetByTitle("Title");
            await Assertions.Expect(input4).ToHaveValueAsync(string.Empty).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-frame.spec.ts", "wait for hidden should succeed when frame is not in dom")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForHiddenShouldSucceedWhenFrameIsNotInDom()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            ILocator button = page.FrameLocator("iframe1").Locator("button");
            Assert.That(await button.IsHiddenAsync().ConfigureAwait(false), Is.True);
            await button.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 1000 }).ConfigureAwait(false);
            await button.WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = 1000 }).ConfigureAwait(false);
            Exception error = null;
            try
            {
                await button.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 1000 }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Timeout 1000ms exceeded"));
        }

        [PlaywrightTest("locator-frame.spec.ts", "should work with COEP/COOP/CORP isolated iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithCoepCoopCorpIsolatedIframe()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Dictionary<string, string> isolatedHeaders = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["cross-origin-embedder-policy"] = "require-corp",
                ["cross-origin-opener-policy"] = "same-origin",
                ["cross-origin-resource-policy"] = "cross-origin",
            };
            string btnUrl = new Uri(new Uri(EmptyPage), "btn.html").AbsoluteUri;
            await page.RouteAsync("**/empty.html", route => route.FulfillAsync(new() { Body = "<iframe src=\"" + btnUrl + "\" allow=\"cross-origin-isolated; fullscreen\" sandbox=\"allow-same-origin allow-scripts allow-popups\" ></iframe>", ContentType = "text/html", Headers = isolatedHeaders })).ConfigureAwait(false);
            await page.RouteAsync("**/btn.html", route => route.FulfillAsync(new() { Body = "<button onclick=\"window.__clicked=true\">Click target</button>", ContentType = "text/html", Headers = isolatedHeaders })).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.FrameLocator("iframe").GetByRole("button").ClickAsync().ConfigureAwait(false);
            List<IFrame> frames = new List<IFrame>(page.Frames);
            Assert.That(frames.Count, Is.GreaterThan(1));
            Assert.That(await frames[1].EvaluateAsync<bool>("(() => window['__clicked'] === true)()").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("locator-frame.spec.ts", "locator.contentFrame should work")]
        [Test]
        [Timeout(30_000)]
        public async Task LocatorContentFrameShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator locator = page.Locator("iframe");
            IFrameLocator frameLocator = locator.ContentFrame;
            ILocator button = frameLocator.Locator("button");
            Assert.That(await button.InnerTextAsync().ConfigureAwait(false), Is.EqualTo("Hello iframe"));
            await Assertions.Expect(button).ToHaveTextAsync("Hello iframe").ConfigureAwait(false);
            await button.ClickAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("locator-frame.spec.ts", "frameLocator.owner should work")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameLocatorOwnerShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrameLocator frameLocator = page.FrameLocator("iframe");
            ILocator locator = frameLocator.Owner;
            await Assertions.Expect(locator).ToBeVisibleAsync().ConfigureAwait(false);
            Assert.That(await locator.GetAttributeAsync("name").ConfigureAwait(false), Is.EqualTo("frame1"));
        }
    }
}
