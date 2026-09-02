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
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>elementhandle-wait-for-element-state.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class ElementHandleWaitForElementStateParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        private static async Task GiveItAChanceToResolveAsync(IPage page)
        {
            for (int i = 0; i < 5; i++)
            {
                await page.EvaluateAsync<object>("(() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r))))()").ConfigureAwait(false);
            }
        }

        private static Task RafrafAsync(IPage page)
            => page.EvaluateAsync<object>("(() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r))))()");

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19880;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    Prefix = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    return;
                }
                catch (Exception)
                {
                }
            }

            Assert.Ignore("Test server is unavailable.");
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }
        }

        [PlaywrightTest("elementhandle-wait-for-element-state.spec.ts", "should wait for visible")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div style='display:none'>content</div>").ConfigureAwait(false);
            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            bool done = false;
            Task promise = MarkDoneAsync();
            await GiveItAChanceToResolveAsync(page).ConfigureAwait(false);
            Assert.That(done, Is.False);
            await div.EvaluateAsync("div => div.style.display = 'block'").ConfigureAwait(false);
            await promise.ConfigureAwait(false);

            async Task MarkDoneAsync()
            {
                await div.WaitForElementStateAsync(ElementState.Visible).ConfigureAwait(false);
                done = true;
            }
        }

        [PlaywrightTest("elementhandle-wait-for-element-state.spec.ts", "should wait for already visible")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForAlreadyVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>content</div>").ConfigureAwait(false);
            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            await div.WaitForElementStateAsync(ElementState.Visible).ConfigureAwait(false);
        }

        [PlaywrightTest("elementhandle-wait-for-element-state.spec.ts", "should timeout waiting for visible")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div style='display:none'>content</div>").ConfigureAwait(false);
            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => div.WaitForElementStateAsync(ElementState.Visible, timeout: 1000));
            Assert.That(error.Message, Does.Contain("Timeout 1000ms exceeded"));
        }

        [PlaywrightTest("elementhandle-wait-for-element-state.spec.ts", "should throw waiting for visible when detached")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowWaitingForVisibleWhenDetached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div style='display:none'>content</div>").ConfigureAwait(false);
            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            Task waitTask = div.WaitForElementStateAsync(ElementState.Visible);
            await div.EvaluateAsync("div => div.remove()").ConfigureAwait(false);
            PlaywrightNativeException error = Assert.ThrowsAsync<PlaywrightNativeException>(() => waitTask);
            Assert.That(error.Message, Does.Contain("Element is not attached to the DOM"));
        }

        [PlaywrightTest("elementhandle-wait-for-element-state.spec.ts", "should wait for hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>content</div>").ConfigureAwait(false);
            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            bool done = false;
            Task promise = MarkDoneAsync();
            await GiveItAChanceToResolveAsync(page).ConfigureAwait(false);
            Assert.That(done, Is.False);
            await div.EvaluateAsync("div => div.style.display = 'none'").ConfigureAwait(false);
            await promise.ConfigureAwait(false);

            async Task MarkDoneAsync()
            {
                await div.WaitForElementStateAsync(ElementState.Hidden).ConfigureAwait(false);
                done = true;
            }
        }

        [PlaywrightTest("elementhandle-wait-for-element-state.spec.ts", "should wait for already hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForAlreadyHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);
            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            await div.WaitForElementStateAsync(ElementState.Hidden).ConfigureAwait(false);
        }

        [PlaywrightTest("elementhandle-wait-for-element-state.spec.ts", "should wait for hidden when detached")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForHiddenWhenDetached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>content</div>").ConfigureAwait(false);
            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            bool done = false;
            Task promise = MarkDoneAsync();
            await GiveItAChanceToResolveAsync(page).ConfigureAwait(false);
            Assert.That(done, Is.False);
            await div.EvaluateAsync("div => div.remove()").ConfigureAwait(false);
            await promise.ConfigureAwait(false);

            async Task MarkDoneAsync()
            {
                await div.WaitForElementStateAsync(ElementState.Hidden).ConfigureAwait(false);
                done = true;
            }
        }

        [PlaywrightTest("elementhandle-wait-for-element-state.spec.ts", "should throw waiting for enabled when detached")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowWaitingForEnabledWhenDetached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button disabled>Target</button>").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("button").ConfigureAwait(false);
            Task waitTask = button.WaitForElementStateAsync(ElementState.Enabled);
            await button.EvaluateAsync("button => button.remove()").ConfigureAwait(false);
            PlaywrightNativeException error = Assert.ThrowsAsync<PlaywrightNativeException>(() => waitTask);
            Assert.That(error.Message, Does.Contain("Element is not attached to the DOM"));
        }

        [PlaywrightTest("elementhandle-wait-for-element-state.spec.ts", "should wait for aria enabled button")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForAriaEnabledButton()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button aria-disabled=true><span>Target</span></button>").ConfigureAwait(false);
            IElementHandle span = await page.QuerySelectorAsync("text=Target").ConfigureAwait(false);
            bool done = false;
            Task promise = MarkDoneAsync();
            await GiveItAChanceToResolveAsync(page).ConfigureAwait(false);
            Assert.That(done, Is.False);
            await span.EvaluateAsync("span => span.parentElement.setAttribute('aria-disabled', 'false')").ConfigureAwait(false);
            await promise.ConfigureAwait(false);

            async Task MarkDoneAsync()
            {
                await span.WaitForElementStateAsync(ElementState.Enabled).ConfigureAwait(false);
                done = true;
            }
        }

        [PlaywrightTest("elementhandle-wait-for-element-state.spec.ts", "should wait for button with an aria-disabled parent")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForButtonWithAnAriaDisabledParent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div role=\"group\" aria-disabled=true><button><span>Target</span></button></div>").ConfigureAwait(false);
            IElementHandle span = await page.QuerySelectorAsync("text=Target").ConfigureAwait(false);
            bool done = false;
            Task promise = MarkDoneAsync();
            await GiveItAChanceToResolveAsync(page).ConfigureAwait(false);
            Assert.That(done, Is.False);
            await span.EvaluateAsync("span => span.parentElement.parentElement.setAttribute('aria-disabled', 'false')").ConfigureAwait(false);
            await promise.ConfigureAwait(false);

            async Task MarkDoneAsync()
            {
                await span.WaitForElementStateAsync(ElementState.Enabled).ConfigureAwait(false);
                done = true;
            }
        }

        [PlaywrightTest("elementhandle-wait-for-element-state.spec.ts", "should wait for stable position")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForStablePosition()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("button").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("button", @"button => {
                button.style.transition = 'margin 10000ms linear 0s';
                button.style.marginLeft = '20000px';
            }").ConfigureAwait(false);
            await RafrafAsync(page).ConfigureAwait(false);
            bool done = false;
            Task promise = MarkDoneAsync();
            await GiveItAChanceToResolveAsync(page).ConfigureAwait(false);
            Assert.That(done, Is.False);
            await button.EvaluateAsync("button => button.style.transition = ''").ConfigureAwait(false);
            await promise.ConfigureAwait(false);

            async Task MarkDoneAsync()
            {
                await button.WaitForElementStateAsync(ElementState.Stable).ConfigureAwait(false);
                done = true;
            }
        }

        [PlaywrightTest("elementhandle-wait-for-element-state.spec.ts", "should wait for editable input")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForEditableInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input readonly>").ConfigureAwait(false);
            IElementHandle input = await page.QuerySelectorAsync("input").ConfigureAwait(false);
            bool done = false;
            Task promise = MarkDoneAsync();
            await GiveItAChanceToResolveAsync(page).ConfigureAwait(false);
            Assert.That(done, Is.False);
            await input.EvaluateAsync("input => input.readOnly = false").ConfigureAwait(false);
            await promise.ConfigureAwait(false);

            async Task MarkDoneAsync()
            {
                await input.WaitForElementStateAsync(ElementState.Editable).ConfigureAwait(false);
                done = true;
            }
        }
    }
}
