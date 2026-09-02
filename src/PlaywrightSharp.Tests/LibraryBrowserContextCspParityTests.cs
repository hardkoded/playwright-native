/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-csp.spec.ts</c> parity.
    /// Do not edit leftover <c>ContextTouchCspTests</c> or
    /// <c>PageAddScriptTagTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextCspParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19836;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    string portText = port.ToString(CultureInfo.InvariantCulture);
                    Prefix = "http://localhost:" + portText;
                    EmptyPage = Prefix + "/empty.html";
                    CrossProcessPrefix = "http://127.0.0.1:" + portText;
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
                CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
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
            if (_browser == null || !_browser.IsConnected)
            {
                if (_browser != null)
                {
                    await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                }

                _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            }

            await CloseLeftoverContextsAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            _ownedServer?.Reset();
            TestServerSetup.Server?.Reset();
            await CloseLeftoverContextsAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-csp.spec.ts", "should bypass CSP meta tag @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBypassCspMetaTag()
        {
            EnsureServer();
            {
                IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/csp.html").ConfigureAwait(false);
                AssertMissing(await page.EvaluateAsync<object>("window[\"__inlineScriptValue\"]").ConfigureAwait(false));
                await CatchAsync(page.AddScriptTagAsync(new() { Content = "window[\"__injected\"] = 42;" })).ConfigureAwait(false);
                AssertMissing(await page.EvaluateAsync<object>("window[\"__injected\"]").ConfigureAwait(false));
                await context.CloseAsync().ConfigureAwait(false);
            }

            {
                IBrowserContext context = await _browser.NewContextAsync(new() { BypassCSP = true }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/csp.html").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<int>("window[\"__inlineScriptValue\"]").ConfigureAwait(false), Is.EqualTo(42));
                await page.AddScriptTagAsync(new() { Content = "window[\"__injected\"] = 42;" }).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<int>("window[\"__injected\"]").ConfigureAwait(false), Is.EqualTo(42));
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("browsercontext-csp.spec.ts", "should bypass CSP header")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBypassCspHeader()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", http =>
            {
                http.Response.Headers["Content-Security-Policy"] = "default-src \"self\"";
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<script type='text/javascript'>window.__inlineScriptValue = 42;</script>");
            });

            {
                IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                AssertMissing(await page.EvaluateAsync<object>("window[\"__inlineScriptValue\"]").ConfigureAwait(false));
                await CatchAsync(page.AddScriptTagAsync(new() { Content = "window[\"__injected\"] = 42;" })).ConfigureAwait(false);
                AssertMissing(await page.EvaluateAsync<object>("window[\"__injected\"]").ConfigureAwait(false));
                await context.CloseAsync().ConfigureAwait(false);
            }

            {
                IBrowserContext context = await _browser.NewContextAsync(new() { BypassCSP = true }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<int>("window[\"__inlineScriptValue\"]").ConfigureAwait(false), Is.EqualTo(42));
                await page.AddScriptTagAsync(new() { Content = "window[\"__injected\"] = 42;" }).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<int>("window[\"__injected\"]").ConfigureAwait(false), Is.EqualTo(42));
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("browsercontext-csp.spec.ts", "should bypass after cross-process navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBypassAfterCrossProcessNavigation()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { BypassCSP = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/csp.html").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("window[\"__inlineScriptValue\"]").ConfigureAwait(false), Is.EqualTo(42));
            await page.AddScriptTagAsync(new() { Content = "window[\"__injected\"] = 42;" }).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("window[\"__injected\"]").ConfigureAwait(false), Is.EqualTo(42));

            await page.GoToAsync(CrossProcessPrefix + "/csp.html").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("window[\"__inlineScriptValue\"]").ConfigureAwait(false), Is.EqualTo(42));
            await page.AddScriptTagAsync(new() { Content = "window[\"__injected\"] = 42;" }).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("window[\"__injected\"]").ConfigureAwait(false), Is.EqualTo(42));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-csp.spec.ts", "should bypass CSP in iframes as well")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBypassCspInIframesAsWell()
        {
            EnsureServer();
            {
                IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                IFrame frame = await AttachFrameAsync(page, "frame1", Prefix + "/csp.html").ConfigureAwait(false);
                AssertMissing(await frame.EvaluateAsync<object>("window[\"__inlineScriptValue\"]").ConfigureAwait(false));
                await CatchAsync(frame.AddScriptTagAsync(new() { Content = "window[\"__injected\"] = 42;" })).ConfigureAwait(false);
                AssertMissing(await frame.EvaluateAsync<object>("window[\"__injected\"]").ConfigureAwait(false));
                await context.CloseAsync().ConfigureAwait(false);
            }

            {
                IBrowserContext context = await _browser.NewContextAsync(new() { BypassCSP = true }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                IFrame frame = await AttachFrameAsync(page, "frame1", Prefix + "/csp.html").ConfigureAwait(false);
                Assert.That(await frame.EvaluateAsync<int>("window[\"__inlineScriptValue\"]").ConfigureAwait(false), Is.EqualTo(42));
                await CatchAsync(frame.AddScriptTagAsync(new() { Content = "window[\"__injected\"] = 42;" })).ConfigureAwait(false);
                Assert.That(await frame.EvaluateAsync<int>("window[\"__injected\"]").ConfigureAwait(false), Is.EqualTo(42));
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        private static async Task<IFrame> AttachFrameAsync(IPage page, string frameId, string url)
        {
            string frameIdJson = JsonSerializer.Serialize(frameId);
            string urlJson = JsonSerializer.Serialize(url);
            await page.EvaluateAsync<object>(
                "(async () => { const frame = document.createElement('iframe'); frame.src = " +
                urlJson + "; frame.id = " + frameIdJson + "; document.body.appendChild(frame); await new Promise(x => frame.onload = x); })()")
                .ConfigureAwait(false);

            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                IFrame named = page.Frame(frameId);
                if (named != null && !named.IsDetached)
                {
                    return named;
                }

                foreach (IFrame frame in page.Frames)
                {
                    if (!ReferenceEquals(frame, page.MainFrame) && !frame.IsDetached)
                    {
                        return frame;
                    }
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            Assert.Fail("Timed out waiting for frame " + frameId);
            return null;
        }

        private static void AssertMissing(object value)
        {
            if (value is JsonElement element
                && (element.ValueKind == JsonValueKind.Undefined || element.ValueKind == JsonValueKind.Null))
            {
                return;
            }

            Assert.That(value, Is.Null);
        }

        private static async Task CatchAsync(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        private async Task CloseLeftoverContextsAsync()
        {
            if (_browser == null)
            {
                return;
            }

            foreach (IBrowserContext context in new System.Collections.Generic.List<IBrowserContext>(_browser.Contexts))
            {
                try
                {
                    await context.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            }
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
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
