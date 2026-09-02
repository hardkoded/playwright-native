/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/chromium/session.spec.ts</c> parity. Leftover
    /// <c>CDPSessionTests.FrameSessionShouldEvaluate</c> aligned to the
    /// official in-process iframe throw. Do not edit leftover
    /// <c>ConnectOverCdpTests</c>. Skipped (Node channel type validation):
    /// <c>should only accept a page or frame</c> (<c>page: expected Page or
    /// Frame</c>; C# <see cref="IBrowserContext.NewCDPSessionAsync(IPage)"/> /
    /// <see cref="IBrowserContext.NewCDPSessionAsync(IFrame)"/> are already typed).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibrarySessionParityTests : PageTestEx
    {
        private const string TargetClosedErrorMessage = "Target page, context or browser has been closed";

        [SetUp]
        public void SkipNonChromium()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Official Chromium-only session.spec.ts.");
            }
        }

        [PlaywrightTest("session.spec.ts", "should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            ICDPSession client = await page.Context.NewCDPSessionAsync(page).ConfigureAwait(false);
            await Task.WhenAll(
                client.SendAsync("Runtime.enable"),
                client.SendAsync("Runtime.evaluate", new { expression = "window.foo = \"bar\"" })).ConfigureAwait(false);
            string foo = await page.EvaluateAsync<string>("() => window['foo']").ConfigureAwait(false);
            Assert.That(foo, Is.EqualTo("bar"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("session.spec.ts", "should send events")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendEvents()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            ICDPSession client = await page.Context.NewCDPSessionAsync(page).ConfigureAwait(false);
            await client.SendAsync("Network.enable").ConfigureAwait(false);
            List<JsonElement?> events = new();
            client.Event("Network.requestWillBeSent").OnEvent += (_, parameters) =>
            {
                events.Add(parameters);
            };
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(events.Count, Is.EqualTo(1));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("session.spec.ts", "should only accept a page or frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldOnlyAcceptAPageOrFrame()
        {
            Assert.Ignore("Official Node channel validation: page: expected Page or Frame.");
        }

        [PlaywrightTest("session.spec.ts", "should enable and disable domains independently")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEnableAndDisableDomainsIndependently()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            ICDPSession client = await page.Context.NewCDPSessionAsync(page).ConfigureAwait(false);
            await client.SendAsync("Runtime.enable").ConfigureAwait(false);
            await client.SendAsync("Debugger.enable").ConfigureAwait(false);
            await page.Coverage().StartJSCoverageAsync().ConfigureAwait(false);
            await page.Coverage().StopJSCoverageAsync().ConfigureAwait(false);
            TaskCompletionSource<bool> parsed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            client.Event("Debugger.scriptParsed").OnEvent += (_, parameters) =>
            {
                if (parameters.HasValue
                    && parameters.Value.TryGetProperty("url", out JsonElement url)
                    && url.GetString() == "foo.js")
                {
                    parsed.TrySetResult(true);
                }
            };
            await Task.WhenAll(
                parsed.Task,
                page.EvaluateAsync("//# sourceURL=foo.js")).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("session.spec.ts", "should be able to detach session")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToDetachSession()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            ICDPSession client = await page.Context.NewCDPSessionAsync(page).ConfigureAwait(false);
            await client.SendAsync("Runtime.enable").ConfigureAwait(false);
            JsonElement? evalResponse = await client.SendAsync(
                "Runtime.evaluate",
                new { expression = "1 + 2", returnByValue = true }).ConfigureAwait(false);
            Assert.That(evalResponse.Value.GetProperty("result").GetProperty("value").GetInt32(), Is.EqualTo(3));
            await client.DetachAsync().ConfigureAwait(false);
            Exception error = Assert.CatchAsync(
                () => client.SendAsync("Runtime.evaluate", new { expression = "3 + 1", returnByValue = true }));
            Assert.That(error.Message, Does.Contain(TargetClosedErrorMessage));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("session.spec.ts", "should throw nice errors")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowNiceErrors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            ICDPSession client = await page.Context.NewCDPSessionAsync(page).ConfigureAwait(false);
            Exception error = await TheSourceOfTheProblemsAsync(client).ConfigureAwait(false);
            Assert.That(error.StackTrace, Does.Contain("TheSourceOfTheProblems"));
            Assert.That(error.Message, Does.Contain("ThisCommand.DoesNotExist"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("session.spec.ts", "should work with main frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithMainFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            ICDPSession client = await page.Context.NewCDPSessionAsync(page.MainFrame).ConfigureAwait(false);
            await Task.WhenAll(
                client.SendAsync("Runtime.enable"),
                client.SendAsync("Runtime.evaluate", new { expression = "window.foo = \"bar\"" })).ConfigureAwait(false);
            string foo = await page.EvaluateAsync<string>("() => window['foo']").ConfigureAwait(false);
            Assert.That(foo, Is.EqualTo("bar"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("session.spec.ts", "should throw if target is part of main")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowIfTargetIsPartOfMain()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/frames/one-frame.html").ConfigureAwait(false);
            List<IFrame> frames = new List<IFrame>(page.Frames);
            Assert.That(frames[0].Url, Does.Contain("/frames/one-frame.html"));
            Assert.That(frames[1].Url, Does.Contain("/frames/frame.html"));
            Exception error = Assert.CatchAsync(() => page.Context.NewCDPSessionAsync(frames[1]));
            Assert.That(
                error.Message,
                Does.Contain("This frame does not have a separate CDP session, it is a part of the parent frame's session"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("session.spec.ts", "should not break page.close()")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotBreakPageClose()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            ICDPSession session = await page.Context.NewCDPSessionAsync(page).ConfigureAwait(false);
            await session.DetachAsync().ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("session.spec.ts", "should detach when page closes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDetachWhenPageCloses()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            ICDPSession session = await context.NewCDPSessionAsync(page).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => session.DetachAsync());
            Assert.That(error, Is.Not.Null);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("session.spec.ts", "should reject protocol calls when page closes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRejectProtocolCallsWhenPageCloses()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            ICDPSession session = await context.NewCDPSessionAsync(page).ConfigureAwait(false);
            Task<JsonElement?> promise = session.SendAsync(
                "Runtime.evaluate",
                new { expression = "new Promise(() => {})", awaitPromise = true });
            await page.CloseAsync().ConfigureAwait(false);
            Exception error1 = Assert.CatchAsync(() => promise);
            Assert.That(error1.Message, Does.Contain(TargetClosedErrorMessage));
            Exception error2 = Assert.CatchAsync(
                () => session.SendAsync(
                    "Runtime.evaluate",
                    new { expression = "new Promise(() => {})", awaitPromise = true }));
            Assert.That(error2.Message, Does.Contain(TargetClosedErrorMessage));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("session.spec.ts", "should emit event for each CDP event")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmitEventForEachCdpEvent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            ICDPSession client = await page.Context.NewCDPSessionAsync(page).ConfigureAwait(false);
            await client.SendAsync("Network.enable").ConfigureAwait(false);
            List<JsonElement?> events = new();
            client.Event("Network.requestWillBeSent").OnEvent += (_, parameters) => events.Add(parameters);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(events.Count, Is.GreaterThan(0));
            JsonElement? requestEvent = events.FirstOrDefault(e => e.HasValue);
            Assert.That(requestEvent.HasValue, Is.True);
            Assert.That(
                requestEvent.Value.GetProperty("request").GetProperty("url").GetString(),
                Is.EqualTo(TestConstants.EmptyPage));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("session.spec.ts", "should emit close event when session is detached")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmitCloseEventWhenSessionIsDetached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            ICDPSession client = await page.Context.NewCDPSessionAsync(page).ConfigureAwait(false);
            ICDPSession closedSession = null;
            client.Close += (sender, _) => closedSession = (ICDPSession)sender;
            await client.DetachAsync().ConfigureAwait(false);
            Assert.That(closedSession, Is.SameAs(client));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("session.spec.ts", "should emit close event when page closes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmitCloseEventWhenPageCloses()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            ICDPSession session = await context.NewCDPSessionAsync(page).ConfigureAwait(false);
            TaskCompletionSource<ICDPSession> closeTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            session.Close += (sender, _) => closeTcs.TrySetResult((ICDPSession)sender);
            await page.CloseAsync().ConfigureAwait(false);
            ICDPSession closedSession = await closeTcs.Task.ConfigureAwait(false);
            Assert.That(closedSession, Is.SameAs(session));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("session.spec.ts", "should work with newBrowserCDPSession")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithNewBrowserCdpSession()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            ICDPSession session = await browser.NewBrowserCDPSessionAsync().ConfigureAwait(false);
            JsonElement? version = await session.SendAsync("Browser.getVersion").ConfigureAwait(false);
            Assert.That(version.Value.GetProperty("userAgent").GetString(), Is.Not.Null.And.Not.Empty);
            bool gotEvent = false;
            session.Event("Target.targetCreated").OnEvent += (_, _) =>
            {
                gotEvent = true;
            };
            await session.SendAsync("Target.setDiscoverTargets", new { discover = true }).ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            Assert.That(gotEvent, Is.True);
            await page.CloseAsync().ConfigureAwait(false);
            await session.DetachAsync().ConfigureAwait(false);
        }

        private static async Task<Exception> TheSourceOfTheProblemsAsync(ICDPSession client)
        {
            try
            {
                await client.SendAsync("ThisCommand.DoesNotExist").ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }
}
