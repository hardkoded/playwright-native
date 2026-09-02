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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Expect page ToHaveScreenshot against golden bytes or a file.
    /// </summary>
    [TestFixture]
    public class ExpectPageScreenshotTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToHaveScreenshot matches captured bytes")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveScreenshotShouldMatchCapturedBytes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(200, 120).ConfigureAwait(false);
            await page.SetContentAsync("<style>html,body{margin:0;background:#0a0;height:100%}</style>").ConfigureAwait(false);

            byte[] expected = await page.ScreenshotAsync().ConfigureAwait(false);
            await Assertions.Expect(page).ToHaveScreenshotAsync(expected).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveScreenshot matches a golden path")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveScreenshotShouldMatchAGoldenPath()
        {
            string file = Path.Combine(Path.GetTempPath(), $"pwsharp-expect-page-shot-{Guid.NewGuid():N}.png");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.SetViewportSizeAsync(200, 120).ConfigureAwait(false);
                await page.SetContentAsync("<style>html,body{margin:0;background:#00c;height:100%}</style>").ConfigureAwait(false);

                await page.ScreenshotAsync(new() { Path = file }).ConfigureAwait(false);
                await Assertions.Expect(page).ToHaveScreenshotAsync(file).ConfigureAwait(false);
            }
            finally
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveScreenshot waits until pixels match")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveScreenshotShouldWaitUntilPixelsMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(200, 120).ConfigureAwait(false);
            await page.SetContentAsync("<style>html,body{margin:0;background:#0a0;height:100%}</style>").ConfigureAwait(false);

            byte[] expected = await page.ScreenshotAsync().ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.body.style.background = '#c00'").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page).ToHaveScreenshotAsync(expected, timeout: 5000);
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.body.style.background = '#0a0'").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveScreenshot times out on a mismatch")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveScreenshotShouldTimeoutOnAMismatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(200, 120).ConfigureAwait(false);
            await page.SetContentAsync("<style>html,body{margin:0;background:#0a0;height:100%}</style>").ConfigureAwait(false);

            byte[] expected = await page.ScreenshotAsync().ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.body.style.background = '#c00'").ConfigureAwait(false);

            TimeoutException ex = Assert.CatchAsync<TimeoutException>(
                () => Assertions.Expect(page).ToHaveScreenshotAsync(expected, timeout: 400));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("expect.toHaveScreenshot"));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("expect-misc.spec.ts", "Not ToHaveScreenshot matches a different image")]
        [Test]
        [Timeout(30_000)]
        public async Task NotToHaveScreenshotShouldMatchADifferentImage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(200, 120).ConfigureAwait(false);
            await page.SetContentAsync("<style>html,body{margin:0;background:#0a0;height:100%}</style>").ConfigureAwait(false);

            byte[] green = await page.ScreenshotAsync().ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.body.style.background = '#c00'").ConfigureAwait(false);
            await Assertions.Expect(page).Not.ToHaveScreenshotAsync(green).ConfigureAwait(false);
        }
    }
}
