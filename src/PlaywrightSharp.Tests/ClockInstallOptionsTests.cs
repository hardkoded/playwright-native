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

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>clock.install({ time })</c>.
    /// </summary>
    [TestFixture]
    public class ClockInstallOptionsTests : PageTestEx
    {
        [PlaywrightTest("page-clock.spec.ts", "TimeDate freezes Date.now")]
        [Test]
        [Timeout(30_000)]
        public async Task TimeDateShouldFreezeDateNow()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            const long frozen = 1_706_871_600_000;
            DateTime timeDate = DateTimeOffset.FromUnixTimeMilliseconds(frozen).UtcDateTime;
            await page.Clock.InstallAsync(new ClockInstallOptions { TimeDate = timeDate }).ConfigureAwait(false);

            long now = await page.EvaluateAsync<long>("Date.now()").ConfigureAwait(false);
            Assert.That(now, Is.EqualTo(frozen));
        }

        [PlaywrightTest("page-clock.spec.ts", "Time string freezes Date.now")]
        [Test]
        [Timeout(30_000)]
        public async Task TimeShouldFreezeDateNow()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            const string time = "2024-02-02T11:00:00.000Z";
            DateTime parsed = DateTime.Parse(time, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            long expected = new DateTimeOffset(parsed.ToUniversalTime()).ToUnixTimeMilliseconds();
            await page.Clock.InstallAsync(new ClockInstallOptions { Time = time }).ConfigureAwait(false);

            long now = await page.EvaluateAsync<long>("Date.now()").ConfigureAwait(false);
            Assert.That(now, Is.EqualTo(expected));
        }

        [PlaywrightTest("page-clock.spec.ts", "TimeString freezes Date.now")]
        [Test]
        [Timeout(30_000)]
        public async Task TimeStringShouldFreezeDateNow()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            const string time = "2024-02-02T11:00:00.000Z";
            DateTime parsed = DateTime.Parse(time, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            long expected = new DateTimeOffset(parsed.ToUniversalTime()).ToUnixTimeMilliseconds();
            await page.Clock.InstallAsync(new ClockInstallOptions { TimeString = time }).ConfigureAwait(false);

            long now = await page.EvaluateAsync<long>("Date.now()").ConfigureAwait(false);
            Assert.That(now, Is.EqualTo(expected));
        }

        [PlaywrightTest("page-clock.spec.ts", "TimeDate wins over Time")]
        [Test]
        [Timeout(30_000)]
        public async Task TimeDateShouldWinOverTime()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            const long frozen = 1_706_871_600_000;
            DateTime timeDate = DateTimeOffset.FromUnixTimeMilliseconds(frozen).UtcDateTime;
            await page.Clock.InstallAsync(new ClockInstallOptions
            {
                TimeDate = timeDate,
                Time = "1999-01-01T00:00:00.000Z",
            }).ConfigureAwait(false);

            long now = await page.EvaluateAsync<long>("Date.now()").ConfigureAwait(false);
            Assert.That(now, Is.EqualTo(frozen));
        }
    }
}
