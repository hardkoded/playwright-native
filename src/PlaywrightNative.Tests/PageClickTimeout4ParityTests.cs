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
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-click-timeout-4.spec.ts</c> parity for unstable-position
    /// and overlay hit-target click timeouts. Skipped (Node-only internals):
    /// <c>should click for the second time after first timeout</c> uses
    /// <c>__testHookBeforePointerAction</c>.
    /// </summary>
    [TestFixture]
    public class PageClickTimeout4ParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19787;
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

        [PlaywrightTest("page-click-timeout-4.spec.ts", "should timeout waiting for stable position")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTimeoutWaitingForStablePosition()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("button").ConfigureAwait(false);
            await button.EvaluateAsync(@"button => {
    button.style.transition = 'margin 5s linear 0s';
    button.style.marginLeft = '200px';
}").ConfigureAwait(false);
            await page.EvaluateAsync(@"() => new Promise(r => {
    requestAnimationFrame(() => requestAnimationFrame(r));
})").ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => button.ClickAsync(new() { Timeout = 3000 }));
            Assert.That(error.Message, Does.Contain("elementHandle.click: Timeout 3000ms exceeded."));
            Assert.That(error.Message, Does.Contain("waiting for element to be visible, enabled and stable"));
            Assert.That(error.Message, Does.Contain("element is not stable"));
            Assert.That(error.Message, Does.Contain("retrying click action"));
        }

        [PlaywrightTest("page-click-timeout-4.spec.ts", "should fail to click the button behind a large header after scrolling around")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailToClickTheButtonBehindALargeHeaderAfterScrollingAround()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 240).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <style>
    * {
      padding: 0;
      margin: 0;
    }
    li {
      height: 80px;
      border: 1px solid black;
    }
    ol {
      padding-top: 160px;
    }
    div.fixed {
      position: fixed;
      z-index: 1001;
      width: 100%;
      background: rgba(255, 0, 0, 0.2);
      height: 2000px;
    }
    </style>

    <div class=fixed></div>

    <ol>
    <li>hi1</li><li>hi2</li><li>hi3</li><li>hi4</li><li>hi5</li><li>hi6</li><li>hi7</li><li>hi8</li>
    <li id=target onclick=""window.__clicked = true"">hi9</li>
    <li>hi10</li><li>hi11</li><li>hi12</li><li>hi13</li><li id=li14>hi14</li>
    </ol>

    <script>
      window.scrollTops = [];
      window.addEventListener('scroll', () => {
        window.scrollTops.push(window.scrollY);
      });
    </script>
  ").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("#li14", "e => e.scrollIntoView()").ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => page.ClickAsync("#target", new() { Timeout = 3500 }));
            Assert.That(error.Message, Does.Contain("<div class=\"fixed\"></div> intercepts pointer events"));
            Assert.That(await page.EvaluateAsync<object>("() => window['__clicked']").ConfigureAwait(false), Is.Null);
            double[] scrollTops = await page.EvaluateAsync<double[]>("() => window['scrollTops']").ConfigureAwait(false);
            HashSet<double> distinct = new HashSet<double>(scrollTops);
            Assert.That(distinct.Count, Is.GreaterThan(2));
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }
    }
}
