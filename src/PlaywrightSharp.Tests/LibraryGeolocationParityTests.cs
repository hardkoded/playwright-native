/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/geolocation.spec.ts</c> parity for geolocation
    /// permissions, overrides, and watchPosition.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryGeolocationParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19828;
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
            _ownedServer?.Reset();
            if (_context != null)
            {
                await DisposeQuietlyAsync(_context).ConfigureAwait(false);
                _context = null;
                _page = null;
            }
        }

        [PlaywrightTest("geolocation.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            EnsureServer();
            await _context.GrantPermissionsAsync(new[] { ContextPermissions.Geolocation }).ConfigureAwait(false);
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await _context.SetGeolocationAsync(new Geolocation { Longitude = 10, Latitude = 10 }).ConfigureAwait(false);
            GeoCoords geolocation = await ReadGeolocationAsync(_page).ConfigureAwait(false);
            Assert.That(geolocation.Latitude, Is.EqualTo(10d));
            Assert.That(geolocation.Longitude, Is.EqualTo(10d));
        }

        [PlaywrightTest("geolocation.spec.ts", "should throw when invalid longitude")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowWhenInvalidLongitude()
        {
            Exception error = Assert.CatchAsync(
                () => _context.SetGeolocationAsync(new Geolocation { Longitude = 200, Latitude = 10 }));
            Assert.That(
                error.Message,
                Does.Contain("geolocation.longitude: precondition -180 <= LONGITUDE <= 180 failed."));
        }

        [PlaywrightTest("geolocation.spec.ts", "should isolate contexts")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIsolateContexts()
        {
            EnsureServer();
            await _context.GrantPermissionsAsync(new[] { ContextPermissions.Geolocation }).ConfigureAwait(false);
            await _context.SetGeolocationAsync(new Geolocation { Longitude = 10, Latitude = 10 }).ConfigureAwait(false);
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);

            IBrowserContext context2 = await _browser.NewContextAsync(new() { Permissions = new[] { ContextPermissions.Geolocation }, Geolocation = new Geolocation { Longitude = 20, Latitude = 20 } }).ConfigureAwait(false);
            try
            {
                IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
                await page2.GoToAsync(EmptyPage).ConfigureAwait(false);

                GeoCoords geolocation = await ReadGeolocationAsync(_page).ConfigureAwait(false);
                Assert.That(geolocation.Latitude, Is.EqualTo(10d));
                Assert.That(geolocation.Longitude, Is.EqualTo(10d));

                GeoCoords geolocation2 = await ReadGeolocationAsync(page2).ConfigureAwait(false);
                Assert.That(geolocation2.Latitude, Is.EqualTo(20d));
                Assert.That(geolocation2.Longitude, Is.EqualTo(20d));
            }
            finally
            {
                await DisposeQuietlyAsync(context2).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("geolocation.spec.ts", "should throw with missing latitude")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowWithMissingLatitude()
        {
            Exception error = Assert.CatchAsync(
                () => _context.SetGeolocationAsync(new Geolocation { Longitude = 10 }));
            Assert.That(error.Message, Does.Contain("geolocation.latitude: expected float, got undefined"));
        }

        [PlaywrightTest("geolocation.spec.ts", "should not modify passed default options object")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotModifyPassedDefaultOptionsObject()
        {
            Geolocation geolocation = new Geolocation { Longitude = 10, Latitude = 10 };
            BrowserContextOptions options = new BrowserContextOptions { Geolocation = geolocation };
            IBrowserContext context = await _browser.NewContextAsync(options).ConfigureAwait(false);
            try
            {
                await context.SetGeolocationAsync(new Geolocation { Longitude = 20, Latitude = 20 })
                    .ConfigureAwait(false);
                Assert.That(options.Geolocation, Is.SameAs(geolocation));
            }
            finally
            {
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("geolocation.spec.ts", "should throw with missing longitude in default options")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowWithMissingLongitudeInDefaultOptions()
        {
            Exception error = Assert.CatchAsync(
                () => _browser.NewContextAsync(new() { Geolocation = new Geolocation { Latitude = 10 } }));
            Assert.That(error.Message, Does.Contain("geolocation.longitude: expected float, got undefined"));
        }

        [PlaywrightTest("geolocation.spec.ts", "should use context options")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseContextOptions()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { Geolocation = new Geolocation { Longitude = 10, Latitude = 10 }, Permissions = new[] { ContextPermissions.Geolocation } }).ConfigureAwait(false);
            try
            {
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                GeoCoords geolocation = await ReadGeolocationAsync(page).ConfigureAwait(false);
                Assert.That(geolocation.Latitude, Is.EqualTo(10d));
                Assert.That(geolocation.Longitude, Is.EqualTo(10d));
            }
            finally
            {
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("geolocation.spec.ts", "watchPosition should be notified")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task WatchPositionShouldBeNotified()
        {
            EnsureServer();
            await _context.GrantPermissionsAsync(new[] { ContextPermissions.Geolocation }).ConfigureAwait(false);
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            List<string> messages = new List<string>();
            _page.Console += (_, message) => messages.Add(message.Text);

            await _context.SetGeolocationAsync(new Geolocation { Latitude = 0, Longitude = 0 }).ConfigureAwait(false);
            await _page.EvaluateAsync(@"() => {
    navigator.geolocation.watchPosition(pos => {
      const coords = pos.coords;
      console.log(`lat=${coords.latitude} lng=${coords.longitude}`);
    }, err => {});
  }").ConfigureAwait(false);

            await Task.WhenAll(
                _page.WaitForEventAsync(PageEvent.Console, message => message.Text.Contains("lat=0 lng=10", StringComparison.Ordinal)),
                _context.SetGeolocationAsync(new Geolocation { Latitude = 0, Longitude = 10 })).ConfigureAwait(false);
            await Task.WhenAll(
                _page.WaitForEventAsync(PageEvent.Console, message => message.Text.Contains("lat=20 lng=30", StringComparison.Ordinal)),
                _context.SetGeolocationAsync(new Geolocation { Latitude = 20, Longitude = 30 })).ConfigureAwait(false);
            await Task.WhenAll(
                _page.WaitForEventAsync(PageEvent.Console, message => message.Text.Contains("lat=40 lng=50", StringComparison.Ordinal)),
                _context.SetGeolocationAsync(new Geolocation { Latitude = 40, Longitude = 50 })).ConfigureAwait(false);

            string allMessages = string.Join("|", messages);
            Assert.That(allMessages, Does.Contain("lat=0 lng=10"));
            Assert.That(allMessages, Does.Contain("lat=20 lng=30"));
            Assert.That(allMessages, Does.Contain("lat=40 lng=50"));
        }

        [PlaywrightTest("geolocation.spec.ts", "should use context options for popup")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseContextOptionsForPopup()
        {
            EnsureServer();
            await _context.GrantPermissionsAsync(new[] { ContextPermissions.Geolocation }).ConfigureAwait(false);
            await _context.SetGeolocationAsync(new Geolocation { Longitude = 10, Latitude = 10 }).ConfigureAwait(false);
            Task<IPage> popupTask = _page.WaitForEventAsync(PageEvent.Popup);
            Task evaluateTask = _page.EvaluateAsync(
                "url => window._popup = window.open(url)",
                Prefix + "/geolocation.html");
            IPage popup = await popupTask.ConfigureAwait(false);
            await evaluateTask.ConfigureAwait(false);
            await popup.WaitForLoadStateAsync().ConfigureAwait(false);
            GeoCoords geolocation = await popup.EvaluateAsync<GeoCoords>("window.geolocationPromise")
                .ConfigureAwait(false);
            Assert.That(geolocation.Latitude, Is.EqualTo(10d));
            Assert.That(geolocation.Longitude, Is.EqualTo(10d));
        }

        private static async Task<GeoCoords> ReadGeolocationAsync(IPage page)
        {
            return await page.EvaluateAsync<GeoCoords>(
                @"new Promise(resolve => navigator.geolocation.getCurrentPosition(position => {
    resolve({ latitude: position.coords.latitude, longitude: position.coords.longitude });
  }))").ConfigureAwait(false);
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
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

        private sealed class GeoCoords
        {
            [JsonPropertyName("latitude")]
            public double Latitude { get; set; }

            [JsonPropertyName("longitude")]
            public double Longitude { get; set; }
        }
    }
}
