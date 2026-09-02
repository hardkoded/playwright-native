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
    /// Official <c>playwright.devices</c>.
    /// </summary>
    [TestFixture]
    public class PlaywrightDevicesTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-device.spec.ts", "Devices iPhone 13 emulates viewport and UA")]
        [Test]
        [Timeout(30_000)]
        public async Task DevicesIPhone13ShouldEmulateViewportAndUserAgent()
        {
            Assert.That(Playwright.Devices.ContainsKey("iPhone 13"), Is.True);
            BrowserContextOptions iphone = Playwright.Devices["iPhone 13"];
            Assert.That(iphone.UserAgent, Does.Contain("iPhone"));
            Assert.That(iphone.HasTouch, Is.True);
            Assert.That(iphone.IsMobile, Is.True);
            Assert.That(iphone.Viewport, Is.Not.Null);
            Assert.That(iphone.Viewport.Width, Is.EqualTo(390));
            Assert.That(iphone.Viewport.Height, Is.EqualTo(664));

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(iphone).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("data:text/html,<html><body>wave458</body></html>").ConfigureAwait(false);

            string userAgent = await page.EvaluateAsync<string>("navigator.userAgent").ConfigureAwait(false);
            Assert.That(userAgent, Does.Contain("iPhone"));
            Assert.That(page.ViewportSize, Is.Not.Null);
            Assert.That(page.ViewportSize.Width, Is.EqualTo(390));
            Assert.That(page.ViewportSize.Height, Is.EqualTo(664));
        }
    }
}
