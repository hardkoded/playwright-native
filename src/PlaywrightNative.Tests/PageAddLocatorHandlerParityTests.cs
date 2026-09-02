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
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-add-locator-handler.spec.ts</c> parity for overlay
    /// locator handlers. Skipped: none (Android-only <c>it.fixme</c> omitted).
    /// </summary>
    [TestFixture]
    public class PageAddLocatorHandlerParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private static bool IsHeadless
        {
            get
            {
                string value = Environment.GetEnvironmentVariable("HEADLESS");
                return string.IsNullOrEmpty(value) || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
            }
        }

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null && await FixtureReachableAsync(TestConstants.ServerUrl).ConfigureAwait(false))
            {
                Prefix = TestConstants.ServerUrl;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19791;
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

        [PlaywrightTest("page-add-locator-handler.spec.ts", "should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/handle-locator.html").ConfigureAwait(false);

            int beforeCount = 0;
            int afterCount = 0;
            ILocator originalLocator = page.GetByText("This interstitial covers the button");
            await page.AddLocatorHandlerAsync(originalLocator, async (ILocator locatorArgument) =>
            {
                Assert.That(locatorArgument, Is.SameAs(originalLocator));
                ++beforeCount;
                await page.Locator("#close").ClickAsync().ConfigureAwait(false);
                ++afterCount;
            }).ConfigureAwait(false);

            object[][] cases =
            {
                new object[] { "mouseover", 1 },
                new object[] { "mouseover", 1, "capture" },
                new object[] { "mouseover", 2 },
                new object[] { "mouseover", 2, "capture" },
                new object[] { "pointerover", 1 },
                new object[] { "pointerover", 1, "capture" },
                new object[] { "none", 1 },
                new object[] { "remove", 1 },
                new object[] { "hide", 1 },
            };

            foreach (object[] args in cases)
            {
                await page.Locator("#aside").HoverAsync().ConfigureAwait(false);
                beforeCount = 0;
                afterCount = 0;
                await page.EvaluateAsync<object>(
                    "args => { window.clicked = 0; window.setupAnnoyingInterstitial(...args); }",
                    args).ConfigureAwait(false);
                Assert.That(beforeCount, Is.EqualTo(0));
                Assert.That(afterCount, Is.EqualTo(0));
                await page.Locator("#target").ClickAsync().ConfigureAwait(false);
                int expected = Convert.ToInt32(args[1], CultureInfo.InvariantCulture);
                Assert.That(beforeCount, Is.EqualTo(expected));
                Assert.That(afterCount, Is.EqualTo(expected));
                Assert.That(await page.EvaluateAsync<int>("window.clicked").ConfigureAwait(false), Is.EqualTo(1));
                await Assertions.Expect(page.Locator("#interstitial")).Not.ToBeVisibleAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("page-add-locator-handler.spec.ts", "should work with a custom check")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithACustomCheck()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/handle-locator.html").ConfigureAwait(false);

            await page.AddLocatorHandlerAsync(
                page.Locator("body"),
                async () =>
                {
                    if (await page.GetByText("This interstitial covers the button").IsVisibleAsync().ConfigureAwait(false))
                    {
                        await page.Locator("#close").ClickAsync().ConfigureAwait(false);
                    }
                },
                noWaitAfter: true).ConfigureAwait(false);

            object[][] cases =
            {
                new object[] { "mouseover", 2 },
                new object[] { "none", 1 },
                new object[] { "remove", 1 },
                new object[] { "hide", 1 },
            };

            foreach (object[] args in cases)
            {
                await page.Locator("#aside").HoverAsync().ConfigureAwait(false);
                await page.EvaluateAsync<object>(
                    "args => { window.clicked = 0; window.setupAnnoyingInterstitial(...args); }",
                    args).ConfigureAwait(false);
                await page.Locator("#target").ClickAsync().ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<int>("window.clicked").ConfigureAwait(false), Is.EqualTo(1));
                await Assertions.Expect(page.Locator("#interstitial")).Not.ToBeVisibleAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("page-add-locator-handler.spec.ts", "should work with locator.hover()")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithLocatorHover()
        {
            if (!IsHeadless)
            {
                Assert.Ignore("Stray hovers in headed mode");
            }

            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/handle-locator.html").ConfigureAwait(false);

            await page.AddLocatorHandlerAsync(
                page.GetByText("This interstitial covers the button"),
                async () =>
                {
                    await page.Locator("#close").ClickAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);

            await page.Locator("#aside").HoverAsync().ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "() => { window.setupAnnoyingInterstitial('pointerover', 1, 'capture'); }")
                .ConfigureAwait(false);
            await page.Locator("#target").HoverAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#interstitial")).Not.ToBeVisibleAsync().ConfigureAwait(false);
            Assert.That(
                await page.EvalOnSelectorAsync<string>("#target", "e => window.getComputedStyle(e).backgroundColor")
                    .ConfigureAwait(false),
                Is.EqualTo("rgb(255, 255, 0)"));
        }

        [PlaywrightTest("page-add-locator-handler.spec.ts", "should not work with force:true")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotWorkWithForceTrue()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/handle-locator.html").ConfigureAwait(false);

            await page.AddLocatorHandlerAsync(
                page.GetByText("This interstitial covers the button"),
                async () =>
                {
                    await page.Locator("#close").ClickAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);

            await page.Locator("#aside").HoverAsync().ConfigureAwait(false);
            await page.EvaluateAsync<object>("() => { window.setupAnnoyingInterstitial('none', 1); }")
                .ConfigureAwait(false);
            await page.Locator("#target").ClickAsync(new() { Force = true, Timeout = 2000 }).ConfigureAwait(false);
            Assert.That(await page.Locator("#interstitial").IsVisibleAsync().ConfigureAwait(false), Is.True);
            Assert.That(await page.EvaluateAsync<object>("window.clicked").ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("page-add-locator-handler.spec.ts", "should throw when handler times out")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowWhenHandlerTimesOut()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/handle-locator.html").ConfigureAwait(false);

            int called = 0;
            await page.AddLocatorHandlerAsync(
                page.GetByText("This interstitial covers the button"),
                async () =>
                {
                    ++called;
                    await Task.Delay(Timeout.Infinite).ConfigureAwait(false);
                }).ConfigureAwait(false);

            await page.Locator("#aside").HoverAsync().ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "() => { window.clicked = 0; window.setupAnnoyingInterstitial('mouseover', 1); }")
                .ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => page.Locator("#target").ClickAsync(new() { Timeout = 3000 }));
            Assert.That(error.Message, Does.Contain("Timeout 3000ms exceeded"));

            TimeoutException error2 = Assert.ThrowsAsync<TimeoutException>(
                () => page.Locator("#target").ClickAsync(new() { Timeout = 3000 }));
            Assert.That(error2.Message, Does.Contain("Timeout 3000ms exceeded"));
            Assert.That(called, Is.EqualTo(1));
        }

        [PlaywrightTest("page-add-locator-handler.spec.ts", "should work with toBeVisible")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithToBeVisible()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/handle-locator.html").ConfigureAwait(false);

            int called = 0;
            await page.AddLocatorHandlerAsync(
                page.GetByText("This interstitial covers the button"),
                async () =>
                {
                    ++called;
                    await page.Locator("#close").ClickAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);

            await page.EvaluateAsync<object>(
                "() => { window.clicked = 0; window.setupAnnoyingInterstitial('remove', 1); }")
                .ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#target")).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#interstitial")).Not.ToBeVisibleAsync().ConfigureAwait(false);
            Assert.That(called, Is.EqualTo(1));
        }

        [PlaywrightTest("page-add-locator-handler.spec.ts", "should work with locator.waitFor")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithLocatorWaitFor()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/handle-locator.html").ConfigureAwait(false);

            int called = 0;
            await page.AddLocatorHandlerAsync(
                page.GetByText("This interstitial covers the button"),
                async () =>
                {
                    ++called;
                    await page.Locator("#close").ClickAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);

            await page.EvaluateAsync<object>(
                "() => { window.clicked = 0; window.setupAnnoyingInterstitial('remove', 1); }")
                .ConfigureAwait(false);
            await page.Locator("#target").WaitForAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#interstitial")).Not.ToBeVisibleAsync().ConfigureAwait(false);
            Assert.That(called, Is.EqualTo(1));
        }

        [PlaywrightTest("page-add-locator-handler.spec.ts", "should work with toHaveScreenshot")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithToHaveScreenshot()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            byte[] expected = await page.ScreenshotAsync().ConfigureAwait(false);

            await page.EvaluateAsync<object>(
                @"() => {
    const overlay = document.createElement('div');
    document.body.append(overlay);
    overlay.style.position = 'absolute';
    overlay.style.left = '0';
    overlay.style.right = '0';
    overlay.style.top = '0';
    overlay.style.bottom = '0';
    overlay.style.backgroundColor = 'red';

    const closeButton = document.createElement('button');
    overlay.appendChild(closeButton);
    closeButton.textContent = 'close';
    closeButton.addEventListener('click', () => overlay.remove());
}").ConfigureAwait(false);

            await page.AddLocatorHandlerAsync(
                page.GetByRole("button", name: "close"),
                async () =>
                {
                    await page.GetByRole("button", name: "close").ClickAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);

            await Assertions.Expect(page).ToHaveScreenshotAsync(expected).ConfigureAwait(false);
        }

        [PlaywrightTest("page-add-locator-handler.spec.ts", "should work when owner frame detaches")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWhenOwnerFrameDetaches()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);

            await page.EvaluateAsync<object>(
                @"() => {
    const iframe = document.createElement('iframe');
    iframe.src = 'data:text/html,<body>hello from iframe</body>';
    document.body.append(iframe);

    const target = document.createElement('button');
    target.textContent = 'Click me';
    target.id = 'target';
    target.addEventListener('click', () => window._clicked = true);
    document.body.appendChild(target);

    const closeButton = document.createElement('button');
    closeButton.textContent = 'close';
    closeButton.id = 'close';
    closeButton.addEventListener('click', () => iframe.remove());
    document.body.appendChild(closeButton);
}").ConfigureAwait(false);

            await page.AddLocatorHandlerAsync(
                page.FrameLocator("iframe").Locator("body"),
                async () =>
                {
                    await page.Locator("#close").ClickAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);

            await page.Locator("#target").ClickAsync().ConfigureAwait(false);
            Assert.That(await page.QuerySelectorAsync("iframe").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvaluateAsync<bool>("window._clicked").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-add-locator-handler.spec.ts", "should work with times: option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithTimesOption()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/handle-locator.html").ConfigureAwait(false);

            int called = 0;
            await page.AddLocatorHandlerAsync(
                page.Locator("body"),
                () =>
                {
                    ++called;
                    return Task.CompletedTask;
                },
                times: 2,
                noWaitAfter: true).ConfigureAwait(false);

            await page.Locator("#aside").HoverAsync().ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "() => { window.clicked = 0; window.setupAnnoyingInterstitial('mouseover', 4); }")
                .ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => page.Locator("#target").ClickAsync(new() { Timeout = 3000 }));
            Assert.That(called, Is.EqualTo(2));
            Assert.That(await page.EvaluateAsync<int>("window.clicked").ConfigureAwait(false), Is.EqualTo(0));
            await Assertions.Expect(page.Locator("#interstitial")).ToBeVisibleAsync().ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("Timeout 3000ms exceeded"));
            Assert.That(
                error.Message,
                Does.Contain("<div>This interstitial covers the button</div> from <div class=\"visible\" id=\"interstitial\">…</div> subtree intercepts pointer events"));
        }

        [PlaywrightTest("page-add-locator-handler.spec.ts", "should wait for hidden by default")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForHiddenByDefault()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/handle-locator.html").ConfigureAwait(false);

            int called = 0;
            await page.AddLocatorHandlerAsync(
                page.GetByRole("button", name: "close"),
                async (ILocator button) =>
                {
                    called++;
                    await button.ClickAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);

            await page.Locator("#aside").HoverAsync().ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "() => { window.clicked = 0; window.setupAnnoyingInterstitial('timeout', 1); }")
                .ConfigureAwait(false);
            await page.Locator("#target").ClickAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("window.clicked").ConfigureAwait(false), Is.EqualTo(1));
            await Assertions.Expect(page.Locator("#interstitial")).Not.ToBeVisibleAsync().ConfigureAwait(false);
            Assert.That(called, Is.EqualTo(1));
        }

        [PlaywrightTest("page-add-locator-handler.spec.ts", "should wait for hidden by default 2")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForHiddenByDefault2()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/handle-locator.html").ConfigureAwait(false);

            int called = 0;
            await page.AddLocatorHandlerAsync(
                page.GetByRole("button", name: "close"),
                (ILocator button) =>
                {
                    called++;
                    return Task.CompletedTask;
                }).ConfigureAwait(false);

            await page.Locator("#aside").HoverAsync().ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "() => { window.clicked = 0; window.setupAnnoyingInterstitial('hide', 1); }")
                .ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => page.Locator("#target").ClickAsync(new() { Timeout = 3000 }));
            Assert.That(await page.EvaluateAsync<int>("window.clicked").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await page.Locator("#interstitial").IsVisibleAsync().ConfigureAwait(false), Is.True);
            Assert.That(called, Is.EqualTo(1));
            Assert.That(
                error.Message,
                Does.Contain("locator handler has finished, waiting for getByRole('button', { name: 'close' }) to be hidden"));
        }

        [PlaywrightTest("page-add-locator-handler.spec.ts", "should work with noWaitAfter")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithNoWaitAfter()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/handle-locator.html").ConfigureAwait(false);

            int called = 0;
            await page.AddLocatorHandlerAsync(
                page.GetByRole("button", name: "close"),
                async (ILocator button) =>
                {
                    called++;
                    if (called == 1)
                    {
                        await button.ClickAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        await page.Locator("#interstitial").WaitForAsync(new() { State = WaitForSelectorState.Hidden }).ConfigureAwait(false);
                    }
                },
                noWaitAfter: true).ConfigureAwait(false);

            await page.Locator("#aside").HoverAsync().ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "() => { window.clicked = 0; window.setupAnnoyingInterstitial('timeout', 1); }")
                .ConfigureAwait(false);
            await page.Locator("#target").ClickAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("window.clicked").ConfigureAwait(false), Is.EqualTo(1));
            await Assertions.Expect(page.Locator("#interstitial")).Not.ToBeVisibleAsync().ConfigureAwait(false);
            Assert.That(called, Is.EqualTo(2));
        }

        [PlaywrightTest("page-add-locator-handler.spec.ts", "should removeLocatorHandler")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveLocatorHandler()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/handle-locator.html").ConfigureAwait(false);

            int called = 0;
            await page.AddLocatorHandlerAsync(
                page.GetByRole("button", name: "close"),
                async (ILocator locator) =>
                {
                    ++called;
                    await locator.ClickAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);

            await page.EvaluateAsync<object>(
                "() => { window.clicked = 0; window.setupAnnoyingInterstitial('hide', 1); }")
                .ConfigureAwait(false);
            await page.Locator("#target").ClickAsync().ConfigureAwait(false);
            Assert.That(called, Is.EqualTo(1));
            Assert.That(await page.EvaluateAsync<int>("window.clicked").ConfigureAwait(false), Is.EqualTo(1));
            await Assertions.Expect(page.Locator("#interstitial")).Not.ToBeVisibleAsync().ConfigureAwait(false);

            await page.EvaluateAsync<object>(
                "() => { window.clicked = 0; window.setupAnnoyingInterstitial('hide', 1); }")
                .ConfigureAwait(false);
            await page.RemoveLocatorHandlerAsync(page.GetByRole("button", name: "close")).ConfigureAwait(false);

            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => page.Locator("#target").ClickAsync(new() { Timeout = 3000 }));
            Assert.That(called, Is.EqualTo(1));
            Assert.That(await page.EvaluateAsync<int>("window.clicked").ConfigureAwait(false), Is.EqualTo(0));
            await Assertions.Expect(page.Locator("#interstitial")).ToBeVisibleAsync().ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("Timeout 3000ms exceeded"));
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static async Task<bool> FixtureReachableAsync(string origin)
        {
            try
            {
                using System.Net.Http.HttpClient client = new System.Net.Http.HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(1),
                };
                using System.Net.Http.HttpResponseMessage response = await client.GetAsync(origin + "/empty.html")
                    .ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
