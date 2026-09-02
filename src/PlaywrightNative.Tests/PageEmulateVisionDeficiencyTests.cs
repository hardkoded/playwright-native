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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page.emulateVisionDeficiency</c> on Chromium.
    /// </summary>
    [TestFixture]
    public class PageEmulateVisionDeficiencyTests : PageTestEx
    {
        [PlaywrightTest("page-emulate-media.spec.ts", "EmulateVisionDeficiency changes the screenshot")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldChangeTheScreenshot()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("EmulateVisionDeficiencyAsync is Chromium-only.");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(120, 80).ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"width:100px;height:60px;background:#c00\"></div>").ConfigureAwait(false);

            byte[] before = await page.ScreenshotAsync().ConfigureAwait(false);
            await page.EmulateVisionDeficiencyAsync(VisionDeficiency.Achromatopsia).ConfigureAwait(false);
            byte[] after = await page.ScreenshotAsync().ConfigureAwait(false);
            await page.EmulateVisionDeficiencyAsync(VisionDeficiency.None).ConfigureAwait(false);

            Assert.That(before, Is.Not.Null);
            Assert.That(after, Is.Not.Null);
            Assert.That(after, Is.Not.EqualTo(before));
        }
    }
}
