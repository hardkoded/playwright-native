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
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Integration tests: page content, screenshot, PDF, viewport,
    /// and init-script delegations to CRPage. Direct-CDP only (no driver).
    /// </summary>
    [TestFixture]
    public class PageContentAndMediaTests : PageTestEx
    {
        [PlaywrightTest("page-set-content.spec.ts", "SetContentAsyncShouldRoundTripHtml")]
        [Test]
        [Timeout(30_000)]
        public async Task SetContentAsyncShouldRoundTripHtml()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"marker\">hello</div>").ConfigureAwait(false);

            string text = await page.EvaluateAsync<string>("document.getElementById('marker').textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("hello"));
        }

        [PlaywrightTest("page-set-content.spec.ts", "SetContentAsync honors timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task SetContentAsyncShouldHonorTimeout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=\"marker\">timeout</div>", new() { Timeout = 5000 }).ConfigureAwait(false);
            string text = await page.EvaluateAsync<string>("document.getElementById('marker').textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("timeout"));
        }

        [PlaywrightTest("page-set-content.spec.ts", "SetContentAsync honors waitUntil")]
        [Test]
        [Timeout(30_000)]
        public async Task SetContentAsyncShouldHonorWaitUntil()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=\"marker\">ready</div>", new() { WaitUntil = WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);
            string text = await page.EvaluateAsync<string>("document.getElementById('marker').textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("ready"));
            string state = await page.EvaluateAsync<string>("document.readyState").ConfigureAwait(false);
            Assert.That(state, Is.EqualTo("interactive").Or.EqualTo("complete"));
        }

        [PlaywrightTest("page-set-content.spec.ts", "ContentAsyncShouldReturnDocTypeAndDocument")]
        [Test]
        [Timeout(30_000)]
        public async Task ContentAsyncShouldReturnDocTypeAndDocument()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<!DOCTYPE html><html><body><span>abc</span></body></html>").ConfigureAwait(false);
            string content = await page.ContentAsync().ConfigureAwait(false);

            Assert.That(content, Does.Contain("<!DOCTYPE html>"));
            Assert.That(content, Does.Contain("<span>abc</span>"));
        }

        [PlaywrightTest("page-set-content.spec.ts", "ScreenshotAsyncShouldReturnPngBytes")]
        [Test]
        [Timeout(30_000)]
        public async Task ScreenshotAsyncShouldReturnPngBytes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div style=\"width:100px;height:100px;background:red\"></div>").ConfigureAwait(false);
            byte[] bytes = await page.ScreenshotAsync().ConfigureAwait(false);

            Assert.That(bytes, Is.Not.Null);
            Assert.That(bytes.Length, Is.GreaterThan(8));
            // PNG magic: 89 50 4E 47 0D 0A 1A 0A
            Assert.That(bytes[0], Is.EqualTo(0x89));
            Assert.That(bytes[1], Is.EqualTo(0x50));
            Assert.That(bytes[2], Is.EqualTo(0x4E));
            Assert.That(bytes[3], Is.EqualTo(0x47));
            Assert.That(bytes[4], Is.EqualTo(0x0D));
            Assert.That(bytes[5], Is.EqualTo(0x0A));
            Assert.That(bytes[6], Is.EqualTo(0x1A));
            Assert.That(bytes[7], Is.EqualTo(0x0A));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "ScreenshotAsync honors timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ScreenshotAsyncShouldHonorTimeout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div style=\"width:80px;height:40px;background:blue\"></div>").ConfigureAwait(false);
            byte[] bytes = await page.ScreenshotAsync(new() { Timeout = 5000 }).ConfigureAwait(false);

            Assert.That(bytes, Is.Not.Null);
            Assert.That(bytes.Length, Is.GreaterThan(8));
            Assert.That(bytes[0], Is.EqualTo(0x89));
            Assert.That(bytes[1], Is.EqualTo(0x50));
            Assert.That(bytes[2], Is.EqualTo(0x4E));
            Assert.That(bytes[3], Is.EqualTo(0x47));
        }

        [PlaywrightTest("page-set-content.spec.ts", "ScreenshotAsyncOmitBackgroundShouldReturnPng")]
        [Test]
        [Timeout(30_000)]
        public async Task ScreenshotAsyncOmitBackgroundShouldReturnPng()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<html><body></body></html>").ConfigureAwait(false);
            byte[] bytes = await page.ScreenshotAsync(new() { OmitBackground = true }).ConfigureAwait(false);

            Assert.That(bytes, Is.Not.Null);
            Assert.That(bytes.Length, Is.GreaterThan(8));
            Assert.That(bytes[0], Is.EqualTo(0x89));
            Assert.That(bytes[1], Is.EqualTo(0x50));
            Assert.That(bytes[2], Is.EqualTo(0x4E));
            Assert.That(bytes[3], Is.EqualTo(0x47));
        }

        [PlaywrightTest("page-set-content.spec.ts", "ScreenshotAsyncWithFullPageShouldReturnLargerBytes")]
        [Test]
        [Timeout(30_000)]
        public async Task ScreenshotAsyncWithFullPageShouldReturnLargerBytes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetViewportSizeAsync(400, 400).ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"height:3000px;background:linear-gradient(red,blue)\"></div>").ConfigureAwait(false);

            byte[] viewportBytes = await page.ScreenshotAsync(new() { FullPage = false }).ConfigureAwait(false);
            byte[] fullPageBytes = await page.ScreenshotAsync(new() { FullPage = true }).ConfigureAwait(false);

            Assert.That(viewportBytes.Length, Is.GreaterThan(0));
            Assert.That(fullPageBytes.Length, Is.GreaterThan(viewportBytes.Length));
        }

        [PlaywrightTest("page-set-content.spec.ts", "ScreenshotAsyncClipShouldReturnPng")]
        [Test]
        [Timeout(30_000)]
        public async Task ScreenshotAsyncClipShouldReturnPng()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div style=\"width:400px;height:400px;background:green\"></div>").ConfigureAwait(false);
            byte[] bytes = await page.ScreenshotAsync(new() { Clip = new Clip { X = 10, Y = 10, Width = 50, Height = 50 } }).ConfigureAwait(false);

            Assert.That(bytes, Is.Not.Null);
            Assert.That(bytes.Length, Is.GreaterThan(8));
            Assert.That(bytes[0], Is.EqualTo(0x89));
            Assert.That(bytes[1], Is.EqualTo(0x50));
            Assert.That(bytes[2], Is.EqualTo(0x4E));
            Assert.That(bytes[3], Is.EqualTo(0x47));
        }

        [PlaywrightTest("page-set-content.spec.ts", "PdfAsyncShouldReturnPdfBytes")]
        [Test]
        [Timeout(30_000)]
        public async Task PdfAsyncShouldReturnPdfBytes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync(headless: true).ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<h1>PDF Test</h1>").ConfigureAwait(false);
            byte[] bytes = await page.PdfAsync().ConfigureAwait(false);

            Assert.That(bytes, Is.Not.Null);
            Assert.That(bytes.Length, Is.GreaterThan(5));
            // PDF magic: %PDF-
            string prefix = Encoding.ASCII.GetString(bytes, 0, 5);
            Assert.That(prefix, Is.EqualTo("%PDF-"));
        }

        [PlaywrightTest("page-set-content.spec.ts", "SetViewportSizeAsyncShouldChangeInnerDimensions")]
        [Test]
        [Timeout(30_000)]
        public async Task SetViewportSizeAsyncShouldChangeInnerDimensions()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.SetViewportSizeAsync(640, 480).ConfigureAwait(false);

            int width = await page.EvaluateAsync<int>("window.innerWidth").ConfigureAwait(false);
            int height = await page.EvaluateAsync<int>("window.innerHeight").ConfigureAwait(false);
            Assert.That(width, Is.EqualTo(640));
            Assert.That(height, Is.EqualTo(480));
        }

        [PlaywrightTest("page-set-content.spec.ts", "AddInitScriptAsyncShouldFireOnNextNavigation")]
        [Test]
        [Timeout(30_000)]
        public async Task AddInitScriptAsyncShouldFireOnNextNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.AddInitScriptAsync("window.__phase6_2e_marker = 42;").ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            int marker = await page.EvaluateAsync<int>("window.__phase6_2e_marker").ConfigureAwait(false);
            Assert.That(marker, Is.EqualTo(42));
        }

        [PlaywrightTest("page-set-content.spec.ts", "AddInitScriptAsync passes arg")]
        [Test]
        [Timeout(30_000)]
        public async Task AddInitScriptAsyncShouldPassArg()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.AddInitScriptAsync("x => { window.__wave170 = x; }", new LocatorWaitForFunctionOptions { Arg = 170 }).ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            int marker = await page.EvaluateAsync<int>("window.__wave170").ConfigureAwait(false);
            Assert.That(marker, Is.EqualTo(170));
        }
    }
}
