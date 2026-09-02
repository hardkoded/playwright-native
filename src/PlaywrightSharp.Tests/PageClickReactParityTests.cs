/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page-click-react.spec.ts</c> parity for click timeout
    /// when a dialog opens and for React hover retargeting. Skipped: none.
    /// </summary>
    [TestFixture]
    public class PageClickReactParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null && await FixtureReachableAsync(TestConstants.ServerUrl).ConfigureAwait(false))
            {
                Prefix = TestConstants.ServerUrl;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19790;
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

        [PlaywrightTest("page-click-react.spec.ts", "should timeout when click opens alert")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTimeoutWhenClickOpensAlert()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IDialog> dialogPromise = page.WaitForEventAsync(PageEvent.Dialog);
            await page.SetContentAsync("<div onclick='window.alert(123)'>Click me</div>").ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => page.ClickAsync("div", new() { Timeout = 3000 }));
            Assert.That(error.Message, Does.Contain("page.click: Timeout 3000ms exceeded."));
            IDialog dialog = await dialogPromise.ConfigureAwait(false);
            await dialog.DismissAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("page-click-react.spec.ts", "should not retarget when element changes on hover")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotRetargetWhenElementChangesOnHover()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/react.html").ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "() => { renderComponent(e('div', {}, [e(MyButton, { name: 'button1', renameOnHover: true }), e(MyButton, { name: 'button2' })])); }")
                .ConfigureAwait(false);
            await page.ClickAsync("text=button1").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.button1").ConfigureAwait(false), Is.True);
            Assert.That(await page.EvaluateAsync<object>("window.button2").ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("page-click-react.spec.ts", "should not retarget when element is recycled on hover")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotRetargetWhenElementIsRecycledOnHover()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/react.html").ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "() => { function shuffle() { renderComponent(e('div', {}, [e(MyButton, { name: 'button2' }), e(MyButton, { name: 'button1' })])); } renderComponent(e('div', {}, [e(MyButton, { name: 'button1', onHover: shuffle }), e(MyButton, { name: 'button2' })])); }")
                .ConfigureAwait(false);
            await page.ClickAsync("text=button1").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<object>("window.button1").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvaluateAsync<bool>("window.button2").ConfigureAwait(false), Is.True);
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
