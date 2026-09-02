/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/page-clock.spec.ts</c> parity. Do not edit leftover
    /// <c>ClockTests</c> or <c>ClockInstallOptionsTests</c>.
    /// Official skip when <c>PW_CLOCK</c> is set.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryPageClockParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private readonly List<object> _calls = new();
        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19884;
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
        }

        [SetUp]
        public async Task SetUpAsync()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PW_CLOCK")))
            {
                Assert.Ignore("official skip: PW_CLOCK");
            }

            Server?.Reset();
            _calls.Clear();
            await DisposeSessionAsync().ConfigureAwait(false);
            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            _context = await _browser.NewContextAsync().ConfigureAwait(false);
            _page = await _context.NewPageAsync().ConfigureAwait(false);
            await _page.ExposeFunctionAsync("stub", (object value) =>
            {
                _calls.Add(value);
            }).ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            Server?.Reset();
            await DisposeSessionAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("page-clock.spec.ts", "triggers immediately without specified delay")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task TriggersImmediatelyWithoutSpecifiedDelay()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            await _page.EvaluateAsync("() => { setTimeout(window.stub); }").ConfigureAwait(false);
            await _page.Clock.RunForAsync(0).ConfigureAwait(false);
            Assert.That(_calls, Has.Count.EqualTo(1));
        }

        [PlaywrightTest("page-clock.spec.ts", "does not trigger without sufficient delay")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DoesNotTriggerWithoutSufficientDelay()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            await _page.EvaluateAsync("() => { setTimeout(window.stub, 100); }").ConfigureAwait(false);
            await _page.Clock.RunForAsync(10).ConfigureAwait(false);
            Assert.That(_calls, Is.Empty);
        }

        [PlaywrightTest("page-clock.spec.ts", "triggers after sufficient delay")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task TriggersAfterSufficientDelay()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            await _page.EvaluateAsync("() => { setTimeout(window.stub, 100); }").ConfigureAwait(false);
            await _page.Clock.RunForAsync(100).ConfigureAwait(false);
            Assert.That(_calls, Has.Count.EqualTo(1));
        }

        [PlaywrightTest("page-clock.spec.ts", "triggers simultaneous timers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task TriggersSimultaneousTimers()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            await _page.EvaluateAsync("() => { setTimeout(window.stub, 100); setTimeout(window.stub, 100); }")
                .ConfigureAwait(false);
            await _page.Clock.RunForAsync(100).ConfigureAwait(false);
            Assert.That(_calls, Has.Count.EqualTo(2));
        }

        [PlaywrightTest("page-clock.spec.ts", "triggers multiple simultaneous timers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task TriggersMultipleSimultaneousTimers()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            await _page.EvaluateAsync(
                    "() => { setTimeout(window.stub, 100); setTimeout(window.stub, 100); setTimeout(window.stub, 99); setTimeout(window.stub, 100); }")
                .ConfigureAwait(false);
            await _page.Clock.RunForAsync(100).ConfigureAwait(false);
            Assert.That(_calls, Has.Count.EqualTo(4));
        }

        [PlaywrightTest("page-clock.spec.ts", "waits after setTimeout was called")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task WaitsAfterSetTimeoutWasCalled()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            await _page.EvaluateAsync("() => { setTimeout(window.stub, 150); }").ConfigureAwait(false);
            await _page.Clock.RunForAsync(50).ConfigureAwait(false);
            Assert.That(_calls, Is.Empty);
            await _page.Clock.RunForAsync(100).ConfigureAwait(false);
            Assert.That(_calls, Has.Count.EqualTo(1));
        }

        [PlaywrightTest("page-clock.spec.ts", "triggers event when some throw")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task TriggersEventWhenSomeThrow()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            await _page.EvaluateAsync(
                    "() => { setTimeout(() => { throw new Error(); }, 100); setTimeout(window.stub, 120); }")
                .ConfigureAwait(false);
            Assert.ThrowsAsync<PlaywrightNativeException>(
                async () => await _page.Clock.RunForAsync(120).ConfigureAwait(false));
            Assert.That(_calls, Has.Count.EqualTo(1));
        }

        [PlaywrightTest("page-clock.spec.ts", "creates updated Date while ticking")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CreatesUpdatedDateWhileTicking()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            await _page.Clock.SetSystemTimeAsync(0).ConfigureAwait(false);
            await _page.EvaluateAsync("() => { setInterval(() => { window.stub(new Date().getTime()); }, 10); }")
                .ConfigureAwait(false);
            await _page.Clock.RunForAsync(100).ConfigureAwait(false);
            Assert.That(CallNumbers(), Is.EqualTo(new[] { 10L, 20L, 30L, 40L, 50L, 60L, 70L, 80L, 90L, 100L }));
        }

        [PlaywrightTest("page-clock.spec.ts", "passes 8 seconds")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task Passes8Seconds()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            await _page.EvaluateAsync("() => { setInterval(window.stub, 4000); }").ConfigureAwait(false);
            await _page.Clock.RunForAsync("08").ConfigureAwait(false);
            Assert.That(_calls, Has.Count.EqualTo(2));
        }

        [PlaywrightTest("page-clock.spec.ts", "passes 1 minute")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task Passes1Minute()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            await _page.EvaluateAsync("() => { setInterval(window.stub, 6000); }").ConfigureAwait(false);
            await _page.Clock.RunForAsync("01:00").ConfigureAwait(false);
            Assert.That(_calls, Has.Count.EqualTo(10));
        }

        [PlaywrightTest("page-clock.spec.ts", "passes 2 hours, 34 minutes and 10 seconds")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task Passes2Hours34MinutesAnd10Seconds()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            await _page.EvaluateAsync("() => { setInterval(window.stub, 10000); }").ConfigureAwait(false);
            await _page.Clock.RunForAsync("02:34:10").ConfigureAwait(false);
            Assert.That(_calls, Has.Count.EqualTo(925));
        }

        [PlaywrightTest("page-clock.spec.ts", "throws for invalid format")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ThrowsForInvalidFormat()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            await _page.EvaluateAsync("() => { setInterval(window.stub, 10000); }").ConfigureAwait(false);
            Assert.ThrowsAsync<PlaywrightNativeException>(
                async () => await _page.Clock.RunForAsync("12:02:34:10").ConfigureAwait(false));
            Assert.That(_calls, Is.Empty);
        }

        [PlaywrightTest("page-clock.spec.ts", "returns the current now value")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ReturnsTheCurrentNowValue()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            await _page.Clock.SetSystemTimeAsync(0).ConfigureAwait(false);
            await _page.Clock.RunForAsync(200).ConfigureAwait(false);
            Assert.That(await _page.EvaluateAsync<long>("() => Date.now()").ConfigureAwait(false), Is.EqualTo(200));
        }

        [PlaywrightTest("page-clock.spec.ts", "ignores timers which wouldn't be run")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task IgnoresTimersWhichWouldntBeRun()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            await _page.EvaluateAsync("() => { setTimeout(() => { window.stub('should not be logged'); }, 1000); }")
                .ConfigureAwait(false);
            await _page.Clock.FastForwardAsync(500).ConfigureAwait(false);
            Assert.That(_calls, Is.Empty);
        }

        [PlaywrightTest("page-clock.spec.ts", "pushes back execution time for skipped timers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PushesBackExecutionTimeForSkippedTimers()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            await _page.EvaluateAsync("() => { setTimeout(() => { window.stub(Date.now()); }, 1000); }")
                .ConfigureAwait(false);
            await _page.Clock.FastForwardAsync(2000).ConfigureAwait(false);
            Assert.That(CallNumbers(), Is.EqualTo(new[] { 3000L }));
        }

        [PlaywrightTest("page-clock.spec.ts", "supports string time arguments")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SupportsStringTimeArguments()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            await _page.EvaluateAsync("() => { setTimeout(() => { window.stub(Date.now()); }, 100000); }")
                .ConfigureAwait(false);
            await _page.Clock.FastForwardAsync("01:50").ConfigureAwait(false);
            Assert.That(CallNumbers(), Is.EqualTo(new[] { 111000L }));
        }

        [PlaywrightTest("page-clock.spec.ts", "sets initial timestamp")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SetsInitialTimestamp()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            await _page.Clock.SetSystemTimeAsync(1400).ConfigureAwait(false);
            Assert.That(await _page.EvaluateAsync<long>("() => Date.now()").ConfigureAwait(false), Is.EqualTo(1400));
        }

        [PlaywrightTest("page-clock.spec.ts", "should throw for invalid date")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowForInvalidDate()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            PlaywrightNativeException invalidDate = Assert.ThrowsAsync<PlaywrightNativeException>(
                async () => await _page.Clock.SetSystemTimeAsync("Invalid Date").ConfigureAwait(false));
            Assert.That(invalidDate.Message, Does.Contain("Invalid date: Invalid Date"));
            PlaywrightNativeException invalid = Assert.ThrowsAsync<PlaywrightNativeException>(
                async () => await _page.Clock.SetSystemTimeAsync("invalid").ConfigureAwait(false));
            Assert.That(invalid.Message, Does.Contain("Invalid date: invalid"));
        }

        [PlaywrightTest("page-clock.spec.ts", "replaces global setTimeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ReplacesGlobalSetTimeout()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            await _page.EvaluateAsync("() => { setTimeout(window.stub, 1000); }").ConfigureAwait(false);
            await _page.Clock.RunForAsync(1000).ConfigureAwait(false);
            Assert.That(_calls, Has.Count.EqualTo(1));
        }

        [PlaywrightTest("page-clock.spec.ts", "global fake setTimeout should return id")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GlobalFakeSetTimeoutShouldReturnId()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            object id = await _page.EvaluateAsync<object>("() => setTimeout(window.stub, 1000)").ConfigureAwait(false);
            Assert.That(Convert.ToInt64(id, CultureInfo.InvariantCulture), Is.GreaterThan(0));
        }

        [PlaywrightTest("page-clock.spec.ts", "replaces global clearTimeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ReplacesGlobalClearTimeout()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            await _page.EvaluateAsync("() => { const to = setTimeout(window.stub, 1000); clearTimeout(to); }")
                .ConfigureAwait(false);
            await _page.Clock.RunForAsync(1000).ConfigureAwait(false);
            Assert.That(_calls, Is.Empty);
        }

        [PlaywrightTest("page-clock.spec.ts", "replaces global setInterval")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ReplacesGlobalSetInterval()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            await _page.EvaluateAsync("() => { setInterval(window.stub, 500); }").ConfigureAwait(false);
            await _page.Clock.RunForAsync(1000).ConfigureAwait(false);
            Assert.That(_calls, Has.Count.EqualTo(2));
        }

        [PlaywrightTest("page-clock.spec.ts", "replaces global clearInterval")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ReplacesGlobalClearInterval()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            await _page.EvaluateAsync("() => { const to = setInterval(window.stub, 500); clearInterval(to); }")
                .ConfigureAwait(false);
            await _page.Clock.RunForAsync(1000).ConfigureAwait(false);
            Assert.That(_calls, Is.Empty);
        }

        [PlaywrightTest("page-clock.spec.ts", "replaces global performance.now")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ReplacesGlobalPerformanceNow()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            Task<Dictionary<string, long>> promise = _page.EvaluateAsync<Dictionary<string, long>>(
                @"async () => {
                    const prev = performance.now();
                    await new Promise(f => setTimeout(f, 1000));
                    const next = performance.now();
                    return { prev, next };
                }");
            await _page.Clock.RunForAsync(1000).ConfigureAwait(false);
            Dictionary<string, long> result = await promise.ConfigureAwait(false);
            Assert.That(result["prev"], Is.EqualTo(1000));
            Assert.That(result["next"], Is.EqualTo(2000));
        }

        [PlaywrightTest("page-clock.spec.ts", "fakes Date constructor")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FakesDateConstructor()
        {
            await InstallPausedAsync().ConfigureAwait(false);
            Assert.That(await _page.EvaluateAsync<long>("() => new Date().getTime()").ConfigureAwait(false), Is.EqualTo(1000));
        }

        [PlaywrightTest("page-clock.spec.ts", "replaces global performance.timeOrigin")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ReplacesGlobalPerformanceTimeOrigin()
        {
            await _page.Clock.InstallAsync(1000).ConfigureAwait(false);
            await _page.Clock.PauseAtAsync(2000).ConfigureAwait(false);
            Task<Dictionary<string, long>> promise = _page.EvaluateAsync<Dictionary<string, long>>(
                @"async () => {
                    const prev = performance.now();
                    await new Promise(f => setTimeout(f, 1000));
                    const next = performance.now();
                    return { prev, next };
                }");
            await _page.Clock.RunForAsync(1000).ConfigureAwait(false);
            Assert.That(await _page.EvaluateAsync<long>("() => performance.timeOrigin").ConfigureAwait(false), Is.EqualTo(1000));
            Dictionary<string, long> result = await promise.ConfigureAwait(false);
            Assert.That(result["prev"], Is.EqualTo(1000));
            Assert.That(result["next"], Is.EqualTo(2000));
        }

        [PlaywrightTest("page-clock.spec.ts", "should tick after popup")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTickAfterPopup()
        {
            await _page.Clock.InstallAsync(0).ConfigureAwait(false);
            DateTime now = new DateTime(2015, 9, 25, 0, 0, 0, DateTimeKind.Utc);
            await _page.Clock.PauseAtAsync(now).ConfigureAwait(false);
            Task<IPage> popupTask = _page.WaitForPopupAsync();
            await _page.EvaluateAsync("() => window.open('about:blank')").ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            long expected = new DateTimeOffset(now).ToUnixTimeMilliseconds();
            Assert.That(await popup.EvaluateAsync<long>("() => Date.now()").ConfigureAwait(false), Is.EqualTo(expected));
            await _page.Clock.RunForAsync(1000).ConfigureAwait(false);
            Assert.That(await popup.EvaluateAsync<long>("() => Date.now()").ConfigureAwait(false), Is.EqualTo(expected + 1000));
        }

        [PlaywrightTest("page-clock.spec.ts", "should tick before popup")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTickBeforePopup()
        {
            await _page.Clock.InstallAsync(0).ConfigureAwait(false);
            DateTime now = new DateTime(2015, 9, 25, 0, 0, 0, DateTimeKind.Utc);
            await _page.Clock.PauseAtAsync(now).ConfigureAwait(false);
            await _page.Clock.RunForAsync(1000).ConfigureAwait(false);
            Task<IPage> popupTask = _page.WaitForPopupAsync();
            await _page.EvaluateAsync("() => window.open('about:blank')").ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            long expected = new DateTimeOffset(now).ToUnixTimeMilliseconds() + 1000;
            Assert.That(await popup.EvaluateAsync<long>("() => Date.now()").ConfigureAwait(false), Is.EqualTo(expected));
        }

        [PlaywrightTest("page-clock.spec.ts", "should run time before popup")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRunTimeBeforePopup()
        {
            EnsureServer();
            Server.SetRoute("/popup.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<script>window.time = Date.now()</script>");
            });
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await _page.WaitForTimeoutAsync(2000).ConfigureAwait(false);
            Task<IPage> popupTask = _page.WaitForPopupAsync();
            await _page.EvaluateAsync("url => window.open(url)", Prefix + "/popup.html").ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            long popupTime = await popup.EvaluateAsync<long>("time").ConfigureAwait(false);
            Assert.That(popupTime, Is.GreaterThanOrEqualTo(2000));
        }

        [PlaywrightTest("page-clock.spec.ts", "should not run time before popup on pause")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotRunTimeBeforePopupOnPause()
        {
            EnsureServer();
            Server.SetRoute("/popup.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<script>window.time = Date.now()</script>");
            });
            await _page.Clock.InstallAsync(0).ConfigureAwait(false);
            await _page.Clock.PauseAtAsync(1000).ConfigureAwait(false);
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await _page.WaitForTimeoutAsync(2000).ConfigureAwait(false);
            Task<IPage> popupTask = _page.WaitForPopupAsync();
            await _page.EvaluateAsync("url => window.open(url)", Prefix + "/popup.html").ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            await popup.WaitForLoadStateAsync().ConfigureAwait(false);
            Assert.That(await popup.EvaluateAsync<long>("time").ConfigureAwait(false), Is.EqualTo(1000));
        }

        [PlaywrightTest("page-clock.spec.ts", "does not fake methods")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DoesNotFakeMethods()
        {
            await _page.Clock.SetFixedTimeAsync(0).ConfigureAwait(false);
            await _page.EvaluateAsync("() => new Promise(f => setTimeout(f, 1))").ConfigureAwait(false);
        }

        [PlaywrightTest("page-clock.spec.ts", "allows setting time multiple times")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task AllowsSettingTimeMultipleTimes()
        {
            await _page.Clock.SetFixedTimeAsync(100).ConfigureAwait(false);
            Assert.That(await _page.EvaluateAsync<long>("() => Date.now()").ConfigureAwait(false), Is.EqualTo(100));
            await _page.Clock.SetFixedTimeAsync(200).ConfigureAwait(false);
            Assert.That(await _page.EvaluateAsync<long>("() => Date.now()").ConfigureAwait(false), Is.EqualTo(200));
        }

        [PlaywrightTest("page-clock.spec.ts", "fixed time is not affected by clock manipulation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FixedTimeIsNotAffectedByClockManipulation()
        {
            await _page.Clock.SetFixedTimeAsync(100).ConfigureAwait(false);
            Assert.That(await _page.EvaluateAsync<long>("() => Date.now()").ConfigureAwait(false), Is.EqualTo(100));
            await _page.Clock.FastForwardAsync(20).ConfigureAwait(false);
            Assert.That(await _page.EvaluateAsync<long>("() => Date.now()").ConfigureAwait(false), Is.EqualTo(100));
        }

        [PlaywrightTest("page-clock.spec.ts", "allows installing fake timers after settings time")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task AllowsInstallingFakeTimersAfterSettingsTime()
        {
            await _page.Clock.SetFixedTimeAsync(100).ConfigureAwait(false);
            Assert.That(await _page.EvaluateAsync<long>("() => Date.now()").ConfigureAwait(false), Is.EqualTo(100));
            await _page.Clock.SetFixedTimeAsync(200).ConfigureAwait(false);
            await _page.EvaluateAsync("() => { setTimeout(() => window.stub(Date.now())); }").ConfigureAwait(false);
            await _page.Clock.RunForAsync(0).ConfigureAwait(false);
            Assert.That(CallNumbers(), Is.EqualTo(new[] { 200L }));
        }

        [PlaywrightTest("page-clock.spec.ts", "should progress time")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldProgressTime()
        {
            await _page.Clock.InstallAsync(0).ConfigureAwait(false);
            long startRealTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _page.GoToAsync("data:text/html,").ConfigureAwait(false);
            await _page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            long now = await _page.EvaluateAsync<long>("() => Date.now()").ConfigureAwait(false);
            long realElapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startRealTime;
            Assert.That(now, Is.GreaterThanOrEqualTo(1000));
            Assert.That(now, Is.LessThanOrEqualTo(realElapsed + 1000));
        }

        [PlaywrightTest("page-clock.spec.ts", "should runFor")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRunFor()
        {
            await _page.Clock.InstallAsync(0).ConfigureAwait(false);
            long startRealTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _page.GoToAsync("data:text/html,").ConfigureAwait(false);
            await _page.Clock.RunForAsync(10000).ConfigureAwait(false);
            long now = await _page.EvaluateAsync<long>("() => Date.now()").ConfigureAwait(false);
            long realElapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startRealTime;
            Assert.That(now, Is.GreaterThanOrEqualTo(10000));
            Assert.That(now, Is.LessThanOrEqualTo(10000 + realElapsed + 1000));
        }

        [PlaywrightTest("page-clock.spec.ts", "should fastForward")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFastForward()
        {
            await _page.Clock.InstallAsync(0).ConfigureAwait(false);
            long startRealTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _page.GoToAsync("data:text/html,").ConfigureAwait(false);
            await _page.Clock.FastForwardAsync(10000).ConfigureAwait(false);
            long now = await _page.EvaluateAsync<long>("() => Date.now()").ConfigureAwait(false);
            long realElapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startRealTime;
            Assert.That(now, Is.GreaterThanOrEqualTo(10000));
            Assert.That(now, Is.LessThanOrEqualTo(10000 + realElapsed + 1000));
        }

        [PlaywrightTest("page-clock.spec.ts", "should pause")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPause()
        {
            await _page.Clock.InstallAsync(0).ConfigureAwait(false);
            await _page.GoToAsync("data:text/html,").ConfigureAwait(false);
            await _page.Clock.PauseAtAsync(60000).ConfigureAwait(false);
            await _page.WaitForTimeoutAsync(1111).ConfigureAwait(false);
            Assert.That(await _page.EvaluateAsync<long>("() => Date.now()").ConfigureAwait(false), Is.EqualTo(60000));
        }

        [PlaywrightTest("page-clock.spec.ts", "should reject an invalid target time with an active requestAnimationFrame loop")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRejectAnInvalidTargetTimeWithAnActiveRequestAnimationFrameLoop()
        {
            await _page.Clock.InstallAsync().ConfigureAwait(false);
            await _page.SetContentAsync("<script>function tick() { requestAnimationFrame(tick); } requestAnimationFrame(tick);</script>")
                .ConfigureAwait(false);
            double now = await _page.EvaluateAsync<double>("() => Date.now()").ConfigureAwait(false);
            long invalidTime = (long)(now * 1_000_000);
            PlaywrightNativeException error = Assert.ThrowsAsync<PlaywrightNativeException>(
                async () => await _page.Clock.PauseAtAsync(invalidTime).ConfigureAwait(false));
            Assert.That(error.Message, Does.Contain("Invalid date: " + invalidTime.ToString(CultureInfo.InvariantCulture)));
        }

        [PlaywrightTest("page-clock.spec.ts", "should pause and fastForward")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPauseAndFastForward()
        {
            await _page.Clock.InstallAsync(0).ConfigureAwait(false);
            await _page.GoToAsync("data:text/html,").ConfigureAwait(false);
            await _page.Clock.PauseAtAsync(60000).ConfigureAwait(false);
            await _page.Clock.FastForwardAsync(1000).ConfigureAwait(false);
            Assert.That(await _page.EvaluateAsync<long>("() => Date.now()").ConfigureAwait(false), Is.EqualTo(61000));
        }

        [PlaywrightTest("page-clock.spec.ts", "should set system time on pause")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetSystemTimeOnPause()
        {
            await _page.Clock.InstallAsync(0).ConfigureAwait(false);
            await _page.GoToAsync("data:text/html,").ConfigureAwait(false);
            await _page.Clock.PauseAtAsync(60000).ConfigureAwait(false);
            Assert.That(await _page.EvaluateAsync<long>("() => Date.now()").ConfigureAwait(false), Is.EqualTo(60000));
        }

        [PlaywrightTest("page-clock.spec.ts", "fastForward should not run nested immediate")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FastForwardShouldNotRunNestedImmediate()
        {
            await _page.Clock.InstallAsync(0).ConfigureAwait(false);
            await _page.GoToAsync("data:text/html,").ConfigureAwait(false);
            await _page.Clock.PauseAtAsync(1000).ConfigureAwait(false);
            await _page.EvaluateAsync(
                    "() => { setTimeout(() => { window.stub('outer'); setTimeout(() => window.stub('inner'), 0); }, 1000); }")
                .ConfigureAwait(false);
            await _page.Clock.FastForwardAsync(1000).ConfigureAwait(false);
            Assert.That(CallStrings(), Is.EqualTo(new[] { "outer" }));
            await _page.Clock.FastForwardAsync(1).ConfigureAwait(false);
            Assert.That(CallStrings(), Is.EqualTo(new[] { "outer", "inner" }));
        }

        [PlaywrightTest("page-clock.spec.ts", "runFor should not run nested immediate")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task RunForShouldNotRunNestedImmediate()
        {
            await _page.Clock.InstallAsync(0).ConfigureAwait(false);
            await _page.GoToAsync("data:text/html,").ConfigureAwait(false);
            await _page.Clock.PauseAtAsync(1000).ConfigureAwait(false);
            await _page.EvaluateAsync(
                    "() => { setTimeout(() => { window.stub('outer'); setTimeout(() => window.stub('inner'), 0); }, 1000); }")
                .ConfigureAwait(false);
            await _page.Clock.RunForAsync(1000).ConfigureAwait(false);
            Assert.That(CallStrings(), Is.EqualTo(new[] { "outer" }));
            await _page.Clock.RunForAsync(1).ConfigureAwait(false);
            Assert.That(CallStrings(), Is.EqualTo(new[] { "outer", "inner" }));
        }

        [PlaywrightTest("page-clock.spec.ts", "runFor should not run nested immediate from microtask")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task RunForShouldNotRunNestedImmediateFromMicrotask()
        {
            await _page.Clock.InstallAsync(0).ConfigureAwait(false);
            await _page.GoToAsync("data:text/html,").ConfigureAwait(false);
            await _page.Clock.PauseAtAsync(1000).ConfigureAwait(false);
            await _page.EvaluateAsync(
                    "() => { setTimeout(() => { window.stub('outer'); void Promise.resolve().then(() => setTimeout(() => window.stub('inner'), 0)); }, 1000); }")
                .ConfigureAwait(false);
            await _page.Clock.RunForAsync(1000).ConfigureAwait(false);
            Assert.That(CallStrings(), Is.EqualTo(new[] { "outer" }));
            await _page.Clock.RunForAsync(1).ConfigureAwait(false);
            Assert.That(CallStrings(), Is.EqualTo(new[] { "outer", "inner" }));
        }

        [PlaywrightTest("page-clock.spec.ts", "check Date.now is an integer")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CheckDateNowIsAnInteger()
        {
            await _page.Clock.InstallAsync().ConfigureAwait(false);
            await _page.GoToAsync("data:text/html,").ConfigureAwait(false);
            await _page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            double first = await _page.EvaluateAsync<double>("Date.now()").ConfigureAwait(false);
            Assert.That(first, Is.EqualTo(Math.Floor(first)));
            await _page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            double second = await _page.EvaluateAsync<double>("Date.now()").ConfigureAwait(false);
            Assert.That(second, Is.EqualTo(Math.Floor(second)));
        }

        [PlaywrightTest("page-clock.spec.ts", "check Date.now is an integer (2)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CheckDateNowIsAnInteger2()
        {
            await _page.Clock.InstallAsync(0).ConfigureAwait(false);
            await _page.GoToAsync("data:text/html,").ConfigureAwait(false);
            await _page.Clock.PauseAtAsync(1000).ConfigureAwait(false);
            await _page.Clock.RunForAsync(0.5).ConfigureAwait(false);
            Assert.That(await _page.EvaluateAsync<long>("Date.now()").ConfigureAwait(false), Is.EqualTo(1001));
        }

        [PlaywrightTest("page-clock.spec.ts", "AbortSignal.timeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task AbortSignalTimeout()
        {
            await _page.Clock.InstallAsync(0).ConfigureAwait(false);
            IJSHandle controller = await _page.EvaluateHandleAsync(
                @"() => {
                    const signal = AbortSignal.any([AbortSignal.timeout(100)]);
                    const handle = { signal, event: false, handler: false };
                    signal.addEventListener('abort', () => handle.event = true);
                    signal.onabort = () => handle.handler = true;
                    return handle;
                }").ConfigureAwait(false);
            Dictionary<string, bool> before = await controller.EvaluateAsync<Dictionary<string, bool>>(
                @"handle => ({
                    signal: handle.signal.aborted,
                    event: handle.event,
                    handler: handle.handler,
                })").ConfigureAwait(false);
            Assert.That(before["signal"], Is.False);
            Assert.That(before["event"], Is.False);
            Assert.That(before["handler"], Is.False);
            await _page.Clock.RunForAsync(200).ConfigureAwait(false);
            JsonElement after = await controller.EvaluateAsync<JsonElement>(
                @"handle => ({
                    signal: handle.signal.aborted,
                    event: handle.event,
                    handler: handle.handler,
                    reason: {
                        name: handle.signal.reason.name,
                        message: handle.signal.reason.message,
                        code: handle.signal.reason.code,
                    },
                })").ConfigureAwait(false);
            Assert.That(after.GetProperty("signal").GetBoolean(), Is.True);
            Assert.That(after.GetProperty("event").GetBoolean(), Is.True);
            Assert.That(after.GetProperty("handler").GetBoolean(), Is.True);
            JsonElement reason = after.GetProperty("reason");
            Assert.That(reason.GetProperty("name").GetString(), Is.EqualTo("TimeoutError"));
            string expectedMessage = TestConstants.IsChromium ? "signal timed out" : "The operation timed out.";
            Assert.That(reason.GetProperty("message").GetString(), Is.EqualTo(expectedMessage));
            Assert.That(reason.GetProperty("code").GetInt32(), Is.EqualTo(23));
            Assert.That(await _page.EvaluateAsync<bool>("() => AbortSignal.abort().aborted").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-clock.spec.ts", "correctly increments Date.now()/performance.now() during blocking execution")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CorrectlyIncrementsDateNowPerformanceNowDuringBlockingExecution()
        {
            EnsureServer();
            await _page.Clock.SetSystemTimeAsync(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).ConfigureAwait(false);
            Server.SetRoute("/repro.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync(
                    "<html><body><script>{" +
                    "const start = performance.now(); while (performance.now() - start < 100) { }" +
                    "}{" +
                    "const start = Date.now(); while (Date.now() - start < 100) { }" +
                    "} console.log('done');</script></body></html>");
            });
            Task<IConsoleMessage> waitForDone = _page.WaitForConsoleMessageAsync(msg => msg.Text == "done");
            await _page.GoToAsync(Prefix + "/repro.html").ConfigureAwait(false);
            await waitForDone.ConfigureAwait(false);
        }

        private async Task InstallPausedAsync()
        {
            await _page.Clock.InstallAsync(0).ConfigureAwait(false);
            await _page.Clock.PauseAtAsync(1000).ConfigureAwait(false);
        }

        private long[] CallNumbers()
        {
            long[] values = new long[_calls.Count];
            for (int i = 0; i < _calls.Count; i++)
            {
                values[i] = Convert.ToInt64(_calls[i], CultureInfo.InvariantCulture);
            }

            return values;
        }

        private string[] CallStrings()
        {
            string[] values = new string[_calls.Count];
            for (int i = 0; i < _calls.Count; i++)
            {
                values[i] = Convert.ToString(_calls[i], CultureInfo.InvariantCulture);
            }

            return values;
        }

        private void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private async Task DisposeSessionAsync()
        {
            if (_context != null)
            {
                try
                {
                    await _context.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                _context = null;
                _page = null;
            }

            if (_browser != null)
            {
                try
                {
                    await _browser.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                _browser = null;
            }
        }
    }
}
