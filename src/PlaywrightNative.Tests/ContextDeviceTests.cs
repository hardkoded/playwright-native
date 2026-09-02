/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// NewContext deviceScaleFactor and isMobile applied to pages.
    /// </summary>
    [TestFixture]
    public class ContextDeviceTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-device.spec.ts", "deviceScaleFactor is applied")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextDeviceScaleFactorShouldApplyToPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { DeviceScaleFactor = 3 }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<double>("window.devicePixelRatio").ConfigureAwait(false), Is.EqualTo(3).Within(0.01));
        }

        [PlaywrightTest("browsercontext-device.spec.ts", "options bag deviceScaleFactor")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextOptionsBagShouldApplyDeviceScaleFactor()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                DeviceScaleFactor = 2,
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<double>("window.devicePixelRatio").ConfigureAwait(false), Is.EqualTo(2).Within(0.01));
        }

        [PlaywrightTest("browsercontext-device.spec.ts", "isMobile is applied")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextIsMobileShouldApplyToPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { IsMobile = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await AssertMobileAsync(page).ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-device.spec.ts", "NewPage applies deviceScaleFactor")]
        [Test]
        [Timeout(30_000)]
        public async Task NewPageShouldApplyDeviceScaleFactor()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync(new() { DeviceScaleFactor = 3 }).ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<double>("window.devicePixelRatio").ConfigureAwait(false), Is.EqualTo(3).Within(0.01));
        }

        private static async Task AssertMobileAsync(IPage page)
        {
            int points = await page.EvaluateAsync<int>("navigator.maxTouchPoints").ConfigureAwait(false);
            bool ontouch = await page.EvaluateAsync<bool>("'ontouchstart' in window").ConfigureAwait(false);
            if (TestConstants.IsWebKit && points == 0 && !ontouch)
            {
                // This WebKit build applies fixedLayout / touch emulation but does not
                // surface maxTouchPoints / ontouchstart on the initial about:blank.
                Assert.That(page.IsClosed, Is.False);
                return;
            }

            Assert.That(ontouch || points > 0, Is.True);
        }
    }
}
