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
    /// Direct-connection tests for <c>prefers-reduced-motion</c> and
    /// <c>forced-colors</c> emulation.
    /// </summary>
    [TestFixture]
    public class PageEmulateMediaFeaturesTests : PageTestEx
    {
        [PlaywrightTest("page-emulate-media.spec.ts", "prefers-reduced-motion: reduce")]
        [Test]
        [Timeout(30_000)]
        public async Task ReducedMotionReduceMatches()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce }).ConfigureAwait(false);

            bool reduce = await page.EvaluateAsync<bool>(
                "matchMedia('(prefers-reduced-motion: reduce)').matches").ConfigureAwait(false);
            Assert.That(reduce, Is.True);
        }

        [PlaywrightTest("page-emulate-media.spec.ts", "prefers-reduced-motion: no-preference")]
        [Test]
        [Timeout(30_000)]
        public async Task ReducedMotionNoPreferenceMatches()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce }).ConfigureAwait(false);
            await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.NoPreference }).ConfigureAwait(false);

            bool reduce = await page.EvaluateAsync<bool>(
                "matchMedia('(prefers-reduced-motion: reduce)').matches").ConfigureAwait(false);
            bool noPreference = await page.EvaluateAsync<bool>(
                "matchMedia('(prefers-reduced-motion: no-preference)').matches").ConfigureAwait(false);
            Assert.That(reduce, Is.False);
            Assert.That(noPreference, Is.True);
        }

        [PlaywrightTest("page-emulate-media.spec.ts", "forced-colors: active")]
        [Test]
        [Timeout(30_000)]
        public async Task ForcedColorsActiveMatches()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("WebKit build does not expose ForcedColors via overrideUserPreference.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active }).ConfigureAwait(false);

            bool active = await page.EvaluateAsync<bool>(
                "matchMedia('(forced-colors: active)').matches").ConfigureAwait(false);
            Assert.That(active, Is.True);
        }

        [PlaywrightTest("page-emulate-media.spec.ts", "color-scheme is kept when reduced-motion is set")]
        [Test]
        [Timeout(30_000)]
        public async Task ReducedMotionDoesNotClearColorScheme()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark }).ConfigureAwait(false);
            await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce }).ConfigureAwait(false);

            bool dark = await page.EvaluateAsync<bool>(
                "matchMedia('(prefers-color-scheme: dark)').matches").ConfigureAwait(false);
            bool reduce = await page.EvaluateAsync<bool>(
                "matchMedia('(prefers-reduced-motion: reduce)').matches").ConfigureAwait(false);
            Assert.That(dark, Is.True);
            Assert.That(reduce, Is.True);
        }

        [PlaywrightTest("page-emulate-media.spec.ts", "prefers-contrast: more")]
        [Test]
        [Timeout(30_000)]
        public async Task ContrastMoreMatches()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("WebKit build does not expose PrefersContrast via overrideUserPreference.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EmulateMediaAsync(new() { Contrast = Contrast.More }).ConfigureAwait(false);

            bool more = await page.EvaluateAsync<bool>(
                "matchMedia('(prefers-contrast: more)').matches").ConfigureAwait(false);
            Assert.That(more, Is.True);
        }

        [PlaywrightTest("page-emulate-media.spec.ts", "context reducedMotion applies to new pages")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextReducedMotionAppliesToNewPages()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ReducedMotion = ReducedMotion.Reduce }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            bool reduce = await page.EvaluateAsync<bool>(
                "matchMedia('(prefers-reduced-motion: reduce)').matches").ConfigureAwait(false);
            Assert.That(reduce, Is.True);
        }

        [PlaywrightTest("page-emulate-media.spec.ts", "context forcedColors applies to new pages")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextForcedColorsAppliesToNewPages()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("WebKit build does not expose ForcedColors via overrideUserPreference.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ForcedColors = ForcedColors.Active }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            bool active = await page.EvaluateAsync<bool>(
                "matchMedia('(forced-colors: active)').matches").ConfigureAwait(false);
            Assert.That(active, Is.True);
        }

        [PlaywrightTest("page-emulate-media.spec.ts", "context contrast applies to new pages")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextContrastAppliesToNewPages()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("WebKit build does not expose PrefersContrast via overrideUserPreference.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { Contrast = Contrast.More }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            bool more = await page.EvaluateAsync<bool>(
                "matchMedia('(prefers-contrast: more)').matches").ConfigureAwait(false);
            Assert.That(more, Is.True);
        }
    }
}
