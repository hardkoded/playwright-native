/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Integration tests: page AddScriptTag / AddStyleTag /
    /// EmulateMedia delegations to CRPage. Direct-CDP only (no driver).
    /// </summary>
    [TestFixture]
    public class PageScriptStyleMediaTests : PageTestEx
    {
        [PlaywrightTest("page-add-script-tag.spec.ts", "AddScriptTagAsyncExecutesInlineContent")]
        [Test]
        [Timeout(30_000)]
        public async Task AddScriptTagAsyncExecutesInlineContent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.AddScriptTagAsync(new() { Content = "window.__marker = 42;" }).ConfigureAwait(false);

            int marker = await page.EvaluateAsync<int>("window.__marker").ConfigureAwait(false);
            Assert.That(marker, Is.EqualTo(42));
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "AddScriptTagAsyncLoadsUrl")]
        [Test]
        [Timeout(30_000)]
        public async Task AddScriptTagAsyncLoadsUrl()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);

            // data:application/javascript URLs are loaded by Chromium the same way as
            // any external script — exercises the url branch of AddScriptTagAsync
            // without requiring a local test HTTP server.
            string src = "data:application/javascript," + Uri.EscapeDataString("window.__fromUrl = 'yes';");
            await page.AddScriptTagAsync(new() { Url = src }).ConfigureAwait(false);

            string value = await page.EvaluateAsync<string>("window.__fromUrl").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("yes"));
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "AddStyleTagAsyncAppliesInlineCss")]
        [Test]
        [Timeout(30_000)]
        public async Task AddStyleTagAsyncAppliesInlineCss()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=\"d\">x</div>").ConfigureAwait(false);
            await page.AddStyleTagAsync(new() { Content = "#d { color: rgb(255, 0, 0); }" }).ConfigureAwait(false);

            string color = await page.EvaluateAsync<string>(
                "getComputedStyle(document.getElementById('d')).color").ConfigureAwait(false);
            Assert.That(color, Is.EqualTo("rgb(255, 0, 0)"));
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "EmulateMediaDarkMatches")]
        [Test]
        [Timeout(30_000)]
        public async Task EmulateMediaDarkMatches()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark }).ConfigureAwait(false);

            bool dark = await page.EvaluateAsync<bool>(
                "matchMedia('(prefers-color-scheme: dark)').matches").ConfigureAwait(false);
            Assert.That(dark, Is.True);
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "EmulateMediaLightMatches")]
        [Test]
        [Timeout(30_000)]
        public async Task EmulateMediaLightMatches()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Light }).ConfigureAwait(false);

            bool light = await page.EvaluateAsync<bool>(
                "matchMedia('(prefers-color-scheme: light)').matches").ConfigureAwait(false);
            Assert.That(light, Is.True);
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "EmulateMediaPrintMatches")]
        [Test]
        [Timeout(30_000)]
        public async Task EmulateMediaPrintMatches()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EmulateMediaAsync(new() { Media = Media.Print }).ConfigureAwait(false);

            bool print = await page.EvaluateAsync<bool>("matchMedia('print').matches").ConfigureAwait(false);
            Assert.That(print, Is.True);
        }

    }
}
