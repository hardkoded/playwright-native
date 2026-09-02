/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// NewContext timezone, locale, offline, and color-scheme applied to pages.
    /// </summary>
    [TestFixture]
    public class ContextEnvironmentTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-locale.spec.ts", "timezone is applied")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextTimezoneShouldApplyToPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { TimezoneId = "Europe/Paris" }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            string timeZone = await page.EvaluateAsync<string>(
                "Intl.DateTimeFormat().resolvedOptions().timeZone").ConfigureAwait(false);
            Assert.That(timeZone, Is.EqualTo("Europe/Paris"));
        }

        [PlaywrightTest("browsercontext-locale.spec.ts", "locale is applied")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextLocaleShouldApplyToPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { Locale = "de-DE" }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            if (TestServerSetup.Server != null)
            {
                Task<IRequest> waitTask = page.WaitForRequestAsync(r => r.Url.Contains("/empty.html", StringComparison.Ordinal));
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                IRequest request = await waitTask.ConfigureAwait(false);
                Assert.That(
                    request.Headers.Any(h =>
                        string.Equals(h.Key, "Accept-Language", StringComparison.OrdinalIgnoreCase) &&
                        h.Value.Contains("de-DE", StringComparison.OrdinalIgnoreCase)),
                    Is.True);
            }
            else if (TestConstants.IsWebKit)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string language = await page.EvaluateAsync<string>("navigator.language").ConfigureAwait(false);
            if (TestConstants.IsChromium)
            {
                Assert.That(language, Does.StartWith("de"));
            }
        }

        [PlaywrightTest("browsercontext-locale.spec.ts", "offline is applied")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextOfflineShouldApplyToPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { Offline = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<bool>("navigator.onLine").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("browsercontext-locale.spec.ts", "color scheme is applied")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextColorSchemeShouldApplyToPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ColorScheme = ColorScheme.Dark }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(
                await page.EvaluateAsync<bool>("matchMedia('(prefers-color-scheme: dark)').matches").ConfigureAwait(false),
                Is.True);
        }

        [PlaywrightTest("browsercontext-locale.spec.ts", "options bag timezone and color scheme")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextOptionsBagShouldApplyTimezoneAndColorScheme()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                TimezoneId = "America/New_York",
                ColorScheme = ColorScheme.Light,
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            string timeZone = await page.EvaluateAsync<string>(
                "Intl.DateTimeFormat().resolvedOptions().timeZone").ConfigureAwait(false);
            Assert.That(timeZone, Is.EqualTo("America/New_York"));
            Assert.That(
                await page.EvaluateAsync<bool>("matchMedia('(prefers-color-scheme: light)').matches").ConfigureAwait(false),
                Is.True);
        }
    }
}
