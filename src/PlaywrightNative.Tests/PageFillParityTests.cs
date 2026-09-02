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
    /// Official <c>page-fill.spec.ts</c> parity for <see cref="IPage.FillAsync"/>.
    /// <c>should throw if passed a non-string value</c> is skipped: C# FillAsync is string-only.
    /// </summary>
    [TestFixture]
    public class PageFillParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 18705;
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

        private static async Task GiveItAChanceToFillAsync(IPage page)
        {
            for (int i = 0; i < 5; i++)
            {
                await page.EvaluateAsync<object>("new Promise(r => requestAnimationFrame(() => r(true)))").ConfigureAwait(false);
            }
        }

        private static Task GoToTextareaAsync(IPage page)
            => page.GoToAsync(Prefix + "/input/textarea.html");

        private static async Task InputEventComposedShouldCrossShadowAsync(string type, string value)
        {
            if (!TestConstants.IsChromium && (type == "month" || type == "week"))
            {
                Assert.Ignore("Some browser/platforms do not implement certain input types");
            }

            if (TestConstants.IsWebKit && TestConstants.IsWindows
                && (type == "color" || type == "date" || type == "time" || type == "datetime-local"))
            {
                Assert.Ignore("Some browser/platforms do not implement certain input types");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            await page.SetContentAsync(@"<body><script>
    const div = document.createElement('div');
    const shadowRoot = div.attachShadow({mode: 'open'});
    shadowRoot.innerHTML = '<input type=" + type + @"></input>';
    document.body.appendChild(div);
  </script></body>").ConfigureAwait(false);

            await page.Locator("body").EvaluateAsync<object>(@"select => {
      window['firedBodyEvents'] = [];
      for (const event of ['input', 'change']) {
        select.addEventListener(event, e => {
          window['firedBodyEvents'].push(e.type + ':' + e.composed);
        }, false);
      }
    }").ConfigureAwait(false);

            IJSHandle inputHandle = await page.EvaluateHandleAsync(
                "document.querySelector('body > div').shadowRoot.querySelector('input')").ConfigureAwait(false);
            IElementHandle input = inputHandle.AsElement();
            Assert.That(input, Is.Not.Null);

            await input.EvaluateAsync<object>(@"select => {
      window['firedEvents'] = [];
      for (const event of ['input', 'change']) {
        select.addEventListener(event, e => {
          window['firedEvents'].push(e.type + ':' + e.composed);
        }, false);
      }
    }").ConfigureAwait(false);
            await input.FillAsync(value).ConfigureAwait(false);

            string[] firedEvents = await page.EvaluateAsync<string[]>("window['firedEvents']").ConfigureAwait(false);
            string[] firedBodyEvents = await page.EvaluateAsync<string[]>("window['firedBodyEvents']").ConfigureAwait(false);
            Assert.That(firedEvents, Is.EqualTo(new[] { "input:true", "change:false" }));
            Assert.That(firedBodyEvents, Is.EqualTo(new[] { "input:true" }));
        }

        [PlaywrightTest("page-fill.spec.ts", "should fill textarea")]
        [PlaywrightTest("page-fill.spec.ts", "should fill textarea @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillTextarea()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToTextareaAsync(page).ConfigureAwait(false);
            await page.FillAsync("textarea", "some value").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window['result']").ConfigureAwait(false), Is.EqualTo("some value"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should fill input")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToTextareaAsync(page).ConfigureAwait(false);
            await page.FillAsync("input", "some value").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window['result']").ConfigureAwait(false), Is.EqualTo("some value"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should throw on unsupported inputs")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowOnUnsupportedInputs()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToTextareaAsync(page).ConfigureAwait(false);
            string[] types = { "button", "checkbox", "file", "image", "radio", "reset", "submit" };
            foreach (string type in types)
            {
                await page.EvalOnSelectorAsync<object>("input", "(input, t) => input.setAttribute('type', t)", type).ConfigureAwait(false);
                Exception error = Assert.CatchAsync(() => page.FillAsync("input", string.Empty));
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Does.Contain("Input of type \"" + type + "\" cannot be filled"));
            }
        }

        [PlaywrightTest("page-fill.spec.ts", "should fill different input types")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillDifferentInputTypes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToTextareaAsync(page).ConfigureAwait(false);
            string[] types = { "password", "search", "tel", "text", "url", "invalid-type" };
            foreach (string type in types)
            {
                await page.EvalOnSelectorAsync<object>("input", "(input, t) => input.setAttribute('type', t)", type).ConfigureAwait(false);
                await page.FillAsync("input", "text " + type).ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("window['result']").ConfigureAwait(false), Is.EqualTo("text " + type));
            }
        }

        [PlaywrightTest("page-fill.spec.ts", "should fill range input")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillRangeInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=range min=0 max=100 value=50>").ConfigureAwait(false);
            await page.FillAsync("input", "42").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("input", "input => input.value").ConfigureAwait(false), Is.EqualTo("42"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should throw on incorrect range value")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowOnIncorrectRangeValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=range min=0 max=100 value=50>").ConfigureAwait(false);
            Exception error1 = Assert.CatchAsync(() => page.FillAsync("input", "foo"));
            Assert.That(error1, Is.Not.Null);
            Assert.That(error1.Message, Does.Contain("Malformed value"));
            Exception error2 = Assert.CatchAsync(() => page.FillAsync("input", "200"));
            Assert.That(error2, Is.Not.Null);
            Assert.That(error2.Message, Does.Contain("Malformed value"));
            Exception error3 = Assert.CatchAsync(() => page.FillAsync("input", "15.43"));
            Assert.That(error3, Is.Not.Null);
            Assert.That(error3.Message, Does.Contain("Malformed value"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should fill color input")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillColorInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=color value=\"#e66465\">").ConfigureAwait(false);
            await page.FillAsync("input", "#aaaaaa").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("input", "input => input.value").ConfigureAwait(false), Is.EqualTo("#aaaaaa"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should fill color input case insensitive")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillColorInputCaseInsensitive()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=color value=\"#e66465\">").ConfigureAwait(false);
            await page.FillAsync("input", "#AbCd00").ConfigureAwait(false);
            string expected = TestConstants.IsWebKit && TestConstants.IsWindows ? "#AbCd00" : "#abcd00";
            Assert.That(await page.EvalOnSelectorAsync<string>("input", "input => input.value").ConfigureAwait(false), Is.EqualTo(expected));
        }

        [PlaywrightTest("page-fill.spec.ts", "should throw on incorrect color value")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowOnIncorrectColorValue()
        {
            if (TestConstants.IsWebKit && TestConstants.IsWindows)
            {
                Assert.Ignore("WebKit win does not support color inputs");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=color value=\"#e66465\">").ConfigureAwait(false);
            Exception error1 = Assert.CatchAsync(() => page.FillAsync("input", "badvalue"));
            Assert.That(error1, Is.Not.Null);
            Assert.That(error1.Message, Does.Contain("Malformed value"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should fill date input after clicking")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillDateInputAfterClicking()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=date>").ConfigureAwait(false);
            await page.ClickAsync("input").ConfigureAwait(false);
            await page.FillAsync("input", "2020-03-02").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("input", "input => input.value").ConfigureAwait(false), Is.EqualTo("2020-03-02"));
        }

        [PlaywrightTest("page-fill.spec.ts", "input event.composed should be true and cross shadow dom boundary - color")]
        [Test]
        [Timeout(30_000)]
        public Task InputEventComposedShouldBeTrueAndCrossShadowDomBoundaryColor()
            => InputEventComposedShouldCrossShadowAsync("color", "#aaaaaa");

        [PlaywrightTest("page-fill.spec.ts", "input event.composed should be true and cross shadow dom boundary - date")]
        [Test]
        [Timeout(30_000)]
        public Task InputEventComposedShouldBeTrueAndCrossShadowDomBoundaryDate()
            => InputEventComposedShouldCrossShadowAsync("date", "2020-03-02");

        [PlaywrightTest("page-fill.spec.ts", "input event.composed should be true and cross shadow dom boundary - time")]
        [Test]
        [Timeout(30_000)]
        public Task InputEventComposedShouldBeTrueAndCrossShadowDomBoundaryTime()
            => InputEventComposedShouldCrossShadowAsync("time", "13:15");

        [PlaywrightTest("page-fill.spec.ts", "input event.composed should be true and cross shadow dom boundary - datetime-local")]
        [Test]
        [Timeout(30_000)]
        public Task InputEventComposedShouldBeTrueAndCrossShadowDomBoundaryDatetimeLocal()
            => InputEventComposedShouldCrossShadowAsync("datetime-local", "2020-03-02T13:15:30");

        [PlaywrightTest("page-fill.spec.ts", "input event.composed should be true and cross shadow dom boundary - month")]
        [Test]
        [Timeout(30_000)]
        public Task InputEventComposedShouldBeTrueAndCrossShadowDomBoundaryMonth()
            => InputEventComposedShouldCrossShadowAsync("month", "2020-03");

        [PlaywrightTest("page-fill.spec.ts", "input event.composed should be true and cross shadow dom boundary - range")]
        [Test]
        [Timeout(30_000)]
        public Task InputEventComposedShouldBeTrueAndCrossShadowDomBoundaryRange()
            => InputEventComposedShouldCrossShadowAsync("range", "42");

        [PlaywrightTest("page-fill.spec.ts", "input event.composed should be true and cross shadow dom boundary - week")]
        [Test]
        [Timeout(30_000)]
        public Task InputEventComposedShouldBeTrueAndCrossShadowDomBoundaryWeek()
            => InputEventComposedShouldCrossShadowAsync("week", "2020-W50");

        [PlaywrightTest("page-fill.spec.ts", "should throw on incorrect date")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowOnIncorrectDate()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("WebKit does not support date inputs");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=date>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.FillAsync("input", "2020-13-05"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Malformed value"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should fill time input")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillTimeInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=time>").ConfigureAwait(false);
            await page.FillAsync("input", "13:15").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("input", "input => input.value").ConfigureAwait(false), Is.EqualTo("13:15"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should fill month input")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillMonthInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=month>").ConfigureAwait(false);
            await page.FillAsync("input", "2020-07").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("input", "input => input.value").ConfigureAwait(false), Is.EqualTo("2020-07"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should throw on incorrect month")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowOnIncorrectMonth()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Only Chromium supports month inputs");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=month>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.FillAsync("input", "2020-13"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Malformed value"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should fill week input")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillWeekInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=week>").ConfigureAwait(false);
            await page.FillAsync("input", "2020-W50").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("input", "input => input.value").ConfigureAwait(false), Is.EqualTo("2020-W50"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should throw on incorrect week")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowOnIncorrectWeek()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Only Chromium supports week inputs");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=week>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.FillAsync("input", "2020-123"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Malformed value"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should throw on incorrect time")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowOnIncorrectTime()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("WebKit does not support time inputs");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=time>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.FillAsync("input", "25:05"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Malformed value"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should fill datetime-local input")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillDatetimeLocalInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=datetime-local>").ConfigureAwait(false);
            await page.FillAsync("input", "2020-03-02T05:15").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("input", "input => input.value").ConfigureAwait(false), Is.EqualTo("2020-03-02T05:15"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should throw on incorrect datetime-local")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowOnIncorrectDatetimeLocal()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Only Chromium supports datetime-local inputs");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=datetime-local>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.FillAsync("input", "abc"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Malformed value"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should fill contenteditable")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillContenteditable()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToTextareaAsync(page).ConfigureAwait(false);
            await page.FillAsync("div[contenteditable]", "some value").ConfigureAwait(false);
            Assert.That(
                await page.EvalOnSelectorAsync<string>("div[contenteditable]", "div => div.textContent").ConfigureAwait(false),
                Is.EqualTo("some value"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should fill contenteditable with new lines")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillContenteditableWithNewLines()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div contenteditable=\"true\"></div>").ConfigureAwait(false);
            await page.Locator("div[contenteditable]").FillAsync("John\nDoe").ConfigureAwait(false);
            Assert.That(await page.Locator("div[contenteditable]").InnerTextAsync().ConfigureAwait(false), Is.EqualTo("John\nDoe"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should not double-fill in contenteditable with beforeinput handler in Firefox")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotDoubleFillInContenteditableWithBeforeinputHandlerInFirefox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div id=""editor"" contenteditable=""true""></div>
    <script>
      const editor = document.getElementById('editor');
      editor.addEventListener('beforeinput', (event) => {
        event.preventDefault();
        editor.textContent = event.data;
      });
    </script>
  ").ConfigureAwait(false);

            ILocator locator = page.Locator("#editor");
            string testValue = "Playwright";
            await locator.FillAsync(testValue).ConfigureAwait(false);
            Assert.That(await locator.TextContentAsync().ConfigureAwait(false), Is.EqualTo(testValue));
        }

        [PlaywrightTest("page-fill.spec.ts", "should fill elements with existing value and selection")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillElementsWithExistingValueAndSelection()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToTextareaAsync(page).ConfigureAwait(false);

            await page.EvalOnSelectorAsync<object>("input", "input => { input.value = 'value one'; }").ConfigureAwait(false);
            await page.FillAsync("input", "another value").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window['result']").ConfigureAwait(false), Is.EqualTo("another value"));

            await page.EvalOnSelectorAsync<object>("input", @"input => {
                input.selectionStart = 1;
                input.selectionEnd = 2;
            }").ConfigureAwait(false);
            await page.FillAsync("input", "maybe this one").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window['result']").ConfigureAwait(false), Is.EqualTo("maybe this one"));

            await page.EvalOnSelectorAsync<object>("div[contenteditable]", @"div => {
                div.innerHTML = 'some text <span>some more text<span> and even more text';
                const range = document.createRange();
                range.selectNodeContents(div.querySelector('span'));
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
            }").ConfigureAwait(false);
            await page.FillAsync("div[contenteditable]", "replace with this").ConfigureAwait(false);
            Assert.That(
                await page.EvalOnSelectorAsync<string>("div[contenteditable]", "div => div.textContent").ConfigureAwait(false),
                Is.EqualTo("replace with this"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should throw nice error without injected script stack when element is not an <input>")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowNiceErrorWithoutInjectedScriptStackWhenElementIsNotAnInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<select><option>value1</option></select>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.FillAsync("select", string.Empty));
            Assert.That(error, Is.Not.Null);
            Assert.That(
                error.Message,
                Does.Contain("page.fill: Error: Element is not an <input>, <textarea> or [contenteditable] element\nCall log:"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should throw if passed a non-string value")]
        [Test]
        [Timeout(30_000)]
        public void ShouldThrowIfPassedANonStringValue()
        {
            Assert.Ignore("C# FillAsync is string-only");
        }

        [PlaywrightTest("page-fill.spec.ts", "should retry on disabled element")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRetryOnDisabledElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToTextareaAsync(page).ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("input", "i => { i.disabled = true; }").ConfigureAwait(false);
            bool done = false;
            async Task FillAsync()
            {
                await page.FillAsync("input", "some value").ConfigureAwait(false);
                done = true;
            }

            Task promise = FillAsync();
            await GiveItAChanceToFillAsync(page).ConfigureAwait(false);
            Assert.That(done, Is.False);
            Assert.That(await page.EvaluateAsync<string>("window['result']").ConfigureAwait(false), Is.EqualTo(string.Empty));

            await page.EvalOnSelectorAsync<object>("input", "i => { i.disabled = false; }").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window['result']").ConfigureAwait(false), Is.EqualTo("some value"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should retry on readonly element")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRetryOnReadonlyElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToTextareaAsync(page).ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("textarea", "i => { i.readOnly = true; }").ConfigureAwait(false);
            bool done = false;
            async Task FillAsync()
            {
                await page.FillAsync("textarea", "some value").ConfigureAwait(false);
                done = true;
            }

            Task promise = FillAsync();
            await GiveItAChanceToFillAsync(page).ConfigureAwait(false);
            Assert.That(done, Is.False);
            Assert.That(await page.EvaluateAsync<string>("window['result']").ConfigureAwait(false), Is.EqualTo(string.Empty));

            await page.EvalOnSelectorAsync<object>("textarea", "i => { i.readOnly = false; }").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window['result']").ConfigureAwait(false), Is.EqualTo("some value"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should retry on invisible element")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRetryOnInvisibleElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToTextareaAsync(page).ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("input", "i => { i.style.display = 'none'; }").ConfigureAwait(false);
            bool done = false;
            async Task FillAsync()
            {
                await page.FillAsync("input", "some value").ConfigureAwait(false);
                done = true;
            }

            Task promise = FillAsync();
            await GiveItAChanceToFillAsync(page).ConfigureAwait(false);
            Assert.That(done, Is.False);
            Assert.That(await page.EvaluateAsync<string>("window['result']").ConfigureAwait(false), Is.EqualTo(string.Empty));

            await page.EvalOnSelectorAsync<object>("input", "i => { i.style.display = 'inline'; }").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window['result']").ConfigureAwait(false), Is.EqualTo("some value"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should be able to fill the body")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldBeAbleToFillTheBody()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<body contentEditable=\"true\"></body>").ConfigureAwait(false);
            await page.FillAsync("body", "some value").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false), Is.EqualTo("some value"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should fill fixed position input")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillFixedPositionInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input style='position: fixed;' />").ConfigureAwait(false);
            await page.FillAsync("input", "some value").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.querySelector('input').value").ConfigureAwait(false), Is.EqualTo("some value"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should be able to fill when focus is in the wrong frame")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldBeAbleToFillWhenFocusIsInTheWrongFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div contentEditable=""true""></div>
    <iframe></iframe>
  ").ConfigureAwait(false);
            await page.FocusAsync("iframe").ConfigureAwait(false);
            await page.FillAsync("div", "some value").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("div", "d => d.textContent").ConfigureAwait(false), Is.EqualTo("some value"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should be able to fill the input[type=number]")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldBeAbleToFillTheInputTypeNumber()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"input\" type=\"number\"></input>").ConfigureAwait(false);
            await page.FillAsync("input", "42").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window['input'].value").ConfigureAwait(false), Is.EqualTo("42"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should be able to fill exponent into the input[type=number]")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldBeAbleToFillExponentIntoTheInputTypeNumber()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"input\" type=\"number\"></input>").ConfigureAwait(false);
            await page.FillAsync("input", "-10e5").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window['input'].value").ConfigureAwait(false), Is.EqualTo("-10e5"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should be able to fill input[type=number] with empty string")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldBeAbleToFillInputTypeNumberWithEmptyString()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"input\" type=\"number\" value=\"123\"></input>").ConfigureAwait(false);
            await page.FillAsync("input", string.Empty).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window['input'].value").ConfigureAwait(false), Is.EqualTo(string.Empty));
        }

        [PlaywrightTest("page-fill.spec.ts", "should not be able to fill text into the input[type=number]")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotBeAbleToFillTextIntoTheInputTypeNumber()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"input\" type=\"number\"></input>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.FillAsync("input", "abc"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Cannot type text into input[type=number]"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should be able to clear using fill()")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldBeAbleToClearUsingFill()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToTextareaAsync(page).ConfigureAwait(false);
            await page.FillAsync("input", "some value").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window['result']").ConfigureAwait(false), Is.EqualTo("some value"));
            await page.FillAsync("input", string.Empty).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window['result']").ConfigureAwait(false), Is.EqualTo(string.Empty));
        }

        [PlaywrightTest("page-fill.spec.ts", "should not throw when fill causes navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotThrowWhenFillCausesNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToTextareaAsync(page).ConfigureAwait(false);
            await page.SetContentAsync("<input type=date>").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("input", "select => select.addEventListener('input', () => window.location.href = '/empty.html')").ConfigureAwait(false);
            await Task.WhenAll(
                page.FillAsync("input", "2020-03-02"),
                page.WaitForNavigationAsync()).ConfigureAwait(false);
            Assert.That(page.Url, Does.Contain("empty.html"));
        }

        [PlaywrightTest("page-fill.spec.ts", "fill back to back")]
        [Test]
        [Timeout(30_000)]
        public async Task FillBackToBack()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"one\"></input><input id=\"two\"></input>").ConfigureAwait(false);
            await page.FillAsync("#one", "first value").ConfigureAwait(false);
            await page.FillAsync("#two", "second value").ConfigureAwait(false);
            Assert.That(await page.Locator("#one").InputValueAsync().ConfigureAwait(false), Is.EqualTo("first value"));
            Assert.That(await page.Locator("#two").InputValueAsync().ConfigureAwait(false), Is.EqualTo("second value"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should fill contenteditable with focus handler that collapses selection")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillContenteditableWithFocusHandlerThatCollapsesSelection()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div contenteditable=""true"">initial text</div>
    <script>
      const editor = document.querySelector('[contenteditable]');
      editor.addEventListener('focus', () => {
        const selection = window.getSelection();
        if (selection.rangeCount > 0)
          selection.collapseToEnd();
      });
    </script>
  ").ConfigureAwait(false);

            await page.FillAsync("div[contenteditable]", "some value").ConfigureAwait(false);
            Assert.That(await page.Locator("div[contenteditable]").TextContentAsync().ConfigureAwait(false), Is.EqualTo("some value"));
        }
    }
}
