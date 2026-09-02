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
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// SetInputFiles, Screenshot, and DispatchEvent on <see cref="ILocator"/>.
    /// </summary>
    [TestFixture]
    public class LocatorFilesScreenshotTests : PageTestEx
    {
        [PlaywrightTest("page-set-input-files.spec.ts", "SetInputFiles sets a payload")]
        [Test]
        [Timeout(30_000)]
        public async Task SetInputFilesShouldSetAPayload()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"f\" type=\"file\" />").ConfigureAwait(false);

            await page.Locator("#f").SetInputFilesAsync(new FilePayload
            {
                Name = "wave.txt",
                MimeType = "text/plain",
                Buffer = Encoding.UTF8.GetBytes("hello"),
            }).ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<string>("document.querySelector('#f').files[0].name").ConfigureAwait(false), Is.EqualTo("wave.txt"));
            Assert.That(await page.EvaluateAsync<int>("document.querySelector('#f').files[0].size").ConfigureAwait(false), Is.EqualTo(5));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "Screenshot clips to the element")]
        [Test]
        [Timeout(30_000)]
        public async Task ScreenshotShouldClipToTheElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(400, 300).ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"width:80px;height:50px;background:#0a0\"></div>").ConfigureAwait(false);

            byte[] bytes = await page.Locator("div").ScreenshotAsync().ConfigureAwait(false);
            Assert.That(bytes, Is.Not.Null);
            Assert.That(bytes.Length, Is.GreaterThan(20));
            Assert.That(bytes[0], Is.EqualTo(0x89));
            Assert.That(bytes[1], Is.EqualTo(0x50));

            using Image<Rgba32> image = Image.Load<Rgba32>(bytes);
            Assert.That(image.Width, Is.LessThan(400));
            Assert.That(image.Height, Is.LessThan(300));
            Assert.That(image.Width, Is.GreaterThan(0));
            Assert.That(image.Height, Is.GreaterThan(0));
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "DispatchEvent fires click")]
        [Test]
        [Timeout(30_000)]
        public async Task DispatchEventShouldFireClick()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"b\" onclick=\"window.clicked=true\">Go</button>").ConfigureAwait(false);

            await page.Locator("#b").DispatchEventAsync("click").ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<bool>("window.clicked === true").ConfigureAwait(false), Is.True);
        }
    }
}
