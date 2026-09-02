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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Expect ToHaveScreenshot maxDiffPixels / maxDiffPixelRatio / threshold.
    /// </summary>
    [TestFixture]
    public class ExpectScreenshotDiffTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "maxDiffPixels allows a small mismatch")]
        [Test]
        [Timeout(30_000)]
        public async Task MaxDiffPixelsShouldAllowASmallMismatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(200, 120).ConfigureAwait(false);
            await page.SetContentAsync(
                "<style>html,body{margin:0}</style>" +
                "<div id=\"box\" style=\"width:80px;height:50px;background:#0a0;position:relative\">" +
                "<div id=\"dot\" style=\"position:absolute;left:0;top:0;width:2px;height:2px;background:#0a0\"></div>" +
                "</div>").ConfigureAwait(false);

            byte[] expected = await page.Locator("#box").ScreenshotAsync().ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#dot').style.background = '#c00'").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#box"))
                .ToHaveScreenshotAsync(expected, maxDiffPixels: 20)
                .ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "maxDiffPixels times out when the mismatch is larger")]
        [Test]
        [Timeout(30_000)]
        public async Task MaxDiffPixelsShouldTimeoutWhenTheMismatchIsLarger()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(200, 120).ConfigureAwait(false);
            await page.SetContentAsync(
                "<style>html,body{margin:0}</style>" +
                "<div id=\"box\" style=\"width:80px;height:50px;background:#0a0;position:relative\">" +
                "<div id=\"dot\" style=\"position:absolute;left:0;top:0;width:2px;height:2px;background:#0a0\"></div>" +
                "</div>").ConfigureAwait(false);

            byte[] expected = await page.Locator("#box").ScreenshotAsync().ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#dot').style.background = '#c00'").ConfigureAwait(false);

            TimeoutException ex = Assert.CatchAsync<TimeoutException>(
                () => Assertions.Expect(page.Locator("#box"))
                    .ToHaveScreenshotAsync(expected, maxDiffPixels: 1, timeout: 400));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("expect.toHaveScreenshot"));
        }

        [PlaywrightTest("expect-misc.spec.ts", "maxDiffPixelRatio allows a small mismatch")]
        [Test]
        [Timeout(30_000)]
        public async Task MaxDiffPixelRatioShouldAllowASmallMismatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(200, 120).ConfigureAwait(false);
            await page.SetContentAsync(
                "<style>html,body{margin:0}</style>" +
                "<div id=\"box\" style=\"width:80px;height:50px;background:#0a0;position:relative\">" +
                "<div id=\"dot\" style=\"position:absolute;left:0;top:0;width:2px;height:2px;background:#0a0\"></div>" +
                "</div>").ConfigureAwait(false);

            byte[] expected = await page.Locator("#box").ScreenshotAsync().ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#dot').style.background = '#c00'").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#box"))
                .ToHaveScreenshotAsync(expected, maxDiffPixelRatio: 0.05f)
                .ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "threshold treats similar colors as equal")]
        [Test]
        [Timeout(30_000)]
        public async Task ThresholdShouldTreatSimilarColorsAsEqual()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(200, 120).ConfigureAwait(false);
            await page.SetContentAsync(
                "<style>html,body{margin:0}</style>" +
                "<div id=\"box\" style=\"width:80px;height:50px;background:#0a0\"></div>").ConfigureAwait(false);

            byte[] expected = await page.Locator("#box").ScreenshotAsync().ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#box').style.background = '#0b0'").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#box"))
                .ToHaveScreenshotAsync(expected, threshold: 0.2f)
                .ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "page maxDiffPixels allows a small mismatch")]
        [Test]
        [Timeout(30_000)]
        public async Task PageMaxDiffPixelsShouldAllowASmallMismatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(80, 50).ConfigureAwait(false);
            await page.SetContentAsync(
                "<style>html,body{margin:0;background:#0a0;height:100%}</style>" +
                "<div id=\"dot\" style=\"position:absolute;left:0;top:0;width:2px;height:2px;background:#0a0\"></div>").ConfigureAwait(false);

            byte[] expected = await page.ScreenshotAsync().ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#dot').style.background = '#c00'").ConfigureAwait(false);

            await Assertions.Expect(page)
                .ToHaveScreenshotAsync(expected, maxDiffPixels: 20)
                .ConfigureAwait(false);
        }
    }
}
