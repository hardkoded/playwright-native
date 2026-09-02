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
    /// Official <c>elementhandle-misc.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class ElementHandleMiscTests : PageTestEx
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

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19272;
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

        [PlaywrightTest("elementhandle-misc.spec.ts", "should hover")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHover()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/scrollable.html").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("#button-6").ConfigureAwait(false);
            await button.HoverAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => document.querySelector('button:hover').id)()").ConfigureAwait(false), Is.EqualTo("button-6"));
        }

        [PlaywrightTest("elementhandle-misc.spec.ts", "should hover when Node is removed")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHoverWhenNodeIsRemoved()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/scrollable.html").ConfigureAwait(false);
            await page.EvaluateAsync("(() => delete window['Node'])()").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("#button-6").ConfigureAwait(false);
            await button.HoverAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => document.querySelector('button:hover').id)()").ConfigureAwait(false), Is.EqualTo("button-6"));
        }

        [PlaywrightTest("elementhandle-misc.spec.ts", "should fill input")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            IElementHandle handle = await page.QuerySelectorAsync("input").ConfigureAwait(false);
            await handle.FillAsync("some value").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("some value"));
        }

        [PlaywrightTest("elementhandle-misc.spec.ts", "should fill input when Node is removed")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillInputWhenNodeIsRemoved()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            await page.EvaluateAsync("(() => delete window['Node'])()").ConfigureAwait(false);
            IElementHandle handle = await page.QuerySelectorAsync("input").ConfigureAwait(false);
            await handle.FillAsync("some value").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("some value"));
        }

        [PlaywrightTest("elementhandle-misc.spec.ts", "should check the box")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldCheckTheBox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id='checkbox' type='checkbox'></input>").ConfigureAwait(false);
            IElementHandle input = await page.QuerySelectorAsync("input").ConfigureAwait(false);
            await input.CheckAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("checkbox.checked").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("elementhandle-misc.spec.ts", "should check the box using setChecked")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldCheckTheBoxUsingSetChecked()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id='checkbox' type='checkbox'></input>").ConfigureAwait(false);
            IElementHandle input = await page.QuerySelectorAsync("input").ConfigureAwait(false);
            await input.SetCheckedAsync(true).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("checkbox.checked").ConfigureAwait(false), Is.True);
            await input.SetCheckedAsync(false).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("checkbox.checked").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("elementhandle-misc.spec.ts", "should uncheck the box")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldUncheckTheBox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id='checkbox' type='checkbox' checked></input>").ConfigureAwait(false);
            IElementHandle input = await page.QuerySelectorAsync("input").ConfigureAwait(false);
            await input.UncheckAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("checkbox.checked").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("elementhandle-misc.spec.ts", "should select single option")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectSingleOption()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/select.html").ConfigureAwait(false);
            IElementHandle select = await page.QuerySelectorAsync("select").ConfigureAwait(false);
            await select.SelectOptionAsync("blue").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string[]>("(() => window['result'].onInput)()").ConfigureAwait(false), Is.EqualTo(new[] { "blue" }));
            Assert.That(await page.EvaluateAsync<string[]>("(() => window['result'].onChange)()").ConfigureAwait(false), Is.EqualTo(new[] { "blue" }));
        }

        [PlaywrightTest("elementhandle-misc.spec.ts", "should focus a button")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFocusAButton()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("button").ConfigureAwait(false);
            Assert.That(await button.EvaluateAsync<bool>("button => document.activeElement === button").ConfigureAwait(false), Is.False);
            await button.FocusAsync().ConfigureAwait(false);
            Assert.That(await button.EvaluateAsync<bool>("button => document.activeElement === button").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("elementhandle-misc.spec.ts", "should allow disposing twice")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAllowDisposingTwice()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section>39</section>").ConfigureAwait(false);
            IElementHandle element = await page.QuerySelectorAsync("section").ConfigureAwait(false);
            Assert.That(element, Is.Not.Null);
            await element.DisposeAsync().ConfigureAwait(false);
            await element.DisposeAsync().ConfigureAwait(false);
        }
    }
}
