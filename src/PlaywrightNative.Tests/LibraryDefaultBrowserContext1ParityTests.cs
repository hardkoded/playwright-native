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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/defaultbrowsercontext-1.spec.ts</c> parity.
    /// Do not edit leftover <c>LaunchPersistent*</c> tests.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryDefaultBrowserContext1ParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string Hostname = "localhost";

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19966;
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
                    Hostname = "localhost";
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
                Hostname = "localhost";
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
        }

        [SetUp]
        public void SetUp()
        {
            Server?.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            Server?.Reset();
            TestServerSetup.Server?.Reset();
        }

        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "context.cookies() should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContextCookiesShouldWork()
        {
            EnsureServer();
            await using PersistentLaunch launch = await LaunchPersistentAsync().ConfigureAwait(false);
            IPage page = launch.Page;
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string documentCookie = await page.EvaluateAsync<string>(
                @"() => {
                    document.cookie = 'username=John Doe';
                    return document.cookie;
                }").ConfigureAwait(false);
            Assert.That(documentCookie, Is.EqualTo("username=John Doe"));
            AssertCookie(
                OnlyCookie(await page.Context.CookiesAsync().ConfigureAwait(false)),
                "username",
                "John Doe",
                Hostname,
                "/",
                -1,
                false,
                false,
                DefaultSameSiteCookieValue());
        }

        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "context.addCookies() should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContextAddCookiesShouldWork()
        {
            EnsureServer();
            await using PersistentLaunch launch = await LaunchPersistentAsync().ConfigureAwait(false);
            IPage page = launch.Page;
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.Context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Url = EmptyPage,
                    Name = "username",
                    Value = "John Doe",
                    SameSite = SameSiteAttribute.Lax,
                },
            }).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("() => document.cookie").ConfigureAwait(false), Is.EqualTo("username=John Doe"));
            AssertCookie(
                OnlyCookie(await page.Context.CookiesAsync().ConfigureAwait(false)),
                "username",
                "John Doe",
                Hostname,
                "/",
                -1,
                false,
                false,
                SameSiteLaxOrWindowsNone());
        }

        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "context.clearCookies() should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContextClearCookiesShouldWork()
        {
            EnsureServer();
            await using PersistentLaunch launch = await LaunchPersistentAsync().ConfigureAwait(false);
            IPage page = launch.Page;
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.Context.AddCookiesAsync(new[]
            {
                new Cookie { Url = EmptyPage, Name = "cookie1", Value = "1" },
                new Cookie { Url = EmptyPage, Name = "cookie2", Value = "2" },
            }).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false), Is.EqualTo("cookie1=1; cookie2=2"));
            await page.Context.ClearCookiesAsync().ConfigureAwait(false);
            await page.ReloadAsync().ConfigureAwait(false);
            Assert.That(await page.Context.CookiesAsync(Array.Empty<string>()).ConfigureAwait(false), Is.Empty);
            Assert.That(await page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false), Is.EqualTo(string.Empty));
        }

        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "should support viewport option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportViewportOption()
        {
            await using PersistentLaunch launch = await LaunchPersistentAsync(new BrowserTypeLaunchPersistentContextOptions
            {
                ViewportSize = new ViewportSize { Width = 456, Height = 789 },
            }).ConfigureAwait(false);
            await VerifyViewportAsync(launch.Page, 456, 789).ConfigureAwait(false);
            IPage page2 = await launch.Context.NewPageAsync().ConfigureAwait(false);
            await VerifyViewportAsync(page2, 456, 789).ConfigureAwait(false);
        }

        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "should support deviceScaleFactor option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportDeviceScaleFactorOption()
        {
            await using PersistentLaunch launch = await LaunchPersistentAsync(new BrowserTypeLaunchPersistentContextOptions
            {
                DeviceScaleFactor = 3,
            }).ConfigureAwait(false);
            Assert.That(await launch.Page.EvaluateAsync<int>("window.devicePixelRatio").ConfigureAwait(false), Is.EqualTo(3));
        }

        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "should support userAgent option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportUserAgentOption()
        {
            EnsureServer();
            await using PersistentLaunch launch = await LaunchPersistentAsync(new BrowserTypeLaunchPersistentContextOptions
            {
                UserAgent = "foobar",
            }).ConfigureAwait(false);
            IPage page = launch.Page;
            Assert.That(await page.EvaluateAsync<string>("() => navigator.userAgent").ConfigureAwait(false), Is.EqualTo("foobar"));
            Task<string> requestTask = Server.WaitForRequest("/empty.html", request => request.Headers["user-agent"].ToString());
            Task gotoTask = page.GoToAsync(EmptyPage);
            await gotoTask.ConfigureAwait(false);
            Assert.That(await requestTask.ConfigureAwait(false), Is.EqualTo("foobar"));
        }

        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "should support bypassCSP option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportBypassCspOption()
        {
            EnsureServer();
            await using PersistentLaunch launch = await LaunchPersistentAsync(new BrowserTypeLaunchPersistentContextOptions
            {
                BypassCSP = true,
            }).ConfigureAwait(false);
            IPage page = launch.Page;
            await page.GoToAsync(Prefix + "/csp.html").ConfigureAwait(false);
            await page.AddScriptTagAsync(new() { Content = "window[\"__injected\"] = 42;" }).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("__injected").ConfigureAwait(false), Is.EqualTo(42));
            Assert.That(await page.EvaluateAsync<int>("window[\"__inlineScriptValue\"]").ConfigureAwait(false), Is.EqualTo(42));
        }

        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "should support javascriptEnabled option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportJavascriptEnabledOption()
        {
            await using PersistentLaunch launch = await LaunchPersistentAsync(new BrowserTypeLaunchPersistentContextOptions
            {
                JavaScriptEnabled = false,
            }).ConfigureAwait(false);
            IPage page = launch.Page;
            await page.GoToAsync("data:text/html, <script>var something = \"forbidden\"</script>").ConfigureAwait(false);
            Exception error = await CatchAsync(() => page.EvaluateAsync<object>("something")).ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            if (TestConstants.IsWebKit)
            {
                Assert.That(error.Message, Does.Contain("Can't find variable: something"));
            }
            else
            {
                Assert.That(error.Message, Does.Contain("something is not defined"));
            }
        }

        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "should support httpCredentials option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportHttpCredentialsOption()
        {
            EnsureServer();
            Server.SetAuth("/playground.html", "user", "pass");
            await using PersistentLaunch launch = await LaunchPersistentAsync(new BrowserTypeLaunchPersistentContextOptions
            {
                HttpCredentials = new HttpCredentials { Username = "user", Password = "pass" },
            }).ConfigureAwait(false);
            IResponse response = await launch.Page.GoToAsync(Prefix + "/playground.html").ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
        }

        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "should support offline option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportOfflineOption()
        {
            EnsureServer();
            await using PersistentLaunch launch = await LaunchPersistentAsync(new BrowserTypeLaunchPersistentContextOptions
            {
                Offline = true,
            }).ConfigureAwait(false);
            Exception error = await CatchAsync(() => launch.Page.GoToAsync(EmptyPage)).ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
        }

        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "should support acceptDownloads option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportAcceptDownloadsOption()
        {
            EnsureServer();
            Server.SetRoute("/download", http =>
            {
                http.Response.ContentType = "application/octet-stream";
                http.Response.Headers["Content-Disposition"] = "attachment";
                return http.Response.WriteAsync("Hello world");
            });
            await using PersistentLaunch launch = await LaunchPersistentAsync().ConfigureAwait(false);
            IPage page = launch.Page;
            await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
            Task<IDownload> downloadTask = page.WaitForEventAsync(PageEvent.Download);
            Task clickTask = page.ClickAsync("a");
            IDownload download = await downloadTask.ConfigureAwait(false);
            await clickTask.ConfigureAwait(false);
            string path = await download.PathAsync().ConfigureAwait(false);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(File.ReadAllText(path), Is.EqualTo("Hello world"));
        }

        private static async Task<PersistentLaunch> LaunchPersistentAsync(BrowserTypeLaunchPersistentContextOptions options = null)
        {
            options ??= new BrowserTypeLaunchPersistentContextOptions();
            options.Headless = true;
            IBrowserType browserType;
            if (TestConstants.IsWebKit)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.WebkitExecutablePath))
                {
                    Assert.Ignore("WebKit executable not available (download skipped or failed).");
                }

                browserType = Playwright.Webkit;
                options.ExecutablePath = BrowserExecutableFixture.WebkitExecutablePath;
            }
            else if (TestConstants.IsFirefox)
            {
                Assert.Ignore("LaunchPersistentContext is not wired for Firefox yet.");
                return null;
            }
            else
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
                {
                    Assert.Ignore("Chromium executable not available (download skipped or failed).");
                }

                browserType = Playwright.Chromium;
                options.ExecutablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            }

            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-wave866-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(userDataDir);
            IBrowserContext context = await browserType.LaunchPersistentContextAsync(userDataDir, options).ConfigureAwait(false);
            IPage page = context.Pages.FirstOrDefault();
            if (page == null)
            {
                page = await context.NewPageAsync().ConfigureAwait(false);
            }

            return new PersistentLaunch(context, page, userDataDir);
        }

        private static async Task VerifyViewportAsync(IPage page, int width, int height)
        {
            Assert.That(page.ViewportSize, Is.Not.Null);
            Assert.That(page.ViewportSize.Width, Is.EqualTo(width));
            Assert.That(page.ViewportSize.Height, Is.EqualTo(height));
            Assert.That(await page.EvaluateAsync<int>("() => window.innerWidth").ConfigureAwait(false), Is.EqualTo(width));
            Assert.That(await page.EvaluateAsync<int>("() => window.innerHeight").ConfigureAwait(false), Is.EqualTo(height));
        }

        private static BrowserContextCookiesResult OnlyCookie(IReadOnlyList<BrowserContextCookiesResult> cookies)
        {
            Assert.That(cookies.Count, Is.EqualTo(1));
            return cookies[0];
        }

        private static void AssertCookie(
            BrowserContextCookiesResult cookie,
            string name,
            string value,
            string domain,
            string path,
            double expires,
            bool httpOnly,
            bool secure,
            SameSiteAttribute sameSite)
        {
            Assert.That(cookie.Name, Is.EqualTo(name));
            Assert.That(cookie.Value, Is.EqualTo(value));
            Assert.That(cookie.Domain, Is.EqualTo(domain));
            Assert.That(cookie.Path, Is.EqualTo(path));
            Assert.That(cookie.Expires, Is.EqualTo(expires));
            Assert.That(cookie.HttpOnly, Is.EqualTo(httpOnly));
            Assert.That(cookie.Secure, Is.EqualTo(secure));
            Assert.That(cookie.SameSite, Is.EqualTo(sameSite));
        }

        private static SameSiteAttribute DefaultSameSiteCookieValue()
        {
            // Upstream defaultSameSiteCookieValue: Chromium and WebKit/Linux are Lax;
            // WebKit on Windows and older macOS (mac14 bots) report None; Firefox is None.
            if (TestConstants.IsChromium)
            {
                return SameSiteAttribute.Lax;
            }

            if (TestConstants.IsWebKit && TestConstants.IsLinux)
            {
                return SameSiteAttribute.Lax;
            }

            if (TestConstants.IsWebKit)
            {
                return SameSiteAttribute.None;
            }

            return SameSiteAttribute.None;
        }

        private static SameSiteAttribute SameSiteLaxOrWindowsNone()
            => TestConstants.IsWebKit && TestConstants.IsWindows
                ? SameSiteAttribute.None
                : SameSiteAttribute.Lax;

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static async Task<Exception> CatchAsync(Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private sealed class PersistentLaunch : IAsyncDisposable
        {
            private readonly string _userDataDir;

            internal PersistentLaunch(IBrowserContext context, IPage page, string userDataDir)
            {
                Context = context;
                Page = page;
                _userDataDir = userDataDir;
            }

            internal IBrowserContext Context { get; }

            internal IPage Page { get; }

            public async ValueTask DisposeAsync()
            {
                try
                {
                    await Context.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                try
                {
                    Directory.Delete(_userDataDir, true);
                }
                catch (IOException)
                {
                }
            }
        }
    }
}
