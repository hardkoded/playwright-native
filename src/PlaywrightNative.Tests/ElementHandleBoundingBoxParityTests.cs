/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>elementhandle-bounding-box.spec.ts</c>.
    /// Android <c>it.skip</c> is Android-only and is not applied.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ElementHandleBoundingBoxParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static void AssertBox(ElementHandleBoundingBoxResult box, float x, float y, float width, float height)
        {
            Assert.That(box, Is.Not.Null);
            Assert.That(box.X, Is.EqualTo(x));
            Assert.That(box.Y, Is.EqualTo(y));
            Assert.That(box.Width, Is.EqualTo(width));
            Assert.That(box.Height, Is.EqualTo(height));
        }

        private static async Task<IFrame> WaitForAttachedChildFrameAsync(IPage page)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                foreach (IFrame candidate in page.Frames)
                {
                    if (candidate != null && candidate.ParentFrame != null)
                    {
                        return candidate;
                    }
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            throw new TimeoutException("Timed out waiting for child frame.");
        }

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

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19746;
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

        [PlaywrightTest("elementhandle-bounding-box.spec.ts", "should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync(".box:nth-of-type(13)").ConfigureAwait(false);
            var box = await elementHandle.BoundingBoxAsync().ConfigureAwait(false);
            AssertBox(box, 100, 50, 50, 50);
        }

        [PlaywrightTest("elementhandle-bounding-box.spec.ts", "should handle nested frames")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHandleNestedFrames()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(616, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/frames/nested-frames.html").ConfigureAwait(false);
            IFrameLocator nestedFrame = page.FrameLocator("[name=\"2frames\"]").FrameLocator("[name=dos]");
            IElementHandle elementHandle = await nestedFrame.Locator("div").ElementHandleAsync().ConfigureAwait(false);
            var box = await elementHandle.BoundingBoxAsync().ConfigureAwait(false);
            AssertBox(box, 24, 224, 268, 18);
        }

        [PlaywrightTest("elementhandle-bounding-box.spec.ts", "should get frame box")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldGetFrameBox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(250, 250).ConfigureAwait(false);
            await page.SetContentAsync(@"<style>
  body {
      display: flex;
      height: 500px;
      margin: 0px;
  }
  body iframe {
      flex-shrink: 1;
      border: 0;
      background-color: green;
  }
  </style>
  <iframe></iframe>
  ").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync("iframe").ConfigureAwait(false);
            var box = await elementHandle.BoundingBoxAsync().ConfigureAwait(false);
            AssertBox(box, 0, 0, 300, 500);
        }

        [PlaywrightTest("elementhandle-bounding-box.spec.ts", "should handle scroll offset and click")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHandleScrollOffsetAndClick()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <style>* { margin: 0; padding: 0; }</style>
    <div style=""width:8000px; height:8000px;"">
      <div id=target style=""width:20px; height:20px; margin-left:230px; margin-top:340px;""
        onclick=""window.__clicked = true"">
      </div>
    </div>
  ").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync("#target").ConfigureAwait(false);
            var box1 = await elementHandle.BoundingBoxAsync().ConfigureAwait(false);
            await page.EvaluateAsync<object>("(() => { window.scrollBy(200, 300); return true; })()").ConfigureAwait(false);
            var box2 = await elementHandle.BoundingBoxAsync().ConfigureAwait(false);
            AssertBox(box1, 230, 340, 20, 20);
            AssertBox(box2, 30, 40, 20, 20);
            await page.Mouse.ClickAsync(box2.X + 10, box2.Y + 10).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("(() => window['__clicked'])()").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("elementhandle-bounding-box.spec.ts", "should return null for invisible elements")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnNullForInvisibleElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"display:none\">hi</div>").ConfigureAwait(false);
            IElementHandle element = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            Assert.That(await element.BoundingBoxAsync().ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("elementhandle-bounding-box.spec.ts", "should get bounding box of element inside a cross-origin iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldGetBoundingBoxOfElementInsideACrossOriginIframe()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync(
                "    <div style=\"height:120px\"></div>\n" +
                "    <iframe style=\"border:0;margin-left:30px;width:300px;height:200px\" src=\"" +
                CrossProcessPrefix +
                "/input/button.html\"></iframe>").ConfigureAwait(false);
            IFrame frame = await WaitForAttachedChildFrameAsync(page).ConfigureAwait(false);

            IElementHandle button = await frame.WaitForSelectorAsync("button").ConfigureAwait(false);
            var iframeBox = await (await page.QuerySelectorAsync("iframe").ConfigureAwait(false))
                .BoundingBoxAsync()
                .ConfigureAwait(false);
            float[] inner = await button.EvaluateAsync<float[]>(
                @"b => {
                    const r = b.getBoundingClientRect();
                    return [r.left, r.top];
                }").ConfigureAwait(false);
            var box = await button.BoundingBoxAsync().ConfigureAwait(false);
            Assert.That(box, Is.Not.Null);
            Assert.That(iframeBox, Is.Not.Null);
            Assert.That(Math.Round(box.X), Is.EqualTo(Math.Round(iframeBox.X + inner[0])));
            Assert.That(Math.Round(box.Y), Is.EqualTo(Math.Round(iframeBox.Y + inner[1])));
        }

        [PlaywrightTest("elementhandle-bounding-box.spec.ts", "should force a layout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldForceALayout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"width: 100px; height: 100px\">hello</div>").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            await elementHandle.EvaluateAsync<bool>("element => { element.style.height = '200px'; return true; }").ConfigureAwait(false);
            var box = await elementHandle.BoundingBoxAsync().ConfigureAwait(false);
            AssertBox(box, 8, 8, 100, 200);
        }

        [PlaywrightTest("elementhandle-bounding-box.spec.ts", "should work with SVG nodes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithSVGNodes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
      <svg xmlns=""http://www.w3.org/2000/svg"" width=""500"" height=""500"">
        <rect id=""theRect"" x=""30"" y=""50"" width=""200"" height=""300""></rect>
      </svg>
    ").ConfigureAwait(false);
            IElementHandle element = await page.QuerySelectorAsync("#therect").ConfigureAwait(false);
            var pwBoundingBox = await element.BoundingBoxAsync().ConfigureAwait(false);
            float[] web = await element.EvaluateAsync<float[]>(
                @"e => {
                    const rect = e.getBoundingClientRect();
                    return [rect.x, rect.y, rect.width, rect.height];
                }").ConfigureAwait(false);
            Assert.That(pwBoundingBox, Is.Not.Null);
            Assert.That(pwBoundingBox.X, Is.EqualTo(web[0]));
            Assert.That(pwBoundingBox.Y, Is.EqualTo(web[1]));
            Assert.That(pwBoundingBox.Width, Is.EqualTo(web[2]));
            Assert.That(pwBoundingBox.Height, Is.EqualTo(web[3]));
        }

        [PlaywrightTest("elementhandle-bounding-box.spec.ts", "should work when inline box child is outside of viewport")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWhenInlineBoxChildIsOutsideOfViewport()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
      <style>
      i {
        position: absolute;
        top: -1000px;
      }
      body {
        margin: 0;
        font-size: 12px;
      }
      </style>
      <span><i>woof</i><b>doggo</b></span>
    ").ConfigureAwait(false);
            IElementHandle handle = await page.QuerySelectorAsync("span").ConfigureAwait(false);
            var box = await handle.BoundingBoxAsync().ConfigureAwait(false);
            float[] web = await handle.EvaluateAsync<float[]>(
                @"e => {
                    const rect = e.getBoundingClientRect();
                    return [rect.x, rect.y, rect.width, rect.height];
                }").ConfigureAwait(false);
            Assert.That(box, Is.Not.Null);
            Assert.That(Math.Round(box.X * 100), Is.EqualTo(Math.Round(web[0] * 100)));
            Assert.That(Math.Round(box.Y * 100), Is.EqualTo(Math.Round(web[1] * 100)));
            Assert.That(Math.Round(box.Width * 100), Is.EqualTo(Math.Round(web[2] * 100)));
            Assert.That(Math.Round(box.Height * 100), Is.EqualTo(Math.Round(web[3] * 100)));
        }
    }
}
