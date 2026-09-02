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
    /// Official <c>locator-click.spec.ts</c> parity for locator <see cref="ILocator.ClickAsync"/>
    /// and <see cref="ILocator.DblClickAsync"/>.
    /// </summary>
    [TestFixture]
    public class LocatorClickParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19110;
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

        [PlaywrightTest("locator-click.spec.ts", "should work")]
        [PlaywrightTest("locator-click.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            ILocator button = page.Locator("button");
            await button.ClickAsync().ConfigureAwait(false);
            string result = await page.EvaluateAsync<string>("window.result").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo("Clicked"));
        }

        [PlaywrightTest("locator-click.spec.ts", "should work with Node removed")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithNodeRemoved()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            await page.EvaluateAsync("(() => { delete window.Node; })()").ConfigureAwait(false);
            ILocator button = page.Locator("button");
            await button.ClickAsync().ConfigureAwait(false);
            string result = await page.EvaluateAsync<string>("window.result").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo("Clicked"));
        }

        [PlaywrightTest("locator-click.spec.ts", "should double click the button")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDoubleClickTheButton()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            await page.EvaluateAsync(@"(() => {
                window['double'] = false;
                const button = document.querySelector('button');
                button.addEventListener('dblclick', event => {
                    window['double'] = true;
                });
            })()").ConfigureAwait(false);
            ILocator button = page.Locator("button");
            await button.DblClickAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("double").ConfigureAwait(false), Is.True);
            Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Clicked"));
        }

        [PlaywrightTest("locator-click.spec.ts", "should click if the target element is removed in pointerup event")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickIfTheTargetElementIsRemovedInPointerupEvent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=clickme>Clickable</button>").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("#clickme", "element => element.addEventListener('pointerup', () => element.remove(), false)").ConfigureAwait(false);
            await page.Locator("#clickme").ClickAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("locator-click.spec.ts", "should click if the target element is removed in pointerdown event")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickIfTheTargetElementIsRemovedInPointerdownEvent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=clickme>Clickable</button>").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("#clickme", "element => element.addEventListener('pointerdown', () => element.remove(), false)").ConfigureAwait(false);
            await page.Locator("#clickme").ClickAsync().ConfigureAwait(false);
        }
    }
}
