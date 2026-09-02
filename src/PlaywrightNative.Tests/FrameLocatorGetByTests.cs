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
    /// GetBy* factories on <see cref="IFrameLocator"/>.
    /// </summary>
    [TestFixture]
    public class FrameLocatorGetByTests : PageTestEx
    {
        [PlaywrightTest("locator-frame.spec.ts", "GetByRole and GetByText resolve inside the iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleAndTextShouldResolveInsideIframe()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.SetContentAsync(
                "<iframe srcdoc=\"<button id='b'>Save</button><p>Hello world</p>\"></iframe>").ConfigureAwait(false);

            IFrameLocator frame = page.FrameLocator("iframe");
            Assert.That(await frame.GetByRole("button", name: "Save").GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("b"));
            Assert.That(await frame.GetByText("Hello world").TextContentAsync().ConfigureAwait(false), Does.Contain("Hello world"));
        }

        [PlaywrightTest("locator-frame.spec.ts", "Attribute GetBy and GetByTestId resolve inside the iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task AttributeGetByShouldResolveInsideIframe()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.SetContentAsync(
                "<iframe srcdoc=\"" +
                "<label for='n'>Name</label><input id='n' placeholder='Your name' data-testid='name' />" +
                "<img alt='Logo' title='Company' />" +
                "\"></iframe>").ConfigureAwait(false);

            IFrameLocator frame = page.FrameLocator("iframe");
            Assert.That(await frame.GetByLabel("Name").GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("n"));
            Assert.That(await frame.GetByPlaceholder("Your name").GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("n"));
            Assert.That(await frame.GetByTestId("name").GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("n"));
            Assert.That(await frame.GetByAltText("Logo").GetAttributeAsync("alt").ConfigureAwait(false), Is.EqualTo("Logo"));
            Assert.That(await frame.GetByTitle("Company").GetAttributeAsync("title").ConfigureAwait(false), Is.EqualTo("Company"));
        }

        [PlaywrightTest("locator-frame.spec.ts", "GetByRole is strict")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleShouldThrowWhenTwoMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.SetContentAsync(
                "<iframe srcdoc=\"<button>One</button><button>Two</button>\"></iframe>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.FrameLocator("iframe").GetByRole("button").ClickAsync());

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
            Assert.That(await page.FrameLocator("iframe").GetByRole("button").CountAsync().ConfigureAwait(false), Is.EqualTo(2));
        }
    }
}
