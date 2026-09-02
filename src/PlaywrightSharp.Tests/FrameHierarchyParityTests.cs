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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>frame-hierarchy.spec.ts</c> parity for the frame tree,
    /// <c>FrameAttached</c> / <c>FrameDetached</c> / <c>FrameNavigated</c>,
    /// <see cref="IFrame.Name"/>, <see cref="IFrame.ParentFrame"/>,
    /// <see cref="IFrame.ChildFrames"/>, and <see cref="IFrame.Page"/>.
    /// Android/BiDi skips are not applied. Firefox x-frame-options is ignored
    /// with the upstream reason.
    /// </summary>
    [TestFixture]
    public class FrameHierarchyParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

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

        /// <summary>
        /// Official <c>dumpFrames</c>: sort children by URL then name, walk
        /// <see cref="IFrame.ChildFrames"/>.
        /// </summary>
        private static IReadOnlyList<string> DumpFrames(IFrame frame, string indentation = "")
        {
            string description = frame.Url;
            if (!string.IsNullOrEmpty(frame.Name))
            {
                description += " (" + frame.Name + ")";
            }

            List<string> result = new List<string>
            {
                indentation + description,
            };

            List<IFrame> childFrames = new List<IFrame>();
            foreach (IFrame child in frame.ChildFrames)
            {
                childFrames.Add(child);
            }

            childFrames.Sort((left, right) =>
            {
                int urlCompare = string.CompareOrdinal(left.Url, right.Url);
                if (urlCompare != 0)
                {
                    return urlCompare;
                }

                return string.CompareOrdinal(left.Name, right.Name);
            });

            foreach (IFrame child in childFrames)
            {
                result.AddRange(DumpFrames(child, "    " + indentation));
            }

            return result;
        }

        private static async Task<IFrame> AttachFrameAsync(IPage page, string frameId, string url)
        {
            string idJson = JsonSerializer.Serialize(frameId);
            string urlJson = JsonSerializer.Serialize(url);
            string script =
                "(async () => { const frame = document.createElement('iframe'); frame.src = " +
                urlJson +
                "; frame.id = " +
                idJson +
                "; frame.name = " +
                idJson +
                "; document.body.appendChild(frame); await new Promise(x => frame.onload = x); })()";
            await page.EvaluateAsync<object>(script).ConfigureAwait(false);

            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                IFrame named = page.Frame(frameId);
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

            Assert.Fail("Timed out waiting for frame " + frameId);
            return null;
        }

        private static async Task DetachFrameAsync(IPage page, string frameId)
        {
            string idJson = JsonSerializer.Serialize(frameId);
            await page.EvaluateAsync<object>(
                "(() => { const frame = document.getElementById(" + idJson + "); if (frame) frame.remove(); })()")
                .ConfigureAwait(false);

            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                IFrame named = page.Frame(frameId);
                if (named == null || named.IsDetached)
                {
                    return;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        private static IFrame FrameAt(IPage page, int index)
        {
            List<IFrame> frames = new List<IFrame>(page.Frames);
            Assert.That(frames.Count, Is.GreaterThan(index));
            return frames[index];
        }

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19850;
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

            if (TestServerSetup.Server != null && await FixtureReachableAsync(TestConstants.ServerUrl).ConfigureAwait(false))
            {
                Prefix = TestConstants.ServerUrl;
                CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
                EmptyPage = TestConstants.EmptyPage;
                return;
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

        [PlaywrightTest("frame-hierarchy.spec.ts", "should handle nested frames")]
        [PlaywrightTest("frame-hierarchy.spec.ts", "should handle nested frames @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHandleNestedFrames()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/frames/nested-frames.html").ConfigureAwait(false);
            Assert.That(DumpFrames(page.MainFrame), Is.EqualTo(new[]
            {
                Prefix + "/frames/nested-frames.html",
                "    " + Prefix + "/frames/frame.html (aframe)",
                "    " + Prefix + "/frames/two-frames.html (2frames)",
                "        " + Prefix + "/frames/frame.html (dos)",
                "        " + Prefix + "/frames/frame.html (uno)",
            }));
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should send events when frames are manipulated dynamically")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSendEventsWhenFramesAreManipulatedDynamically()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            List<IFrame> attachedFrames = new List<IFrame>();
            page.FrameAttached += (_, frame) => attachedFrames.Add(frame);
            await AttachFrameAsync(page, "frame1", "./assets/frame.html").ConfigureAwait(false);
            Assert.That(attachedFrames.Count, Is.EqualTo(1));
            Assert.That(attachedFrames[0].Url, Does.Contain("/assets/frame.html"));

            List<IFrame> navigatedFrames = new List<IFrame>();
            page.FrameNavigated += (_, frame) => navigatedFrames.Add(frame);
            await page.EvaluateAsync<object>(@"(() => {
                const frame = document.getElementById('frame1');
                frame.src = './empty.html';
                return new Promise(x => frame.onload = x);
            })()").ConfigureAwait(false);
            Assert.That(navigatedFrames.Count, Is.EqualTo(1));
            Assert.That(navigatedFrames[0].Url, Is.EqualTo(EmptyPage));

            List<IFrame> detachedFrames = new List<IFrame>();
            page.FrameDetached += (_, frame) => detachedFrames.Add(frame);
            await DetachFrameAsync(page, "frame1").ConfigureAwait(false);
            Assert.That(detachedFrames.Count, Is.EqualTo(1));
            Assert.That(detachedFrames[0].IsDetached, Is.True);
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should send \"framenavigated\" when navigating on anchor URLs")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSendFramenavigatedWhenNavigatingOnAnchorUrls()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            await Task.WhenAll(
                page.GoToAsync(EmptyPage + "#foo"),
                page.WaitForFrameNavigatedAsync()).ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(EmptyPage + "#foo"));
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should persist mainFrame on cross-process navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldPersistMainFrameOnCrossProcessNavigation()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame mainFrame = page.MainFrame;
            await page.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            Assert.That(page.MainFrame, Is.SameAs(mainFrame));
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should not send attach/detach events for main frame")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotSendAttachDetachEventsForMainFrame()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            bool hasEvents = false;
            page.FrameAttached += (_, _) => hasEvents = true;
            page.FrameDetached += (_, _) => hasEvents = true;
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(hasEvents, Is.False);
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should detach child frames on navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDetachChildFramesOnNavigation()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<IFrame> attachedFrames = new List<IFrame>();
            List<IFrame> detachedFrames = new List<IFrame>();
            List<IFrame> navigatedFrames = new List<IFrame>();
            page.FrameAttached += (_, frame) => attachedFrames.Add(frame);
            page.FrameDetached += (_, frame) => detachedFrames.Add(frame);
            page.FrameNavigated += (_, frame) => navigatedFrames.Add(frame);
            await page.GoToAsync(Prefix + "/frames/nested-frames.html").ConfigureAwait(false);
            Assert.That(attachedFrames.Count, Is.EqualTo(4));
            Assert.That(detachedFrames.Count, Is.EqualTo(0));
            Assert.That(navigatedFrames.Count, Is.EqualTo(5));

            attachedFrames = new List<IFrame>();
            detachedFrames = new List<IFrame>();
            navigatedFrames = new List<IFrame>();
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(attachedFrames.Count, Is.EqualTo(0));
            Assert.That(detachedFrames.Count, Is.EqualTo(4));
            Assert.That(navigatedFrames.Count, Is.EqualTo(1));
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should support framesets")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportFramesets()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<IFrame> attachedFrames = new List<IFrame>();
            List<IFrame> detachedFrames = new List<IFrame>();
            List<IFrame> navigatedFrames = new List<IFrame>();
            page.FrameAttached += (_, frame) => attachedFrames.Add(frame);
            page.FrameDetached += (_, frame) => detachedFrames.Add(frame);
            page.FrameNavigated += (_, frame) => navigatedFrames.Add(frame);
            await page.GoToAsync(Prefix + "/frames/frameset.html").ConfigureAwait(false);
            Assert.That(attachedFrames.Count, Is.EqualTo(4));
            Assert.That(detachedFrames.Count, Is.EqualTo(0));
            Assert.That(navigatedFrames.Count, Is.EqualTo(5));

            attachedFrames = new List<IFrame>();
            detachedFrames = new List<IFrame>();
            navigatedFrames = new List<IFrame>();
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(attachedFrames.Count, Is.EqualTo(0));
            Assert.That(detachedFrames.Count, Is.EqualTo(4));
            Assert.That(navigatedFrames.Count, Is.EqualTo(1));
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should report frame from-inside shadow DOM")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportFrameFromInsideShadowDom()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/shadow.html").ConfigureAwait(false);

            string urlJson = JsonSerializer.Serialize(EmptyPage);
            await page.EvaluateAsync<object>(
                "(async () => { const url = " + urlJson + @";
                    const frame = document.createElement('iframe');
                    frame.src = url;
                    document.body.shadowRoot.appendChild(frame);
                    await new Promise(x => frame.onload = x);
                })()").ConfigureAwait(false);

            List<IFrame> frames = new List<IFrame>(page.Frames);
            Assert.That(frames.Count, Is.EqualTo(2));
            Assert.That(frames[1].Url, Is.EqualTo(EmptyPage));
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should report frame.name()")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportFrameName()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await AttachFrameAsync(page, "theFrameId", EmptyPage).ConfigureAwait(false);
            string urlJson = JsonSerializer.Serialize(EmptyPage);
            await page.EvaluateAsync<object>(
                "(async () => { const url = " + urlJson + @";
                    const frame = document.createElement('iframe');
                    frame.name = 'theFrameName';
                    frame.src = url;
                    document.body.appendChild(frame);
                    await new Promise(x => frame.onload = x);
                })()").ConfigureAwait(false);

            Assert.That(FrameAt(page, 0).Name, Is.EqualTo(string.Empty));
            Assert.That(FrameAt(page, 1).Name, Is.EqualTo("theFrameId"));
            Assert.That(FrameAt(page, 2).Name, Is.EqualTo("theFrameName"));
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should report frame.parent()")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportFrameParent()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            await AttachFrameAsync(page, "frame2", EmptyPage).ConfigureAwait(false);
            Assert.That(FrameAt(page, 0).ParentFrame, Is.Null);
            Assert.That(FrameAt(page, 1).ParentFrame, Is.SameAs(page.MainFrame));
            Assert.That(FrameAt(page, 2).ParentFrame, Is.SameAs(page.MainFrame));
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should report different frame instance when frame re-attaches")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportDifferentFrameInstanceWhenFrameReAttaches()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IFrame frame1 = await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync<object>(@"(() => {
                window['frame'] = document.querySelector('#frame1');
                window['frame'].remove();
            })()").ConfigureAwait(false);
            Assert.That(frame1.IsDetached, Is.True);

            Task<IFrame> attached = page.WaitForFrameAttachedAsync();
            await page.EvaluateAsync<object>("(() => document.body.appendChild(window['frame']))()").ConfigureAwait(false);
            IFrame frame2 = await attached.ConfigureAwait(false);
            Assert.That(frame2.IsDetached, Is.False);
            Assert.That(frame1, Is.Not.SameAs(frame2));
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should refuse to display x-frame-options:deny iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRefuseToDisplayXFrameOptionsDenyIframe()
        {
            EnsureServer();
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("Firefox does not emit a console message for x-frame-options:deny.");
            }

            Server.Reset();
            Server.SetRoute("/x-frame-options-deny.html", async http =>
            {
                http.Response.ContentType = "text/html";
                http.Response.Headers["X-Frame-Options"] = "DENY";
                await http.Response.WriteAsync(
                    "<!DOCTYPE html><html><head><title>login</title></head><body style=\"background-color: red;\"><p>dangerous login page</p></body></html>")
                    .ConfigureAwait(false);
            });

            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);

                TaskCompletionSource<string> refusalText = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                page.Console += (_, message) =>
                {
                    string text = message == null ? string.Empty : (message.Text ?? string.Empty);
                    if (Regex.IsMatch(text, "Refused to display", RegexOptions.IgnoreCase))
                    {
                        refusalText.TrySetResult(text);
                    }
                };

                string iframeHtml = "<iframe src=\"" + Prefix + "/x-frame-options-deny.html\"></iframe>";
                await page.SetContentAsync(iframeHtml).ConfigureAwait(false);
                string refusal = await refusalText.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
                Assert.That(
                    refusal,
                    Does.Match("(?i)Refused to display .* in a frame because it set 'X-Frame-Options' to 'deny'."));
            }
            finally
            {
                Server.Reset();
            }
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should return frame.page()")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnFramePage()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);
            Assert.That(page.MainFrame.Page, Is.SameAs(page));
            IFrame child = null;
            foreach (IFrame frame in page.MainFrame.ChildFrames)
            {
                child = frame;
                break;
            }

            Assert.That(child, Is.Not.Null);
            Assert.That(child.Page, Is.SameAs(page));
        }
    }
}
