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
    /// Locator-returning GetBy* factories on <see cref="IPage"/> and <see cref="IFrame"/>.
    /// </summary>
    [TestFixture]
    public class LocatorGetByTests : PageTestEx
    {
        [PlaywrightTest("selectors-get-by.spec.ts", "GetByTestId clicks the match")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByTestIdShouldClick()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button data-testid=\"go\" id=\"b\">Go</button>").ConfigureAwait(false);

            await page.GetByTestId("go").ClickAsync().ConfigureAwait(false);

            string id = await page.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);
            Assert.That(id, Is.EqualTo("b"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "GetByRole and GetByText")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleAndTextShouldResolve()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"b\">Save</button><p>Hello world</p>").ConfigureAwait(false);

            await page.GetByRole("button", name: "Save").ClickAsync().ConfigureAwait(false);
            string id = await page.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);
            string text = await page.GetByText("Hello world").TextContentAsync().ConfigureAwait(false);

            Assert.That(id, Is.EqualTo("b"));
            Assert.That(text, Does.Contain("Hello world"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "GetByLabel GetByPlaceholder GetByAltText GetByTitle")]
        [Test]
        [Timeout(30_000)]
        public async Task AttributeGetByShouldResolve()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<label for=\"n\">Name</label><input id=\"n\" placeholder=\"Your name\" />" +
                "<img alt=\"Logo\" title=\"Company\" />").ConfigureAwait(false);

            Assert.That(await page.GetByLabel("Name").GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("n"));
            Assert.That(await page.GetByPlaceholder("Your name").GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("n"));
            Assert.That(await page.GetByAltText("Logo").GetAttributeAsync("alt").ConfigureAwait(false), Is.EqualTo("Logo"));
            Assert.That(await page.GetByTitle("Company").GetAttributeAsync("title").ConfigureAwait(false), Is.EqualTo("Company"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "GetByRole is strict")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleShouldThrowWhenTwoMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button>One</button><button>Two</button>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.GetByRole("button").ClickAsync());

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
            Assert.That(await page.GetByRole("button").CountAsync().ConfigureAwait(false), Is.EqualTo(2));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "Frame GetByTestId")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameGetByTestIdShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
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

                if (child == null)
                {
                    await Task.Delay(100).ConfigureAwait(false);
                }
            }

            Assert.That(child, Is.Not.Null);
            await child.SetContentAsync("<button data-testid=\"inner\" id=\"b\">Go</button>").ConfigureAwait(false);
            await child.GetByTestId("inner").ClickAsync().ConfigureAwait(false);
            string id = await child.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);
            Assert.That(id, Is.EqualTo("b"));
        }
    }
}
