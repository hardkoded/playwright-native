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
    /// Official <c>page-add-style-tag.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class PageAddStyleTagTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;

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

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19190;
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

        [PlaywrightTest("page-add-style-tag.spec.ts", "should throw an error if no options are provided")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowAnErrorIfNoOptionsAreProvided()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(() => page.AddStyleTagAsync());
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Provide an object with a `url`, `path` or `content` property"));
        }

        [PlaywrightTest("page-add-style-tag.spec.ts", "should work with a url")]
        [PlaywrightTest("page-add-style-tag.spec.ts", "should work with a url @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithAUrl()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IElementHandle styleHandle = await page.AddStyleTagAsync(new() { Url = "/injectedstyle.css" }).ConfigureAwait(false);
            Assert.That(styleHandle.AsElement(), Is.Not.Null);
            string color = await page.EvaluateAsync<string>(
                "(() => window.getComputedStyle(document.querySelector('body')).getPropertyValue('background-color'))()").ConfigureAwait(false);
            Assert.That(color, Is.EqualTo("rgb(255, 0, 0)"));
        }

        [PlaywrightTest("page-add-style-tag.spec.ts", "should throw an error if loading from url fail")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowAnErrorIfLoadingFromUrlFail()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.AddStyleTagAsync(new() { Url = "/nonexistfile.js" }));
            Assert.That(error, Is.Not.Null);
        }

        [PlaywrightTest("page-add-style-tag.spec.ts", "should work with a path")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithAPath()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IElementHandle styleHandle = await page.AddStyleTagAsync(new() { Path = TestUtils.GetWebServerFile("injectedstyle.css") }).ConfigureAwait(false);
            Assert.That(styleHandle.AsElement(), Is.Not.Null);
            string color = await page.EvaluateAsync<string>(
                "(() => window.getComputedStyle(document.querySelector('body')).getPropertyValue('background-color'))()").ConfigureAwait(false);
            Assert.That(color, Is.EqualTo("rgb(255, 0, 0)"));
        }

        [PlaywrightTest("page-add-style-tag.spec.ts", "should include sourceURL when path is provided")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldIncludeSourceURLWhenPathIsProvided()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string assetPath = TestUtils.GetWebServerFile("injectedstyle.css");
            await page.AddStyleTagAsync(new() { Path = assetPath }).ConfigureAwait(false);
            IElementHandle styleHandle = await page.QuerySelectorAsync("style").ConfigureAwait(false);
            Assert.That(styleHandle, Is.Not.Null);
            string styleContent = await page.EvaluateAsync<string>(
                "(() => document.querySelector('style').innerHTML)()").ConfigureAwait(false);
            Assert.That(styleContent, Does.Contain(assetPath));
        }

        [PlaywrightTest("page-add-style-tag.spec.ts", "should work with content")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithContent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IElementHandle styleHandle = await page.AddStyleTagAsync(new() { Content = "body { background-color: green; }" }).ConfigureAwait(false);
            Assert.That(styleHandle.AsElement(), Is.Not.Null);
            string color = await page.EvaluateAsync<string>(
                "(() => window.getComputedStyle(document.querySelector('body')).getPropertyValue('background-color'))()").ConfigureAwait(false);
            Assert.That(color, Is.EqualTo("rgb(0, 128, 0)"));
        }

        [PlaywrightTest("page-add-style-tag.spec.ts", "should throw when added with content to the CSP page")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowWhenAddedWithContentToTheCSPPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/csp.html").ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.AddStyleTagAsync(new() { Content = "body { background-color: green; }" }));
            Assert.That(error, Is.Not.Null);
        }

        [PlaywrightTest("page-add-style-tag.spec.ts", "should throw when added with URL to the CSP page")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowWhenAddedWithURLToTheCSPPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/csp.html").ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.AddStyleTagAsync(new() { Url = CrossProcessPrefix + "/injectedstyle.css" }));
            Assert.That(error, Is.Not.Null);
        }
    }
}
