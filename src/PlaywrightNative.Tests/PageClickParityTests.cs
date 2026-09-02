/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-click.spec.ts</c> parity for <see cref="IPage.ClickAsync"/>,
    /// <see cref="IPage.DblClickAsync"/>, element-handle click, and locator click.
    /// Skipped (Node-only internals / no public C# equivalent):
    /// <c>should not throw protocol error when navigating during the click</c>,
    /// <c>should retry when navigating during the click</c>,
    /// <c>should not hang when frame is detached</c> (<c>__testHookBeforeStable</c>);
    /// <c>should not wait with noAutoWaiting</c>,
    /// <c>should not wait with noAutoWaiting 2</c>,
    /// <c>should not wait with noAutoWaiting 3</c> (<c>__testHookNoAutoWaiting</c>);
    /// <c>ensure events are dispatched in the individual tasks</c> (<c>window.builtins</c> is Playwright injected internals).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageClickParityTests : PageTestEx
    {
        private const string CounterHtml = @"<!DOCTYPE html>
<button>increment</button>
<h1>count: 0</h1>
<script>
window.count = 0;
document.querySelector('button').addEventListener('click', () => {
  ++window.count;
  document.querySelector('h1').textContent = `count: ${window.count}`;
});
</script>
";

        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;

        private static SimpleServer ActiveServer => _ownedServer ?? TestServerSetup.Server;

        private static bool IsHeadless
        {
            get
            {
                string value = Environment.GetEnvironmentVariable("HEADLESS");
                return string.IsNullOrEmpty(value)
                    || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
            }
        }

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19771;
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

        private static async Task WithPageAsync(Func<IPage, Task> body)
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await body(page).ConfigureAwait(false);
        }

        private static async Task GiveItAChanceToClickAsync(IPage page)
        {
            for (int i = 0; i < 5; i++)
            {
                await page.EvaluateAsync<object>("(() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r))))()").ConfigureAwait(false);
            }
        }

        private static async Task<IFrame> AttachFrameAsync(IPage page, string name, string url)
        {
            string nameJson = JsonSerializer.Serialize(name);
            string urlJson = JsonSerializer.Serialize(url);
            string script =
                "(() => { const f = document.createElement('iframe'); f.name = " +
                nameJson +
                "; f.id = " +
                nameJson +
                "; f.src = " +
                urlJson +
                "; document.body.appendChild(f); })()";
            await page.EvaluateAsync<object>(script).ConfigureAwait(false);

            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                IFrame named = page.Frame(name);
                if (named == null)
                {
                    foreach (IFrame frame in page.Frames)
                    {
                        if (!ReferenceEquals(frame, page.MainFrame))
                        {
                            named = frame;
                            break;
                        }
                    }
                }

                if (named != null)
                {
                    try
                    {
                        await named.WaitForLoadStateAsync(LoadState.Load, new() { Timeout = 5000 }).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                    }

                    return named;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.Fail("Timed out attaching frame " + name);
            return null;
        }

        private static IFrame FrameAt(IPage page, int index)
        {
            List<IFrame> frames = new List<IFrame>(page.Frames);
            Assert.That(frames.Count, Is.GreaterThan(index));
            return frames[index];
        }

        private static async Task PollUntilAsync(Func<Task<bool>> ready, int timeoutMs = 5000)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (await ready().ConfigureAwait(false))
                {
                    return;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.Fail("Timed out waiting for condition.");
        }

        [PlaywrightTest("page-click.spec.ts", "should click the button")]
        [PlaywrightTest("page-click.spec.ts", "should click the button @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickTheButton()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click button inside frameset")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickButtonInsideFrameset()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/frames/frameset.html").ConfigureAwait(false);
                IElementHandle frameElement = await page.QuerySelectorAsync("frame").ConfigureAwait(false);
                await frameElement.EvaluateAsync<object>("frame => { frame.src = '/input/button.html'; }").ConfigureAwait(false);
                IFrame frame = await frameElement.ContentFrameAsync().ConfigureAwait(false);
                DateTime deadline = DateTime.UtcNow.AddSeconds(10);
                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        await frame.WaitForLoadStateAsync(LoadState.Load, new() { Timeout = 2000 }).ConfigureAwait(false);
                        if (await frame.QuerySelectorAsync("button").ConfigureAwait(false) != null)
                        {
                            break;
                        }
                    }
                    catch (Exception)
                    {
                    }

                    await Task.Delay(50).ConfigureAwait(false);
                }

                await frame.ClickAsync("button").ConfigureAwait(false);
                Assert.That(await frame.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should issue clicks in parallel in page and popup")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldIssueClicksInParallelInPageAndPopup()
        {
            await WithPageAsync(async page =>
            {
                SimpleServer server = ActiveServer;
                if (server == null)
                {
                    Assert.Ignore("Test server is unavailable.");
                }

                server.Reset();
                server.SetRoute("/counter.html", ctx =>
                {
                    ctx.Response.ContentType = "text/html";
                    return ctx.Response.WriteAsync(CounterHtml);
                });
                await page.GoToAsync(Prefix + "/counter.html").ConfigureAwait(false);
                Task<IPage> popupTask = page.WaitForPopupAsync();
                await page.EvaluateAsync("(() => { window.open('/counter.html'); })()").ConfigureAwait(false);
                IPage popup = await popupTask.ConfigureAwait(false);
                await popup.WaitForLoadStateAsync().ConfigureAwait(false);
                await popup.WaitForURLAsync(
                    url => url != null && url.IndexOf("counter.html", StringComparison.Ordinal) >= 0).ConfigureAwait(false);

                List<Task> clickPromises = new List<Task>();
                for (int i = 0; i < 21; ++i)
                {
                    if (i % 3 == 0)
                    {
                        clickPromises.Add(popup.Locator("button").ClickAsync());
                    }
                    else
                    {
                        clickPromises.Add(page.Locator("button").ClickAsync());
                    }
                }

                await Task.WhenAll(clickPromises).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<int>("(() => window['count'])()").ConfigureAwait(false), Is.EqualTo(14));
                Assert.That(await popup.EvaluateAsync<int>("(() => window['count'])()").ConfigureAwait(false), Is.EqualTo(7));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click svg")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickSvg()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
    <svg height=""100"" width=""100"">
      <circle onclick=""window.__CLICKED=42"" cx=""50"" cy=""50"" r=""40"" stroke=""black"" stroke-width=""3"" fill=""red"" />
    </svg>
  ").ConfigureAwait(false);
                await page.ClickAsync("circle").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<int>("__CLICKED").ConfigureAwait(false), Is.EqualTo(42));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click the button if window.Node is removed")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickTheButtonIfWindowNodeIsRemoved()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.EvaluateAsync("(() => { delete window.Node; })()").ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click on a span with an inline element inside")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickOnASpanWithAnInlineElementInside()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
    <style>
    span::before {
      content: 'q';
    }
    </style>
    <span onclick='window.CLICKED=42'></span>
  ").ConfigureAwait(false);
                await page.ClickAsync("span").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<int>("CLICKED").ConfigureAwait(false), Is.EqualTo(42));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click the aligned 1x1 div")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickTheAligned1x1Div()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"<div style=""width: 1px; height: 1px;"" onclick=""window.__clicked = true""></div>").ConfigureAwait(false);
                await page.ClickAsync("div").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("window.__clicked").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click the half-aligned 1x1 div")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickTheHalfAligned1x1Div()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"<div style=""margin-left: 20.5px; margin-top: 11.5px; width: 1px; height: 1px;"" onclick=""window.__clicked = true""></div>").ConfigureAwait(false);
                await page.ClickAsync("div").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("window.__clicked").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click the unaligned 1x1 div v1")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickTheUnaligned1x1DivV1()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"<div style=""margin-left: 20.23px; margin-top: 11.65px; width: 1px; height: 1px;"" onclick=""window.__clicked = true""></div>").ConfigureAwait(false);
                await page.ClickAsync("div").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("window.__clicked").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click the unaligned 1x1 div v2")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickTheUnaligned1x1DivV2()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"<div style=""margin-left: 20.68px; margin-top: 11.13px; width: 1px; height: 1px;"" onclick=""window.__clicked = true""></div>").ConfigureAwait(false);
                await page.ClickAsync("div").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("window.__clicked").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click the unaligned 1x1 div v3")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickTheUnaligned1x1DivV3()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"<div style=""margin-left: 20.68px; margin-top: 11.52px; width: 1px; height: 1px;"" onclick=""window.__clicked = true""></div>").ConfigureAwait(false);
                await page.ClickAsync("div").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("window.__clicked").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click the unaligned 1x1 div v4")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickTheUnaligned1x1DivV4()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"<div style=""margin-left: 20.15px; margin-top: 11.24px; width: 1px; height: 1px;"" onclick=""window.__clicked = true""></div>").ConfigureAwait(false);
                await page.ClickAsync("div").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("window.__clicked").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click the button after navigation ")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickTheButtonAfterNavigation()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click the button after a cross origin navigation ")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickTheButtonAfterACrossOriginNavigation()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
                await page.GoToAsync(CrossProcessPrefix + "/input/button.html").ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click when one of inline box children is outside of viewport")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickWhenOneOfInlineBoxChildrenIsOutsideOfViewport()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
    <style>
    i {
      position: absolute;
      top: -1000px;
    }
    </style>
    <span onclick='window.CLICKED = 42;'><i>woof</i><b>doggo</b></span>
  ").ConfigureAwait(false);
                await page.ClickAsync("span").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<int>("CLICKED").ConfigureAwait(false), Is.EqualTo(42));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should select the text by triple clicking")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectTheTextByTripleClicking()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
                string text = "This is the text that we are going to try to select. Let's see how it goes.";
                await page.FillAsync("textarea", text).ConfigureAwait(false);
                await page.ClickAsync("textarea", new() { ClickCount = 3 }).ConfigureAwait(false);
                string selected = await page.EvaluateAsync<string>(@"(() => {
    const textarea = document.querySelector('textarea');
    return textarea.value.substring(textarea.selectionStart, textarea.selectionEnd);
  })()").ConfigureAwait(false);
                Assert.That(selected, Is.EqualTo(text));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click offscreen buttons")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickOffscreenButtons()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/offscreenbuttons.html").ConfigureAwait(false);
                List<string> messages = new List<string>();
                page.Console += (_, msg) =>
                {
                    if (msg.Text != null && msg.Text.StartsWith("button #", StringComparison.Ordinal))
                    {
                        messages.Add(msg.Text);
                    }
                };
                for (int i = 0; i < 11; ++i)
                {
                    await page.EvaluateAsync("(() => window.scrollTo(0, 0))()").ConfigureAwait(false);
                    await page.ClickAsync("#btn" + i.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                }

                Assert.That(messages, Is.EqualTo(new[]
                {
                    "button #0 clicked",
                    "button #1 clicked",
                    "button #2 clicked",
                    "button #3 clicked",
                    "button #4 clicked",
                    "button #5 clicked",
                    "button #6 clicked",
                    "button #7 clicked",
                    "button #8 clicked",
                    "button #9 clicked",
                    "button #10 clicked",
                }));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should waitFor visible when already visible")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForVisibleWhenAlreadyVisible()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should not wait with force")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotWaitWithForce()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.EvalOnSelectorAsync<object>("button", "b => b.style.display = 'none'").ConfigureAwait(false);
                Exception error = Assert.CatchAsync(() => page.ClickAsync("button", new() { Force = true }));
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Does.Contain("Element is not visible"));
                Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Was not clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should waitFor display:none to be gone")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForDisplayNoneToBeGone()
        {
            await WithPageAsync(async page =>
            {
                bool done = false;
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.EvalOnSelectorAsync<object>("button", "b => b.style.display = 'none'").ConfigureAwait(false);
                Task clicked = page.ClickAsync("button", new() { Timeout = 0 }).ContinueWith(_ => { done = true; }, TaskScheduler.Default);
                await GiveItAChanceToClickAsync(page).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Was not clicked"));
                Assert.That(done, Is.False);
                await page.EvalOnSelectorAsync<object>("button", "b => b.style.display = 'block'").ConfigureAwait(false);
                await clicked.ConfigureAwait(false);
                Assert.That(done, Is.True);
                Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should waitFor visibility:hidden to be gone")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForVisibilityHiddenToBeGone()
        {
            await WithPageAsync(async page =>
            {
                bool done = false;
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.EvalOnSelectorAsync<object>("button", "b => b.style.visibility = 'hidden'").ConfigureAwait(false);
                Task clicked = page.ClickAsync("button", new() { Timeout = 0 }).ContinueWith(_ => { done = true; }, TaskScheduler.Default);
                await GiveItAChanceToClickAsync(page).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Was not clicked"));
                Assert.That(done, Is.False);
                await page.EvalOnSelectorAsync<object>("button", "b => b.style.visibility = 'visible'").ConfigureAwait(false);
                await clicked.ConfigureAwait(false);
                Assert.That(done, Is.True);
                Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should waitFor visible when parent is hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForVisibleWhenParentIsHidden()
        {
            await WithPageAsync(async page =>
            {
                bool done = false;
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.EvalOnSelectorAsync<object>("button", "b => b.parentElement.style.display = 'none'").ConfigureAwait(false);
                Task clicked = page.ClickAsync("button", new() { Timeout = 0 }).ContinueWith(_ => { done = true; }, TaskScheduler.Default);
                await GiveItAChanceToClickAsync(page).ConfigureAwait(false);
                Assert.That(done, Is.False);
                await page.EvalOnSelectorAsync<object>("button", "b => b.parentElement.style.display = 'block'").ConfigureAwait(false);
                await clicked.ConfigureAwait(false);
                Assert.That(done, Is.True);
                Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click wrapped links")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickWrappedLinks()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/wrappedlink.html").ConfigureAwait(false);
                await page.ClickAsync("a").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("__clicked").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click on checkbox input and toggle")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickOnCheckboxInputAndToggle()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/checkbox.html").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool?>("(() => window['result'].check)()").ConfigureAwait(false), Is.Null);
                await page.ClickAsync("input#agree").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("(() => window['result'].check)()").ConfigureAwait(false), Is.True);
                string[] events = await page.EvaluateAsync<string[]>("(() => window['result'].events)()").ConfigureAwait(false);
                if (!IsHeadless)
                {
                    List<string> filtered = new List<string>();
                    foreach (string item in events)
                    {
                        if (item != "mouseout" && item != "mouseleave")
                        {
                            filtered.Add(item);
                        }
                    }

                    events = filtered.ToArray();
                }

                Assert.That(events, Is.EqualTo(new[]
                {
                    "mouseover",
                    "mouseenter",
                    "mousemove",
                    "mousedown",
                    "mouseup",
                    "click",
                    "input",
                    "change",
                }));
                await page.ClickAsync("input#agree").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("(() => window['result'].check)()").ConfigureAwait(false), Is.False);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click on checkbox label and toggle")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickOnCheckboxLabelAndToggle()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/checkbox.html").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool?>("(() => window['result'].check)()").ConfigureAwait(false), Is.Null);
                await page.ClickAsync("label[for=\"agree\"]").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("(() => window['result'].check)()").ConfigureAwait(false), Is.True);
                Assert.That(await page.EvaluateAsync<string[]>("(() => window['result'].events)()").ConfigureAwait(false), Is.EqualTo(new[]
                {
                    "click",
                    "input",
                    "change",
                }));
                await page.ClickAsync("label[for=\"agree\"]").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("(() => window['result'].check)()").ConfigureAwait(false), Is.False);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should scroll and click the button")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldScrollAndClickTheButton()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/scrollable.html").ConfigureAwait(false);
                await page.ClickAsync("#button-5").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("(() => document.querySelector('#button-5').textContent)()").ConfigureAwait(false), Is.EqualTo("clicked"));
                await page.ClickAsync("#button-80").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("(() => document.querySelector('#button-80').textContent)()").ConfigureAwait(false), Is.EqualTo("clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should scroll and click the button with smooth scroll behavior")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldScrollAndClickTheButtonWithSmoothScrollBehavior()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/scrollable.html").ConfigureAwait(false);
                await page.AddStyleTagAsync(new() { Content = "html { scroll-behavior: smooth; }" }).ConfigureAwait(false);
                for (int i = 0; i < 5; i++)
                {
                    await page.ClickAsync("#button-80").ConfigureAwait(false);
                    Assert.That(await page.EvaluateAsync<string>("(() => document.querySelector('#button-80').textContent)()").ConfigureAwait(false), Is.EqualTo("clicked"));
                    await page.ClickAsync("#button-20").ConfigureAwait(false);
                    Assert.That(await page.EvaluateAsync<string>("(() => document.querySelector('#button-20').textContent)()").ConfigureAwait(false), Is.EqualTo("clicked"));
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should double click the button")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDoubleClickTheButton()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.EvaluateAsync(@"(() => {
    window['double'] = false;
    const button = document.querySelector('button');
    button.addEventListener('dblclick', event => {
      window['double'] = true;
    });
  })()").ConfigureAwait(false);
                await page.DblClickAsync("button").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("double").ConfigureAwait(false), Is.True);
                Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click a partially obscured button")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickAPartiallyObscuredButton()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.EvaluateAsync(@"(() => {
    const button = document.querySelector('button');
    button.textContent = 'Some really long text that will go offscreen';
    button.style.position = 'absolute';
    button.style.left = '368px';
  })()").ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("Clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click a rotated button")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickARotatedButton()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/rotatedButton.html").ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should fire contextmenu event on right click")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireContextmenuEventOnRightClick()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/scrollable.html").ConfigureAwait(false);
                await page.ClickAsync("#button-8", new() { Button = MouseButton.Right }).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("(() => document.querySelector('#button-8').textContent)()").ConfigureAwait(false), Is.EqualTo("context menu"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click links which cause navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickLinksWhichCauseNavigation()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<a href=\"" + EmptyPage + "\">empty.html</a>").ConfigureAwait(false);
                await page.ClickAsync("a").ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click the button inside an iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickTheButtonInsideAnIframe()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.SetContentAsync("<div style=\"width:100px;height:100px\">spacer</div>").ConfigureAwait(false);
                await AttachFrameAsync(page, "button-test", Prefix + "/input/button.html").ConfigureAwait(false);
                IFrame frame = FrameAt(page, 1);
                IElementHandle button = await frame.QuerySelectorAsync("button").ConfigureAwait(false);
                await button.ClickAsync().ConfigureAwait(false);
                Assert.That(await frame.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("Clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click the button with fixed position inside an iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickTheButtonWithFixedPositionInsideAnIframe()
        {
            if (TestConstants.IsChromium)
            {
                Assert.Ignore("upstream it.fixme(chromium)");
            }

            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
                await page.SetContentAsync("<div style=\"width:100px;height:2000px\">spacer</div>").ConfigureAwait(false);
                await AttachFrameAsync(page, "button-test", CrossProcessPrefix + "/input/button.html").ConfigureAwait(false);
                IFrame frame = FrameAt(page, 1);
                await frame.EvalOnSelectorAsync<object>("button", "button => button.style.setProperty('position', 'fixed')").ConfigureAwait(false);
                await frame.ClickAsync("button").ConfigureAwait(false);
                Assert.That(await frame.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("Clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click the button behind sticky header")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickTheButtonBehindStickyHeader()
        {
            await WithPageAsync(async page =>
            {
                await page.SetViewportSizeAsync(500, 240).ConfigureAwait(false);
                await page.SetContentAsync(@"
    <style>
    * { padding: 0; margin: 0; }
    li { height: 80px; border: 1px solid black; }
    ol { padding-top: 160px; }
    div.fixed { position: fixed; z-index: 1001; width: 100%; background: red; height: 160px; }
    </style>
    <div class=fixed></div>
    <ol>
    <li>hi1</li><li>hi2</li><li>hi3</li><li>hi4</li><li>hi5</li><li>hi6</li><li>hi7</li><li>hi8</li>
    <li id=target onclick=""window.__clicked = true"">hi9</li>
    <li>hi10</li><li>hi11</li><li>hi12</li><li>hi13</li><li id=li14>hi14</li>
    </ol>
  ").ConfigureAwait(false);
                await page.EvalOnSelectorAsync<object>("#li14", "e => e.scrollIntoView()").ConfigureAwait(false);
                await page.ClickAsync("#target").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("(() => window['__clicked'])()").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click the button behind position:absolute header")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickTheButtonBehindPositionAbsoluteHeader()
        {
            await WithPageAsync(async page =>
            {
                await page.SetViewportSizeAsync(500, 240).ConfigureAwait(false);
                await page.SetContentAsync(@"
    <style>
    * { padding: 0; margin: 0; }
    li { height: 80px; border: 1px solid black; }
    ol { height: 100vh; overflow: scroll; padding-top: 160px; }
    body { position: relative; }
    div.fixed { position: absolute; top: 0; z-index: 1001; width: 100%; background: red; height: 160px; }
    </style>
    <ol>
    <li>hi1</li><li>hi2</li><li>hi3</li><li>hi4</li><li>hi5</li><li>hi6</li><li>hi7</li><li>hi8</li>
    <li id=target onclick=""window.__clicked = true"">hi9</li>
    <li>hi10</li><li>hi11</li><li>hi12</li><li>hi13</li><li id=li14>hi14</li>
    </ol>
    <div class=fixed>Overlay</div>
  ").ConfigureAwait(false);
                await page.EvalOnSelectorAsync<object>("ol", @"e => {
    const target = document.querySelector('#target');
    e.scrollTo({ top: target.offsetTop, behavior: 'instant' });
  }").ConfigureAwait(false);
                await page.ClickAsync("#target").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("(() => window['__clicked'])()").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click the button with px border with offset")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickTheButtonWithPxBorderWithOffset()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.EvalOnSelectorAsync<object>("button", "button => button.style.borderWidth = '8px'").ConfigureAwait(false);
                await page.ClickAsync("button", new() { Position = new Position { X = 20, Y = 10 } }).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Clicked"));
                Assert.That(await page.EvaluateAsync<int>("offsetX").ConfigureAwait(false), Is.EqualTo(20));
                Assert.That(await page.EvaluateAsync<int>("offsetY").ConfigureAwait(false), Is.EqualTo(10));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click the button with em border with offset")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickTheButtonWithEmBorderWithOffset()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.EvalOnSelectorAsync<object>("button", "button => button.style.borderWidth = '2em'").ConfigureAwait(false);
                await page.EvalOnSelectorAsync<object>("button", "button => button.style.fontSize = '12px'").ConfigureAwait(false);
                await page.ClickAsync("button", new() { Position = new Position { X = 20, Y = 10 } }).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Clicked"));
                Assert.That(await page.EvaluateAsync<int>("offsetX").ConfigureAwait(false), Is.EqualTo(20));
                Assert.That(await page.EvaluateAsync<int>("offsetY").ConfigureAwait(false), Is.EqualTo(10));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click a very large button with offset")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickAVeryLargeButtonWithOffset()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.EvalOnSelectorAsync<object>("button", "button => button.style.borderWidth = '8px'").ConfigureAwait(false);
                await page.EvalOnSelectorAsync<object>("button", "button => button.style.height = button.style.width = '2000px'").ConfigureAwait(false);
                await page.ClickAsync("button", new() { Position = new Position { X = 1900, Y = 1910 } }).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("Clicked"));
                Assert.That(await page.EvaluateAsync<int>("offsetX").ConfigureAwait(false), Is.EqualTo(1900));
                Assert.That(await page.EvaluateAsync<int>("offsetY").ConfigureAwait(false), Is.EqualTo(1910));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click a button in scrolling container with offset")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickAButtonInScrollingContainerWithOffset()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.EvalOnSelectorAsync<object>("button", @"button => {
    const container = document.createElement('div');
    container.style.overflow = 'auto';
    container.style.width = '200px';
    container.style.height = '200px';
    button.parentElement.insertBefore(container, button);
    container.appendChild(button);
    button.style.height = '2000px';
    button.style.width = '2000px';
    button.style.borderWidth = '8px';
  }").ConfigureAwait(false);
                await page.ClickAsync("button", new() { Position = new Position { X = 1900, Y = 1910 } }).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("Clicked"));
                Assert.That(await page.EvaluateAsync<int>("offsetX").ConfigureAwait(false), Is.EqualTo(1900));
                Assert.That(await page.EvaluateAsync<int>("offsetY").ConfigureAwait(false), Is.EqualTo(1910));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should wait for stable position")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForStablePosition()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.EvalOnSelectorAsync<object>("button", @"button => {
    button.style.transition = 'margin 500ms linear 0s';
    button.style.marginLeft = '200px';
    button.style.borderWidth = '0';
    button.style.width = '200px';
    button.style.height = '20px';
    button.style.display = 'block';
    document.body.style.margin = '0';
  }").ConfigureAwait(false);
                await page.EvaluateAsync("(() => new Promise(f => requestAnimationFrame(() => requestAnimationFrame(f))))()").ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("Clicked"));
                Assert.That(await page.EvaluateAsync<int>("pageX").ConfigureAwait(false), Is.EqualTo(300));
                Assert.That(await page.EvaluateAsync<int>("pageY").ConfigureAwait(false), Is.EqualTo(10));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should wait for becoming hit target")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForBecomingHitTarget()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.EvalOnSelectorAsync<object>("button", @"button => {
    button.style.borderWidth = '0';
    button.style.width = '200px';
    button.style.height = '20px';
    document.body.style.margin = '0';
    document.body.style.position = 'relative';
    const flyOver = document.createElement('div');
    flyOver.className = 'flyover';
    flyOver.style.position = 'absolute';
    flyOver.style.width = '400px';
    flyOver.style.height = '20px';
    flyOver.style.left = '-200px';
    flyOver.style.top = '0';
    flyOver.style.background = 'red';
    document.body.appendChild(flyOver);
  }").ConfigureAwait(false);
                bool clicked = false;
                Task clickPromise = page.ClickAsync("button").ContinueWith(_ => { clicked = true; }, TaskScheduler.Default);
                Assert.That(clicked, Is.False);
                await page.EvalOnSelectorAsync<object>(".flyover", "flyOver => flyOver.style.left = '0'").ConfigureAwait(false);
                await GiveItAChanceToClickAsync(page).ConfigureAwait(false);
                Assert.That(clicked, Is.False);
                await page.EvalOnSelectorAsync<object>(".flyover", "flyOver => flyOver.style.left = '200px'").ConfigureAwait(false);
                await clickPromise.ConfigureAwait(false);
                Assert.That(clicked, Is.True);
                Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("Clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should wait for becoming hit target with trial run")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForBecomingHitTargetWithTrialRun()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.EvalOnSelectorAsync<object>("button", @"button => {
    button.style.borderWidth = '0';
    button.style.width = '200px';
    button.style.height = '20px';
    document.body.style.margin = '0';
    document.body.style.position = 'relative';
    const flyOver = document.createElement('div');
    flyOver.className = 'flyover';
    flyOver.style.position = 'absolute';
    flyOver.style.width = '400px';
    flyOver.style.height = '20px';
    flyOver.style.left = '-200px';
    flyOver.style.top = '0';
    flyOver.style.background = 'red';
    document.body.appendChild(flyOver);
  }").ConfigureAwait(false);
                bool clicked = false;
                Task clickPromise = page.ClickAsync("button", new() { Trial = true }).ContinueWith(_ => { clicked = true; }, TaskScheduler.Default);
                Assert.That(clicked, Is.False);
                await page.EvalOnSelectorAsync<object>(".flyover", "flyOver => flyOver.style.left = '0'").ConfigureAwait(false);
                await GiveItAChanceToClickAsync(page).ConfigureAwait(false);
                Assert.That(clicked, Is.False);
                await page.EvalOnSelectorAsync<object>(".flyover", "flyOver => flyOver.style.left = '200px'").ConfigureAwait(false);
                await clickPromise.ConfigureAwait(false);
                Assert.That(clicked, Is.True);
                Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("Was not clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "trial run should work with short timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task TrialRunShouldWorkWithShortTimeout()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.EvalOnSelectorAsync<object>("button", "button => button.disabled = true").ConfigureAwait(false);
                Exception error = Assert.CatchAsync(() => page.ClickAsync("button", new() { Trial = true, Timeout = 2000 }));
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Does.Contain("click action (trial run)"));
                Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("Was not clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "trial run should not click")]
        [Test]
        [Timeout(30_000)]
        public async Task TrialRunShouldNotClick()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.ClickAsync("button", new() { Trial = true }).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("Was not clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "trial run should not double click")]
        [Test]
        [Timeout(30_000)]
        public async Task TrialRunShouldNotDoubleClick()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.EvaluateAsync(@"(() => {
    window['double'] = false;
    const button = document.querySelector('button');
    button.addEventListener('dblclick', event => {
      window['double'] = true;
    });
  })()").ConfigureAwait(false);
                await page.DblClickAsync("button", new() { Trial = true }).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("double").ConfigureAwait(false), Is.False);
                Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Was not clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should fail when obscured and not waiting for hit target")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailWhenObscuredAndNotWaitingForHitTarget()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                IElementHandle button = await page.QuerySelectorAsync("button").ConfigureAwait(false);
                await page.EvaluateAsync(@"(() => {
    document.body.style.position = 'relative';
    const blocker = document.createElement('div');
    blocker.style.position = 'absolute';
    blocker.style.width = '400px';
    blocker.style.height = '20px';
    blocker.style.left = '0';
    blocker.style.top = '0';
    document.body.appendChild(blocker);
  })()").ConfigureAwait(false);
                await button.ClickAsync(new() { Force = true }).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("Was not clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should wait for button to be enabled")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForButtonToBeEnabled()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<button onclick=\"window.__CLICKED=true;\" disabled><span>Click target</span></button>").ConfigureAwait(false);
                bool done = false;
                Task clickPromise = page.ClickAsync("text=Click target").ContinueWith(_ => { done = true; }, TaskScheduler.Default);
                await GiveItAChanceToClickAsync(page).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<object>("window.__CLICKED").ConfigureAwait(false), Is.Null);
                Assert.That(done, Is.False);
                await page.EvaluateAsync("(() => document.querySelector('button').removeAttribute('disabled'))()").ConfigureAwait(false);
                await clickPromise.ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("__CLICKED").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should wait for input to be enabled")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForInputToBeEnabled()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<input onclick=\"window.__CLICKED=true;\" disabled>").ConfigureAwait(false);
                bool done = false;
                Task clickPromise = page.ClickAsync("input").ContinueWith(_ => { done = true; }, TaskScheduler.Default);
                await GiveItAChanceToClickAsync(page).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<object>("window.__CLICKED").ConfigureAwait(false), Is.Null);
                Assert.That(done, Is.False);
                await page.EvaluateAsync("(() => document.querySelector('input').removeAttribute('disabled'))()").ConfigureAwait(false);
                await clickPromise.ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("__CLICKED").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should wait for select to be enabled")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForSelectToBeEnabled()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
    <select disabled><option selected>Hello</option></select>
    <script>
      document.querySelector('select').addEventListener('mousedown', event => {
        window.__CLICKED=true;
        event.preventDefault();
      });
    </script>
  ").ConfigureAwait(false);
                bool done = false;
                Task clickPromise = page.ClickAsync("select").ContinueWith(_ => { done = true; }, TaskScheduler.Default);
                await GiveItAChanceToClickAsync(page).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<object>("window.__CLICKED").ConfigureAwait(false), Is.Null);
                Assert.That(done, Is.False);
                await page.EvaluateAsync("(() => document.querySelector('select').removeAttribute('disabled'))()").ConfigureAwait(false);
                await clickPromise.ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("__CLICKED").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click disabled div")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickDisabledDiv()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<div onclick=\"window.__CLICKED=true\" disabled>Click target</div>").ConfigureAwait(false);
                await page.ClickAsync("text=Click target").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("__CLICKED").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should wait for BUTTON to be clickable when it has pointer-events:none")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForButtonToBeClickableWhenItHasPointerEventsNone()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<button onclick=\"window.__CLICKED=true\" style=\"pointer-events:none\"><span>Click target</span></button>").ConfigureAwait(false);
                bool done = false;
                Task clickPromise = page.ClickAsync("text=Click target").ContinueWith(_ => { done = true; }, TaskScheduler.Default);
                await GiveItAChanceToClickAsync(page).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<object>("window.__CLICKED").ConfigureAwait(false), Is.Null);
                Assert.That(done, Is.False);
                await page.EvaluateAsync("(() => document.querySelector('button').style.removeProperty('pointer-events'))()").ConfigureAwait(false);
                await clickPromise.ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("__CLICKED").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should wait for LABEL to be clickable when it has pointer-events:none")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForLabelToBeClickableWhenItHasPointerEventsNone()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<label onclick=\"window.__CLICKED=true\" style=\"pointer-events:none\"><span>Click target</span></label>").ConfigureAwait(false);
                Task clickPromise = page.ClickAsync("text=Click target");
                for (int i = 0; i < 5; ++i)
                {
                    Assert.That(await page.EvaluateAsync<object>("window.__CLICKED").ConfigureAwait(false), Is.Null);
                }

                await page.EvaluateAsync("(() => document.querySelector('label').style.removeProperty('pointer-events'))()").ConfigureAwait(false);
                await clickPromise.ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("__CLICKED").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should update modifiers correctly")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldUpdateModifiersCorrectly()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.ClickAsync("button", new() { Modifiers = new[] { KeyboardModifier.Shift } }).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("shiftKey").ConfigureAwait(false), Is.True);
                await page.ClickAsync("button", new() { Modifiers = Array.Empty<KeyboardModifier>() }).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("shiftKey").ConfigureAwait(false), Is.False);
                await page.Keyboard.DownAsync("Shift").ConfigureAwait(false);
                await page.ClickAsync("button", new() { Modifiers = Array.Empty<KeyboardModifier>() }).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("shiftKey").ConfigureAwait(false), Is.False);
                await page.ClickAsync("button").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("shiftKey").ConfigureAwait(false), Is.True);
                await page.Keyboard.UpAsync("Shift").ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("shiftKey").ConfigureAwait(false), Is.False);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click an offscreen element when scroll-behavior is smooth")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickAnOffscreenElementWhenScrollBehaviorIsSmooth()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
    <div style=""border: 1px solid black; height: 500px; overflow: auto; width: 500px; scroll-behavior: smooth"">
    <button style=""margin-top: 2000px"" onClick=""window.clicked = true"">hi</button>
    </div>
  ").ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("window.clicked").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should report nice error when element is detached and force-clicked")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportNiceErrorWhenElementIsDetachedAndForceClicked()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/animating-button.html").ConfigureAwait(false);
                await page.EvaluateAsync("addButton()").ConfigureAwait(false);
                IElementHandle handle = await page.QuerySelectorAsync("button").ConfigureAwait(false);
                await page.EvaluateAsync("stopButton(true)").ConfigureAwait(false);
                Exception error = Assert.CatchAsync(() => handle.ClickAsync(new() { Force = true }));
                Assert.That(await page.EvaluateAsync<object>("window.clicked").ConfigureAwait(false), Is.Null);
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Does.Contain("Element is not attached to the DOM"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should fail when element detaches after animation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailWhenElementDetachesAfterAnimation()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/animating-button.html").ConfigureAwait(false);
                await page.EvaluateAsync("addButton()").ConfigureAwait(false);
                IElementHandle handle = await page.QuerySelectorAsync("button").ConfigureAwait(false);
                Task<Exception> promise = handle.ClickAsync().ContinueWith(t => t.Exception?.GetBaseException() ?? (t.IsFaulted ? t.Exception : null), TaskScheduler.Default);
                await page.EvaluateAsync("stopButton(true)").ConfigureAwait(false);
                Exception error = await promise.ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<object>("window.clicked").ConfigureAwait(false), Is.Null);
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Does.Contain("Element is not attached to the DOM"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should retry when element detaches after animation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRetryWhenElementDetachesAfterAnimation()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/animating-button.html").ConfigureAwait(false);
                await page.EvaluateAsync("addButton()").ConfigureAwait(false);
                bool clicked = false;
                Task promise = page.ClickAsync("button").ContinueWith(_ => { clicked = true; }, TaskScheduler.Default);
                Assert.That(clicked, Is.False);
                Assert.That(await page.EvaluateAsync<object>("window.clicked").ConfigureAwait(false), Is.Null);
                await page.EvaluateAsync("stopButton(true)").ConfigureAwait(false);
                await page.EvaluateAsync("addButton()").ConfigureAwait(false);
                Assert.That(clicked, Is.False);
                Assert.That(await page.EvaluateAsync<object>("window.clicked").ConfigureAwait(false), Is.Null);
                await page.EvaluateAsync("stopButton(true)").ConfigureAwait(false);
                await page.EvaluateAsync("addButton()").ConfigureAwait(false);
                Assert.That(clicked, Is.False);
                Assert.That(await page.EvaluateAsync<object>("window.clicked").ConfigureAwait(false), Is.Null);
                await page.EvaluateAsync("stopButton(false)").ConfigureAwait(false);
                await promise.ConfigureAwait(false);
                Assert.That(clicked, Is.True);
                Assert.That(await page.EvaluateAsync<bool>("clicked").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should retry when element is animating from outside the viewport")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRetryWhenElementIsAnimatingFromOutsideTheViewport()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"<style>
    @keyframes move {
      from { left: -300px; }
      to { left: 0; }
    }
    button {
      position: absolute;
      left: -300px;
      top: 0;
      bottom: 0;
      width: 200px;
    }
    button.animated {
      animation: 1s linear 1s move forwards;
    }
    </style>
    <div style=""position: relative; width: 300px; height: 300px;"">
      <button onclick=""window.clicked=true""></button>
    </div>
  ").ConfigureAwait(false);
                IElementHandle handle = await page.QuerySelectorAsync("button").ConfigureAwait(false);
                Task promise = handle.ClickAsync();
                await handle.EvaluateAsync<object>("button => button.className = 'animated'").ConfigureAwait(false);
                await promise.ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("clicked").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should fail when element is animating from outside the viewport with force")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailWhenElementIsAnimatingFromOutsideTheViewportWithForce()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"<style>
    @keyframes move {
      from { left: -300px; }
      to { left: 0; }
    }
    button {
      position: absolute;
      left: -300px;
      top: 0;
      bottom: 0;
      width: 200px;
    }
    button.animated {
      animation: 1s linear 1s move forwards;
    }
    </style>
    <div style=""position: relative; width: 300px; height: 300px;"">
      <button onclick=""window.clicked=true""></button>
    </div>
  ").ConfigureAwait(false);
                IElementHandle handle = await page.QuerySelectorAsync("button").ConfigureAwait(false);
                Task<Exception> promise = handle.ClickAsync(new() { Force = true }).ContinueWith(t => t.Exception?.GetBaseException(), TaskScheduler.Default);
                await handle.EvaluateAsync<object>("button => button.className = 'animated'").ConfigureAwait(false);
                Exception error = await promise.ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<object>("window.clicked").ConfigureAwait(false), Is.Null);
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Does.Contain("Element is outside of the viewport"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should dispatch microtasks in order")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchMicrotasksInOrder()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
    <button id=button>Click me</button>
    <script>
      let mutationCount = 0;
      const observer = new MutationObserver((mutationsList, observer) => {
        for(let mutation of mutationsList)
          ++mutationCount;
      });
      observer.observe(document.body, { attributes: true, childList: true, subtree: true });
      button.addEventListener('mousedown', () => {
        mutationCount = 0;
        document.body.appendChild(document.createElement('div'));
      });
      button.addEventListener('mouseup', () => {
        window['result'] = mutationCount;
      });
    </script>
  ").ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<int>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo(1));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click the button when window.innerWidth is corrupted")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickTheButtonWhenWindowInnerWidthIsCorrupted()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
                await page.EvaluateAsync<object>("(() => { Object.defineProperty(window, 'innerWidth', { value: 0 }); })()").ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click zero-sized input by label")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickZeroSizedInputByLabel()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
    <label>
      Click me
      <input onclick=""window.__clicked=true"" style=""width:0;height:0;padding:0;margin:0;border:0;"">
    </label>
  ").ConfigureAwait(false);
                await page.ClickAsync("text=Click me").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("window.__clicked").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should climb dom for inner label with pointer-events:none")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClimbDomForInnerLabelWithPointerEventsNone()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<button onclick=\"window.__CLICKED=true;\"><label style=\"pointer-events:none\">Click target</label></button>").ConfigureAwait(false);
                await page.ClickAsync("text=Click target").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("__CLICKED").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should climb up to [role=button]")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClimbUpToRoleButton()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<div role=button onclick=\"window.__CLICKED=true;\"><div style=\"pointer-events:none\"><span><div>Click target</div></span></div>").ConfigureAwait(false);
                await page.ClickAsync("text=Click target").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("__CLICKED").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should climb up to a anchor")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClimbUpToAAnchor()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<a href=\"#\" onclick=\"window.__CLICKED=true\" id=\"outer\"><div id=\"intermediate\"><div id=\"inner\" style=\"pointer-events: none\">Inner</div></div></a>").ConfigureAwait(false);
                await page.ClickAsync("#inner").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("__CLICKED").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should climb up to a [role=link]")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClimbUpToARoleLink()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<div role=link onclick=\"window.__CLICKED=true\" id=\"outer\"><div id=\"inner\" style=\"pointer-events: none\">Inner</div></div>").ConfigureAwait(false);
                await page.ClickAsync("#inner").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("__CLICKED").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click in an iframe with border")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickInAnIframeWithBorder()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
    <style>
      body, html, iframe { margin: 0; padding: 0; border: none; box-sizing: border-box; }
      iframe { border: 4px solid black; background: gray; margin-left: 33px; margin-top: 24px; width: 400px; height: 400px; }
    </style>
    <iframe srcdoc=""
      <style>
        body, html { margin: 0; padding: 0; }
        div { margin-left: 10px; margin-top: 20px; width: 2px; height: 2px; }
      </style>
      <div>Target</div>
      <script>
        document.querySelector('div').addEventListener('click', () => window.top._clicked = true);
      </script>
    ""></iframe>
  ").ConfigureAwait(false);
                ILocator locator = page.FrameLocator("iframe").Locator("div");
                await locator.ClickAsync().ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("window._clicked").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click in an iframe with border 2")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickInAnIframeWithBorder2()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
    <style>
      body, html, iframe { margin: 0; padding: 0; border: none; }
      iframe { border: 4px solid black; background: gray; margin-left: 33px; margin-top: 24px; width: 400px; height: 400px; }
    </style>
    <iframe srcdoc=""
      <style>
        body, html { margin: 0; padding: 0; }
        div { margin-left: 10px; margin-top: 20px; width: 2px; height: 2px; }
      </style>
      <div>Target</div>
      <script>
        document.querySelector('div').addEventListener('click', () => window.top._clicked = true);
      </script>
    ""></iframe>
  ").ConfigureAwait(false);
                ILocator locator = page.FrameLocator("iframe").Locator("div");
                await locator.ClickAsync().ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("window._clicked").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click in a transformed iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickInATransformedIframe()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
    <style>
      body, html, iframe { margin: 0; padding: 0; border: none; }
      iframe {
        border: 4px solid black;
        background: gray;
        margin-left: 33px;
        margin-top: 24px;
        width: 400px;
        height: 400px;
        transform: translate(100px, 100px) scale(1.2) rotate3d(1, 1, 1, 25deg);
      }
    </style>
    <iframe srcdoc=""
      <style>
        body, html { margin: 0; padding: 0; }
        div { margin-left: 10px; margin-top: 20px; width: 2px; height: 2px; }
      </style>
      <div>Target</div>
      <script>
        document.querySelector('div').addEventListener('click', () => window.top._clicked = true);
      </script>
    ""></iframe>
  ").ConfigureAwait(false);
                ILocator locator = page.FrameLocator("iframe").Locator("div");
                await locator.ClickAsync().ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("window._clicked").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click a button that is overlaid by a permission popup")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickAButtonThatIsOverlaidByAPermissionPopup()
        {
            await WithPageAsync(async page =>
            {
                await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.SetContentAsync(@"
    <style>body, html { padding: 0; margin: 0; }</style>
    <script type='text/javascript'>
      window.addEventListener('DOMContentLoaded', () => {
        for (let i = 0; i < 100; ++i) {
          const button = document.createElement('button');
          button.textContent = i;
          button.style.setProperty('width', '50px');
          button.style.setProperty('height', '50px');
          document.body.append(button);
        }
      }, false);
    </script>
  ").ConfigureAwait(false);
                await page.EvaluateAsync("(() => { navigator.geolocation.getCurrentPosition(position => { }); })()").ConfigureAwait(false);
                for (int i = 0; i < 30; ++i)
                {
                    await page.ClickAsync("text=" + i.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click in a transformed iframe with force")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickInATransformedIframeWithForce()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
    <style>
      body, html, iframe { margin: 0; padding: 0; border: none; }
      iframe { background: gray; margin-left: 33px; margin-top: 24px; width: 400px; height: 400px; transform: translate(-40px, -40px) scale(0.8); }
    </style>
    <iframe srcdoc=""
      <style>
        body, html { margin: 0; padding: 0; }
        div { margin-left: 10px; margin-top: 20px; width: 2px; height: 2px; }
      </style>
      <div>Target</div>
      <script>
        document.querySelector('div').addEventListener('click', () => window.top._clicked = true);
      </script>
    ""></iframe>
  ").ConfigureAwait(false);
                ILocator locator = page.FrameLocator("iframe").Locator("div");
                await locator.ClickAsync(new() { Force = true }).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("window._clicked").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click in a nested transformed iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickInANestedTransformedIframe()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
    <style>
      body, html, iframe { margin: 0; padding: 0; box-sizing: border-box; }
      iframe { border: 1px solid black; background: gray; margin-left: 33px; margin-top: 24px; width: 400px; height: 400px; transform: scale(0.8); }
    </style>
    <iframe srcdoc=""
      <style>
        body, html, iframe { margin: 0; padding: 0; box-sizing: border-box; }
        iframe { border: 3px solid black; background: gray; margin-left: 18px; margin-top: 14px; width: 200px; height: 200px; transform: scale(0.7); }
      </style>
      <iframe srcdoc='
        <style>
          div { margin-left: 10px; margin-top: 20px; width: 2px; height: 2px; }
        </style>
        <div>Target</div>
      '></iframe>
    ""></iframe>
  ").ConfigureAwait(false);
                ILocator locator = page.FrameLocator("iframe").FrameLocator("iframe").Locator("div");
                await locator.EvaluateAsync<object>(@"div => {
    div.addEventListener('click', () => window.top['_clicked'] = true);
  }").ConfigureAwait(false);
                await locator.ClickAsync().ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>("window._clicked").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click if opened select covers the button")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickIfOpenedSelectCoversTheButton()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
    <div>
      <select>
        <option>very long text #1</option>
        <option>very long text #2</option>
        <option>very long text #3</option>
        <option>very long text #4</option>
        <option>very long text #5</option>
        <option>very long text #6</option>
      </select>
    </div>
    <div>
      <button onclick=""window.__CLICKED=42"">clickme</button>
    </div>
  ").ConfigureAwait(false);
                await page.ClickAsync("select").ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<int>("window.__CLICKED").ConfigureAwait(false), Is.EqualTo(42));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should fire contextmenu event on right click in correct order")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireContextmenuEventOnRightClickInCorrectOrder()
        {
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.SetContentAsync("<button id=\"target\">Click me</button>").ConfigureAwait(false);
                await page.EvaluateAsync(@"(() => {
    const logEvent = e => console.log(e.type);
    document.addEventListener('mousedown', logEvent);
    document.addEventListener('mouseup', logEvent);
    document.addEventListener('contextmenu', logEvent);
  })()").ConfigureAwait(false);
                List<string> entries = new List<string>();
                page.Console += (_, message) => entries.Add(message.Text);
                await page.GetByRole("button", name: "Click me").ClickAsync(new() { Button = MouseButton.Right }).ConfigureAwait(false);
                string[] expected = TestConstants.IsChromium && RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? new[] { "mousedown", "mouseup", "contextmenu" }
                    : new[] { "mousedown", "contextmenu", "mouseup" };
                await PollUntilAsync(async () =>
                {
                    return entries.Count >= expected.Length;
                }).ConfigureAwait(false);
                Assert.That(entries, Is.EqualTo(expected));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click after a right click")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickAfterARightClick()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
    <button>Click me</button>
    <script>
      const button = document.querySelector('button');
      button.addEventListener('click', () => button.textContent = 'Clicked!');
    </script>
  ").ConfigureAwait(false);
                await page.GetByRole("button").ClickAsync(new() { Button = MouseButton.Right }).ConfigureAwait(false);
                await page.GetByRole("button").ClickAsync().ConfigureAwait(false);
                await Assertions.Expect(page.GetByRole("button")).ToHaveTextAsync("Clicked!").ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should set PointerEvent.pressure on pointerdown")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSetPointerEventPressureOnPointerdown()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
    <button id=""target"">Click me</button>
    <script>
      window['pressures'] = [];
      document.addEventListener('pointerdown', e => window['pressures'].push(['pointerdown', e.pressure]));
      document.addEventListener('pointerup', e => window['pressures'].push(['pointerup', e.pressure]));
    </script>
  ").ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
                string json = await page.EvaluateAsync<string>("(() => JSON.stringify(window['pressures']))()").ConfigureAwait(false);
                Assert.That(json, Does.Contain("[\"pointerdown\",0.5]").Or.Contain("[\"pointerdown\", 0.5]"));
                Assert.That(json, Does.Contain("[\"pointerup\",0]").Or.Contain("[\"pointerup\", 0]"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should set PointerEvent.pressure on pointermove")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSetPointerEventPressureOnPointermove()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
    <body style=""margin: 0; padding: 0;"">
      <div id=""target"" style=""width: 500px; height: 500px; background-color: red;""></div>
      <script>
        window['pressures'] = [];
        document.addEventListener('pointermove', e => window['pressures'].push([e.pressure, e.clientX, e.clientY]));
      </script>
    </body>
  ").ConfigureAwait(false);
                await page.ClickAsync("div#target").ConfigureAwait(false);
                await page.Mouse.MoveAsync(10, 10).ConfigureAwait(false);
                await page.Mouse.DownAsync().ConfigureAwait(false);
                await page.Mouse.MoveAsync(250, 250).ConfigureAwait(false);
                await page.Mouse.UpAsync().ConfigureAwait(false);
                await page.Mouse.MoveAsync(50, 50).ConfigureAwait(false);
                string json = await page.EvaluateAsync<string>("(() => JSON.stringify(window['pressures']))()").ConfigureAwait(false);
                Assert.That(json, Does.Contain("[0,250,250]").Or.Contain("[0, 250, 250]"));
                Assert.That(json, Does.Contain("[0,10,10]").Or.Contain("[0, 10, 10]"));
                Assert.That(json, Does.Contain("[0.5,250,250]").Or.Contain("[0.5, 250, 250]"));
                Assert.That(json, Does.Contain("[0,50,50]").Or.Contain("[0, 50, 50]"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click into shadow root with slotted div")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickIntoShadowRootWithSlottedDiv()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
    <my-button>
      <template shadowrootmode=""open"">
        <button><slot></slot></button>
      </template>
      <div>Foo</div>
    </my-button>
  ").ConfigureAwait(false);
                await page.GetByRole("button", name: "Foo").ClickAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click shadow root button")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickShadowRootButton()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
    <my-button>
      <template shadowrootmode=""open"">
        <button><slot></slot></button>
      </template>
      <div>Foo</div>
    </my-button>
  ").ConfigureAwait(false);
                await page.Locator("my-button").ClickAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should click with tweened mouse movement")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickWithTweenedMouseMovement()
        {
            if (!IsHeadless)
            {
                Assert.Ignore("System cursor tends to interfere with this test");
            }

            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
    <body style=""margin: 0; padding: 0; height: 500px; width: 500px;"">
      <div style=""position: relative; top: 280px; left: 150px; width: 100px; height: 40px"">Click me</div>
    </body>
  ").ConfigureAwait(false);
                if (TestConstants.IsWebKit)
                {
                    await page.EvaluateAsync("(() => new Promise(requestAnimationFrame))()").ConfigureAwait(false);
                }

                await page.Mouse.MoveAsync(100, 100).ConfigureAwait(false);
                await page.EvaluateAsync(@"(() => {
    window['result'] = [];
    document.addEventListener('mousemove', event => {
      window['result'].push([event.clientX, event.clientY]);
    });
  })()").ConfigureAwait(false);
                await page.Locator("div").ClickAsync(new LocatorClickOptions { Steps = 5 }).ConfigureAwait(false);
                int[][] result = await page.EvaluateAsync<int[][]>("result").ConfigureAwait(false);
                Assert.That(result, Is.EqualTo(new[]
                {
                    new[] { 120, 140 },
                    new[] { 140, 180 },
                    new[] { 160, 220 },
                    new[] { 180, 260 },
                    new[] { 200, 300 },
                }));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should abort via signal")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAbortViaSignal()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<button style=\"display:none\">click me</button>").ConfigureAwait(false);
                AbortController controller = new AbortController();
                Task click = page.Locator("button").ClickAsync(new() { Timeout = 0 });
                await page.WaitForTimeoutAsync(500).ConfigureAwait(false);
                Exception reason = new Exception("foo bar");
                controller.Abort(reason);
                Exception error = Assert.CatchAsync(() => click);
                Assert.That(error, Is.InstanceOf<AbortError>());
                Assert.That(error.Message, Does.Contain("locator.click: foo bar"));
                Assert.That(error.Message, Does.Match("Call log:[\\s\\S]*operation was aborted: foo bar"));
                Assert.That(((AbortError)error).Cause, Is.SameAs(reason));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should throw an Error when aborted in-flight with a string reason")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowAnErrorWhenAbortedInFlightWithAStringReason()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<button style=\"display:none\">click me</button>").ConfigureAwait(false);
                AbortController controller = new AbortController();
                Task click = page.Locator("button").ClickAsync(new() { Timeout = 0 });
                controller.Abort("aborted by user");
                Exception error = Assert.CatchAsync(() => click);
                Assert.That(error, Is.InstanceOf<AbortError>());
                Assert.That(error.Message, Does.Contain("locator.click: aborted by user"));
                Assert.That(((AbortError)error).Cause, Is.EqualTo("aborted by user"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should abort via already-aborted signal")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAbortViaAlreadyAbortedSignal()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<button>click me</button>").ConfigureAwait(false);
                AbortController controller = new AbortController();
                Exception reason = new Exception("Already aborted");
                controller.Abort(reason);
                Exception error = Assert.CatchAsync(() => page.Locator("button").ClickAsync(new LocatorClickOptions { Signal = controller.Signal }));
                Assert.That(error, Is.InstanceOf<AbortError>());
                Assert.That(error.Message, Does.Contain("The operation was aborted"));
                Assert.That(((AbortError)error).Cause, Is.SameAs(reason));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click.spec.ts", "should throw an Error when aborted via an already-aborted signal with a string reason")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowAnErrorWhenAbortedViaAnAlreadyAbortedSignalWithAStringReason()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<button>click me</button>").ConfigureAwait(false);
                AbortController controller = new AbortController();
                controller.Abort("already aborted");
                Exception error = Assert.CatchAsync(() => page.Locator("button").ClickAsync(new LocatorClickOptions { Signal = controller.Signal }));
                Assert.That(error, Is.InstanceOf<AbortError>());
                Assert.That(((AbortError)error).Cause, Is.EqualTo("already aborted"));
            }).ConfigureAwait(false);
        }

    }
}
