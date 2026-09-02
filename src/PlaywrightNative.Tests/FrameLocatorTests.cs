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
    /// Official <see cref="IFrameLocator"/> on <see cref="IPage"/> / <see cref="IFrame"/>.
    /// </summary>
    [TestFixture]
    public class FrameLocatorTests : PageTestEx
    {
        [PlaywrightTest("locator-frame.spec.ts", "FrameLocator clicks inside an iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameLocatorShouldClickInsideIframe()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);
            await child.SetContentAsync("<button id=\"inner\">Go</button>").ConfigureAwait(false);

            await page.FrameLocator("iframe").Locator("button").ClickAsync().ConfigureAwait(false);

            string id = await child.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);
            Assert.That(id, Is.EqualTo("inner"));
            Assert.That(page.FrameLocator("iframe").Owner.Frame, Is.SameAs(page.MainFrame));
        }

        [PlaywrightTest("locator-frame.spec.ts", "First narrows two iframes")]
        [Test]
        [Timeout(30_000)]
        public async Task FirstShouldNarrowTwoIframes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.SetContentAsync(
                "<iframe id=\"a\" srcdoc=\"<button id='x'>A</button>\"></iframe>" +
                "<iframe id=\"b\" srcdoc=\"<button id='y'>B</button>\"></iframe>").ConfigureAwait(false);

            await page.FrameLocator("iframe").First.Locator("button").ClickAsync().ConfigureAwait(false);
            string firstId = await page.FrameLocator("iframe").First.Locator("button").GetAttributeAsync("id").ConfigureAwait(false);
            string secondText = await page.FrameLocator("iframe").Nth(1).Locator("button").TextContentAsync().ConfigureAwait(false);
            Assert.That(firstId, Is.EqualTo("x"));
            Assert.That(secondText, Is.EqualTo("B"));
        }

        [PlaywrightTest("locator-frame.spec.ts", "FrameLocator is strict")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameLocatorShouldThrowWhenTwoIframesMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<iframe srcdoc=\"<button>A</button>\"></iframe>" +
                "<iframe srcdoc=\"<button>B</button>\"></iframe>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.FrameLocator("iframe").Locator("button").ClickAsync());

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
            Assert.That(ex.Message, Does.Contain("frame locator"));
        }

        [PlaywrightTest("locator-frame.spec.ts", "ContentFrame enters an iframe locator")]
        [Test]
        [Timeout(30_000)]
        public async Task ContentFrameShouldEnterIframeLocator()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);
            await child.SetContentAsync("<button id=\"inner\">Go</button>").ConfigureAwait(false);

            await page.Locator("iframe").ContentFrame.Locator("button").ClickAsync().ConfigureAwait(false);

            string id = await child.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);
            Assert.That(id, Is.EqualTo("inner"));
        }

        [PlaywrightTest("locator-frame.spec.ts", "Nested FrameLocator")]
        [Test]
        [Timeout(30_000)]
        public async Task NestedFrameLocatorShouldClick()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);
            await child.SetContentAsync("<iframe id=\"inner\"></iframe>").ConfigureAwait(false);

            IFrame grand = null;
            for (int i = 0; i < 50 && grand == null; i++)
            {
                foreach (IFrame frame in child.ChildFrames)
                {
                    grand = frame;
                    break;
                }

                if (grand == null)
                {
                    await Task.Delay(100).ConfigureAwait(false);
                }
            }

            Assert.That(grand, Is.Not.Null);
            await WaitForFrameReadyAsync(grand).ConfigureAwait(false);
            await grand.SetContentAsync("<button id=\"deep\">Go</button>").ConfigureAwait(false);

            ILocator button = page.FrameLocator("iframe").FrameLocator("iframe").Locator("button");
            Assert.That(await button.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("deep"));
            Assert.That(await button.TextContentAsync().ConfigureAwait(false), Is.EqualTo("Go"));
        }

        private static async Task<IFrame> AttachBlankChildFrameAsync(IPage page)
        {
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EvaluateAsync<bool>(@"
                const iframe = document.createElement('iframe');
                iframe.src = 'about:blank';
                document.body.appendChild(iframe);
                true
            ").ConfigureAwait(false);

            IFrame child = null;
            for (int i = 0; i < 50 && child == null; i++)
            {
                foreach (IFrame frame in page.MainFrame.ChildFrames)
                {
                    child = frame;
                    break;
                }

                if (child != null)
                {
                    break;
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            Assert.That(child, Is.Not.Null);
            await WaitForFrameReadyAsync(child).ConfigureAwait(false);
            return child;
        }

        private static async Task WaitForFrameReadyAsync(IFrame frame)
        {
            for (int i = 0; i < 50; i++)
            {
                try
                {
                    if (!await frame.EvaluateAsync<bool>("window === window.top").ConfigureAwait(false))
                    {
                        return;
                    }
                }
                catch (PlaywrightNativeException)
                {
                    // Execution context is not ready yet.
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            throw new TimeoutException("Child frame execution context did not become ready.");
        }
    }
}
