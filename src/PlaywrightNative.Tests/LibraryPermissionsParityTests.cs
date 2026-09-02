/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/permissions.spec.ts</c> parity.
    /// Do not edit leftover <c>LaunchPersistentPermissionsTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryPermissionsParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static SimpleServer _ownedHttps;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string HttpsPrefix = TestConstants.HttpsPrefix;
        private static string HttpsEmptyPage = TestConstants.HttpsPrefix + "/empty.html";

        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private static SimpleServer HttpsServer => _ownedHttps ?? TestServerSetup.HttpsServer;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            await StartOwnedHttpAsync(contentRoot).ConfigureAwait(false);
            await StartOwnedHttpsAsync(contentRoot).ConfigureAwait(false);
            if (Server == null && TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
            }

            if (HttpsServer == null && TestServerSetup.HttpsServer != null)
            {
                HttpsPrefix = TestConstants.HttpsPrefix;
                HttpsEmptyPage = HttpsPrefix + "/empty.html";
            }
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }

            if (_ownedHttps != null)
            {
                await _ownedHttps.StopAsync().ConfigureAwait(false);
                _ownedHttps = null;
            }

            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
            }
        }

        [SetUp]
        public async Task SetUpAsync()
        {
            Server?.Reset();
            HttpsServer?.Reset();
            if (_browser == null || !_browser.IsConnected)
            {
                if (_browser != null)
                {
                    await RecycleBrowserAsync().ConfigureAwait(false);
                }
                else
                {
                    _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                }
            }

            try
            {
                _context = await NewContextOrRecycleAsync().ConfigureAwait(false);
                _page = await _context.NewPageAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                await RecycleBrowserAsync().ConfigureAwait(false);
                _context = await _browser.NewContextAsync().ConfigureAwait(false);
                _page = await _context.NewPageAsync().ConfigureAwait(false);
            }
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            Server?.Reset();
            HttpsServer?.Reset();
            TestServerSetup.Server?.Reset();
            TestServerSetup.HttpsServer?.Reset();
            if (_context != null)
            {
                await DisposeQuietlyAsync(_context).ConfigureAwait(false);
                _context = null;
                _page = null;
            }
        }

        [PlaywrightTest("permissions.spec.ts", "should be prompt by default")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBePromptByDefault()
        {
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(await GetPermissionAsync(_page, "geolocation").ConfigureAwait(false), Is.EqualTo("prompt"));
        }

        [PlaywrightTest("permissions.spec.ts", "should deny permission when not listed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDenyPermissionWhenNotListed()
        {
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await _context.GrantPermissionsAsync(Array.Empty<string>(), EmptyPage).ConfigureAwait(false);
            if (TestConstants.IsWebKit)
            {
                Assert.That(await GetPermissionAsync(_page, "geolocation").ConfigureAwait(false), Is.EqualTo("prompt"));
                await _page.EvaluateAsync("() => navigator.geolocation.getCurrentPosition(() => { })").ConfigureAwait(false);
                Assert.That(await GetPermissionAsync(_page, "geolocation").ConfigureAwait(false), Is.EqualTo("denied"));
            }
            else
            {
                Assert.That(await GetPermissionAsync(_page, "geolocation").ConfigureAwait(false), Is.EqualTo("denied"));
            }
        }

        [PlaywrightTest("permissions.spec.ts", "should fail when bad permission is given")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailWhenBadPermissionIsGiven()
        {
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Exception error = await CatchAsync(() => _context.GrantPermissionsAsync(new[] { "foo" }, EmptyPage)).ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Unknown permission: foo"));
        }

        [PlaywrightTest("permissions.spec.ts", "should grant geolocation permission when origin is listed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldGrantGeolocationPermissionWhenOriginIsListed()
        {
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await _context.GrantPermissionsAsync(new[] { ContextPermissions.Geolocation }, EmptyPage).ConfigureAwait(false);
            Assert.That(await GetPermissionAsync(_page, "geolocation").ConfigureAwait(false), Is.EqualTo("granted"));
        }

        [PlaywrightTest("permissions.spec.ts", "should prompt for geolocation permission when origin is not listed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPromptForGeolocationPermissionWhenOriginIsNotListed()
        {
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await _context.GrantPermissionsAsync(new[] { ContextPermissions.Geolocation }, EmptyPage).ConfigureAwait(false);
            await _page.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            Assert.That(await GetPermissionAsync(_page, "geolocation").ConfigureAwait(false), Is.EqualTo("prompt"));
        }

        [PlaywrightTest("permissions.spec.ts", "should grant notifications permission when listed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldGrantNotificationsPermissionWhenListed()
        {
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await _context.GrantPermissionsAsync(new[] { ContextPermissions.Notifications }, EmptyPage).ConfigureAwait(false);
            Assert.That(await GetPermissionAsync(_page, "notifications").ConfigureAwait(false), Is.EqualTo("granted"));
        }

        [PlaywrightTest("permissions.spec.ts", "should accumulate when adding")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAccumulateWhenAdding()
        {
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await _context.GrantPermissionsAsync(new[] { ContextPermissions.Geolocation }).ConfigureAwait(false);
            await _context.GrantPermissionsAsync(new[] { ContextPermissions.Notifications }).ConfigureAwait(false);
            Assert.That(await GetPermissionAsync(_page, "geolocation").ConfigureAwait(false), Is.EqualTo("granted"));
            Assert.That(await GetPermissionAsync(_page, "notifications").ConfigureAwait(false), Is.EqualTo("granted"));
        }

        [PlaywrightTest("permissions.spec.ts", "should clear permissions")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClearPermissions()
        {
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await _context.GrantPermissionsAsync(new[] { ContextPermissions.Geolocation }).ConfigureAwait(false);
            await _context.ClearPermissionsAsync().ConfigureAwait(false);
            await _context.GrantPermissionsAsync(new[] { ContextPermissions.Notifications }).ConfigureAwait(false);
            Assert.That(await GetPermissionAsync(_page, "geolocation").ConfigureAwait(false), Is.Not.EqualTo("granted"));
            Assert.That(await GetPermissionAsync(_page, "notifications").ConfigureAwait(false), Is.EqualTo("granted"));
        }

        [PlaywrightTest("permissions.spec.ts", "should grant permission when listed for all domains")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldGrantPermissionWhenListedForAllDomains()
        {
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await _context.GrantPermissionsAsync(new[] { ContextPermissions.Geolocation }).ConfigureAwait(false);
            Assert.That(await GetPermissionAsync(_page, "geolocation").ConfigureAwait(false), Is.EqualTo("granted"));
        }

        [PlaywrightTest("permissions.spec.ts", "should grant permission when creating context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldGrantPermissionWhenCreatingContext()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { Permissions = new[] { ContextPermissions.Geolocation } }).ConfigureAwait(false);
            try
            {
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(await GetPermissionAsync(page, "geolocation").ConfigureAwait(false), Is.EqualTo("granted"));
            }
            finally
            {
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("permissions.spec.ts", "should reset permissions")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldResetPermissions()
        {
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await _context.GrantPermissionsAsync(new[] { ContextPermissions.Geolocation }, EmptyPage).ConfigureAwait(false);
            Assert.That(await GetPermissionAsync(_page, "geolocation").ConfigureAwait(false), Is.EqualTo("granted"));
            await _context.ClearPermissionsAsync().ConfigureAwait(false);
            Assert.That(await GetPermissionAsync(_page, "geolocation").ConfigureAwait(false), Is.EqualTo("prompt"));
        }

        [PlaywrightTest("permissions.spec.ts", "should trigger permission onchange")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTriggerPermissionOnchange()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("it.fail(browserName === 'webkit')");
            }

            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await _page.EvaluateAsync(
                @"() => {
                    window.events = [];
                    return navigator.permissions.query({ name: 'geolocation' }).then(function(result) {
                        window.events.push(result.state);
                        result.onchange = function() {
                            window.events.push(result.state);
                        };
                    });
                }").ConfigureAwait(false);
            List<string> expectedEvents = new List<string> { "prompt" };
            Assert.That(await ReadEventsAsync().ConfigureAwait(false), Is.EqualTo(expectedEvents));
            await _context.GrantPermissionsAsync(Array.Empty<string>(), EmptyPage).ConfigureAwait(false);
            expectedEvents.Add("denied");
            Assert.That(await ReadEventsAsync().ConfigureAwait(false), Is.EqualTo(expectedEvents));
            await _context.GrantPermissionsAsync(new[] { ContextPermissions.Geolocation }, EmptyPage).ConfigureAwait(false);
            expectedEvents.Add("granted");
            Assert.That(await ReadEventsAsync().ConfigureAwait(false), Is.EqualTo(expectedEvents));
            await _context.ClearPermissionsAsync().ConfigureAwait(false);
            expectedEvents.Add("prompt");
            Assert.That(await ReadEventsAsync().ConfigureAwait(false), Is.EqualTo(expectedEvents));
        }

        [PlaywrightTest("permissions.spec.ts", "should isolate permissions between browser contexts")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIsolatePermissionsBetweenBrowserContexts()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IBrowserContext otherContext = await _browser.NewContextAsync().ConfigureAwait(false);
            try
            {
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                IPage otherPage = await otherContext.NewPageAsync().ConfigureAwait(false);
                await otherPage.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(await GetPermissionAsync(page, "geolocation").ConfigureAwait(false), Is.EqualTo("prompt"));
                Assert.That(await GetPermissionAsync(otherPage, "geolocation").ConfigureAwait(false), Is.EqualTo("prompt"));

                await context.GrantPermissionsAsync(Array.Empty<string>(), EmptyPage).ConfigureAwait(false);
                await otherContext.GrantPermissionsAsync(new[] { ContextPermissions.Geolocation }, EmptyPage).ConfigureAwait(false);
                if (TestConstants.IsWebKit)
                {
                    Assert.That(await GetPermissionAsync(page, "geolocation").ConfigureAwait(false), Is.EqualTo("prompt"));
                    await page.EvaluateAsync("() => navigator.geolocation.getCurrentPosition(() => { })").ConfigureAwait(false);
                    Assert.That(await GetPermissionAsync(page, "geolocation").ConfigureAwait(false), Is.EqualTo("denied"));
                }
                else
                {
                    Assert.That(await GetPermissionAsync(page, "geolocation").ConfigureAwait(false), Is.EqualTo("denied"));
                }

                Assert.That(await GetPermissionAsync(otherPage, "geolocation").ConfigureAwait(false), Is.EqualTo("granted"));

                await context.ClearPermissionsAsync().ConfigureAwait(false);
                if (TestConstants.IsWebKit)
                {
                    Assert.That(await GetPermissionAsync(page, "geolocation").ConfigureAwait(false), Is.EqualTo("denied"));
                    IPage page2 = await context.NewPageAsync().ConfigureAwait(false);
                    await page2.GoToAsync(EmptyPage).ConfigureAwait(false);
                    Assert.That(await GetPermissionAsync(page2, "geolocation").ConfigureAwait(false), Is.EqualTo("prompt"));
                    await page2.CloseAsync().ConfigureAwait(false);
                }
                else
                {
                    Assert.That(await GetPermissionAsync(page, "geolocation").ConfigureAwait(false), Is.EqualTo("prompt"));
                }

                Assert.That(await GetPermissionAsync(otherPage, "geolocation").ConfigureAwait(false), Is.EqualTo("granted"));
            }
            finally
            {
                await DisposeQuietlyAsync(otherContext).ConfigureAwait(false);
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("permissions.spec.ts", "should support clipboard read")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportClipboardRead()
        {
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            if (!TestConstants.IsWebKit)
            {
                Assert.That(await GetPermissionAsync(_page, "clipboard-read").ConfigureAwait(false), Is.EqualTo("prompt"));
            }

            await _context.GrantPermissionsAsync(new[] { ContextPermissions.ClipboardRead }).ConfigureAwait(false);
            if (!TestConstants.IsWebKit)
            {
                Assert.That(await GetPermissionAsync(_page, "clipboard-read").ConfigureAwait(false), Is.EqualTo("granted"));
            }

            if (TestConstants.IsChromium)
            {
                await _context.GrantPermissionsAsync(new[] { ContextPermissions.ClipboardWrite }).ConfigureAwait(false);
            }

            await _page.EvaluateAsync("() => navigator.clipboard.writeText('test content')").ConfigureAwait(false);
            Assert.That(await _page.EvaluateAsync<string>("() => navigator.clipboard.readText()").ConfigureAwait(false), Is.EqualTo("test content"));
        }

        [PlaywrightTest("permissions.spec.ts", "should isolate the headless clipboard from the operating system")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIsolateTheHeadlessClipboardFromTheOperatingSystem()
        {
            EnsureServer();
            IBrowser browser1 = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowser browser2 = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            try
            {
                IPage page1 = await NewClipboardPageAsync(browser1).ConfigureAwait(false);
                IPage page2 = await NewClipboardPageAsync(browser2).ConfigureAwait(false);
                await page1.EvaluateAsync("() => navigator.clipboard.writeText('first')").ConfigureAwait(false);
                Assert.That(await page1.EvaluateAsync<string>("() => navigator.clipboard.readText()").ConfigureAwait(false), Is.EqualTo("first"));
                await page2.EvaluateAsync("() => navigator.clipboard.writeText('second')").ConfigureAwait(false);
                Assert.That(await page2.EvaluateAsync<string>("() => navigator.clipboard.readText()").ConfigureAwait(false), Is.EqualTo("second"));
                Assert.That(await page1.EvaluateAsync<string>("() => navigator.clipboard.readText()").ConfigureAwait(false), Is.EqualTo("first"));
            }
            finally
            {
                await DisposeQuietlyAsync(browser1).ConfigureAwait(false);
                await DisposeQuietlyAsync(browser2).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("permissions.spec.ts", "storage access")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task StorageAccess()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("chromium-only api");
            }

            EnsureServer();
            await _context.GrantPermissionsAsync(new[] { ContextPermissions.StorageAccess }).ConfigureAwait(false);
            Assert.That(await GetPermissionAsync(_page, "storage-access").ConfigureAwait(false), Is.EqualTo("granted"));
            Server.SetRoute("/set-cookie.html", http =>
            {
                http.Response.Headers["Set-Cookie"] = "name=value; Path=/; SameSite=Strict; Secure";
                return http.Response.WriteAsync(string.Empty);
            });
            Server.SetRoute("/my-frame.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<iframe src=\"" + CrossProcessPrefix + "/empty.html\"></iframe>");
            });
            await _page.GoToAsync(CrossProcessPrefix + "/set-cookie.html").ConfigureAwait(false);
            await _page.GoToAsync(Prefix + "/my-frame.html").ConfigureAwait(false);
            List<IFrame> frames = new(_page.Frames);
            IFrame frame = frames[1];
            Assert.That(await GetPermissionAsync(frame, "storage-access").ConfigureAwait(false), Is.EqualTo("granted"));
            bool access = await frame.EvaluateAsync<bool>("() => document.requestStorageAccess().then(() => true, () => false)").ConfigureAwait(false);
            Assert.That(access, Is.True);
            Assert.That(await frame.EvaluateAsync<bool>("() => document.hasStorageAccess()").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("permissions.spec.ts", "should be able to use the local-fonts API")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToUseTheLocalFontsApi()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("chromium-only api");
            }

            EnsureHttps();
            IBrowserContext context = await _browser.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            try
            {
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(HttpsEmptyPage).ConfigureAwait(false);
                Assert.That(await GetPermissionAsync(page, "local-fonts").ConfigureAwait(false), Is.EqualTo("prompt"));
                await context.GrantPermissionsAsync(new[] { ContextPermissions.LocalFonts }).ConfigureAwait(false);
                Assert.That(await GetPermissionAsync(page, "local-fonts").ConfigureAwait(false), Is.EqualTo("granted"));
                Assert.That(
                    await page.EvaluateAsync<bool>("async () => (await queryLocalFonts()).length > 0").ConfigureAwait(false),
                    Is.True);
            }
            finally
            {
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("permissions.spec.ts", "local network request is allowed from public origin")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task LocalNetworkRequestIsAllowedFromPublicOrigin()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("it.skip(browserName === 'webkit')");
            }

            if (TestConstants.IsChromium && ChromiumMajorVersion(_browser) < 145)
            {
                Assert.Ignore("local-network-access permission support has changed between versions");
            }

            EnsureServer();
            if (TestConstants.IsChromium || TestConstants.IsFirefox)
            {
                await _context.GrantPermissionsAsync(new[] { ContextPermissions.LocalNetworkAccess }).ConfigureAwait(false);
            }

            List<string> serverRequests = new List<string>();
            Server.SetRoute("/cors", http =>
            {
                serverRequests.Add(http.Request.Method + " " + http.Request.Path.Value);
                if (string.Equals(http.Request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                {
                    http.Response.StatusCode = 204;
                    http.Response.Headers["Access-Control-Allow-Origin"] = "*";
                    http.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, OPTIONS";
                    http.Response.Headers["Access-Control-Allow-Headers"] = "*";
                    return http.Response.CompleteAsync();
                }

                http.Response.ContentType = "text/plain";
                http.Response.Headers["Access-Control-Allow-Origin"] = "*";
                return http.Response.WriteAsync("Hello there!");
            });
            List<string> clientRequests = new List<string>();
            await _page.GoToAsync("https://demo.playwright.dev/todomvc/").ConfigureAwait(false);
            _page.Request += (_, request) =>
            {
                clientRequests.Add(request.Method + " " + request.Url);
            };
            string corsUrl = CrossProcessPrefix + "/cors";
            string response = await _page.EvaluateAsync<string>(
                @"async url => {
                    const response = await fetch(url, {
                        method: 'POST',
                        body: '',
                        headers: {
                            'Content-Type': 'application/json',
                            'X-Custom-Header': 'test-value'
                        }
                    });
                    return await response.text();
                }",
                corsUrl).ConfigureAwait(false);
            Assert.That(response, Is.EqualTo("Hello there!"));
            Assert.That(serverRequests, Is.EqualTo(new[] { "OPTIONS /cors", "POST /cors" }));
            Assert.That(clientRequests, Is.EqualTo(new[] { "POST " + corsUrl }));
        }

        [PlaywrightTest("permissions.spec.ts", "can request screen-wake-lock")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CanRequestScreenWakeLock()
        {
            await _context.GrantPermissionsAsync(new[] { ContextPermissions.ScreenWakeLock }).ConfigureAwait(false);
            await _page.RouteAsync("**/*", route => route.FulfillAsync(new() { Status = 200, Body = "<div>Hello there!</div>", ContentType = "text/html" })).ConfigureAwait(false);
            await _page.GoToAsync("https://example.com").ConfigureAwait(false);
            await _page.EvaluateAsync("() => navigator.wakeLock.request('screen')").ConfigureAwait(false);
        }

        [PlaywrightTest("permissions.spec.ts", "should capture when camera and microphone are granted")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCaptureWhenCameraAndMicrophoneAreGranted()
        {
            SkipUnlessWebKitCamera();
            EnsureServer();
            await _context.GrantPermissionsAsync(
                new[] { ContextPermissions.Camera, ContextPermissions.Microphone },
                Prefix).ConfigureAwait(false);
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            UserMediaResult result = await GetUserMediaAsync(_page, video: true, audio: true).ConfigureAwait(false);
            Assert.That(result.Error, Is.Null);
            Assert.That(result.Tracks, Has.Count.EqualTo(2));
            Assert.That(result.Tracks[0].Kind, Is.EqualTo("audio"));
            Assert.That(result.Tracks[0].Live, Is.True);
            Assert.That(result.Tracks[1].Kind, Is.EqualTo("video"));
            Assert.That(result.Tracks[1].Live, Is.True);
        }

        [PlaywrightTest("permissions.spec.ts", "should reject when no permission is granted")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRejectWhenNoPermissionIsGranted()
        {
            SkipUnlessWebKitCamera();
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            UserMediaResult result = await GetUserMediaAsync(_page, video: true, audio: true).ConfigureAwait(false);
            Assert.That(result.Error, Is.EqualTo("NotAllowedError"));
        }

        [PlaywrightTest("permissions.spec.ts", "should gate audio and video independently")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldGateAudioAndVideoIndependently()
        {
            SkipUnlessWebKitCamera();
            EnsureServer();
            await _context.GrantPermissionsAsync(new[] { ContextPermissions.Camera }, Prefix).ConfigureAwait(false);
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            UserMediaResult camera = await GetUserMediaAsync(_page, video: true, audio: null).ConfigureAwait(false);
            Assert.That(camera.Error, Is.Null);
            Assert.That(camera.Tracks, Has.Count.EqualTo(1));
            Assert.That(camera.Tracks[0].Kind, Is.EqualTo("video"));
            Assert.That(camera.Tracks[0].Live, Is.True);
            UserMediaResult mic = await GetUserMediaAsync(_page, video: null, audio: true).ConfigureAwait(false);
            Assert.That(mic.Error, Is.EqualTo("NotAllowedError"));
        }

        [PlaywrightTest("permissions.spec.ts", "should stop capturing after permissions are cleared")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldStopCapturingAfterPermissionsAreCleared()
        {
            SkipUnlessWebKitCamera();
            EnsureServer();
            await _context.GrantPermissionsAsync(new[] { ContextPermissions.Camera }, Prefix).ConfigureAwait(false);
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            UserMediaResult granted = await GetUserMediaAsync(_page, video: true, audio: null).ConfigureAwait(false);
            Assert.That(granted.Error, Is.Null);
            Assert.That(granted.Tracks, Has.Count.EqualTo(1));
            Assert.That(granted.Tracks[0].Kind, Is.EqualTo("video"));
            Assert.That(granted.Tracks[0].Live, Is.True);
            await _context.ClearPermissionsAsync().ConfigureAwait(false);
            UserMediaResult denied = await GetUserMediaAsync(_page, video: true, audio: null).ConfigureAwait(false);
            Assert.That(denied.Error, Is.EqualTo("NotAllowedError"));
        }

        private static void SkipUnlessWebKitCamera()
        {
            if (!TestConstants.IsWebKit)
            {
                Assert.Ignore("WebKit mock-capture-device based test");
            }

            // Official it.skip(isFrozenWebkit): mock capture needs WebKit r2332.
            // This repo ships webkit-2276, which returns OverconstrainedError.
            Assert.Ignore("Mock capture device support requires a newer WebKit build");
        }

        private static int ChromiumMajorVersion(IBrowser browser)
        {
            if (browser == null || string.IsNullOrEmpty(browser.Version))
            {
                return int.MaxValue;
            }

            string version = browser.Version;
            int start = -1;
            for (int i = 0; i < version.Length; i++)
            {
                if (char.IsDigit(version[i]))
                {
                    start = i;
                    break;
                }
            }

            if (start < 0)
            {
                return int.MaxValue;
            }

            int end = start;
            while (end < version.Length && char.IsDigit(version[end]))
            {
                end++;
            }

            return int.TryParse(version.AsSpan(start, end - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : int.MaxValue;
        }

        private static Task<string> GetPermissionAsync(IPage page, string name)
            => page.EvaluateAsync<string>(
                "name => navigator.permissions.query({ name }).then(result => result.state)",
                name);

        private static Task<string> GetPermissionAsync(IFrame frame, string name)
            => frame.EvaluateAsync<string>(
                "name => navigator.permissions.query({ name }).then(result => result.state)",
                name);

        private Task<string[]> ReadEventsAsync()
            => _page.EvaluateAsync<string[]>("() => window.events");

        private static async Task<UserMediaResult> GetUserMediaAsync(IPage page, bool? video, bool? audio)
        {
            Dictionary<string, bool> constraints = new Dictionary<string, bool>(StringComparer.Ordinal);
            if (video.HasValue)
            {
                constraints["video"] = video.Value;
            }

            if (audio.HasValue)
            {
                constraints["audio"] = audio.Value;
            }

            return await page.EvaluateAsync<UserMediaResult>(
                @"async constraints => {
                    try {
                        const stream = await navigator.mediaDevices.getUserMedia(constraints);
                        const tracks = stream.getTracks().map(track => ({ kind: track.kind, live: track.readyState === 'live' }));
                        stream.getTracks().forEach(track => track.stop());
                        tracks.sort((a, b) => a.kind.localeCompare(b.kind));
                        return { tracks };
                    } catch (error) {
                        return { error: error.name };
                    }
                }",
                constraints).ConfigureAwait(false);
        }

        private async Task<IPage> NewClipboardPageAsync(IBrowser browser)
        {
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            if (TestConstants.IsChromium)
            {
                await context.GrantPermissionsAsync(new[] { ContextPermissions.ClipboardRead, ContextPermissions.ClipboardWrite }).ConfigureAwait(false);
            }
            else if (TestConstants.IsWebKit)
            {
                await context.GrantPermissionsAsync(new[] { ContextPermissions.ClipboardRead }).ConfigureAwait(false);
            }

            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            return page;
        }

        private static async Task StartOwnedHttpAsync(string contentRoot)
        {
            int basePort = 19964;
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
        }

        private static async Task StartOwnedHttpsAsync(string contentRoot)
        {
            if (TestServerSetup.HttpsServer != null)
            {
                HttpsPrefix = TestConstants.HttpsPrefix;
                HttpsEmptyPage = HttpsPrefix + "/empty.html";
                return;
            }

            string certPath = EnsureTestCertificate(contentRoot);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PATH", certPath);
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PASSWORD")))
            {
                Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PASSWORD", "playwright");
            }

            int basePort = 19984;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer https = SimpleServer.CreateHttps(port, contentRoot);
                    await https.StartAsync().ConfigureAwait(false);
                    _ownedHttps = https;
                    string portText = port.ToString(CultureInfo.InvariantCulture);
                    HttpsPrefix = "https://localhost:" + portText;
                    HttpsEmptyPage = HttpsPrefix + "/empty.html";
                    return;
                }
                catch (Exception)
                {
                }
            }
        }

        private static string EnsureTestCertificate(string contentRoot)
        {
            string certPath = Path.Combine(contentRoot, "key.pfx");
            if (File.Exists(certPath))
            {
                return certPath;
            }

            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new(
                "CN=localhost",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            SubjectAlternativeNameBuilder san = new();
            san.AddDnsName("localhost");
            san.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(san.Build());
            using X509Certificate2 cert = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(10));
            File.WriteAllBytes(certPath, cert.Export(X509ContentType.Pfx, "playwright"));
            return certPath;
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static void EnsureHttps()
        {
            if (HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
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

        private async Task<IBrowserContext> NewContextOrRecycleAsync()
        {
            Task<IBrowserContext> create = _browser.NewContextAsync();
            Task finished = await Task.WhenAny(create, Task.Delay(5000)).ConfigureAwait(false);
            if (!ReferenceEquals(finished, create))
            {
                await RecycleBrowserAsync().ConfigureAwait(false);
                return await _browser.NewContextAsync().ConfigureAwait(false);
            }

            return await create.ConfigureAwait(false);
        }

        private async Task RecycleBrowserAsync()
        {
            IBrowser previous = _browser;
            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            if (previous != null)
            {
                await DisposeQuietlyAsync(previous).ConfigureAwait(false);
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

        private sealed class UserMediaResult
        {
            [JsonPropertyName("tracks")]
            public List<UserMediaTrack> Tracks { get; set; }

            [JsonPropertyName("error")]
            public string Error { get; set; }
        }

        private sealed class UserMediaTrack
        {
            [JsonPropertyName("kind")]
            public string Kind { get; set; }

            [JsonPropertyName("live")]
            public bool Live { get; set; }
        }
    }
}
