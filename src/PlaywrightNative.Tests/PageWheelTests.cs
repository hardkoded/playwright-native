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
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>wheel.spec.ts</c> parity for <see cref="IMouse.WheelAsync"/>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// File-level Android <c>it.skip</c> is not applied.
    /// </summary>
    [TestFixture]
    public class PageWheelTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        /// <summary>
        /// Chromium on macOS reports <c>deltaX</c>/<c>deltaY</c> scaled by the host
        /// device scale factor. Upstream ignores those fields instead of guessing
        /// the scale factor.
        /// https://bugs.chromium.org/p/chromium/issues/detail?id=1324819
        /// https://github.com/microsoft/playwright/issues/7362
        /// </summary>
        private static bool IgnoreDelta => TestConstants.IsChromium && TestConstants.IsMacOSX;

        private static async Task RafrafAsync(IPage page, int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                await page.EvaluateAsync<object>("new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))").ConfigureAwait(false);
            }
        }

        private static Task ListenForWheelEventsAsync(IPage page, string selector)
        {
            string selectorJson = JsonSerializer.Serialize(selector);
            string script =
                "((selector) => {" +
                "  document.querySelector(selector).addEventListener('wheel', event => {" +
                "    window['lastEvent'] = {" +
                "      deltaX: event.deltaX," +
                "      deltaY: event.deltaY," +
                "      clientX: event.clientX," +
                "      clientY: event.clientY," +
                "      deltaMode: event.deltaMode," +
                "      ctrlKey: event.ctrlKey," +
                "      shiftKey: event.shiftKey," +
                "      altKey: event.altKey," +
                "      metaKey: event.metaKey" +
                "    };" +
                "  }, { passive: false });" +
                "})(" + selectorJson + ")";
            return page.EvaluateAsync(script);
        }

        private static async Task ExpectEventAsync(
            IPage page,
            double deltaX,
            double deltaY,
            double clientX,
            double clientY,
            int deltaMode,
            bool ctrlKey,
            bool shiftKey,
            bool altKey,
            bool metaKey)
        {
            await page.WaitForFunctionAsync("window.lastEvent").ConfigureAwait(false);
            JsonElement received = await page.EvaluateAsync<JsonElement>("window.lastEvent").ConfigureAwait(false);
            Assert.That(received.ValueKind, Is.EqualTo(JsonValueKind.Object));

            if (!IgnoreDelta)
            {
                Assert.That(received.GetProperty("deltaX").GetDouble(), Is.EqualTo(deltaX));
                Assert.That(received.GetProperty("deltaY").GetDouble(), Is.EqualTo(deltaY));
            }

            Assert.That(received.GetProperty("clientX").GetDouble(), Is.EqualTo(clientX));
            Assert.That(received.GetProperty("clientY").GetDouble(), Is.EqualTo(clientY));
            Assert.That(received.GetProperty("deltaMode").GetInt32(), Is.EqualTo(deltaMode));
            Assert.That(received.GetProperty("ctrlKey").GetBoolean(), Is.EqualTo(ctrlKey));
            Assert.That(received.GetProperty("shiftKey").GetBoolean(), Is.EqualTo(shiftKey));
            Assert.That(received.GetProperty("altKey").GetBoolean(), Is.EqualTo(altKey));
            Assert.That(received.GetProperty("metaKey").GetBoolean(), Is.EqualTo(metaKey));
        }

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19728;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    Prefix = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
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

        [PlaywrightTest("wheel.spec.ts", "should dispatch wheel events")]
        [PlaywrightTest("wheel.spec.ts", "should dispatch wheel events @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchWheelEvents()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div style=\"width: 5000px; height: 5000px;\"></div>").ConfigureAwait(false);
            await page.Mouse.MoveAsync(50, 60).ConfigureAwait(false);
            await ListenForWheelEventsAsync(page, "div").ConfigureAwait(false);
            await page.Mouse.WheelAsync(0, 100).ConfigureAwait(false);
            await page.WaitForFunctionAsync("window.scrollY === 100").ConfigureAwait(false);
            await ExpectEventAsync(page, 0, 100, 50, 60, 0, false, false, false, false).ConfigureAwait(false);
        }

        [PlaywrightTest("wheel.spec.ts", "should dispatch wheel events after popup was opened")]
        [PlaywrightTest("wheel.spec.ts", "should dispatch wheel events after popup was opened @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchWheelEventsAfterPopupWasOpened()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div style=\"width: 5000px; height: 5000px;\"></div>").ConfigureAwait(false);
            await page.Mouse.MoveAsync(50, 60).ConfigureAwait(false);
            await ListenForWheelEventsAsync(page, "div").ConfigureAwait(false);
            Task<IPage> popupTask = page.WaitForPopupAsync();
            await page.EvaluateAsync("(() => { window.open(''); })()").ConfigureAwait(false);
            await popupTask.ConfigureAwait(false);
            await page.Mouse.WheelAsync(0, 100).ConfigureAwait(false);
            await page.WaitForFunctionAsync("window.scrollY === 100").ConfigureAwait(false);
            await ExpectEventAsync(page, 0, 100, 50, 60, 0, false, false, false, false).ConfigureAwait(false);
        }

        [PlaywrightTest("wheel.spec.ts", "should dispatch wheel event on svg element")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchWheelEventOnSvgElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            // https://github.com/microsoft/playwright/issues/15566
            await page.SetContentAsync(
                "<body>" +
                "  <svg class=\"scroll-box\"></svg>" +
                "</body>" +
                "<style>" +
                "  .scroll-box {" +
                "    position: absolute;" +
                "    top: 0px;" +
                "    left: 0px;" +
                "    background-color: brown;" +
                "    width: 200px;" +
                "    height: 200px;" +
                "  }" +
                "</style>").ConfigureAwait(false);
            await ListenForWheelEventsAsync(page, "svg").ConfigureAwait(false);
            await page.Mouse.MoveAsync(100, 100).ConfigureAwait(false);
            await page.Mouse.WheelAsync(0, 100).ConfigureAwait(false);
            await page.WaitForFunctionAsync("!!window.lastEvent").ConfigureAwait(false);
            await ExpectEventAsync(page, 0, 100, 100, 100, 0, false, false, false, false).ConfigureAwait(false);
        }

        [PlaywrightTest("wheel.spec.ts", "should scroll when nobody is listening")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldScrollWhenNobodyIsListening()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/input/scrollable.html").ConfigureAwait(false);
            await page.Mouse.MoveAsync(50, 60).ConfigureAwait(false);
            await page.Mouse.WheelAsync(0, 100).ConfigureAwait(false);
            await page.WaitForFunctionAsync("window.scrollY === 100").ConfigureAwait(false);
        }

        [PlaywrightTest("wheel.spec.ts", "should set the modifiers")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSetTheModifiers()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div style=\"width: 5000px; height: 5000px;\"></div>").ConfigureAwait(false);
            await page.Mouse.MoveAsync(50, 60).ConfigureAwait(false);
            await ListenForWheelEventsAsync(page, "div").ConfigureAwait(false);
            await page.Keyboard.DownAsync("Shift").ConfigureAwait(false);
            await page.Mouse.WheelAsync(0, 100).ConfigureAwait(false);
            await ExpectEventAsync(page, 0, 100, 50, 60, 0, false, true, false, false).ConfigureAwait(false);
        }

        [PlaywrightTest("wheel.spec.ts", "should scroll horizontally")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldScrollHorizontally()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div style=\"width: 5000px; height: 5000px;\"></div>").ConfigureAwait(false);
            await page.Mouse.MoveAsync(50, 60).ConfigureAwait(false);
            await ListenForWheelEventsAsync(page, "div").ConfigureAwait(false);
            await page.Mouse.WheelAsync(100, 0).ConfigureAwait(false);
            await ExpectEventAsync(page, 100, 0, 50, 60, 0, false, false, false, false).ConfigureAwait(false);
            await page.WaitForFunctionAsync("window.scrollX === 100").ConfigureAwait(false);
        }

        [PlaywrightTest("wheel.spec.ts", "should work when the event is canceled")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWhenTheEventIsCanceled()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div style=\"width: 5000px; height: 5000px;\"></div>").ConfigureAwait(false);
            await page.Mouse.MoveAsync(50, 60).ConfigureAwait(false);
            await ListenForWheelEventsAsync(page, "div").ConfigureAwait(false);
            await page.EvaluateAsync(@"(() => {
                document.querySelector('div').addEventListener('wheel', e => e.preventDefault());
            })()").ConfigureAwait(false);
            await RafrafAsync(page, 10).ConfigureAwait(false);
            await page.Mouse.WheelAsync(0, 100).ConfigureAwait(false);
            await ExpectEventAsync(page, 0, 100, 50, 60, 0, false, false, false, false).ConfigureAwait(false);
            await page.WaitForFunctionAsync("!!window['lastEvent']").ConfigureAwait(false);
            double scrollY = await page.EvaluateAsync<double>("window.scrollY").ConfigureAwait(false);
            Assert.That(scrollY, Is.EqualTo(0));
        }
    }
}
