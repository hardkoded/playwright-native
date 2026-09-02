/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-locale.spec.ts</c> parity.
    /// Do not edit leftover <c>ContextEnvironmentTests</c> or
    /// <c>LaunchPersistentLocaleTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextLocaleParityTests : PageTestEx
    {
        private const string LocaleNumber =
            "() => (1000000.50).toLocaleString()";

        private const string LocaleNumberFr =
            "() => (1000000.50).toLocaleString().replace(/\\s/g, ' ')";

        private const string OpenUrl =
            "(url) => window.open(url)";

        private const string OpenUrlStmt =
            "(url) => { window.open(url); }";

        private const string FetchWithAcceptLanguage =
            "(url) => fetch(url, { headers: { 'Content-Type': 'application/json', 'Accept-Language': 'de' } })";

        private const string FetchWithoutAcceptLanguage =
            "(url) => fetch(url, { headers: { 'Content-Type': 'application/json' } })";

        private const string OpenWebSocket =
            "(port) => { new WebSocket('ws://localhost:' + port + '/ws'); }";

        private const string OpenWebSocketFromWorker =
            "(port) => { const code = 'new WebSocket(' + JSON.stringify('ws://localhost:' + port + '/ws') + ');'; new Worker(URL.createObjectURL(new Blob([code], { type: 'text/javascript' }))); }";

        private const string WorkerLocale =
            "() => new Worker(URL.createObjectURL(new Blob(['console.log(\"locale:\" + Intl.NumberFormat().resolvedOptions().locale)'], { type: 'application/javascript' })))";

        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static int Port;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19843;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    Port = port;
                    Prefix = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    EmptyPage = Prefix + "/empty.html";
                    return;
                }
                catch (Exception)
                {
                }
            }

            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                Port = new Uri(Prefix).Port;
                return;
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

            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
            }
        }

        [SetUp]
        public async Task SetUpAsync()
        {
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }

            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            _ownedServer?.Reset();
            TestServerSetup.Server?.Reset();
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }
        }

        [PlaywrightTest("browsercontext-locale.spec.ts", "should affect accept-language header @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAffectAcceptLanguageHeader()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { Locale = "fr-CH" }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<string> requestTask = Server.WaitForRequest("/empty.html", Header("accept-language"));
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string acceptLanguage = await requestTask.ConfigureAwait(false);
            Assert.That(acceptLanguage.Substring(0, 5), Is.EqualTo("fr-CH"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-locale.spec.ts", "should affect navigator.language")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAffectNavigatorLanguage()
        {
            IBrowserContext context = await _browser.NewContextAsync(new() { Locale = "fr-FR" }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("(() => navigator.language)()").ConfigureAwait(false),
                Is.EqualTo("fr-FR"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-locale.spec.ts", "should format number")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFormatNumber()
        {
            EnsureServer();
            {
                IBrowserContext context = await _browser.NewContextAsync(new() { Locale = "en-US" }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(
                    await page.EvaluateAsync<string>(LocaleNumber).ConfigureAwait(false),
                    Is.EqualTo("1,000,000.5"));
                await context.CloseAsync().ConfigureAwait(false);
            }

            {
                IBrowserContext context = await _browser.NewContextAsync(new() { Locale = "fr-FR" }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(
                    await page.EvaluateAsync<string>(LocaleNumberFr).ConfigureAwait(false),
                    Is.EqualTo("1 000 000,5"));
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("browsercontext-locale.spec.ts", "should format date")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFormatDate()
        {
            EnsureServer();
            {
                IBrowserContext context = await _browser.NewContextAsync(new() { Locale = "en-US", TimezoneId = "America/Los_Angeles" }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(
                    await page.EvaluateAsync<string>("(() => new Date(1479579154987).toString())()").ConfigureAwait(false),
                    Is.EqualTo("Sat Nov 19 2016 10:12:34 GMT-0800 (Pacific Standard Time)"));
                await context.CloseAsync().ConfigureAwait(false);
            }

            {
                IBrowserContext context = await _browser.NewContextAsync(new() { Locale = "de-DE", TimezoneId = "Europe/Berlin" }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(
                    await page.EvaluateAsync<string>("(() => new Date(1479579154987).toString())()").ConfigureAwait(false),
                    Is.EqualTo("Sat Nov 19 2016 19:12:34 GMT+0100 (Mitteleuropäische Normalzeit)"));
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("browsercontext-locale.spec.ts", "should format number in popups")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFormatNumberInPopups()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { Locale = "fr-FR" }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IPage> popupTask = page.WaitForPopupAsync();
            await page.EvaluateAsync(OpenUrl, Prefix + "/formatted-number.html").ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            await popup.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
            Assert.That(
                await popup.EvaluateAsync<string>("window[\"result\"]").ConfigureAwait(false),
                Is.EqualTo("1 000 000,5"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-locale.spec.ts", "should affect navigator.language in popups")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAffectNavigatorLanguageInPopups()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { Locale = "fr-FR" }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IPage> popupTask = page.WaitForPopupAsync();
            await page.EvaluateAsync(OpenUrl, Prefix + "/formatted-number.html").ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            await popup.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
            Assert.That(
                await popup.EvaluateAsync<string>("window.initialNavigatorLanguage").ConfigureAwait(false),
                Is.EqualTo("fr-FR"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-locale.spec.ts", "should work for multiple pages sharing same process")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForMultiplePagesSharingSameProcess()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { Locale = "ru-RU" }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IPage> popupTask = page.WaitForPopupAsync();
            await page.EvaluateAsync(OpenUrlStmt, EmptyPage).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            Task<IPage> nestedTask = popup.WaitForPopupAsync();
            await popup.EvaluateAsync(OpenUrlStmt, EmptyPage).ConfigureAwait(false);
            await nestedTask.ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-locale.spec.ts", "should be isolated between contexts")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeIsolatedBetweenContexts()
        {
            IBrowserContext context1 = await _browser.NewContextAsync(new() { Locale = "en-US" }).ConfigureAwait(false);
            Task[] pages = new Task[8];
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i] = context1.NewPageAsync();
            }

            await Task.WhenAll(pages).ConfigureAwait(false);

            IBrowserContext context2 = await _browser.NewContextAsync(new() { Locale = "ru-RU" }).ConfigureAwait(false);
            IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);

            List<Task<string>> numbers = new();
            foreach (IPage page in context1.Pages)
            {
                numbers.Add(page.EvaluateAsync<string>(LocaleNumber));
            }

            string[] values = await Task.WhenAll(numbers).ConfigureAwait(false);
            foreach (string value in values)
            {
                Assert.That(value, Is.EqualTo("1,000,000.5"));
            }

            Assert.That(
                await page2.EvaluateAsync<string>(LocaleNumber).ConfigureAwait(false),
                Is.EqualTo("1\u00a0000\u00a0000,5"));

            await Task.WhenAll(context1.CloseAsync(), context2.CloseAsync()).ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-locale.spec.ts", "should not change default locale in another context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotChangeDefaultLocaleInAnotherContext()
        {
            string defaultLocale;
            {
                IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
                defaultLocale = await GetContextLocaleAsync(context).ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
            }

            string localeOverride = defaultLocale == "es-MX" ? "de-DE" : "es-MX";
            {
                IBrowserContext context = await _browser.NewContextAsync(new() { Locale = localeOverride }).ConfigureAwait(false);
                Assert.That(await GetContextLocaleAsync(context).ConfigureAwait(false), Is.EqualTo(localeOverride));
                await context.CloseAsync().ConfigureAwait(false);
            }

            {
                IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
                Assert.That(await GetContextLocaleAsync(context).ConfigureAwait(false), Is.EqualTo(defaultLocale));
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("browsercontext-locale.spec.ts", "should propagate locale to workers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPropagateLocaleToWorkers()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { Locale = "ru-RU" }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IConsoleMessage> messageTask = page.WaitForEventAsync(
                PageEvent.Console,
                e => e.Text.StartsWith("locale:", StringComparison.Ordinal));
            await page.EvaluateAsync(WorkerLocale).ConfigureAwait(false);
            IConsoleMessage message = await messageTask.ConfigureAwait(false);
            if (TestConstants.IsWebKit)
            {
                Assert.That(message.Text, Does.Contain("locale:ru"));
            }
            else
            {
                Assert.That(message.Text, Is.EqualTo("locale:ru-RU"));
            }

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-locale.spec.ts", "should affect Intl.DateTimeFormat().resolvedOptions().locale")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAffectIntlDateTimeFormatResolvedOptionsLocale()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { Locale = "en-GB" }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("(() => (new Intl.DateTimeFormat()).resolvedOptions().locale)()").ConfigureAwait(false),
                Is.EqualTo("en-GB"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-locale.spec.ts", "should send user Accept-Language header")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendUserAcceptLanguageHeader()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { Locale = "en-GB" }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            {
                Task<string> reqPromise = Server.WaitForRequest("/empty.html", Header("accept-language"));
                await page.EvaluateAsync(FetchWithAcceptLanguage, EmptyPage).ConfigureAwait(false);
                string acceptLanguage = await reqPromise.ConfigureAwait(false);
                Assert.That(acceptLanguage, Is.EqualTo("de"));
            }

            {
                Task<string> reqPromise = Server.WaitForRequest("/empty.html", Header("accept-language"));
                await page.EvaluateAsync(FetchWithoutAcceptLanguage, EmptyPage).ConfigureAwait(false);
                string acceptLanguage = await reqPromise.ConfigureAwait(false);
                Assert.That(acceptLanguage, Does.Contain("en-GB"));
            }

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-locale.spec.ts", "should send Accept-Language header on WebSocket handshake")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendAcceptLanguageHeaderOnWebSocketHandshake()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { Locale = "en-GB" }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<string> reqPromise = WaitForWebSocketAcceptLanguageAsync();
            await page.EvaluateAsync(OpenWebSocket, Port).ConfigureAwait(false);
            string acceptLanguage = await reqPromise.ConfigureAwait(false);
            Assert.That(acceptLanguage, Does.Contain("en-GB"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-locale.spec.ts", "should send Accept-Language header on WebSocket handshake from a worker")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendAcceptLanguageHeaderOnWebSocketHandshakeFromAWorker()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { Locale = "en-GB" }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<string> reqPromise = WaitForWebSocketAcceptLanguageAsync();
            await page.EvaluateAsync(OpenWebSocketFromWorker, Port).ConfigureAwait(false);
            string acceptLanguage = await reqPromise.ConfigureAwait(false);
            Assert.That(acceptLanguage, Does.Contain("en-GB"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static Func<HttpRequest, string> Header(string name)
            => request => request.Headers[name].ToString();

        private static async Task<string> GetContextLocaleAsync(IBrowserContext context)
        {
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            return await page.EvaluateAsync<string>("(() => (new Intl.NumberFormat()).resolvedOptions().locale)()")
                .ConfigureAwait(false);
        }

        private static Task<string> WaitForWebSocketAcceptLanguageAsync()
        {
            TaskCompletionSource<string> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<RequestReceivedEventArgs> handler = null;
            handler = (_, args) =>
            {
                if (args.Request.Path == "/ws")
                {
                    tcs.TrySetResult(args.Request.Headers["accept-language"].ToString());
                    Server.RequestReceived -= handler;
                }
            };
            Server.RequestReceived += handler;
            return tcs.Task;
        }

        private static async Task DisposeQuietlyAsync(IAsyncDisposable disposable)
        {
            try
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }
    }
}
