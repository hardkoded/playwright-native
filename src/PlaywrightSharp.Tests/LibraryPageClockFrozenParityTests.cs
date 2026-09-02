/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/page-clock.frozen.spec.ts</c> parity. Official
    /// <c>browserTest</c> fixture installs <c>clock.install({ time: 0 })</c>
    /// plus <c>pauseAt(1000)</c> when <c>PW_CLOCK=frozen</c>, or
    /// <c>clock.install({ time: 0 })</c> when <c>PW_CLOCK=realtime</c>.
    /// Each title skips unless that env value is set.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryPageClockFrozenParityTests : PageTestEx
    {
        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        [SetUp]
        public async Task SetUpAsync()
        {
            await DisposeSessionAsync().ConfigureAwait(false);
            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            _context = await _browser.NewContextAsync().ConfigureAwait(false);
            string mode = Environment.GetEnvironmentVariable("PW_CLOCK");
            if (string.Equals(mode, "frozen", StringComparison.Ordinal))
            {
                await _context.Clock.InstallAsync(0).ConfigureAwait(false);
                await _context.Clock.PauseAtAsync(1000).ConfigureAwait(false);
            }
            else if (string.Equals(mode, "realtime", StringComparison.Ordinal))
            {
                await _context.Clock.InstallAsync(0).ConfigureAwait(false);
            }

            _page = await _context.NewPageAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            await DisposeSessionAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("page-clock.frozen.spec.ts", "clock should be frozen")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ClockShouldBeFrozen()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("PW_CLOCK"), "frozen", StringComparison.Ordinal))
            {
                Assert.Ignore("official skip: PW_CLOCK !== frozen");
            }

            Assert.That(await _page.EvaluateAsync<long>("Date.now()").ConfigureAwait(false), Is.EqualTo(1000));
        }

        [PlaywrightTest("page-clock.frozen.spec.ts", "clock should be realtime")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ClockShouldBeRealtime()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("PW_CLOCK"), "realtime", StringComparison.Ordinal))
            {
                Assert.Ignore("official skip: PW_CLOCK !== realtime");
            }

            Assert.That(await _page.EvaluateAsync<long>("Date.now()").ConfigureAwait(false), Is.LessThan(10000));
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
