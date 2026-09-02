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
    /// Official <c>frame-frame-element.spec.ts</c> parity for
    /// <see cref="IFrame.FrameElementAsync"/>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class FrameFrameElementParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null && await FixtureReachableAsync(TestConstants.ServerUrl).ConfigureAwait(false))
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19753;
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

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
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
                "  frame.onload = () => resolve(true);" +
                "  document.body.appendChild(frame);" +
                "}))()";
            await page.EvaluateAsync<object>(script).ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#" + frameId).ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            IFrame content = await handle.ContentFrameAsync().ConfigureAwait(false);
            Assert.That(content, Is.Not.Null);
            return content;
        }

        private static async Task<IFrame> WaitForChildFrameAsync(IPage page)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                List<IFrame> frames = new List<IFrame>(page.Frames);
                if (frames.Count >= 2)
                {
                    return frames[1];
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.Fail("Child frame was not created.");
            return null;
        }

        [PlaywrightTest("frame-frame-element.spec.ts", "should work")]
        [PlaywrightTest("frame-frame-element.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            EnsureServer();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame frame1 = await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            await AttachFrameAsync(page, "frame2", EmptyPage).ConfigureAwait(false);
            IFrame frame3 = await AttachFrameAsync(page, "frame3", EmptyPage).ConfigureAwait(false);
            IElementHandle frame1handle1 = await page.QuerySelectorAsync("#frame1").ConfigureAwait(false);
            IElementHandle frame1handle2 = await frame1.FrameElementAsync().ConfigureAwait(false);
            IElementHandle frame3handle1 = await page.QuerySelectorAsync("#frame3").ConfigureAwait(false);
            IElementHandle frame3handle2 = await frame3.FrameElementAsync().ConfigureAwait(false);
            Assert.That(await frame1handle1.EvaluateAsync<bool>("(a, b) => a === b", frame1handle2).ConfigureAwait(false), Is.True);
            Assert.That(await frame3handle1.EvaluateAsync<bool>("(a, b) => a === b", frame3handle2).ConfigureAwait(false), Is.True);
            Assert.That(await frame1handle1.EvaluateAsync<bool>("(a, b) => a === b", frame3handle1).ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("frame-frame-element.spec.ts", "should work with contentFrame")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithContentFrame()
        {
            EnsureServer();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame frame = await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            IElementHandle handle = await frame.FrameElementAsync().ConfigureAwait(false);
            IFrame contentFrame = await handle.ContentFrameAsync().ConfigureAwait(false);
            Assert.That(contentFrame, Is.SameAs(frame));
        }

        [PlaywrightTest("frame-frame-element.spec.ts", "should work with frameset")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithFrameset()
        {
            EnsureServer();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/frames/frameset.html").ConfigureAwait(false);
            IElementHandle frameElement1 = await page.QuerySelectorAsync("frame").ConfigureAwait(false);
            IFrame frame = await frameElement1.ContentFrameAsync().ConfigureAwait(false);
            IElementHandle frameElement2 = await frame.FrameElementAsync().ConfigureAwait(false);
            Assert.That(await frameElement1.EvaluateAsync<bool>("(a, b) => a === b", frameElement2).ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("frame-frame-element.spec.ts", "should throw when detached")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowWhenDetached()
        {
            EnsureServer();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame frame1 = await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("#frame1", "e => e.remove()").ConfigureAwait(false);
            PlaywrightSharpException error = Assert.ThrowsAsync<PlaywrightSharpException>(
                () => frame1.FrameElementAsync());
            Assert.That(error.Message, Does.Contain("Frame has been detached."));
        }

        [PlaywrightTest("frame-frame-element.spec.ts", "should work inside closed shadow root")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkInsideClosedShadowRoot()
        {
            EnsureServer();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div id=framecontainer>
    </div>
    <script>
      const iframe = document.createElement('iframe');
      iframe.setAttribute('name', 'myframe');
      iframe.setAttribute('srcdoc', 'find me');
      const div = document.getElementById('framecontainer');
      const host = div.attachShadow({ mode: 'closed' });
      host.appendChild(iframe);
    </script>
  ").ConfigureAwait(false);

            IFrame frame = await WaitForChildFrameAsync(page).ConfigureAwait(false);
            IElementHandle element = await frame.FrameElementAsync().ConfigureAwait(false);
            Assert.That(await element.GetAttributeAsync("name").ConfigureAwait(false), Is.EqualTo("myframe"));
        }

        [PlaywrightTest("frame-frame-element.spec.ts", "should work inside declarative shadow root")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkInsideDeclarativeShadowRoot()
        {
            EnsureServer();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div>
      <template shadowrootmode=""open"">
        <iframe name=""myframe"" srcdoc=""<h1>Hi!</h1>""></iframe>
        <slot></slot>
      </template>
      <span>footer</span>
    </div>
  ").ConfigureAwait(false);

            IFrame frame = await WaitForChildFrameAsync(page).ConfigureAwait(false);
            IElementHandle element = await frame.FrameElementAsync().ConfigureAwait(false);
            Assert.That(await element.GetAttributeAsync("name").ConfigureAwait(false), Is.EqualTo("myframe"));
        }
    }
}
