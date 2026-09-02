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
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Path overloads for init scripts, script/style tags, screenshots, and PDF.
    /// </summary>
    [TestFixture]
    public class PagePathTests : PageTestEx
    {
        [PlaywrightTest("page-add-init-script.spec.ts", "should add init script from path")]
        [Test]
        [Timeout(30_000)]
        public async Task AddInitScriptAsyncShouldReadScriptPath()
        {
            string file = Path.Combine(Path.GetTempPath(), $"pwsharp-init-{Guid.NewGuid():N}.js");
            File.WriteAllText(file, "window.__fromPath = 11;");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);

                await page.AddInitScriptAsync(scriptPath: file).ConfigureAwait(false);
                await page.GoToAsync("about:blank").ConfigureAwait(false);

                int marker = await page.EvaluateAsync<int>("window.__fromPath").ConfigureAwait(false);
                Assert.That(marker, Is.EqualTo(11));
            }
            finally
            {
                File.Delete(file);
            }
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "should add script tag from path")]
        [Test]
        [Timeout(30_000)]
        public async Task AddScriptTagAsyncShouldReadPath()
        {
            string file = Path.Combine(Path.GetTempPath(), $"pwsharp-script-{Guid.NewGuid():N}.js");
            File.WriteAllText(file, "window.__fromScriptFile = 22;");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);

                await page.GoToAsync("about:blank").ConfigureAwait(false);
                await page.AddScriptTagAsync(new() { Path = file }).ConfigureAwait(false);

                int marker = await page.EvaluateAsync<int>("window.__fromScriptFile").ConfigureAwait(false);
                Assert.That(marker, Is.EqualTo(22));
            }
            finally
            {
                File.Delete(file);
            }
        }

        [PlaywrightTest("page-add-style-tag.spec.ts", "should add style tag from path")]
        [Test]
        [Timeout(30_000)]
        public async Task AddStyleTagAsyncShouldReadPath()
        {
            string file = Path.Combine(Path.GetTempPath(), $"pwsharp-style-{Guid.NewGuid():N}.css");
            File.WriteAllText(file, "#d { color: rgb(0, 128, 0); }");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);

                await page.SetContentAsync("<div id=\"d\">x</div>").ConfigureAwait(false);
                await page.AddStyleTagAsync(new() { Path = file }).ConfigureAwait(false);

                string color = await page.EvaluateAsync<string>(
                    "getComputedStyle(document.getElementById('d')).color").ConfigureAwait(false);
                Assert.That(color, Is.EqualTo("rgb(0, 128, 0)"));
            }
            finally
            {
                File.Delete(file);
            }
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should write screenshot to path")]
        [Test]
        [Timeout(30_000)]
        public async Task ScreenshotAsyncShouldWritePath()
        {
            string file = Path.Combine(Path.GetTempPath(), $"pwsharp-shot-{Guid.NewGuid():N}.png");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);

                await page.SetContentAsync("<div style=\"width:40px;height:40px;background:blue\"></div>").ConfigureAwait(false);
                byte[] bytes = await page.ScreenshotAsync(new() { Path = file }).ConfigureAwait(false);

                Assert.That(File.Exists(file), Is.True);
                byte[] fromDisk = File.ReadAllBytes(file);
                Assert.That(fromDisk, Is.EqualTo(bytes));
                Assert.That(fromDisk[0], Is.EqualTo(0x89));
                Assert.That(fromDisk[1], Is.EqualTo(0x50));
                Assert.That(fromDisk[2], Is.EqualTo(0x4E));
                Assert.That(fromDisk[3], Is.EqualTo(0x47));
            }
            finally
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        [PlaywrightTest("pdf.spec.ts", "should write pdf to path")]
        [Test]
        [Timeout(30_000)]
        public async Task PdfAsyncShouldWritePath()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("PDF generation is Chromium-only.");
                return;
            }

            string file = Path.Combine(Path.GetTempPath(), $"pwsharp-pdf-{Guid.NewGuid():N}.pdf");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync(headless: true).ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);

                await page.SetContentAsync("<h1>path pdf</h1>").ConfigureAwait(false);
                byte[] bytes = await page.PdfAsync(new() { Path = file }).ConfigureAwait(false);

                Assert.That(File.Exists(file), Is.True);
                byte[] fromDisk = File.ReadAllBytes(file);
                Assert.That(fromDisk, Is.EqualTo(bytes));
                Assert.That(Encoding.ASCII.GetString(fromDisk, 0, 5), Is.EqualTo("%PDF-"));
            }
            finally
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }
    }
}
