/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IClock.SetFixedTimeAsync(long)"/>.
    /// </summary>
    [TestFixture]
    public class ClockTests : PageTestEx
    {
        [PlaywrightTest("page-clock.spec.ts", "SetFixedTime pins Date.now")]
        [Test]
        [Timeout(30_000)]
        public async Task SetFixedTimeShouldPinDateNow()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            const long frozen = 1_706_871_600_000;
            await page.Clock.SetFixedTimeAsync(frozen).ConfigureAwait(false);

            long now = await page.EvaluateAsync<long>("Date.now()").ConfigureAwait(false);
            Assert.That(now, Is.EqualTo(frozen));
        }

        [PlaywrightTest("page-clock.spec.ts", "SetFixedTime pins new Date")]
        [Test]
        [Timeout(30_000)]
        public async Task SetFixedTimeShouldPinNewDate()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            const long frozen = 1_706_871_600_000;
            await context.Clock.SetFixedTimeAsync(frozen).ConfigureAwait(false);

            long constructed = await page.EvaluateAsync<long>("new Date().getTime()").ConfigureAwait(false);
            Assert.That(constructed, Is.EqualTo(frozen));
            Assert.That(page.Clock, Is.SameAs(context.Clock));
        }

        [PlaywrightTest("page-clock.spec.ts", "SetFixedTime survives navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task SetFixedTimeShouldSurviveNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            const long frozen = 1_706_871_600_000;
            await page.Clock.SetFixedTimeAsync(frozen).ConfigureAwait(false);
            await page.GoToAsync("data:text/html,<html><body>clock</body></html>").ConfigureAwait(false);

            long now = await page.EvaluateAsync<long>("Date.now()").ConfigureAwait(false);
            Assert.That(now, Is.EqualTo(frozen));
        }

        [PlaywrightTest("page-clock.spec.ts", "Install freezes Date.now")]
        [Test]
        [Timeout(30_000)]
        public async Task InstallShouldFreezeDateNow()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            const long frozen = 1_706_871_600_000;
            await page.Clock.InstallAsync(frozen).ConfigureAwait(false);

            long now = await page.EvaluateAsync<long>("Date.now()").ConfigureAwait(false);
            Assert.That(now, Is.EqualTo(frozen));
        }

        [PlaywrightTest("page-clock.spec.ts", "FastForward fires setTimeout")]
        [Test]
        [Timeout(30_000)]
        public async Task FastForwardShouldFireSetTimeout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            const long frozen = 1_706_871_600_000;
            await page.Clock.InstallAsync(frozen).ConfigureAwait(false);
            await page.EvaluateAsync("window.__fired = 0; setTimeout(() => { window.__fired = 1; }, 1000);").ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<int>("window.__fired").ConfigureAwait(false), Is.EqualTo(0));
            await page.Clock.FastForwardAsync(1000).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("window.__fired").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvaluateAsync<long>("Date.now()").ConfigureAwait(false), Is.EqualTo(frozen + 1000));
        }

        [PlaywrightTest("page-clock.spec.ts", "RunFor accepts mm:ss")]
        [Test]
        [Timeout(30_000)]
        public async Task RunForShouldAcceptMinuteSecondString()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            const long frozen = 1_706_871_600_000;
            await page.Clock.InstallAsync(frozen).ConfigureAwait(false);
            await page.Clock.RunForAsync("00:02").ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<long>("Date.now()").ConfigureAwait(false), Is.EqualTo(frozen + 2000));
        }

        [PlaywrightTest("page-clock.spec.ts", "PauseAt jumps and stays frozen")]
        [Test]
        [Timeout(30_000)]
        public async Task PauseAtShouldJumpAndStayFrozen()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            const long frozen = 1_706_871_600_000;
            await page.Clock.InstallAsync(frozen).ConfigureAwait(false);
            await page.Clock.PauseAtAsync(frozen + 5_000).ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<long>("Date.now()").ConfigureAwait(false), Is.EqualTo(frozen + 5_000));
            await page.WaitForTimeoutAsync(40).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<long>("Date.now()").ConfigureAwait(false), Is.EqualTo(frozen + 5_000));
        }

        [PlaywrightTest("page-clock.spec.ts", "Resume lets Date.now progress")]
        [Test]
        [Timeout(30_000)]
        public async Task ResumeShouldLetDateNowProgress()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            const long frozen = 1_706_871_600_000;
            await page.Clock.InstallAsync(frozen).ConfigureAwait(false);
            await page.Clock.ResumeAsync().ConfigureAwait(false);
            await page.WaitForTimeoutAsync(50).ConfigureAwait(false);

            long now = await page.EvaluateAsync<long>("Date.now()").ConfigureAwait(false);
            Assert.That(now, Is.GreaterThan(frozen));
        }

        [PlaywrightTest("page-clock.spec.ts", "SetSystemTime progresses from the origin")]
        [Test]
        [Timeout(30_000)]
        public async Task SetSystemTimeShouldProgressFromOrigin()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            const long origin = 1_706_871_600_000;
            await page.Clock.SetSystemTimeAsync(origin).ConfigureAwait(false);
            long first = await page.EvaluateAsync<long>("Date.now()").ConfigureAwait(false);
            Assert.That(first, Is.GreaterThanOrEqualTo(origin));
            await page.WaitForTimeoutAsync(50).ConfigureAwait(false);
            long second = await page.EvaluateAsync<long>("Date.now()").ConfigureAwait(false);
            Assert.That(second, Is.GreaterThan(first));
        }
    }
}
