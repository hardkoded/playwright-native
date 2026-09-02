/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Browser-context chrome: Browser getter, timeouts, extra headers, init scripts.
    /// </summary>
    [TestFixture]
    public class BrowserContextChromeTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-basic.spec.ts", "should return the owning browser")]
        [Test]
        [Timeout(30_000)]
        public async Task BrowserGetterShouldReturnOwner()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            Assert.That(context.Browser, Is.SameAs(browser));
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should store default timeouts")]
        [Test]
        [Timeout(30_000)]
        public async Task DefaultTimeoutShouldRoundTrip()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            context.SetDefaultTimeout(1111);
            context.SetDefaultNavigationTimeout(2222);
            Assert.That(context.DefaultTimeout, Is.EqualTo(1111f));
            Assert.That(context.DefaultNavigationTimeout, Is.EqualTo(2222f));
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should run context init script on new pages")]
        [Test]
        [Timeout(30_000)]
        public async Task AddInitScriptAsyncShouldApplyToNewPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            await context.AddInitScriptAsync("window.__fromContext = 9;").ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            int marker = await page.EvaluateAsync<int>("window.__fromContext").ConfigureAwait(false);
            Assert.That(marker, Is.EqualTo(9));
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should add context init script from path")]
        [Test]
        [Timeout(30_000)]
        public async Task AddInitScriptAsyncShouldReadScriptPath()
        {
            string file = Path.Combine(Path.GetTempPath(), $"pwsharp-ctx-init-{Guid.NewGuid():N}.js");
            File.WriteAllText(file, "window.__fromContextPath = 13;");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

                await context.AddInitScriptAsync(scriptPath: file).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("about:blank").ConfigureAwait(false);

                int marker = await page.EvaluateAsync<int>("window.__fromContextPath").ConfigureAwait(false);
                Assert.That(marker, Is.EqualTo(13));
            }
            finally
            {
                File.Delete(file);
            }
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should set extra http headers")]
        [Test]
        [Timeout(30_000)]
        public async Task SetExtraHttpHeadersAsyncShouldNotThrow()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await context.SetExtraHttpHeadersAsync(new[] { new KeyValuePair<string, string>("X-Wave", "37") }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            Assert.That(page, Is.Not.Null);
        }
    }
}
