/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/chromium/oopif.spec.ts</c> parity. Official
    /// <c>--site-per-process</c> out-of-process iframe sessions. Chromium-only;
    /// WebKit official skip. Do not edit leftover <c>ConnectOverCdpTests</c>
    /// or leftover CDP session tests. Skipped:
    /// <c>should get the proper viewport</c> (official <c>it.fixme</c>);
    /// <c>should take screenshot</c> (official <c>toMatchSnapshot</c> pixel-diff);
    /// <c>should report google.com frame with headed</c> (official headed /
    /// headless-shell skip).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryOopifParityTests : PageTestEx
    {
        private static readonly string[] SitePerProcessArgs = { "--site-per-process" };

        private static SimpleServer Server => TestServerSetup.Server;

        [SetUp]
        public void SkipNonChromium()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Official Chromium-only oopif.spec.ts.");
            }
        }

        [TearDown]
        public void ResetServer()
        {
            Server?.Reset();
        }

        [PlaywrightTest("oopif.spec.ts", "should report oopif frames")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportOopifFrames()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html").ConfigureAwait(false);
            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            Assert.That(page.Frames.Count, Is.EqualTo(2));
            string href = await FrameAt(page, 1).EvaluateAsync<string>("() => '' + location.href").ConfigureAwait(false);
            Assert.That(href, Is.EqualTo(TestConstants.CrossProcessHttpPrefix + "/grid.html"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should handle oopif detach")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHandleOopifDetach()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html").ConfigureAwait(false);
            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            Assert.That(page.Frames.Count, Is.EqualTo(2));
            IFrame frame = FrameAt(page, 1);
            string href = await frame.EvaluateAsync<string>("() => '' + location.href").ConfigureAwait(false);
            Assert.That(href, Is.EqualTo(TestConstants.CrossProcessHttpPrefix + "/grid.html"));
            Task<IFrame> detachedTask = page.WaitForEventAsync(PageEvent.FrameDetached);
            await page.EvaluateAsync("() => document.querySelector('iframe').remove()").ConfigureAwait(false);
            IFrame detachedFrame = await detachedTask.ConfigureAwait(false);
            Assert.That(detachedFrame, Is.SameAs(frame));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should remove workers of a detached oopif")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveWorkersOfADetachedOopif()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Task<IWorker> workerTask = page.WaitForEventAsync(PageEvent.Worker);
            await AttachFrameAsync(page, "frame1", TestConstants.CrossProcessHttpPrefix + "/worker/worker.html").ConfigureAwait(false);
            IWorker worker = await workerTask.ConfigureAwait(false);
            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            Assert.That(page.Workers.Count, Is.EqualTo(1));
            TaskCompletionSource<bool> closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            worker.Close += (_, _) => closed.TrySetResult(true);
            await Task.WhenAll(
                closed.Task,
                page.GoToAsync(TestConstants.ServerUrl + "/title.html")).ConfigureAwait(false);
            Assert.That(page.Workers.Count, Is.EqualTo(0));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should not hang in unrouteAll when oopif worker is gone")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotHangInUnrouteAllWhenOopifWorkerIsGone()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await context.RouteAsync("**/*", route => route.ContinueAsync()).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Task<IWorker> workerTask = page.WaitForEventAsync(PageEvent.Worker);
            await AttachFrameAsync(page, "frame1", TestConstants.CrossProcessHttpPrefix + "/worker/worker.html").ConfigureAwait(false);
            await workerTask.ConfigureAwait(false);
            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.CrossProcessHttpPrefix + "/title.html").ConfigureAwait(false);
            await context.UnrouteAllAsync().ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should handle remote -> local -> remote transitions")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHandleRemoteLocalRemoteTransitions()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html").ConfigureAwait(false);
            Assert.That(page.Frames.Count, Is.EqualTo(2));
            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            string remoteHref = await FrameAt(page, 1).EvaluateAsync<string>("() => '' + location.href").ConfigureAwait(false);
            Assert.That(remoteHref, Is.EqualTo(TestConstants.CrossProcessHttpPrefix + "/grid.html"));
            await Task.WhenAll(
                FrameAt(page, 1).WaitForNavigationAsync(),
                page.EvaluateAsync("goLocal()")).ConfigureAwait(false);
            string localHref = await FrameAt(page, 1).EvaluateAsync<string>("() => '' + location.href").ConfigureAwait(false);
            Assert.That(localHref, Is.EqualTo(TestConstants.ServerUrl + "/grid.html"));
            await AssertOopifCountAsync(browser, 0).ConfigureAwait(false);
            await Task.WhenAll(
                FrameAt(page, 1).WaitForNavigationAsync(),
                page.EvaluateAsync("goRemote()")).ConfigureAwait(false);
            string remoteAgain = await FrameAt(page, 1).EvaluateAsync<string>("() => '' + location.href").ConfigureAwait(false);
            Assert.That(remoteAgain, Is.EqualTo(TestConstants.CrossProcessHttpPrefix + "/grid.html"));
            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should get the proper viewport")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldGetTheProperViewport()
        {
            Assert.Ignore("Official it.fixme().");
        }

        [PlaywrightTest("oopif.spec.ts", "should expose function")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldExposeFunction()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html").ConfigureAwait(false);
            Assert.That(page.Frames.Count, Is.EqualTo(2));
            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            IFrame oopif = FrameAt(page, 1);
            await page.ExposeFunctionAsync<int, int, int>("mul", (a, b) => a * b).ConfigureAwait(false);
            int result = await oopif.EvaluateAsync<int>("async function() { return await window['mul'](9, 4); }").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(36));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should emulate media")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmulateMedia()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html").ConfigureAwait(false);
            Assert.That(page.Frames.Count, Is.EqualTo(2));
            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            IFrame oopif = FrameAt(page, 1);
            Assert.That(
                await oopif.EvaluateAsync<bool>("() => matchMedia('(prefers-color-scheme: dark)').matches").ConfigureAwait(false),
                Is.False);
            await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark }).ConfigureAwait(false);
            Assert.That(
                await oopif.EvaluateAsync<bool>("() => matchMedia('(prefers-color-scheme: dark)').matches").ConfigureAwait(false),
                Is.True);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should emulate offline")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmulateOffline()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html").ConfigureAwait(false);
            Assert.That(page.Frames.Count, Is.EqualTo(2));
            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            IFrame oopif = FrameAt(page, 1);
            Assert.That(await oopif.EvaluateAsync<bool>("() => navigator.onLine").ConfigureAwait(false), Is.True);
            await page.Context.SetOfflineAsync(true).ConfigureAwait(false);
            Assert.That(await oopif.EvaluateAsync<bool>("() => navigator.onLine").ConfigureAwait(false), Is.False);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should support context options")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportContextOptions()
        {
            EnsureServer();
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            BrowserContextOptions iPhone = Playwright.Devices["iPhone 6"];
            BrowserContextOptions options = new BrowserContextOptions(iPhone)
            {
                TimezoneId = "America/Jamaica",
                Locale = "fr-CH",
                UserAgent = "UA",
            };
            IBrowserContext context = await browser.NewContextAsync(options).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<string> requestTask = Server.WaitForRequest(
                "/grid.html",
                request => request.Headers["user-agent"].ToString());
            await Task.WhenAll(
                requestTask,
                page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html")).ConfigureAwait(false);
            Assert.That(page.Frames.Count, Is.EqualTo(2));
            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            IFrame oopif = FrameAt(page, 1);
            Assert.That(
                await oopif.EvaluateAsync<bool>("() => 'ontouchstart' in window").ConfigureAwait(false),
                Is.True);
            Assert.That(
                await oopif.EvaluateAsync<string>("() => new Date(1479579154987).toString()").ConfigureAwait(false),
                Is.EqualTo("Sat Nov 19 2016 13:12:34 GMT-0500 (heure normale de l’Est nord-américain)"));
            Assert.That(
                await oopif.EvaluateAsync<string>("() => navigator.language").ConfigureAwait(false),
                Is.EqualTo("fr-CH"));
            Assert.That(
                await oopif.EvaluateAsync<string>("() => navigator.userAgent").ConfigureAwait(false),
                Is.EqualTo("UA"));
            Assert.That(requestTask.Result, Is.EqualTo("UA"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should respect route")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRespectRoute()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            bool intercepted = false;
            await page.RouteAsync("**/digits/0.png", route =>
            {
                intercepted = true;
                return route.ContinueAsync();
            }).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html").ConfigureAwait(false);
            Assert.That(page.Frames.Count, Is.EqualTo(2));
            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            Assert.That(intercepted, Is.True);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should take screenshot")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldTakeScreenshot()
        {
            Assert.Ignore("Official toMatchSnapshot('screenshot-oopif.png') pixel-diff; playbook skip.");
        }

        [PlaywrightTest("oopif.spec.ts", "should load oopif iframes with subresources and route")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldLoadOopifIframesWithSubresourcesAndRoute()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.RouteAsync("**/*", route => route.ContinueAsync()).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html").ConfigureAwait(false);
            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should report main requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportMainRequests()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            List<IFrame> requestFrames = new List<IFrame>();
            page.Request += (_, r) => requestFrames.Add(r.Frame);
            List<IFrame> finishedFrames = new List<IFrame>();
            page.RequestFinished += (_, r) => finishedFrames.Add(r.Frame);

            await page.GoToAsync(TestConstants.ServerUrl + "/empty.html").ConfigureAwait(false);
            IFrame main = page.MainFrame;
            await main.EvaluateAsync(
                "url => { const iframe = document.createElement('iframe'); iframe.src = url; document.body.appendChild(iframe); return new Promise(f => iframe.onload = f); }",
                TestConstants.CrossProcessHttpPrefix + "/empty.html").ConfigureAwait(false);
            Assert.That(page.Frames.Count, Is.EqualTo(2));
            IFrame child = FirstChild(main);
            await child.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);

            await child.EvaluateAsync(
                "url => { const iframe = document.createElement('iframe'); iframe.src = url; document.body.appendChild(iframe); return new Promise(f => iframe.onload = f); }",
                TestConstants.ServerUrl + "/empty.html").ConfigureAwait(false);
            Assert.That(page.Frames.Count, Is.EqualTo(3));
            IFrame grandChild = FirstChild(child);
            await grandChild.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);

            await AssertOopifCountAsync(browser, 2).ConfigureAwait(false);
            Assert.That(requestFrames[0], Is.SameAs(main));
            Assert.That(finishedFrames[0], Is.SameAs(main));
            Assert.That(requestFrames[1], Is.SameAs(child));
            Assert.That(finishedFrames[1], Is.SameAs(child));
            Assert.That(requestFrames[2], Is.SameAs(grandChild));
            Assert.That(finishedFrames[2], Is.SameAs(grandChild));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should support exposeFunction")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportExposeFunction()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.Context.ExposeFunctionAsync<int, int>("dec", a => a - 1).ConfigureAwait(false);
            await page.ExposeFunctionAsync<int, int>("inc", a => a + 1).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html").ConfigureAwait(false);
            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            Assert.That(page.Frames.Count, Is.EqualTo(2));
            Assert.That(await FrameAt(page, 0).EvaluateAsync<int>("() => window['inc'](3)").ConfigureAwait(false), Is.EqualTo(4));
            Assert.That(await FrameAt(page, 1).EvaluateAsync<int>("() => window['inc'](4)").ConfigureAwait(false), Is.EqualTo(5));
            Assert.That(await FrameAt(page, 0).EvaluateAsync<int>("() => window['dec'](3)").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await FrameAt(page, 1).EvaluateAsync<int>("() => window['dec'](4)").ConfigureAwait(false), Is.EqualTo(3));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should support addInitScript")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportAddInitScript()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.Context.AddInitScriptAsync("() => { window['bar'] = 17; }").ConfigureAwait(false);
            await page.AddInitScriptAsync("() => { window['foo'] = 42; }").ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html").ConfigureAwait(false);
            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            Assert.That(page.Frames.Count, Is.EqualTo(2));
            Assert.That(await FrameAt(page, 0).EvaluateAsync<int>("() => window['foo']").ConfigureAwait(false), Is.EqualTo(42));
            Assert.That(await FrameAt(page, 1).EvaluateAsync<int>("() => window['foo']").ConfigureAwait(false), Is.EqualTo(42));
            Assert.That(await FrameAt(page, 0).EvaluateAsync<int>("() => window['bar']").ConfigureAwait(false), Is.EqualTo(17));
            Assert.That(await FrameAt(page, 1).EvaluateAsync<int>("() => window['bar']").ConfigureAwait(false), Is.EqualTo(17));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should click a button when it overlays oopif")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickAButtonWhenItOverlaysOopif()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/button-overlay-oopif.html").ConfigureAwait(false);
            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            await page.ClickAsync("button").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("() => window['BUTTON_CLICKED']").ConfigureAwait(false), Is.True);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should report google.com frame with headed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldReportGoogleComFrameWithHeaded()
        {
            Assert.Ignore("Official skip: Headless Shell does not support headed mode");
        }

        [PlaywrightTest("oopif.spec.ts", "ElementHandle.boundingBox() should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ElementHandleBoundingBoxShouldWork()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>(
                "iframe",
                "iframe => { iframe.style.width = '520px'; iframe.style.height = '520px'; iframe.style.marginLeft = '42px'; iframe.style.marginTop = '17px'; }").ConfigureAwait(false);
            await FrameAt(page, 1).GoToAsync(FrameAt(page, 1).Url).ConfigureAwait(false);

            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            IElementHandle handle1 = await FrameAt(page, 1).QuerySelectorAsync(".box:nth-of-type(13)").ConfigureAwait(false);
            await PollBoundingBoxAsync(handle1, 100 + 42, 50 + 17, 50, 50).ConfigureAwait(false);

            await Task.WhenAll(
                FrameAt(page, 1).WaitForNavigationAsync(),
                page.EvaluateAsync("goLocal()")).ConfigureAwait(false);
            await AssertOopifCountAsync(browser, 0).ConfigureAwait(false);
            IElementHandle handle2 = await FrameAt(page, 1).QuerySelectorAsync(".box:nth-of-type(13)").ConfigureAwait(false);
            await PollBoundingBoxAsync(handle2, 100 + 42, 50 + 17, 50, 50).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should click")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClick()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>(
                "iframe",
                "iframe => { iframe.style.width = '500px'; iframe.style.height = '500px'; iframe.style.marginLeft = '102px'; iframe.style.marginTop = '117px'; }").ConfigureAwait(false);
            await FrameAt(page, 1).GoToAsync(FrameAt(page, 1).Url).ConfigureAwait(false);

            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            IElementHandle handle1 = await FrameAt(page, 1).QuerySelectorAsync(".box:nth-of-type(13)").ConfigureAwait(false);
            await handle1.EvaluateAsync("div => div.addEventListener('click', () => window['_clicked'] = true, false)").ConfigureAwait(false);
            await handle1.ClickAsync().ConfigureAwait(false);
            Assert.That(await handle1.EvaluateAsync<bool>("() => window['_clicked']").ConfigureAwait(false), Is.True);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "contentFrame should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContentFrameShouldWork()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html").ConfigureAwait(false);
            Assert.That(page.Frames.Count, Is.EqualTo(2));
            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            Assert.That(await page.Locator("iframe").ContentFrame.Locator("div").CountAsync().ConfigureAwait(false), Is.EqualTo(200));
            IElementHandle oopif = await page.QuerySelectorAsync("iframe").ConfigureAwait(false);
            IFrame content = await oopif.ContentFrameAsync().ConfigureAwait(false);
            Assert.That(await content.Locator("div").CountAsync().ConfigureAwait(false), Is.EqualTo(200));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should allow cdp sessions on oopifs")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAllowCdpSessionsOnOopifs()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html").ConfigureAwait(false);
            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            Assert.That(page.Frames.Count, Is.EqualTo(2));
            string href = await FrameAt(page, 1).EvaluateAsync<string>("() => '' + location.href").ConfigureAwait(false);
            Assert.That(href, Is.EqualTo(TestConstants.CrossProcessHttpPrefix + "/grid.html"));

            ICDPSession parentCdp = await page.Context.NewCDPSessionAsync(FrameAt(page, 0)).ConfigureAwait(false);
            JsonElement? parent = await parentCdp.SendAsync("DOM.getDocument", new { pierce = true, depth = -1 }).ConfigureAwait(false);
            Assert.That(JsonSerializer.Serialize(parent), Does.Not.Contain("./digits/1.png"));

            ICDPSession oopifCdp = await page.Context.NewCDPSessionAsync(FrameAt(page, 1)).ConfigureAwait(false);
            JsonElement? oopif = await oopifCdp.SendAsync("DOM.getDocument", new { pierce = true, depth = -1 }).ConfigureAwait(false);
            Assert.That(JsonSerializer.Serialize(oopif), Does.Contain("./digits/1.png"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should emit filechooser event for iframe")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmitFilechooserEventForIframe()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            Task<IFileChooser> chooserTask = page.WaitForFileChooserAsync();
            await page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html").ConfigureAwait(false);
            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            Assert.That(page.Frames.Count, Is.EqualTo(2));
            IFrame frame = FrameAt(page, 1);
            await frame.SetContentAsync("<input type=file>").ConfigureAwait(false);
            await frame.ClickAsync("input").ConfigureAwait(false);
            IFileChooser chooser = await chooserTask.ConfigureAwait(false);
            Assert.That(chooser, Is.Not.Null);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should be able to click in iframe")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToClickInIframe()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html").ConfigureAwait(false);
            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            Assert.That(page.Frames.Count, Is.EqualTo(2));
            IFrame frame = FrameAt(page, 1);
            await frame.SetContentAsync("<button onclick=\"console.log('clicked')\">OK</button>").ConfigureAwait(false);
            Task<IConsoleMessage> messageTask = page.WaitForEventAsync(PageEvent.Console);
            await frame.ClickAsync("button").ConfigureAwait(false);
            IConsoleMessage message = await messageTask.ConfigureAwait(false);
            Assert.That(message.Text, Is.EqualTo("clicked"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should not throw on exposeFunction when oopif detaches")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrowOnExposeFunctionWhenOopifDetaches()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html").ConfigureAwait(false);
            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            await Task.WhenAll(
                page.ExposeFunctionAsync<int>("myFunc", () => 2022),
                page.EvaluateAsync("() => document.querySelector('iframe').remove()")).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.myFunc()").ConfigureAwait(false), Is.EqualTo(2022));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should intercept response body from oopif")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInterceptResponseBodyFromOopif()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            Task<IResponse> responseTask = page.WaitForResponseAsync("**/grid.html");
            await page.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html").ConfigureAwait(false);
            IResponse response = await responseTask.ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.Not.Null.And.Not.Empty);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("oopif.spec.ts", "should allow to re-connect to OOPIFs with CDP when iframes were there already")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAllowToReConnectToOopifsWithCdpWhenIframesWereThereAlready()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PWTEST_CHANNEL")))
            {
                Assert.Ignore("Official skip: Test default channel only");
            }

            // Official uses 10123 + parallelIndex * 4 so workers do not collide.
            int cdpPort = FreeCdpPort();
            List<string> hostArgs = new List<string>(SitePerProcessArgs)
            {
                "--remote-debugging-port=" + cdpPort,
            };
            await using IBrowser hostBrowser = await BrowserLauncher.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Args = hostArgs,
            }).ConfigureAwait(false);
            IPage hostPage = await hostBrowser.NewPageAsync().ConfigureAwait(false);
            await hostPage.GoToAsync(TestConstants.ServerUrl + "/dynamic-oopif.html").ConfigureAwait(false);
            Assert.That(hostPage.Frames.Count, Is.EqualTo(2));

            await using IBrowser browser = await Playwright.Chromium.ConnectOverCDPAsync("http://localhost:" + cdpPort).ConfigureAwait(false);
            IPage page = FirstPage(browser.Contexts);
            Assert.That(page.Frames.Count, Is.EqualTo(2));
            await AssertOopifCountAsync(browser, 1).ConfigureAwait(false);
            string href = await FrameAt(page, 1).EvaluateAsync<string>("() => '' + location.href").ConfigureAwait(false);
            Assert.That(href, Is.EqualTo(TestConstants.CrossProcessHttpPrefix + "/grid.html"));
            await browser.CloseAsync().ConfigureAwait(false);
            await hostBrowser.CloseAsync().ConfigureAwait(false);
        }

        private static int FreeCdpPort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static Task<IBrowser> LaunchAsync()
            => BrowserLauncher.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Args = SitePerProcessArgs,
            });

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is not running.");
            }
        }

        private static IFrame FrameAt(IPage page, int index)
        {
            List<IFrame> frames = new List<IFrame>(page.Frames);
            return frames[index];
        }

        private static IFrame FirstChild(IFrame frame)
        {
            foreach (IFrame child in frame.ChildFrames)
            {
                return child;
            }

            return null;
        }

        private static IPage FirstPage(IEnumerable<IBrowserContext> contexts)
        {
            foreach (IBrowserContext context in contexts)
            {
                foreach (IPage page in context.Pages)
                {
                    return page;
                }
            }

            return null;
        }

        private static async Task<IFrame> AttachFrameAsync(IPage page, string name, string url)
        {
            string nameJson = JsonSerializer.Serialize(name);
            string urlJson = JsonSerializer.Serialize(url);
            await page.EvaluateAsync<object>(
                "async () => { const f = document.createElement('iframe'); f.name = " +
                nameJson + "; f.id = " + nameJson + "; f.src = " + urlJson +
                "; document.body.appendChild(f); await new Promise(x => f.onload = x); }").ConfigureAwait(false);
            return page.Frame(name);
        }

        private static async Task AssertOopifCountAsync(IBrowser browser, int count)
        {
            if (browser.BrowserType.Name != "chromium")
            {
                return;
            }

            Assert.That(await CountOopifsAsync(browser).ConfigureAwait(false), Is.EqualTo(count));
        }

        private static async Task<int> CountOopifsAsync(IBrowser browser)
        {
            ICDPSession browserSession = await browser.NewBrowserCDPSessionAsync().ConfigureAwait(false);
            List<JsonElement> oopifs = new List<JsonElement>();
            browserSession.Event("Target.targetCreated").OnEvent += (_, parameters) =>
            {
                if (!parameters.HasValue)
                {
                    return;
                }

                if (parameters.Value.TryGetProperty("targetInfo", out JsonElement targetInfo)
                    && targetInfo.TryGetProperty("type", out JsonElement type)
                    && type.GetString() == "iframe")
                {
                    oopifs.Add(targetInfo);
                }
            };
            await browserSession.SendAsync("Target.setDiscoverTargets", new { discover = true }).ConfigureAwait(false);
            await browserSession.DetachAsync().ConfigureAwait(false);
            return oopifs.Count;
        }

        private static async Task PollBoundingBoxAsync(IElementHandle handle, float x, float y, float width, float height)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            ElementHandleBoundingBoxResult last = null;
            while (DateTime.UtcNow < deadline)
            {
                last = await handle.BoundingBoxAsync().ConfigureAwait(false);
                if (last != null
                    && last.X == x
                    && last.Y == y
                    && last.Width == width
                    && last.Height == height)
                {
                    return;
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            Assert.That(last, Is.Not.Null);
            Assert.That(last.X, Is.EqualTo(x));
            Assert.That(last.Y, Is.EqualTo(y));
            Assert.That(last.Width, Is.EqualTo(width));
            Assert.That(last.Height, Is.EqualTo(height));
        }
    }
}
