/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>elementhandle-owner-frame.spec.ts</c> parity for
    /// <see cref="IElementHandle.OwnerFrameAsync"/>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ElementHandleOwnerFrameParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static IFrame FrameAt(IPage page, int index)
        {
            int current = 0;
            foreach (IFrame frame in page.Frames)
            {
                if (current == index)
                {
                    return frame;
                }

                current++;
            }

            return null;
        }

        private static IFrame FirstChild(IFrame frame)
        {
            foreach (IFrame child in frame.ChildFrames)
            {
                return child;
            }

            return null;
        }

        private static async Task<IFrame> AttachFrameAsync(IPage page, string frameId, string url)
        {
            string frameIdJson = JsonSerializer.Serialize(frameId);
            string urlJson = JsonSerializer.Serialize(url);
            string script =
                "(() => new Promise(resolve => {" +
                "  const frame = document.createElement('iframe');" +
                "  frame.src = " + urlJson + ";" +
                "  frame.id = " + frameIdJson + ";" +
                "  frame.name = " + frameIdJson + ";" +
                "  frame.onload = () => resolve(true);" +
                "  document.body.appendChild(frame);" +
                "}))()";
            await page.EvaluateAsync<object>(script).ConfigureAwait(false);

            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                IFrame named = page.Frame(frameId);
                if (named == null)
                {
                    named = FrameAt(page, 1);
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

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19763;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    string origin = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
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

        [PlaywrightTest("elementhandle-owner-frame.spec.ts", "should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            IFrame frame = FrameAt(page, 1);
            IJSHandle elementHandle = await frame.EvaluateHandleAsync("() => document.body").ConfigureAwait(false);
            Assert.That(await elementHandle.AsElement().OwnerFrameAsync().ConfigureAwait(false), Is.SameAs(frame));
        }

        [PlaywrightTest("elementhandle-owner-frame.spec.ts", "should work for cross-process iframes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForCrossProcessIframes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await AttachFrameAsync(page, "frame1", CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            IFrame frame = FrameAt(page, 1);
            IJSHandle elementHandle = await frame.EvaluateHandleAsync("() => document.body").ConfigureAwait(false);
            Assert.That(await elementHandle.AsElement().OwnerFrameAsync().ConfigureAwait(false), Is.SameAs(frame));
        }

        [PlaywrightTest("elementhandle-owner-frame.spec.ts", "should work for document")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForDocument()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            IFrame frame = FrameAt(page, 1);
            IJSHandle elementHandle = await frame.EvaluateHandleAsync("() => document").ConfigureAwait(false);
            Assert.That(await elementHandle.AsElement().OwnerFrameAsync().ConfigureAwait(false), Is.SameAs(frame));
        }

        [PlaywrightTest("elementhandle-owner-frame.spec.ts", "should work for iframe elements")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForIframeElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            IFrame frame = page.MainFrame;
            IJSHandle elementHandle = await frame.EvaluateHandleAsync("() => document.querySelector('#frame1')").ConfigureAwait(false);
            Assert.That(await elementHandle.AsElement().OwnerFrameAsync().ConfigureAwait(false), Is.SameAs(frame));
        }

        [PlaywrightTest("elementhandle-owner-frame.spec.ts", "should work for cross-frame evaluations")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForCrossFrameEvaluations()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            IFrame frame = page.MainFrame;
            IJSHandle elementHandle = await frame.EvaluateHandleAsync("() => document.querySelector('iframe').contentWindow.document.body").ConfigureAwait(false);
            Assert.That(await elementHandle.AsElement().OwnerFrameAsync().ConfigureAwait(false), Is.SameAs(FirstChild(frame)));
        }

        [PlaywrightTest("elementhandle-owner-frame.spec.ts", "should work for detached elements")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForDetachedElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IJSHandle divHandle = await page.EvaluateHandleAsync(@"() => {
                const div = document.createElement('div');
                document.body.appendChild(div);
                return div;
            }").ConfigureAwait(false);
            Assert.That(await divHandle.AsElement().OwnerFrameAsync().ConfigureAwait(false), Is.SameAs(page.MainFrame));
            await page.EvaluateAsync("(() => {" +
                "  const div = document.querySelector('div');" +
                "  document.body.removeChild(div);" +
                "})()").ConfigureAwait(false);
            Assert.That(await divHandle.AsElement().OwnerFrameAsync().ConfigureAwait(false), Is.SameAs(page.MainFrame));
        }

        [PlaywrightTest("elementhandle-owner-frame.spec.ts", "should work for adopted elements")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForAdoptedElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IPage> popupTask = page.WaitForPopupAsync();
            Task evaluateTask = page.EvaluateAsync<object>(
                "url => { window['__popup'] = window.open(url); }",
                EmptyPage);
            await Task.WhenAll(popupTask, evaluateTask).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            IJSHandle divHandle = await page.EvaluateHandleAsync(@"() => {
                const div = document.createElement('div');
                document.body.appendChild(div);
                return div;
            }").ConfigureAwait(false);
            Assert.That(await divHandle.AsElement().OwnerFrameAsync().ConfigureAwait(false), Is.SameAs(page.MainFrame));
            await popup.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
            await page.EvaluateAsync("(() => {" +
                "  const div = document.querySelector('div');" +
                "  window['__popup'].document.body.appendChild(div);" +
                "})()").ConfigureAwait(false);
            Assert.That(await divHandle.AsElement().OwnerFrameAsync().ConfigureAwait(false), Is.SameAs(popup.MainFrame));
        }
    }
}
