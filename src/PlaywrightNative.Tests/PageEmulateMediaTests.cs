/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-emulate-media.spec.ts</c>.
    /// Skipped: <c>should throw in case of bad media argument</c>,
    /// <c>should throw in case of bad colorScheme argument</c>
    /// (C# EmulateMediaAsync is typed).
    /// </summary>
    [TestFixture]
    public class PageEmulateMediaTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 18717;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    EmptyPage = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture) + "/empty.html";
                    return;
                }
                catch (Exception)
                {
                }
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

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static Task<bool> MatchesMediaAsync(IPage page, string query)
            => page.EvaluateAsync<bool>("matchMedia('" + query + "').matches");

        [PlaywrightTest("page-emulate-media.spec.ts", "should emulate type")]
        [PlaywrightTest("page-emulate-media.spec.ts", "should emulate type @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEmulateType()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(await MatchesMediaAsync(page, "screen").ConfigureAwait(false), Is.True);
            Assert.That(await MatchesMediaAsync(page, "print").ConfigureAwait(false), Is.False);

            await page.EmulateMediaAsync(new() { Media = Media.Print }).ConfigureAwait(false);
            Assert.That(await MatchesMediaAsync(page, "screen").ConfigureAwait(false), Is.False);
            Assert.That(await MatchesMediaAsync(page, "print").ConfigureAwait(false), Is.True);

            await page.EmulateMediaAsync(new() { Media = default, ColorScheme = default }).ConfigureAwait(false);
            Assert.That(await MatchesMediaAsync(page, "screen").ConfigureAwait(false), Is.False);
            Assert.That(await MatchesMediaAsync(page, "print").ConfigureAwait(false), Is.True);

            await page.EmulateMediaAsync(new() { Media = Media.Null }).ConfigureAwait(false);
            Assert.That(await MatchesMediaAsync(page, "screen").ConfigureAwait(false), Is.True);
            Assert.That(await MatchesMediaAsync(page, "print").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("page-emulate-media.spec.ts", "should throw in case of bad media argument")]
        [Test]
        [Timeout(30_000)]
        public void ShouldThrowInCaseOfBadMediaArgument()
        {
            Assert.Ignore("C# EmulateMediaAsync is typed");
        }

        [PlaywrightTest("page-emulate-media.spec.ts", "should emulate colorScheme should work")]
        [PlaywrightTest("page-emulate-media.spec.ts", "should emulate colorScheme should work @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEmulateColorSchemeShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Light }).ConfigureAwait(false);
            Assert.That(await MatchesMediaAsync(page, "(prefers-color-scheme: light)").ConfigureAwait(false), Is.True);
            Assert.That(await MatchesMediaAsync(page, "(prefers-color-scheme: dark)").ConfigureAwait(false), Is.False);

            await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark }).ConfigureAwait(false);
            Assert.That(await MatchesMediaAsync(page, "(prefers-color-scheme: dark)").ConfigureAwait(false), Is.True);
            Assert.That(await MatchesMediaAsync(page, "(prefers-color-scheme: light)").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("page-emulate-media.spec.ts", "should default to light")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDefaultToLight()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(await MatchesMediaAsync(page, "(prefers-color-scheme: light)").ConfigureAwait(false), Is.True);
            Assert.That(await MatchesMediaAsync(page, "(prefers-color-scheme: dark)").ConfigureAwait(false), Is.False);

            await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark }).ConfigureAwait(false);
            Assert.That(await MatchesMediaAsync(page, "(prefers-color-scheme: dark)").ConfigureAwait(false), Is.True);
            Assert.That(await MatchesMediaAsync(page, "(prefers-color-scheme: light)").ConfigureAwait(false), Is.False);

            await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Null }).ConfigureAwait(false);
            Assert.That(await MatchesMediaAsync(page, "(prefers-color-scheme: dark)").ConfigureAwait(false), Is.False);
            Assert.That(await MatchesMediaAsync(page, "(prefers-color-scheme: light)").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-emulate-media.spec.ts", "should throw in case of bad colorScheme argument")]
        [Test]
        [Timeout(30_000)]
        public void ShouldThrowInCaseOfBadColorSchemeArgument()
        {
            Assert.Ignore("C# EmulateMediaAsync is typed");
        }

        [PlaywrightTest("page-emulate-media.spec.ts", "should work during navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkDuringNavigation()
        {
            EnsureServer();
            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Light }).ConfigureAwait(false);
            Task<IResponse> navigated = page.GoToAsync(EmptyPage);
            for (int i = 0; i < 9; i++)
            {
                ColorScheme scheme = (i & 1) == 0 ? ColorScheme.Dark : ColorScheme.Light;
                await Task.WhenAll(
                    page.EmulateMediaAsync(scheme),
                    Task.Delay(1)).ConfigureAwait(false);
            }

            await navigated.ConfigureAwait(false);
            Assert.That(await MatchesMediaAsync(page, "(prefers-color-scheme: dark)").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-emulate-media.spec.ts", "should change the actual colors in css")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldChangeTheActualColorsInCss()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <style>
      @media (prefers-color-scheme: dark) {
        div {
          background: black;
          color: white;
        }
      }
      @media (prefers-color-scheme: light) {
        div {
          background: white;
          color: black;
        }
      }

    </style>
    <div>Hello</div>
  ").ConfigureAwait(false);

            await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Light }).ConfigureAwait(false);
            string lightBackground = await page.EvalOnSelectorAsync<string>(
                "div",
                "div => window.getComputedStyle(div).backgroundColor").ConfigureAwait(false);
            Assert.That(lightBackground, Is.EqualTo("rgb(255, 255, 255)"));

            await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark }).ConfigureAwait(false);
            string darkBackground = await page.EvalOnSelectorAsync<string>(
                "div",
                "div => window.getComputedStyle(div).backgroundColor").ConfigureAwait(false);
            Assert.That(darkBackground, Is.EqualTo("rgb(0, 0, 0)"));

            await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Light }).ConfigureAwait(false);
            string lightAgain = await page.EvalOnSelectorAsync<string>(
                "div",
                "div => window.getComputedStyle(div).backgroundColor").ConfigureAwait(false);
            Assert.That(lightAgain, Is.EqualTo("rgb(255, 255, 255)"));
        }

        [PlaywrightTest("page-emulate-media.spec.ts", "should emulate reduced motion")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEmulateReducedMotion()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(await MatchesMediaAsync(page, "(prefers-reduced-motion: no-preference)").ConfigureAwait(false), Is.True);

            await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce }).ConfigureAwait(false);
            Assert.That(await MatchesMediaAsync(page, "(prefers-reduced-motion: reduce)").ConfigureAwait(false), Is.True);
            Assert.That(await MatchesMediaAsync(page, "(prefers-reduced-motion: no-preference)").ConfigureAwait(false), Is.False);

            await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.NoPreference }).ConfigureAwait(false);
            Assert.That(await MatchesMediaAsync(page, "(prefers-reduced-motion: reduce)").ConfigureAwait(false), Is.False);
            Assert.That(await MatchesMediaAsync(page, "(prefers-reduced-motion: no-preference)").ConfigureAwait(false), Is.True);

            await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Null }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-emulate-media.spec.ts", "should keep reduced motion and color emulation after reload")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldKeepReducedMotionAndColorEmulationAfterReload()
        {
            EnsureServer();
            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(await MatchesMediaAsync(page, "(prefers-reduced-motion: reduce)").ConfigureAwait(false), Is.False);
            Assert.That(await MatchesMediaAsync(page, "(forced-colors: active)").ConfigureAwait(false), Is.False);

            await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce, ForcedColors = ForcedColors.Active }).ConfigureAwait(false);
            Assert.That(await MatchesMediaAsync(page, "(prefers-reduced-motion: reduce)").ConfigureAwait(false), Is.True);
            Assert.That(await MatchesMediaAsync(page, "(forced-colors: active)").ConfigureAwait(false), Is.True);

            Server.SetRoute("/empty.html", async http =>
            {
                http.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync(@"
      <div>Hello there!</div>
      <script>window.onload = () => console.log('onload')</script>
    ").ConfigureAwait(false);
            });

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            Assert.That(await MatchesMediaAsync(page, "(prefers-reduced-motion: reduce)").ConfigureAwait(false), Is.True);
            Assert.That(await MatchesMediaAsync(page, "(forced-colors: active)").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-emulate-media.spec.ts", "should emulate forcedColors ")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEmulateForcedColors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(await MatchesMediaAsync(page, "(forced-colors: none)").ConfigureAwait(false), Is.True);

            await page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.None }).ConfigureAwait(false);
            Assert.That(await MatchesMediaAsync(page, "(forced-colors: none)").ConfigureAwait(false), Is.True);
            Assert.That(await MatchesMediaAsync(page, "(forced-colors: active)").ConfigureAwait(false), Is.False);

            await page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active }).ConfigureAwait(false);
            Assert.That(await MatchesMediaAsync(page, "(forced-colors: none)").ConfigureAwait(false), Is.False);
            Assert.That(await MatchesMediaAsync(page, "(forced-colors: active)").ConfigureAwait(false), Is.True);

            await page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Null }).ConfigureAwait(false);
            Assert.That(await MatchesMediaAsync(page, "(forced-colors: none)").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-emulate-media.spec.ts", "should emulate contrast ")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEmulateContrast()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(await MatchesMediaAsync(page, "(prefers-contrast: no-preference)").ConfigureAwait(false), Is.True);

            await page.EmulateMediaAsync(new() { Contrast = Contrast.NoPreference }).ConfigureAwait(false);
            Assert.That(await MatchesMediaAsync(page, "(prefers-contrast: no-preference)").ConfigureAwait(false), Is.True);
            Assert.That(await MatchesMediaAsync(page, "(prefers-contrast: more)").ConfigureAwait(false), Is.False);

            await page.EmulateMediaAsync(new() { Contrast = Contrast.More }).ConfigureAwait(false);
            Assert.That(await MatchesMediaAsync(page, "(prefers-contrast: no-preference)").ConfigureAwait(false), Is.False);
            Assert.That(await MatchesMediaAsync(page, "(prefers-contrast: more)").ConfigureAwait(false), Is.True);

            await page.EmulateMediaAsync(new() { Contrast = Contrast.Null }).ConfigureAwait(false);
            Assert.That(await MatchesMediaAsync(page, "(prefers-contrast: no-preference)").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-emulate-media.spec.ts", "should report hover and fine pointer for desktop")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportHoverAndFinePointerForDesktop()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(await MatchesMediaAsync(page, "(hover: hover)").ConfigureAwait(false), Is.True);
            Assert.That(await MatchesMediaAsync(page, "(pointer: fine)").ConfigureAwait(false), Is.True);
        }
    }
}
