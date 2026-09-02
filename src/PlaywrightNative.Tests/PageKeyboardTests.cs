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
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-keyboard.spec.ts</c> parity for <see cref="IKeyboard"/>.
    /// </summary>
    [TestFixture]
    public class PageKeyboardTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19111;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    string origin = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    Prefix = origin;
                    CrossProcessPrefix = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
                    EmptyPage = origin + "/empty.html";
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

        private static async Task<IPage> NewPageAsync(IBrowser browser)
        {
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            return await context.NewPageAsync().ConfigureAwait(false);
        }

        private static Task<string> GetResultAsync(IPage page)
            => page.EvaluateAsync<string>("getResult()");

        private static async Task<IJSHandle> CaptureLastKeydownAsync(IPage page)
        {
            return await page.EvaluateHandleAsync(@"() => {
                const lastEvent = {
                    repeat: false,
                    location: -1,
                    code: '',
                    key: '',
                    metaKey: false,
                    keyIdentifier: 'unsupported'
                };
                document.addEventListener('keydown', e => {
                    lastEvent.repeat = e.repeat;
                    lastEvent.location = e.location;
                    lastEvent.key = e.key;
                    lastEvent.code = e.code;
                    lastEvent.metaKey = e.metaKey;
                    lastEvent.keyIdentifier = 'keyIdentifier' in e && typeof e['keyIdentifier'] === 'string' && e['keyIdentifier'];
                }, true);
                return lastEvent;
            }").ConfigureAwait(false);
        }

        private static async Task<IFrame> AttachFrameAsync(IPage page, string name, string url)
        {
            string script =
                "(() => { const f = document.createElement('iframe'); f.name = '" +
                name +
                "'; f.id = '" +
                name +
                "'; f.src = '" +
                url +
                "'; document.body.appendChild(f); })()";
            await page.EvaluateAsync<object>(script).ConfigureAwait(false);

            IFrame named = null;
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (named == null && DateTime.UtcNow < deadline)
            {
                named = page.Frame(name);
                if (named != null)
                {
                    break;
                }

                foreach (IFrame child in page.MainFrame.ChildFrames)
                {
                    named = child;
                    break;
                }

                if (named == null)
                {
                    await Task.Delay(50).ConfigureAwait(false);
                }
            }

            if (named != null)
            {
                await WaitForFrameTextareaAsync(named).ConfigureAwait(false);
                return named;
            }

            named = page.Frame(name);
            if (named != null)
            {
                return named;
            }

            foreach (IFrame child in page.MainFrame.ChildFrames)
            {
                await WaitForFrameTextareaAsync(child).ConfigureAwait(false);
                return child;
            }

            List<IFrame> frames = new List<IFrame>(page.Frames);
            if (frames.Count > 1)
            {
                await WaitForFrameTextareaAsync(frames[1]).ConfigureAwait(false);
                return frames[1];
            }

            Assert.Fail("Child frame was not created.");
            return null;
        }

        private static async Task PollAsync(Func<Task<bool>> condition)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if (await condition().ConfigureAwait(false))
                {
                    return;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.That(await condition().ConfigureAwait(false), Is.True);
        }

        private static async Task TestEnterKeyAsync(IPage page, IJSHandle lastEventHandle, string key, string expectedKey, string expectedCode)
        {
            await page.Keyboard.PressAsync(key).ConfigureAwait(false);
            Assert.That(await lastEventHandle.EvaluateAsync<string>("e => e.key").ConfigureAwait(false), Is.EqualTo(expectedKey));
            Assert.That(await lastEventHandle.EvaluateAsync<string>("e => e.code").ConfigureAwait(false), Is.EqualTo(expectedCode));
            Assert.That(await page.EvalOnSelectorAsync<string>("textarea", "t => t.value").ConfigureAwait(false), Is.EqualTo("\n"));
            await page.EvalOnSelectorAsync<object>("textarea", "t => t.value = ''").ConfigureAwait(false);
        }

        private static async Task WaitForFrameTextareaAsync(IFrame frame)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    IElementHandle textarea = await frame.QuerySelectorAsync("textarea").ConfigureAwait(false);
                    if (textarea != null)
                    {
                        return;
                    }
                }
                catch (Exception)
                {
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        private static async Task WaitForShadowElementAsync(IPage page)
        {
            await PollAsync(async () =>
                await page.EvaluateAsync<bool>(
                    "!!(document.querySelector('shadow-element') && document.querySelector('shadow-element').shadowRoot)").ConfigureAwait(false)).ConfigureAwait(false);
        }

        private static async Task<Exception> CatchAsync(Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should type into a textarea")]
        [PlaywrightTest("page-keyboard.spec.ts", "should type into a textarea @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTypeIntoATextarea()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.EvaluateAsync<object>(@"(() => {
                const textarea = document.createElement('textarea');
                document.body.appendChild(textarea);
                textarea.focus();
            })()").ConfigureAwait(false);
            string text = "Hello world. I am the text that was typed!";
            await page.Keyboard.TypeAsync(text).ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("document.querySelector('textarea').value").ConfigureAwait(false),
                Is.EqualTo(text));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should move with the arrow keys")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldMoveWithTheArrowKeys()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            await page.TypeAsync("textarea", "Hello World!").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("document.querySelector('textarea').value").ConfigureAwait(false),
                Is.EqualTo("Hello World!"));
            for (int i = 0; i < "World!".Length; i++)
            {
                await page.Keyboard.PressAsync("ArrowLeft").ConfigureAwait(false);
            }

            await page.Keyboard.TypeAsync("inserted ").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("document.querySelector('textarea').value").ConfigureAwait(false),
                Is.EqualTo("Hello inserted World!"));
            await page.Keyboard.DownAsync("Shift").ConfigureAwait(false);
            for (int i = 0; i < "inserted ".Length; i++)
            {
                await page.Keyboard.PressAsync("ArrowLeft").ConfigureAwait(false);
            }

            await page.Keyboard.UpAsync("Shift").ConfigureAwait(false);
            await page.Keyboard.PressAsync("Backspace").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("document.querySelector('textarea').value").ConfigureAwait(false),
                Is.EqualTo("Hello World!"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should send a character with ElementHandle.press")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSendACharacterWithElementHandlePress()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            IElementHandle textarea = await page.QuerySelectorAsync("textarea").ConfigureAwait(false);
            await textarea.PressAsync("a").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("document.querySelector('textarea').value").ConfigureAwait(false),
                Is.EqualTo("a"));

            await page.EvaluateAsync<object>("window.addEventListener('keydown', e => e.preventDefault(), true)").ConfigureAwait(false);
            await textarea.PressAsync("b").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("document.querySelector('textarea').value").ConfigureAwait(false),
                Is.EqualTo("a"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should send a character with insertText")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSendACharacterWithInsertText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            await page.FocusAsync("textarea").ConfigureAwait(false);
            await page.Keyboard.InsertTextAsync("嗨").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("document.querySelector('textarea').value").ConfigureAwait(false),
                Is.EqualTo("嗨"));
            await page.EvaluateAsync<object>("window.addEventListener('keydown', e => e.preventDefault(), true)").ConfigureAwait(false);
            await page.Keyboard.InsertTextAsync("a").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("document.querySelector('textarea').value").ConfigureAwait(false),
                Is.EqualTo("嗨a"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "insertText should only emit input event")]
        [Test]
        [Timeout(30_000)]
        public async Task InsertTextShouldOnlyEmitInputEvent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            await page.FocusAsync("textarea").ConfigureAwait(false);
            IJSHandle events = await page.EvaluateHandleAsync(@"() => {
                const events = [];
                document.addEventListener('keydown', e => events.push(e.type));
                document.addEventListener('keyup', e => events.push(e.type));
                document.addEventListener('keypress', e => events.push(e.type));
                document.addEventListener('input', e => events.push(e.type));
                return events;
            }").ConfigureAwait(false);
            await page.Keyboard.InsertTextAsync("hello world").ConfigureAwait(false);
            Assert.That(await events.JsonValueAsync<string[]>().ConfigureAwait(false), Is.EqualTo(new[] { "input" }));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should emit keydown, keypress, textInput and input when typing a character")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEmitKeydownKeypressTextInputAndInputWhenTypingACharacter()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.SetContentAsync("<input>").ConfigureAwait(false);
            IJSHandle events = await page.EvaluateHandleAsync(@"() => {
                const events = [];
                for (const type of ['keydown', 'keypress', 'textInput', 'input', 'keyup'])
                    document.querySelector('input').addEventListener(type, () => events.push(type));
                return events;
            }").ConfigureAwait(false);
            await page.FocusAsync("input").ConfigureAwait(false);
            await page.Keyboard.PressAsync("f").ConfigureAwait(false);
            Assert.That(
                await events.JsonValueAsync<string[]>().ConfigureAwait(false),
                Is.EqualTo(new[] { "keydown", "keypress", "textInput", "input", "keyup" }));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should dispatch key events in separate tasks")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchKeyEventsInSeparateTasks()
        {
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("Firefox/Juggler dispatches keydown and keypress in the same task");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.SetContentAsync("<input>").ConfigureAwait(false);
            IJSHandle log = await page.EvaluateHandleAsync(@"() => {
                const log = [];
                const input = document.querySelector('input');
                for (const type of ['keydown', 'keypress'])
                    input.addEventListener(type, () => { log.push(type); queueMicrotask(() => log.push('microtask-' + type)); });
                input.focus();
                return log;
            }").ConfigureAwait(false);
            await page.Keyboard.PressAsync("a").ConfigureAwait(false);
            Assert.That(
                await log.JsonValueAsync<string[]>().ConfigureAwait(false),
                Is.EqualTo(new[] { "keydown", "microtask-keydown", "keypress", "microtask-keypress" }));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should report shiftKey")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportShiftKey()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/keyboard.html").ConfigureAwait(false);
            IKeyboard keyboard = page.Keyboard;
            string[] modifierKeys = { "Shift", "Alt", "Control" };
            foreach (string modifierKey in modifierKeys)
            {
                await keyboard.DownAsync(modifierKey).ConfigureAwait(false);
                Assert.That(
                    await GetResultAsync(page).ConfigureAwait(false),
                    Is.EqualTo("Keydown: " + modifierKey + " " + modifierKey + "Left LEFT [" + modifierKey + "]"));
                await keyboard.DownAsync("!").ConfigureAwait(false);
                if (modifierKey == "Shift")
                {
                    Assert.That(
                        await GetResultAsync(page).ConfigureAwait(false),
                        Is.EqualTo("Keydown: ! Digit1 STANDARD [" + modifierKey + "]\nKeypress: ! Digit1 STANDARD 33 [" + modifierKey + "]"));
                }
                else
                {
                    Assert.That(
                        await GetResultAsync(page).ConfigureAwait(false),
                        Is.EqualTo("Keydown: ! Digit1 STANDARD [" + modifierKey + "]"));
                }

                await keyboard.UpAsync("!").ConfigureAwait(false);
                Assert.That(
                    await GetResultAsync(page).ConfigureAwait(false),
                    Is.EqualTo("Keyup: ! Digit1 STANDARD [" + modifierKey + "]"));
                await keyboard.UpAsync(modifierKey).ConfigureAwait(false);
                Assert.That(
                    await GetResultAsync(page).ConfigureAwait(false),
                    Is.EqualTo("Keyup: " + modifierKey + " " + modifierKey + "Left LEFT []"));
            }
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should report multiple modifiers")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportMultipleModifiers()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/keyboard.html").ConfigureAwait(false);
            IKeyboard keyboard = page.Keyboard;
            await keyboard.DownAsync("Control").ConfigureAwait(false);
            Assert.That(await GetResultAsync(page).ConfigureAwait(false), Is.EqualTo("Keydown: Control ControlLeft LEFT [Control]"));
            await keyboard.DownAsync("Alt").ConfigureAwait(false);
            Assert.That(await GetResultAsync(page).ConfigureAwait(false), Is.EqualTo("Keydown: Alt AltLeft LEFT [Alt Control]"));
            await keyboard.DownAsync(";").ConfigureAwait(false);
            Assert.That(await GetResultAsync(page).ConfigureAwait(false), Is.EqualTo("Keydown: ; Semicolon STANDARD [Alt Control]"));
            await keyboard.UpAsync(";").ConfigureAwait(false);
            Assert.That(await GetResultAsync(page).ConfigureAwait(false), Is.EqualTo("Keyup: ; Semicolon STANDARD [Alt Control]"));
            await keyboard.UpAsync("Control").ConfigureAwait(false);
            Assert.That(await GetResultAsync(page).ConfigureAwait(false), Is.EqualTo("Keyup: Control ControlLeft LEFT [Alt]"));
            await keyboard.UpAsync("Alt").ConfigureAwait(false);
            Assert.That(await GetResultAsync(page).ConfigureAwait(false), Is.EqualTo("Keyup: Alt AltLeft LEFT []"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should send proper codes while typing")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSendProperCodesWhileTyping()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/keyboard.html").ConfigureAwait(false);
            await page.Keyboard.TypeAsync("!").ConfigureAwait(false);
            Assert.That(
                await GetResultAsync(page).ConfigureAwait(false),
                Is.EqualTo(string.Join("\n", new[] { "Keydown: ! Digit1 STANDARD []", "Keypress: ! Digit1 STANDARD 33 []", "Keyup: ! Digit1 STANDARD []" })));
            await page.Keyboard.TypeAsync("^").ConfigureAwait(false);
            Assert.That(
                await GetResultAsync(page).ConfigureAwait(false),
                Is.EqualTo(string.Join("\n", new[] { "Keydown: ^ Digit6 STANDARD []", "Keypress: ^ Digit6 STANDARD 94 []", "Keyup: ^ Digit6 STANDARD []" })));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should send proper codes while typing with shift")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSendProperCodesWhileTypingWithShift()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/keyboard.html").ConfigureAwait(false);
            IKeyboard keyboard = page.Keyboard;
            await keyboard.DownAsync("Shift").ConfigureAwait(false);
            await page.Keyboard.TypeAsync("~").ConfigureAwait(false);
            Assert.That(
                await GetResultAsync(page).ConfigureAwait(false),
                Is.EqualTo(string.Join("\n", new[]
                {
                    "Keydown: Shift ShiftLeft LEFT [Shift]",
                    "Keydown: ~ Backquote STANDARD [Shift]",
                    "Keypress: ~ Backquote STANDARD 126 [Shift]",
                    "Keyup: ~ Backquote STANDARD [Shift]",
                })));
            await keyboard.UpAsync("Shift").ConfigureAwait(false);
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should not type canceled events")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotTypeCanceledEvents()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            await page.FocusAsync("textarea").ConfigureAwait(false);
            await page.EvaluateAsync<object>(@"(() => {
                window.addEventListener('keydown', event => {
                    event.stopPropagation();
                    event.stopImmediatePropagation();
                    if (event.key === 'l')
                        event.preventDefault();
                    if (event.key === 'o')
                        event.preventDefault();
                }, false);
            })()").ConfigureAwait(false);
            await page.Keyboard.TypeAsync("Hello World!").ConfigureAwait(false);
            Assert.That(
                await page.EvalOnSelectorAsync<string>("textarea", "textarea => textarea.value").ConfigureAwait(false),
                Is.EqualTo("He Wrd!"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should press plus")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldPressPlus()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/keyboard.html").ConfigureAwait(false);
            await page.Keyboard.PressAsync("+").ConfigureAwait(false);
            Assert.That(
                await GetResultAsync(page).ConfigureAwait(false),
                Is.EqualTo(string.Join("\n", new[] { "Keydown: + Equal STANDARD []", "Keypress: + Equal STANDARD 43 []", "Keyup: + Equal STANDARD []" })));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should press shift plus")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldPressShiftPlus()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/keyboard.html").ConfigureAwait(false);
            await page.Keyboard.PressAsync("Shift++").ConfigureAwait(false);
            Assert.That(
                await GetResultAsync(page).ConfigureAwait(false),
                Is.EqualTo(string.Join("\n", new[]
                {
                    "Keydown: Shift ShiftLeft LEFT [Shift]",
                    "Keydown: + Equal STANDARD [Shift]",
                    "Keypress: + Equal STANDARD 43 [Shift]",
                    "Keyup: + Equal STANDARD [Shift]",
                    "Keyup: Shift ShiftLeft LEFT []",
                })));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should support plus-separated modifiers")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportPlusSeparatedModifiers()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/keyboard.html").ConfigureAwait(false);
            await page.Keyboard.PressAsync("Shift+~").ConfigureAwait(false);
            Assert.That(
                await GetResultAsync(page).ConfigureAwait(false),
                Is.EqualTo(string.Join("\n", new[]
                {
                    "Keydown: Shift ShiftLeft LEFT [Shift]",
                    "Keydown: ~ Backquote STANDARD [Shift]",
                    "Keypress: ~ Backquote STANDARD 126 [Shift]",
                    "Keyup: ~ Backquote STANDARD [Shift]",
                    "Keyup: Shift ShiftLeft LEFT []",
                })));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should support multiple plus-separated modifiers")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportMultiplePlusSeparatedModifiers()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/keyboard.html").ConfigureAwait(false);
            await page.Keyboard.PressAsync("Control+Shift+~").ConfigureAwait(false);
            Assert.That(
                await GetResultAsync(page).ConfigureAwait(false),
                Is.EqualTo(string.Join("\n", new[]
                {
                    "Keydown: Control ControlLeft LEFT [Control]",
                    "Keydown: Shift ShiftLeft LEFT [Control Shift]",
                    "Keydown: ~ Backquote STANDARD [Control Shift]",
                    "Keyup: ~ Backquote STANDARD [Control Shift]",
                    "Keyup: Shift ShiftLeft LEFT [Control]",
                    "Keyup: Control ControlLeft LEFT []",
                })));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should shift raw codes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldShiftRawCodes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/keyboard.html").ConfigureAwait(false);
            await page.Keyboard.PressAsync("Shift+Digit3").ConfigureAwait(false);
            Assert.That(
                await GetResultAsync(page).ConfigureAwait(false),
                Is.EqualTo(string.Join("\n", new[]
                {
                    "Keydown: Shift ShiftLeft LEFT [Shift]",
                    "Keydown: # Digit3 STANDARD [Shift]",
                    "Keypress: # Digit3 STANDARD 35 [Shift]",
                    "Keyup: # Digit3 STANDARD [Shift]",
                    "Keyup: Shift ShiftLeft LEFT []",
                })));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should specify repeat property")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSpecifyRepeatProperty()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            await page.FocusAsync("textarea").ConfigureAwait(false);
            IJSHandle lastEvent = await CaptureLastKeydownAsync(page).ConfigureAwait(false);
            await page.Keyboard.DownAsync("a").ConfigureAwait(false);
            Assert.That(await lastEvent.EvaluateAsync<bool>("e => e.repeat").ConfigureAwait(false), Is.False);
            await page.Keyboard.PressAsync("a").ConfigureAwait(false);
            Assert.That(await lastEvent.EvaluateAsync<bool>("e => e.repeat").ConfigureAwait(false), Is.True);

            await page.Keyboard.DownAsync("b").ConfigureAwait(false);
            Assert.That(await lastEvent.EvaluateAsync<bool>("e => e.repeat").ConfigureAwait(false), Is.False);
            await page.Keyboard.DownAsync("b").ConfigureAwait(false);
            Assert.That(await lastEvent.EvaluateAsync<bool>("e => e.repeat").ConfigureAwait(false), Is.True);

            await page.Keyboard.UpAsync("a").ConfigureAwait(false);
            await page.Keyboard.DownAsync("a").ConfigureAwait(false);
            Assert.That(await lastEvent.EvaluateAsync<bool>("e => e.repeat").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should type all kinds of characters")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTypeAllKindsOfCharacters()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            await page.FocusAsync("textarea").ConfigureAwait(false);
            string text = "This text goes onto two lines.\nThis character is 嗨.";
            await page.Keyboard.TypeAsync(text).ConfigureAwait(false);
            Assert.That(
                await page.EvalOnSelectorAsync<string>("textarea", "t => t.value").ConfigureAwait(false),
                Is.EqualTo(text));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should specify location")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSpecifyLocation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            IJSHandle lastEvent = await CaptureLastKeydownAsync(page).ConfigureAwait(false);
            IElementHandle textarea = await page.QuerySelectorAsync("textarea").ConfigureAwait(false);

            await textarea.PressAsync("Digit5").ConfigureAwait(false);
            Assert.That(await lastEvent.EvaluateAsync<int>("e => e.location").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await lastEvent.EvaluateAsync<string>("e => e.key").ConfigureAwait(false), Is.EqualTo("5"));
            Assert.That(await lastEvent.EvaluateAsync<string>("e => e.code").ConfigureAwait(false), Is.EqualTo("Digit5"));

            await textarea.PressAsync("ControlLeft").ConfigureAwait(false);
            Assert.That(await lastEvent.EvaluateAsync<int>("e => e.location").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await lastEvent.EvaluateAsync<string>("e => e.key").ConfigureAwait(false), Is.EqualTo("Control"));
            Assert.That(await lastEvent.EvaluateAsync<string>("e => e.code").ConfigureAwait(false), Is.EqualTo("ControlLeft"));

            await textarea.PressAsync("ControlRight").ConfigureAwait(false);
            Assert.That(await lastEvent.EvaluateAsync<int>("e => e.location").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await lastEvent.EvaluateAsync<string>("e => e.key").ConfigureAwait(false), Is.EqualTo("Control"));
            Assert.That(await lastEvent.EvaluateAsync<string>("e => e.code").ConfigureAwait(false), Is.EqualTo("ControlRight"));

            await textarea.PressAsync("NumpadSubtract").ConfigureAwait(false);
            Assert.That(await lastEvent.EvaluateAsync<int>("e => e.location").ConfigureAwait(false), Is.EqualTo(3));
            Assert.That(await lastEvent.EvaluateAsync<string>("e => e.key").ConfigureAwait(false), Is.EqualTo("-"));
            Assert.That(await lastEvent.EvaluateAsync<string>("e => e.code").ConfigureAwait(false), Is.EqualTo("NumpadSubtract"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should press Enter")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldPressEnter()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.SetContentAsync("<textarea></textarea>").ConfigureAwait(false);
            await page.FocusAsync("textarea").ConfigureAwait(false);
            IJSHandle lastEventHandle = await CaptureLastKeydownAsync(page).ConfigureAwait(false);
            await TestEnterKeyAsync(page, lastEventHandle, "Enter", "Enter", "Enter").ConfigureAwait(false);
            await TestEnterKeyAsync(page, lastEventHandle, "NumpadEnter", "Enter", "NumpadEnter").ConfigureAwait(false);
            await TestEnterKeyAsync(page, lastEventHandle, "\n", "Enter", "Enter").ConfigureAwait(false);
            await TestEnterKeyAsync(page, lastEventHandle, "\r", "Enter", "Enter").ConfigureAwait(false);
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should press audio and media control keys")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldPressAudioAndMediaControlKeys()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.SetContentAsync("<input autofocus>").ConfigureAwait(false);
            await page.FocusAsync("input").ConfigureAwait(false);
            IJSHandle lastEvent = await CaptureLastKeydownAsync(page).ConfigureAwait(false);
            (string Key, string Code)[] mediaKeys =
            {
                ("AudioVolumeMute", TestConstants.IsFirefox ? "VolumeMute" : "AudioVolumeMute"),
                ("AudioVolumeDown", TestConstants.IsFirefox ? "VolumeDown" : "AudioVolumeDown"),
                ("AudioVolumeUp", TestConstants.IsFirefox ? "VolumeUp" : "AudioVolumeUp"),
                ("MediaTrackNext", "MediaTrackNext"),
                ("MediaTrackPrevious", "MediaTrackPrevious"),
                ("MediaPlayPause", "MediaPlayPause"),
            };

            foreach ((string Key, string Code) mediaKey in mediaKeys)
            {
                await page.Keyboard.PressAsync(mediaKey.Key).ConfigureAwait(false);
                Assert.That(await lastEvent.EvaluateAsync<string>("e => e.key").ConfigureAwait(false), Is.EqualTo(mediaKey.Key));
                Assert.That(await lastEvent.EvaluateAsync<string>("e => e.code").ConfigureAwait(false), Is.EqualTo(mediaKey.Code));
                Assert.That(await lastEvent.EvaluateAsync<int>("e => e.location").ConfigureAwait(false), Is.EqualTo(0));
            }
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should throw on unknown keys")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowOnUnknownKeys()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            Exception error = await CatchAsync(() => page.Keyboard.PressAsync("NotARealKey")).ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("Unknown key: \"NotARealKey\""));

            error = await CatchAsync(() => page.Keyboard.PressAsync("ё")).ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("Unknown key: \"ё\""));

            error = await CatchAsync(() => page.Keyboard.PressAsync("😊")).ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("Unknown key: \"😊\""));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should type emoji")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTypeEmoji()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            await page.TypeAsync("textarea", "👹 Tokyo street Japan 🇯🇵").ConfigureAwait(false);
            Assert.That(
                await page.EvalOnSelectorAsync<string>("textarea", "textarea => textarea.value").ConfigureAwait(false),
                Is.EqualTo("👹 Tokyo street Japan 🇯🇵"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should type emoji into an iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTypeEmojiIntoAnIframe()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame frame = await AttachFrameAsync(page, "emoji-test", Prefix + "/input/textarea.html").ConfigureAwait(false);
            IElementHandle textarea = await frame.QuerySelectorAsync("textarea").ConfigureAwait(false);
            await textarea.TypeAsync("👹 Tokyo street Japan 🇯🇵").ConfigureAwait(false);
            Assert.That(
                await frame.EvalOnSelectorAsync<string>("textarea", "textarea => textarea.value").ConfigureAwait(false),
                Is.EqualTo("👹 Tokyo street Japan 🇯🇵"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should handle selectAll")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHandleSelectAll()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            IElementHandle textarea = await page.QuerySelectorAsync("textarea").ConfigureAwait(false);
            await textarea.TypeAsync("some text").ConfigureAwait(false);
            await page.Keyboard.DownAsync("ControlOrMeta").ConfigureAwait(false);
            await page.Keyboard.PressAsync("a").ConfigureAwait(false);
            await page.Keyboard.UpAsync("ControlOrMeta").ConfigureAwait(false);
            await page.Keyboard.PressAsync("Backspace").ConfigureAwait(false);
            Assert.That(
                await page.EvalOnSelectorAsync<string>("textarea", "textarea => textarea.value").ConfigureAwait(false),
                Is.EqualTo(string.Empty));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "pressing Meta should not result in any text insertion on any platform")]
        [Test]
        [Timeout(30_000)]
        public async Task PressingMetaShouldNotResultInAnyTextInsertionOnAnyPlatform()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.SetContentAsync("<input type=\"text\" value=\"hello world\">").ConfigureAwait(false);
            ILocator input = page.Locator("input");
            Assert.That(await input.InputValueAsync().ConfigureAwait(false), Is.EqualTo("hello world"));
            await input.FocusAsync().ConfigureAwait(false);
            await page.Keyboard.PressAsync("Meta").ConfigureAwait(false);
            Assert.That(await input.InputValueAsync().ConfigureAwait(false), Is.EqualTo("hello world"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should be able to prevent selectAll")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldBeAbleToPreventSelectAll()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            IElementHandle textarea = await page.QuerySelectorAsync("textarea").ConfigureAwait(false);
            await textarea.TypeAsync("some text").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("textarea", @"textarea => {
                textarea.addEventListener('keydown', event => {
                    if (event.key === 'a' && (event.metaKey || event.ctrlKey))
                        event.preventDefault();
                }, false);
            }").ConfigureAwait(false);
            await page.Keyboard.DownAsync("ControlOrMeta").ConfigureAwait(false);
            await page.Keyboard.PressAsync("a").ConfigureAwait(false);
            await page.Keyboard.UpAsync("ControlOrMeta").ConfigureAwait(false);
            await page.Keyboard.PressAsync("Backspace").ConfigureAwait(false);
            Assert.That(
                await page.EvalOnSelectorAsync<string>("textarea", "textarea => textarea.value").ConfigureAwait(false),
                Is.EqualTo("some tex"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should support MacOS shortcuts")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportMacOSShortcuts()
        {
            if (!TestConstants.IsMacOSX)
            {
                Assert.Ignore("MacOS only");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            IElementHandle textarea = await page.QuerySelectorAsync("textarea").ConfigureAwait(false);
            await textarea.TypeAsync("some text").ConfigureAwait(false);
            await page.Keyboard.PressAsync("Shift+Control+Alt+KeyB").ConfigureAwait(false);
            await page.Keyboard.PressAsync("Backspace").ConfigureAwait(false);
            Assert.That(
                await page.EvalOnSelectorAsync<string>("textarea", "textarea => textarea.value").ConfigureAwait(false),
                Is.EqualTo("some "));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should press the meta key")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldPressTheMetaKey()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            IJSHandle lastEvent = await CaptureLastKeydownAsync(page).ConfigureAwait(false);
            await page.Keyboard.PressAsync("Meta").ConfigureAwait(false);
            Assert.That(await lastEvent.EvaluateAsync<string>("e => e.key").ConfigureAwait(false), Is.EqualTo("Meta"));
            Assert.That(await lastEvent.EvaluateAsync<string>("e => e.code").ConfigureAwait(false), Is.EqualTo("MetaLeft"));
            Assert.That(await lastEvent.EvaluateAsync<bool>("e => e.metaKey").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should work with keyboard events with empty.html")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithKeyboardEventsWithEmptyHtml()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            IJSHandle lastEvent = await CaptureLastKeydownAsync(page).ConfigureAwait(false);
            await page.Keyboard.PressAsync("a").ConfigureAwait(false);
            Assert.That(await lastEvent.EvaluateAsync<string>("l => l.key").ConfigureAwait(false), Is.EqualTo("a"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should work after a cross origin navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkAfterACrossOriginNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            await page.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            IJSHandle lastEvent = await CaptureLastKeydownAsync(page).ConfigureAwait(false);
            await page.Keyboard.PressAsync("a").ConfigureAwait(false);
            Assert.That(await lastEvent.EvaluateAsync<string>("l => l.key").ConfigureAwait(false), Is.EqualTo("a"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should expose keyIdentifier in webkit")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldExposeKeyIdentifierInWebkit()
        {
            if (!TestConstants.IsWebKit)
            {
                Assert.Ignore("event.keyIdentifier has been removed from all browsers except WebKit");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            IJSHandle lastEvent = await CaptureLastKeydownAsync(page).ConfigureAwait(false);
            (string Key, string Identifier)[] keyMap =
            {
                ("ArrowUp", "Up"),
                ("ArrowDown", "Down"),
                ("ArrowLeft", "Left"),
                ("ArrowRight", "Right"),
                ("Backspace", "U+0008"),
                ("Tab", "U+0009"),
                ("Delete", "U+007F"),
                ("a", "U+0041"),
                ("b", "U+0042"),
                ("F12", "F12"),
            };
            foreach ((string Key, string Identifier) entry in keyMap)
            {
                await page.Keyboard.PressAsync(entry.Key).ConfigureAwait(false);
                Assert.That(
                    await lastEvent.EvaluateAsync<string>("e => e.keyIdentifier").ConfigureAwait(false),
                    Is.EqualTo(entry.Identifier));
            }
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should scroll with PageDown")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldScrollWithPageDown()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/scrollable.html").ConfigureAwait(false);
            await page.ClickAsync("body").ConfigureAwait(false);
            await page.Keyboard.PressAsync("PageDown").ConfigureAwait(false);
            await PollAsync(async () => await page.EvaluateAsync<double>("scrollY").ConfigureAwait(false) > 0).ConfigureAwait(false);
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should move around the selection in a contenteditable")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldMoveAroundTheSelectionInAContenteditable()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.SetContentAsync("<div contenteditable></div>").ConfigureAwait(false);
            await page.FocusAsync("div").ConfigureAwait(false);
            string modifier = TestConstants.IsMacOSX ? "Alt" : "Control";
            await page.Keyboard.TypeAsync("Hello World").ConfigureAwait(false);
            await page.Keyboard.DownAsync(modifier).ConfigureAwait(false);
            await page.Keyboard.DownAsync("Shift").ConfigureAwait(false);
            await page.Keyboard.PressAsync("ArrowLeft").ConfigureAwait(false);
            await page.Keyboard.UpAsync("Shift").ConfigureAwait(false);
            await page.Keyboard.UpAsync(modifier).ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("window.getSelection().toString()").ConfigureAwait(false),
                Is.EqualTo("World"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should move to the start of the document")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldMoveToTheStartOfTheDocument()
        {
            if (!TestConstants.IsMacOSX)
            {
                Assert.Ignore("MacOS only");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.SetContentAsync("<div contenteditable></div>").ConfigureAwait(false);
            await page.FocusAsync("div").ConfigureAwait(false);
            await page.Keyboard.TypeAsync("1\n2\n3\n").ConfigureAwait(false);
            await page.Keyboard.PressAsync("Shift+Meta+ArrowUp").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("window.getSelection().toString()").ConfigureAwait(false),
                Is.EqualTo("1\n2\n3\n"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should dispatch a click event on a button when Space gets pressed")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchAClickEventOnAButtonWhenSpaceGetsPressed()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.SetContentAsync("<button type=\"button\">a11y</button>").ConfigureAwait(false);
            IJSHandle actual = await page.EvaluateHandleAsync(@"() => {
                const actual = { clicked: false };
                document.querySelector('button').addEventListener('click', () => (actual.clicked = true));
                return actual;
            }").ConfigureAwait(false);
            await page.FocusAsync("button").ConfigureAwait(false);
            await page.Keyboard.PressAsync("Space").ConfigureAwait(false);
            Assert.That(await actual.EvaluateAsync<bool>("a => a.clicked").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should dispatch a click event on a button when Enter gets pressed")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchAClickEventOnAButtonWhenEnterGetsPressed()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.SetContentAsync("<button type=\"button\">a11y</button>").ConfigureAwait(false);
            IJSHandle actual = await page.EvaluateHandleAsync(@"() => {
                const actual = { clicked: false };
                document.querySelector('button').addEventListener('click', () => (actual.clicked = true));
                return actual;
            }").ConfigureAwait(false);
            await page.FocusAsync("button").ConfigureAwait(false);
            await page.Keyboard.PressAsync("Enter").ConfigureAwait(false);
            Assert.That(await actual.EvaluateAsync<bool>("a => a.clicked").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should support simple copy-pasting")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportSimpleCopyPasting()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await context.GrantPermissionsAsync(new[] { ContextPermissions.ClipboardRead, ContextPermissions.ClipboardWrite }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div contenteditable>123</div>").ConfigureAwait(false);
            await page.FocusAsync("div").ConfigureAwait(false);
            await page.Keyboard.PressAsync("ControlOrMeta+KeyA").ConfigureAwait(false);
            await page.Keyboard.PressAsync("ControlOrMeta+KeyC").ConfigureAwait(false);
            await page.Keyboard.PressAsync("ControlOrMeta+KeyV").ConfigureAwait(false);
            await page.Keyboard.PressAsync("ControlOrMeta+KeyV").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("document.querySelector('div').textContent").ConfigureAwait(false),
                Is.EqualTo("123123"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should support simple cut-pasting")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportSimpleCutPasting()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await context.GrantPermissionsAsync(new[] { ContextPermissions.ClipboardRead, ContextPermissions.ClipboardWrite }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div contenteditable>123</div>").ConfigureAwait(false);
            await page.FocusAsync("div").ConfigureAwait(false);
            await page.Keyboard.PressAsync("ControlOrMeta+KeyA").ConfigureAwait(false);
            await page.Keyboard.PressAsync("ControlOrMeta+KeyX").ConfigureAwait(false);
            await page.Keyboard.PressAsync("ControlOrMeta+KeyV").ConfigureAwait(false);
            await page.Keyboard.PressAsync("ControlOrMeta+KeyV").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("document.querySelector('div').textContent").ConfigureAwait(false),
                Is.EqualTo("123123"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should support undo-redo")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportUndoRedo()
        {
            if (TestConstants.IsWebKit && !TestConstants.IsWindows && !TestConstants.IsMacOSX)
            {
                Assert.Ignore("https://github.com/microsoft/playwright/issues/12000");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.SetContentAsync("<div contenteditable></div>").ConfigureAwait(false);
            ILocator div = page.Locator("div");
            Assert.That(await div.TextContentAsync().ConfigureAwait(false), Is.EqualTo(string.Empty));
            await div.TypeAsync("123").ConfigureAwait(false);
            Assert.That(await div.TextContentAsync().ConfigureAwait(false), Is.EqualTo("123"));
            await page.Keyboard.PressAsync("ControlOrMeta+KeyZ").ConfigureAwait(false);
            Assert.That(await div.TextContentAsync().ConfigureAwait(false), Is.EqualTo(string.Empty));
            await page.Keyboard.PressAsync("Shift+ControlOrMeta+KeyZ").ConfigureAwait(false);
            Assert.That(await div.TextContentAsync().ConfigureAwait(false), Is.EqualTo("123"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should type repeatedly in contenteditable in shadow dom")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTypeRepeatedlyInContenteditableInShadowDom()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <html>
      <body>
        <shadow-element></shadow-element>
        <script>
          customElements.define('shadow-element', class extends HTMLElement {
            constructor() {
              super();
              this.attachShadow({ mode: 'open' });
            }

            connectedCallback() {
              this.shadowRoot.innerHTML = `
                <style>
                  .editor { padding: 1rem; margin: 1rem; border: 1px solid #ccc; }
                </style>
                <div class=editor contenteditable id=foo></div>
                <hr>
                <section>
                  <div class=editor contenteditable id=bar></div>
                </section>
              `;
            }
          });
        </script>
      </body>
    </html>
  ").ConfigureAwait(false);

            await WaitForShadowElementAsync(page).ConfigureAwait(false);
            ILocator editor = page.Locator("shadow-element > .editor").First;
            await editor.TypeAsync("This is the first box.").ConfigureAwait(false);

            ILocator sectionEditor = page.Locator("section .editor");
            await sectionEditor.TypeAsync("This is the second box.").ConfigureAwait(false);

            Assert.That(await editor.TextContentAsync().ConfigureAwait(false), Is.EqualTo("This is the first box."));
            Assert.That(await sectionEditor.TextContentAsync().ConfigureAwait(false), Is.EqualTo("This is the second box."));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should type repeatedly in contenteditable in shadow dom with nested elements")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTypeRepeatedlyInContenteditableInShadowDomWithNestedElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <html>
      <body>
        <shadow-element></shadow-element>
        <script>
          customElements.define('shadow-element', class extends HTMLElement {
            constructor() {
              super();
              this.attachShadow({ mode: 'open' });
            }

            connectedCallback() {
              this.shadowRoot.innerHTML = `
                <style>
                  .editor { padding: 1rem; margin: 1rem; border: 1px solid #ccc; }
                </style>
                <div class=editor contenteditable id=foo><p>hello</p></div>
                <hr>
                <section>
                  <div class=editor contenteditable id=bar><p>world</p></div>
                </section>
              `;
            }
          });
        </script>
      </body>
    </html>
  ").ConfigureAwait(false);

            await WaitForShadowElementAsync(page).ConfigureAwait(false);
            ILocator editor = page.Locator("shadow-element > .editor").First;
            await editor.TypeAsync("This is the first box: ").ConfigureAwait(false);

            ILocator sectionEditor = page.Locator("section .editor");
            await sectionEditor.TypeAsync("This is the second box: ").ConfigureAwait(false);

            Assert.That(await editor.TextContentAsync().ConfigureAwait(false), Is.EqualTo("This is the first box: hello"));
            Assert.That(await sectionEditor.TextContentAsync().ConfigureAwait(false), Is.EqualTo("This is the second box: world"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should type repeatedly in input in shadow dom")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTypeRepeatedlyInInputInShadowDom()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <html>
      <body>
        <shadow-element></shadow-element>
        <script>
          customElements.define('shadow-element', class extends HTMLElement {
            constructor() {
              super();
              this.attachShadow({ mode: 'open' });
            }

            connectedCallback() {
              this.shadowRoot.innerHTML = `
                <style>
                  .editor { padding: 1rem; margin: 1rem; border: 1px solid #ccc; }
                </style>
                <input class=editor id=foo>
                <hr>
                <section>
                  <input class=editor id=bar>
                </section>
              `;
            }
          });
        </script>
      </body>
    </html>
  ").ConfigureAwait(false);

            await WaitForShadowElementAsync(page).ConfigureAwait(false);
            ILocator editor = page.Locator("shadow-element > .editor").First;
            await editor.TypeAsync("This is the first box.").ConfigureAwait(false);

            ILocator sectionEditor = page.Locator("section .editor");
            await sectionEditor.TypeAsync("This is the second box.").ConfigureAwait(false);

            Assert.That(await editor.InputValueAsync().ConfigureAwait(false), Is.EqualTo("This is the first box."));
            Assert.That(await sectionEditor.InputValueAsync().ConfigureAwait(false), Is.EqualTo("This is the second box."));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "type to non-focusable element should maintain old focus")]
        [Test]
        [Timeout(30_000)]
        public async Task TypeToNonFocusableElementShouldMaintainOldFocus()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div id=""focusable"" tabindex=""0"">focusable div</div>
    <div id=""non-focusable-and-non-editable"">non-editable, non-focusable</div>
  ").ConfigureAwait(false);

            await page.Locator("#focusable").FocusAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement?.id").ConfigureAwait(false), Is.EqualTo("focusable"));
            await page.Locator("#non-focusable-and-non-editable").TypeAsync("foo").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement?.id").ConfigureAwait(false), Is.EqualTo("focusable"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should dispatch insertText after context menu was opened")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchInsertTextAfterContextMenuWasOpened()
        {
            if (TestConstants.IsChromium && TestConstants.IsWindows)
            {
                Assert.Ignore("context menu support is best-effort for Linux and MacOS");
            }

            if (TestConstants.IsWebKit && TestConstants.IsWindows)
            {
                Assert.Ignore("context menu support is best-effort for Linux and MacOS");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            await page.EvaluateAsync<object>(@"(() => {
                window['contextMenuPromise'] = new Promise(x => {
                    window.addEventListener('contextmenu', x, false);
                });
            })()").ConfigureAwait(false);

            var box = await page.Locator("textarea").BoundingBoxAsync().ConfigureAwait(false);
            float cx = box.X + (box.Width / 2);
            float cy = box.Y + (box.Height / 2);
            await page.Mouse.ClickAsync(cx, cy, new() { Button = MouseButton.Right }).ConfigureAwait(false);
            await page.EvaluateAsync<object>("window['contextMenuPromise']").ConfigureAwait(false);

            await page.Keyboard.InsertTextAsync("嗨").ConfigureAwait(false);
            await PollAsync(async () =>
                string.Equals(await page.Locator("textarea").InputValueAsync().ConfigureAwait(false), "嗨", StringComparison.Ordinal)).ConfigureAwait(false);
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should type after context menu was opened")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTypeAfterContextMenuWasOpened()
        {
            if (TestConstants.IsChromium && TestConstants.IsWindows)
            {
                Assert.Ignore("context menu support is best-effort for Linux and MacOS");
            }

            if (TestConstants.IsWebKit && TestConstants.IsWindows)
            {
                Assert.Ignore("context menu support is best-effort for Linux and MacOS");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.EvaluateAsync<object>(@"(() => {
                window['keys'] = [];
                window.addEventListener('keydown', event => window['keys'].push(event.key));
                window['contextMenuPromise'] = new Promise(x => {
                    window.addEventListener('contextmenu', x, false);
                });
            })()").ConfigureAwait(false);

            await page.Mouse.MoveAsync(100, 100).ConfigureAwait(false);
            await page.Mouse.DownAsync(MouseButton.Right).ConfigureAwait(false);
            await page.EvaluateAsync<object>("window['contextMenuPromise']").ConfigureAwait(false);

            await page.Keyboard.DownAsync("ArrowDown").ConfigureAwait(false);

            await PollAsync(async () =>
            {
                string[] keys = await page.EvaluateAsync<string[]>("window.keys").ConfigureAwait(false);
                return keys != null && keys.Length == 1 && string.Equals(keys[0], "ArrowDown", StringComparison.Ordinal);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should have correct Keydown/Keyup order when pressing Escape key")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHaveCorrectKeydownKeyupOrderWhenPressingEscapeKey()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/keyboard.html").ConfigureAwait(false);
            await page.Keyboard.PressAsync("Escape").ConfigureAwait(false);
            Assert.That(
                await GetResultAsync(page).ConfigureAwait(false),
                Is.EqualTo("Keydown: Escape Escape STANDARD []\nKeyup: Escape Escape STANDARD []"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should close dialog on Escape key press in contenteditable")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldCloseDialogOnEscapeKeyPressInContenteditable()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(browser).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <dialog>
      <div contenteditable>Edit Me</div>
    </dialog>
  ").ConfigureAwait(false);

            ILocator dialog = page.Locator("dialog");
            ILocator widget = dialog.Locator("[contenteditable]");
            await dialog.EvaluateAsync<object>("(node) => node.showModal()").ConfigureAwait(false);
            Assert.That(await dialog.EvaluateAsync<bool>("el => el.open").ConfigureAwait(false), Is.True);
            Assert.That(await widget.IsVisibleAsync().ConfigureAwait(false), Is.True);

            await widget.PressAsync("Escape").ConfigureAwait(false);
            Assert.That(await dialog.EvaluateAsync<bool>("el => el.open").ConfigureAwait(false), Is.False);
            Assert.That(await widget.IsVisibleAsync().ConfigureAwait(false), Is.False);
        }
    }
}
