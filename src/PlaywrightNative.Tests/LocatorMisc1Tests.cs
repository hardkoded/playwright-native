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
    /// Official <c>locator-misc-1.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class LocatorMisc1Tests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        private static bool IsHeadless
        {
            get
            {
                string value = Environment.GetEnvironmentVariable("HEADLESS");
                return string.IsNullOrEmpty(value)
                    || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
            }
        }

        private static void SkipIfHeaded()
        {
            if (!IsHeadless)
            {
                Assert.Ignore("headed messes up with hover");
            }
        }

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19324;
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

        [PlaywrightTest("locator-misc-1.spec.ts", "should hover")]
        [PlaywrightTest("locator-misc-1.spec.ts", "should hover @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHover()
        {
            SkipIfHeaded();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/scrollable.html").ConfigureAwait(false);
            ILocator button = page.Locator("#button-6");
            await button.HoverAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => document.querySelector('button:hover').id)()").ConfigureAwait(false), Is.EqualTo("button-6"));
        }

        [PlaywrightTest("locator-misc-1.spec.ts", "should hover when Node is removed")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHoverWhenNodeIsRemoved()
        {
            SkipIfHeaded();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/scrollable.html").ConfigureAwait(false);
            await page.EvaluateAsync("(() => delete window['Node'])()").ConfigureAwait(false);
            ILocator button = page.Locator("#button-6");
            await button.HoverAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => document.querySelector('button:hover').id)()").ConfigureAwait(false), Is.EqualTo("button-6"));
        }

        [PlaywrightTest("locator-misc-1.spec.ts", "should fill input")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            ILocator handle = page.Locator("input");
            await handle.FillAsync("some value").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("some value"));
        }

        [PlaywrightTest("locator-misc-1.spec.ts", "should fill input when Node is removed")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillInputWhenNodeIsRemoved()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            await page.EvaluateAsync("(() => delete window['Node'])()").ConfigureAwait(false);
            ILocator handle = page.Locator("input");
            await handle.FillAsync("some value").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("some value"));
        }

        [PlaywrightTest("locator-misc-1.spec.ts", "should clear input")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClearInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            ILocator handle = page.Locator("input");
            await handle.FillAsync("some value").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("some value"));
            await handle.ClearAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo(string.Empty));
        }

        [PlaywrightTest("locator-misc-1.spec.ts", "should check the box")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldCheckTheBox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id='checkbox' type='checkbox'></input>").ConfigureAwait(false);
            ILocator input = page.Locator("input");
            await input.CheckAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("checkbox.checked").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("locator-misc-1.spec.ts", "should check the box using setChecked")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldCheckTheBoxUsingSetChecked()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id='checkbox' type='checkbox'></input>").ConfigureAwait(false);
            ILocator input = page.Locator("input");
            await input.SetCheckedAsync(true).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("checkbox.checked").ConfigureAwait(false), Is.True);
            await input.SetCheckedAsync(false).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("checkbox.checked").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("locator-misc-1.spec.ts", "should uncheck the box")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldUncheckTheBox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id='checkbox' type='checkbox' checked></input>").ConfigureAwait(false);
            ILocator input = page.Locator("input");
            await input.UncheckAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("checkbox.checked").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("locator-misc-1.spec.ts", "should select single option")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectSingleOption()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/select.html").ConfigureAwait(false);
            ILocator select = page.Locator("select");
            await select.SelectOptionAsync("blue").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string[]>("(() => window['result'].onInput)()").ConfigureAwait(false), Is.EqualTo(new[] { "blue" }));
            Assert.That(await page.EvaluateAsync<string[]>("(() => window['result'].onChange)()").ConfigureAwait(false), Is.EqualTo(new[] { "blue" }));
        }

        [PlaywrightTest("locator-misc-1.spec.ts", "should focus and blur a button")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFocusAndBlurAButton()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            ILocator button = page.Locator("button");
            Assert.That(await button.EvaluateAsync<bool>("button => document.activeElement === button").ConfigureAwait(false), Is.False);

            bool focused = false;
            bool blurred = false;
            await page.ExposeFunctionAsync("focusEvent", () => focused = true).ConfigureAwait(false);
            await page.ExposeFunctionAsync("blurEvent", () => blurred = true).ConfigureAwait(false);
            await button.EvaluateAsync<object>(@"button => {
                button.addEventListener('focus', window['focusEvent']);
                button.addEventListener('blur', window['blurEvent']);
            }").ConfigureAwait(false);

            await button.FocusAsync().ConfigureAwait(false);
            Assert.That(focused, Is.True);
            Assert.That(blurred, Is.False);
            Assert.That(await button.EvaluateAsync<bool>("button => document.activeElement === button").ConfigureAwait(false), Is.True);

            await button.BlurAsync().ConfigureAwait(false);
            Assert.That(focused, Is.True);
            Assert.That(blurred, Is.True);
            Assert.That(await button.EvaluateAsync<bool>("button => document.activeElement === button").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("locator-misc-1.spec.ts", "focus should respect strictness")]
        [Test]
        [Timeout(30_000)]
        public async Task FocusShouldRespectStrictness()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>A</div><div>B</div>").ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.Locator("div").FocusAsync());
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("strict mode violation"));
        }

        [PlaywrightTest("locator-misc-1.spec.ts", "should dispatch click event via ElementHandles")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchClickEventViaElementHandles()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            ILocator button = page.Locator("button");
            await button.DispatchEventAsync("click").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("Clicked"));
        }

        [PlaywrightTest("locator-misc-1.spec.ts", "should upload the file")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldUploadTheFile()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/fileupload.html").ConfigureAwait(false);
            ILocator input = page.Locator("input[type=file]");
            await input.SetInputFilesAsync(TestConstants.FileToUpload).ConfigureAwait(false);
            IElementHandle element = await input.ElementHandleAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("e => e.files[0].name", element).ConfigureAwait(false), Is.EqualTo("file-to-upload.txt"));
        }
    }
}
