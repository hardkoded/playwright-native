/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>frame-evaluate.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android):
    /// <c>should dispose context on navigation</c>,
    /// <c>should dispose context on cross-origin navigation</c>.
    /// </summary>
    [TestFixture]
    public class FrameEvaluateTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19276;
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
                    CrossProcessPrefix = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
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

        [PlaywrightTest("frame-evaluate.spec.ts", "should have different execution contexts")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHaveDifferentExecutionContexts()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            Assert.That(page.Frames.Count, Is.EqualTo(2));

            List<IFrame> frames = new List<IFrame>(page.Frames);
            await frames[0].EvaluateAsync("(() => { window['FOO'] = 'foo'; })()").ConfigureAwait(false);
            await frames[1].EvaluateAsync("(() => { window['FOO'] = 'bar'; })()").ConfigureAwait(false);
            Assert.That(await frames[0].EvaluateAsync<string>("(() => window['FOO'])()").ConfigureAwait(false), Is.EqualTo("foo"));
            Assert.That(await frames[1].EvaluateAsync<string>("(() => window['FOO'])()").ConfigureAwait(false), Is.EqualTo("bar"));
        }

        [PlaywrightTest("frame-evaluate.spec.ts", "should have correct execution contexts")]
        [PlaywrightTest("frame-evaluate.spec.ts", "should have correct execution contexts @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHaveCorrectExecutionContexts()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);
            Assert.That(page.Frames.Count, Is.EqualTo(2));

            List<IFrame> frames = new List<IFrame>(page.Frames);
            Assert.That(await frames[0].EvaluateAsync<string>("(() => document.body.textContent.trim())()").ConfigureAwait(false), Is.Empty);
            Assert.That(await frames[1].EvaluateAsync<string>("(() => document.body.textContent.trim())()").ConfigureAwait(false), Is.EqualTo("Hi, I'm frame"));
        }

        [PlaywrightTest("frame-evaluate.spec.ts", "should dispose context on navigation")]
        [Test]
        [Timeout(30_000)]
        public void ShouldDisposeContextOnNavigation()
        {
            Assert.Ignore("Node-only internals: toImpl execution-context map (also skipped on Electron).");
        }

        [PlaywrightTest("frame-evaluate.spec.ts", "should dispose context on cross-origin navigation")]
        [Test]
        [Timeout(30_000)]
        public void ShouldDisposeContextOnCrossOriginNavigation()
        {
            Assert.Ignore("Node-only internals: toImpl execution-context map (also skipped on Electron).");
        }

        [PlaywrightTest("frame-evaluate.spec.ts", "should execute after cross-site navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldExecuteAfterCrossSiteNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame mainFrame = page.MainFrame;
            Assert.That(await mainFrame.EvaluateAsync<string>("(() => window.location.href)()").ConfigureAwait(false), Does.Contain(EmptyPage));
            await page.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            Assert.That(await mainFrame.EvaluateAsync<string>("(() => window.location.href)()").ConfigureAwait(false), Does.Contain(CrossProcessPrefix));
        }

        [PlaywrightTest("frame-evaluate.spec.ts", "should not allow cross-frame js handles")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotAllowCrossFrameJsHandles()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);
            IJSHandle handle = await page.EvaluateHandleAsync(@"() => {
                const iframe = document.querySelector('iframe');
                const foo = { bar: 'baz' };
                iframe.contentWindow['__foo'] = foo;
                return foo;
            }").ConfigureAwait(false);

            IFrame childFrame = FirstChild(page.MainFrame);
            Assert.That(childFrame, Is.Not.Null);

            JsonElement childResult = await childFrame.EvaluateAsync<JsonElement>("(() => window['__foo'])()").ConfigureAwait(false);
            Assert.That(childResult.GetProperty("bar").GetString(), Is.EqualTo("baz"));

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => childFrame.EvaluateAsync("foo => foo.bar", handle));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("JSHandles can be evaluated only in the context they were created!"));
        }

        [PlaywrightTest("frame-evaluate.spec.ts", "should allow cross-frame element handles")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAllowCrossFrameElementHandles()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);
            IFrame child = FirstChild(page.MainFrame);
            Assert.That(child, Is.Not.Null);

            IElementHandle bodyHandle = await child.QuerySelectorAsync("body").ConfigureAwait(false);
            string result = await page.EvaluateAsync<string>("body => body.innerHTML", bodyHandle).ConfigureAwait(false);
            Assert.That(result.Trim(), Is.EqualTo("<div>Hi, I'm frame</div>"));
        }

        [PlaywrightTest("frame-evaluate.spec.ts", "should not allow cross-frame element handles when frames do not script each other")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotAllowCrossFrameElementHandlesWhenFramesDoNotScriptEachOther()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame frame = await AttachFrameAsync(page, "frame1", CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            IElementHandle bodyHandle = await frame.QuerySelectorAsync("body").ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.EvaluateAsync("body => body.innerHTML", bodyHandle));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Unable to adopt element handle from a different document"));
        }

        [PlaywrightTest("frame-evaluate.spec.ts", "should throw for detached frames")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowForDetachedFrames()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame frame1 = await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            await DetachFrameAsync(page, "frame1").ConfigureAwait(false);

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => frame1.EvaluateAsync("(() => 7 * 8)()"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("frame.evaluate: Frame was detached"));
        }

        [PlaywrightTest("frame-evaluate.spec.ts", "should be isolated between frames")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldBeIsolatedBetweenFrames()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            Assert.That(page.Frames.Count, Is.EqualTo(2));

            List<IFrame> frames = new List<IFrame>(page.Frames);
            IFrame frame1 = frames[0];
            IFrame frame2 = frames[1];
            Assert.That(frame1, Is.Not.EqualTo(frame2));

            await Task.WhenAll(
                frame1.EvaluateAsync("(() => { window['a'] = 1; })()"),
                frame2.EvaluateAsync("(() => { window['a'] = 2; })()")).ConfigureAwait(false);

            int[] results = await Task.WhenAll(
                frame1.EvaluateAsync<int>("(() => window['a'])()"),
                frame2.EvaluateAsync<int>("(() => window['a'])()")).ConfigureAwait(false);
            Assert.That(results[0], Is.EqualTo(1));
            Assert.That(results[1], Is.EqualTo(2));
        }

        [PlaywrightTest("frame-evaluate.spec.ts", "should work in iframes that failed initial navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkInIframesThatFailedInitialNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<meta http-equiv=\"Content-Security-Policy\" content=\"script-src 'none';\">\n    <iframe src='javascript:\"\"'></iframe>", new() { WaitUntil = WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);

            await page.EvaluateAsync(@"(() => {
                const iframe = document.querySelector('iframe');
                const div = iframe.contentDocument.createElement('div');
                iframe.contentDocument.body.appendChild(div);
            })()").ConfigureAwait(false);

            List<IFrame> frames = new List<IFrame>(page.Frames);
            Assert.That(frames.Count, Is.GreaterThan(1));
            Assert.That(frames[1].Url, Is.EqualTo(TestConstants.IsWebKit ? "about:blank" : string.Empty));
            Assert.That(await frames[1].EvaluateAsync<string>("(() => window.location.href)()").ConfigureAwait(false), Is.EqualTo("about:blank"));
            Assert.That(await frames[1].QuerySelectorAsync("div").ConfigureAwait(false), Is.Not.Null);
        }

        [PlaywrightTest("frame-evaluate.spec.ts", "should work in iframes that interrupted initial javascript url navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkInIframesThatInterruptedInitialJavascriptUrlNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync(@"(() => {
                const iframe = document.createElement('iframe');
                iframe.src = 'javascript:""""';
                document.body.appendChild(iframe);
                iframe.contentDocument.open();
                iframe.contentDocument.write('<div>hello</div>');
                iframe.contentDocument.close();
            })()").ConfigureAwait(false);

            IFrame child = null;
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (child == null && DateTime.UtcNow < deadline)
            {
                List<IFrame> frames = new List<IFrame>(page.Frames);
                if (frames.Count > 1)
                {
                    child = frames[1];
                    break;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.That(child, Is.Not.Null);
            Assert.That(await child.EvaluateAsync<string>("(() => window.top.location.href)()").ConfigureAwait(false), Is.EqualTo(EmptyPage));
            Assert.That(await child.QuerySelectorAsync("div").ConfigureAwait(false), Is.Not.Null);
        }

        [PlaywrightTest("frame-evaluate.spec.ts", "evaluateHandle should work")]
        [Test]
        [Timeout(30_000)]
        public async Task EvaluateHandleShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame mainFrame = page.MainFrame;
            IJSHandle windowHandle = await mainFrame.EvaluateHandleAsync("() => window").ConfigureAwait(false);
            Assert.That(windowHandle, Is.Not.Null);
        }

        private static IFrame FirstChild(IFrame frame)
        {
            foreach (IFrame child in frame.ChildFrames)
            {
                return child;
            }

            return null;
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
                    foreach (IFrame child in page.MainFrame.ChildFrames)
                    {
                        named = child;
                        break;
                    }
                }

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
                    return named;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.Fail("Child frame was not created.");
            return null;
        }

        private static async Task DetachFrameAsync(IPage page, string name)
        {
            string nameJson = JsonSerializer.Serialize(name);
            await page.EvaluateAsync<object>(
                "(() => { const f = document.getElementById(" + nameJson + "); if (f) f.remove(); })()")
                .ConfigureAwait(false);

            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                IFrame named = page.Frame(name);
                if (named == null)
                {
                    return;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
        }
    }
}
