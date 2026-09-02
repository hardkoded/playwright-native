/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// NewContext viewport and user-agent applied to pages.
    /// </summary>
    [TestFixture]
    public class ContextEmulationTests : PageTestEx
    {
        [PlaywrightTest("emulation-focus.spec.ts", "viewport is applied")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextViewportShouldApplyToPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 512, Height = 384 } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(page.ViewportSize, Is.Not.Null);
            Assert.That(page.ViewportSize.Width, Is.EqualTo(512));
            Assert.That(page.ViewportSize.Height, Is.EqualTo(384));
            Assert.That(await page.EvaluateAsync<int>("window.innerWidth").ConfigureAwait(false), Is.EqualTo(512));
            Assert.That(await page.EvaluateAsync<int>("window.innerHeight").ConfigureAwait(false), Is.EqualTo(384));
        }

        [PlaywrightTest("emulation-focus.spec.ts", "user agent is applied")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextUserAgentShouldApplyToPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { UserAgent = "PW-Wave51" }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<string>("navigator.userAgent").ConfigureAwait(false), Is.EqualTo("PW-Wave51"));
        }

        [PlaywrightTest("emulation-focus.spec.ts", "options bag viewport and user agent")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextOptionsBagShouldApplyViewportAndUserAgent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                Viewport = new ViewportSize { Width = 640, Height = 480 },
                UserAgent = "PW-Wave51-Bag",
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(page.ViewportSize.Width, Is.EqualTo(640));
            Assert.That(page.ViewportSize.Height, Is.EqualTo(480));
            Assert.That(await page.EvaluateAsync<string>("navigator.userAgent").ConfigureAwait(false), Is.EqualTo("PW-Wave51-Bag"));
        }

        [PlaywrightTest("emulation-focus.spec.ts", "extra headers are applied")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextExtraHeadersShouldApplyToPage()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ExtraHTTPHeaders = new[] { new KeyValuePair<string, string>("X-Wave51", "headers") } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> waitTask = page.WaitForRequestAsync(r => r.Url.Contains("/empty.html", StringComparison.Ordinal));
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);

            Assert.That(
                request.Headers.Any(h =>
                    string.Equals(h.Key, "X-Wave51", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(h.Value, "headers", StringComparison.Ordinal)),
                Is.True);
        }
    }
}
