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
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-mouse.spec.ts</c> parity for <see cref="IMouse"/>.
    /// </summary>
    [TestFixture]
    public class PageMouseTests : PageTestEx
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

        private static void SkipIfHeaded(string reason)
        {
            if (!IsHeadless)
            {
                Assert.Ignore(reason);
            }
        }

        private static Task<IBrowser> LaunchAsync()
            => BrowserLauncher.LaunchAsync(headless: IsHeadless);

        private static Task RafrafAsync(IPage page)
            => page.EvaluateAsync<object>("new Promise(r => requestAnimationFrame(() => r(true)))");

        private static async Task AssertLogEqualsAsync(IPage page, params string[] expected)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            string[] actual = Array.Empty<string>();
            while (DateTime.UtcNow < deadline)
            {
                actual = await page.EvaluateAsync<string[]>("window.__log").ConfigureAwait(false)
                    ?? Array.Empty<string>();
                if (actual.SequenceEqual(expected))
                {
                    return;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.That(actual, Is.EqualTo(expected));
        }

        private static void AssertMouseButtons(JsonElement evt, string type, int button, int buttons)
        {
            Assert.That(evt.GetProperty("type").GetString(), Is.EqualTo(type));
            Assert.That(evt.GetProperty("button").GetInt32(), Is.EqualTo(button));
            Assert.That(evt.GetProperty("buttons").GetInt32(), Is.EqualTo(buttons));
        }

        private static void AssertPointerType(JsonElement evt, string type, string pointerType)
        {
            Assert.That(evt.GetProperty("type").GetString(), Is.EqualTo(type));
            Assert.That(evt.GetProperty("pointerType").GetString(), Is.EqualTo(pointerType));
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
            int basePort = 19112;
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

        [PlaywrightTest("page-mouse.spec.ts", "should click the document")]
        [PlaywrightTest("page-mouse.spec.ts", "should click the document @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickTheDocument()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            await page.EvaluateAsync(@"(() => {
                window['clickPromise'] = new Promise(resolve => {
                    document.addEventListener('click', event => {
                        resolve({
                            type: event.type,
                            detail: event.detail,
                            clientX: event.clientX,
                            clientY: event.clientY,
                            isTrusted: event.isTrusted,
                            button: event.button
                        });
                    });
                });
            })()").ConfigureAwait(false);
            await page.Mouse.ClickAsync(50, 60).ConfigureAwait(false);
            JsonElement evt = await page.EvaluateAsync<JsonElement>("window['clickPromise']").ConfigureAwait(false);
            Assert.That(evt.GetProperty("type").GetString(), Is.EqualTo("click"));
            Assert.That(evt.GetProperty("detail").GetInt32(), Is.EqualTo(1));
            Assert.That(evt.GetProperty("clientX").GetInt32(), Is.EqualTo(50));
            Assert.That(evt.GetProperty("clientY").GetInt32(), Is.EqualTo(60));
            Assert.That(evt.GetProperty("isTrusted").GetBoolean(), Is.True);
            Assert.That(evt.GetProperty("button").GetInt32(), Is.EqualTo(0));
        }

        [PlaywrightTest("page-mouse.spec.ts", "should dblclick the div")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDblclickTheDiv()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div style='width: 100px; height: 100px;'>Click me</div>").ConfigureAwait(false);
            await page.EvaluateAsync(@"(() => {
                window['dblclickPromise'] = new Promise(resolve => {
                    document.querySelector('div').addEventListener('dblclick', event => {
                        resolve({
                            type: event.type,
                            detail: event.detail,
                            clientX: event.clientX,
                            clientY: event.clientY,
                            isTrusted: event.isTrusted,
                            button: event.button,
                        });
                    });
                });
            })()").ConfigureAwait(false);
            await page.Mouse.DblClickAsync(50, 60).ConfigureAwait(false);
            JsonElement evt = await page.EvaluateAsync<JsonElement>("window['dblclickPromise']").ConfigureAwait(false);
            Assert.That(evt.GetProperty("type").GetString(), Is.EqualTo("dblclick"));
            Assert.That(evt.GetProperty("detail").GetInt32(), Is.EqualTo(2));
            Assert.That(evt.GetProperty("clientX").GetInt32(), Is.EqualTo(50));
            Assert.That(evt.GetProperty("clientY").GetInt32(), Is.EqualTo(60));
            Assert.That(evt.GetProperty("isTrusted").GetBoolean(), Is.True);
            Assert.That(evt.GetProperty("button").GetInt32(), Is.EqualTo(0));
        }

        [PlaywrightTest("page-mouse.spec.ts", "down and up should generate click")]
        [Test]
        [Timeout(30_000)]
        public async Task DownAndUpShouldGenerateClick()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            await page.EvaluateAsync(@"(() => {
                window['clickPromise'] = new Promise(resolve => {
                    document.addEventListener('click', event => {
                        resolve({
                            type: event.type,
                            detail: event.detail,
                            clientX: event.clientX,
                            clientY: event.clientY,
                            isTrusted: event.isTrusted,
                            button: event.button
                        });
                    });
                });
            })()").ConfigureAwait(false);
            await page.Mouse.MoveAsync(50, 60).ConfigureAwait(false);
            await page.Mouse.DownAsync().ConfigureAwait(false);
            await page.Mouse.UpAsync().ConfigureAwait(false);
            JsonElement evt = await page.EvaluateAsync<JsonElement>("window['clickPromise']").ConfigureAwait(false);
            Assert.That(evt.GetProperty("type").GetString(), Is.EqualTo("click"));
            Assert.That(evt.GetProperty("detail").GetInt32(), Is.EqualTo(1));
            Assert.That(evt.GetProperty("clientX").GetInt32(), Is.EqualTo(50));
            Assert.That(evt.GetProperty("clientY").GetInt32(), Is.EqualTo(60));
            Assert.That(evt.GetProperty("isTrusted").GetBoolean(), Is.True);
            Assert.That(evt.GetProperty("button").GetInt32(), Is.EqualTo(0));
        }

        [PlaywrightTest("page-mouse.spec.ts", "should pointerdown the div with a custom button")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldPointerdownTheDivWithACustomButton()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div style='width: 100px; height: 100px;'>Click me</div>").ConfigureAwait(false);
            await page.EvaluateAsync(@"(() => {
                window['pointerdownPromise'] = new Promise(resolve => {
                    document.querySelector('div').addEventListener('pointerdown', event => {
                        resolve({
                            type: event.type,
                            detail: event.detail,
                            clientX: event.clientX,
                            clientY: event.clientY,
                            isTrusted: event.isTrusted,
                            button: event.button,
                            buttons: event.buttons,
                            pointerId: event.pointerId,
                        });
                    });
                });
            })()").ConfigureAwait(false);
            await page.Mouse.ClickAsync(50, 60, button: MouseButton.Middle).ConfigureAwait(false);
            JsonElement evt = await page.EvaluateAsync<JsonElement>("window['pointerdownPromise']").ConfigureAwait(false);
            Assert.That(evt.GetProperty("type").GetString(), Is.EqualTo("pointerdown"));
            Assert.That(evt.GetProperty("detail").GetInt32(), Is.EqualTo(TestConstants.IsWebKit ? 1 : 0));
            Assert.That(evt.GetProperty("clientX").GetInt32(), Is.EqualTo(50));
            Assert.That(evt.GetProperty("clientY").GetInt32(), Is.EqualTo(60));
            Assert.That(evt.GetProperty("isTrusted").GetBoolean(), Is.True);
            Assert.That(evt.GetProperty("button").GetInt32(), Is.EqualTo(1));
            Assert.That(evt.GetProperty("buttons").GetInt32(), Is.EqualTo(4));
            Assert.That(evt.GetProperty("pointerId").GetInt32(), Is.EqualTo(TestConstants.IsFirefox ? 0 : 1));
        }

        [PlaywrightTest("page-mouse.spec.ts", "should report correct buttons property")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportCorrectButtonsProperty()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            await page.EvaluateAsync(@"(() => {
                window.__EVENTS = [];
                const handler = event => {
                    window.__EVENTS.push({
                        type: event.type,
                        button: event.button,
                        buttons: event.buttons,
                    });
                };
                window.addEventListener('mousedown', handler, false);
                window.addEventListener('mouseup', handler, false);
            })()").ConfigureAwait(false);
            await page.Mouse.MoveAsync(50, 60).ConfigureAwait(false);
            await page.Mouse.DownAsync(button: MouseButton.Middle).ConfigureAwait(false);
            await page.Mouse.DownAsync(button: MouseButton.Left).ConfigureAwait(false);
            await page.Mouse.UpAsync(button: MouseButton.Middle).ConfigureAwait(false);
            await page.Mouse.UpAsync(button: MouseButton.Left).ConfigureAwait(false);
            JsonElement events = await page.EvaluateAsync<JsonElement>("window.__EVENTS").ConfigureAwait(false);
            Assert.That(events.GetArrayLength(), Is.EqualTo(4));
            AssertMouseButtons(events[0], "mousedown", 1, 4);
            AssertMouseButtons(events[1], "mousedown", 0, 5);
            AssertMouseButtons(events[2], "mouseup", 1, 1);
            AssertMouseButtons(events[3], "mouseup", 0, 0);
        }

        [PlaywrightTest("page-mouse.spec.ts", "should report correct pointerType property")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportCorrectPointerTypeProperty()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            await page.Mouse.MoveAsync(50, 60).ConfigureAwait(false);
            await page.EvaluateAsync(@"(() => {
                window.__EVENTS = [];
                const handler = event => {
                    window.__EVENTS.push({
                        type: event.type,
                        pointerType: event.pointerType,
                    });
                };
                window.addEventListener('pointerdown', handler, false);
                window.addEventListener('pointermove', handler, false);
                window.addEventListener('pointerup', handler, false);
            })()").ConfigureAwait(false);
            await page.Mouse.MoveAsync(60, 50).ConfigureAwait(false);
            await page.Mouse.DownAsync().ConfigureAwait(false);
            await page.Mouse.UpAsync().ConfigureAwait(false);
            JsonElement events = await page.EvaluateAsync<JsonElement>("window.__EVENTS").ConfigureAwait(false);
            Assert.That(events.GetArrayLength(), Is.EqualTo(3));
            AssertPointerType(events[0], "pointermove", "mouse");
            AssertPointerType(events[1], "pointerdown", "mouse");
            AssertPointerType(events[2], "pointerup", "mouse");
        }

        [PlaywrightTest("page-mouse.spec.ts", "should select the text with mouse")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectTheTextWithMouse()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            await page.WaitForSelectorAsync("textarea").ConfigureAwait(false);
            await page.FocusAsync("textarea").ConfigureAwait(false);
            const string text = "This is the text that we are going to try to select. Let's see how it goes.";
            await page.Keyboard.TypeAsync(text).ConfigureAwait(false);
            await RafrafAsync(page).ConfigureAwait(false);
            await page.EvaluateAsync("document.querySelector('textarea').scrollTop = 0").ConfigureAwait(false);
            JsonElement dimensions = await page.EvaluateAsync<JsonElement>(@"(() => {
                const rect = document.querySelector('textarea').getBoundingClientRect();
                return { x: rect.left, y: rect.top, width: rect.width, height: rect.height };
            })()").ConfigureAwait(false);
            float x = dimensions.GetProperty("x").GetSingle();
            float y = dimensions.GetProperty("y").GetSingle();
            await page.Mouse.MoveAsync(x + 2, y + 2).ConfigureAwait(false);
            await page.Mouse.DownAsync().ConfigureAwait(false);
            await page.Mouse.MoveAsync(200, 200).ConfigureAwait(false);
            await page.Mouse.UpAsync().ConfigureAwait(false);
            string selected = await page.EvaluateAsync<string>(@"(() => {
                const textarea = document.querySelector('textarea');
                return textarea.value.substring(textarea.selectionStart, textarea.selectionEnd);
            })()").ConfigureAwait(false);
            Assert.That(selected, Is.EqualTo(text));
        }

        [PlaywrightTest("page-mouse.spec.ts", "should trigger hover state")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTriggerHoverState()
        {
            SkipIfHeaded("headed messes up with hover");
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/input/scrollable.html").ConfigureAwait(false);
            await page.HoverAsync("#button-6").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.querySelector('button:hover').id").ConfigureAwait(false), Is.EqualTo("button-6"));
            await page.HoverAsync("#button-2").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.querySelector('button:hover').id").ConfigureAwait(false), Is.EqualTo("button-2"));
            await page.HoverAsync("#button-91").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.querySelector('button:hover').id").ConfigureAwait(false), Is.EqualTo("button-91"));
        }

        [PlaywrightTest("page-mouse.spec.ts", "should trigger hover state on disabled button")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTriggerHoverStateOnDisabledButton()
        {
            SkipIfHeaded("headed messes up with hover");
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/input/scrollable.html").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("#button-6", "button => { button.disabled = true; }").ConfigureAwait(false);
            await page.HoverAsync("#button-6", new() { Timeout = 5000 }).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.querySelector('button:hover').id").ConfigureAwait(false), Is.EqualTo("button-6"));
        }

        [PlaywrightTest("page-mouse.spec.ts", "should trigger hover state with removed window.Node")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTriggerHoverStateWithRemovedWindowNode()
        {
            SkipIfHeaded("headed messes up with hover");
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/input/scrollable.html").ConfigureAwait(false);
            await page.EvaluateAsync("delete window.Node").ConfigureAwait(false);
            await page.HoverAsync("#button-6").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.querySelector('button:hover').id").ConfigureAwait(false), Is.EqualTo("button-6"));
        }

        [PlaywrightTest("page-mouse.spec.ts", "should set modifier keys on click")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSetModifierKeysOnClick()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/input/scrollable.html").ConfigureAwait(false);
            await page.EvaluateAsync("document.querySelector('#button-3').addEventListener('mousedown', e => window['lastEvent'] = e, true)").ConfigureAwait(false);
            Dictionary<string, string> modifiers = new Dictionary<string, string>
            {
                ["Shift"] = "shiftKey",
                ["Control"] = "ctrlKey",
                ["Alt"] = "altKey",
                ["Meta"] = "metaKey",
            };
            if (TestConstants.IsFirefox && !TestConstants.IsMacOSX)
            {
                modifiers.Remove("Meta");
            }

            foreach (KeyValuePair<string, string> modifier in modifiers)
            {
                await page.Keyboard.DownAsync(modifier.Key).ConfigureAwait(false);
                await page.ClickAsync("#button-3").ConfigureAwait(false);
                bool isSet = await page.EvaluateAsync<bool>("mod => window['lastEvent'][mod]", modifier.Value).ConfigureAwait(false);
                Assert.That(isSet, Is.True, modifier.Value + " should be true");
                await page.Keyboard.UpAsync(modifier.Key).ConfigureAwait(false);
            }

            await page.ClickAsync("#button-3").ConfigureAwait(false);
            foreach (KeyValuePair<string, string> modifier in modifiers)
            {
                bool isSet = await page.EvaluateAsync<bool>("mod => window['lastEvent'][mod]", modifier.Value).ConfigureAwait(false);
                Assert.That(isSet, Is.False, modifier.Value + " should be false");
            }
        }

        [PlaywrightTest("page-mouse.spec.ts", "should tween mouse movement")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTweenMouseMovement()
        {
            SkipIfHeaded("actual mouse interferes with the exact mousemove events");
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            if (TestConstants.IsWebKit)
            {
                await page.EvaluateAsync("new Promise(requestAnimationFrame)").ConfigureAwait(false);
            }

            await page.Mouse.MoveAsync(100, 100).ConfigureAwait(false);
            await page.EvaluateAsync(@"(() => {
                window['result'] = [];
                document.addEventListener('mousemove', event => {
                    window['result'].push([event.clientX, event.clientY]);
                });
            })()").ConfigureAwait(false);
            await page.Mouse.MoveAsync(200, 300, steps: 5).ConfigureAwait(false);
            int[][] result = await page.EvaluateAsync<int[][]>("window['result']").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(new[]
            {
                new[] { 120, 140 },
                new[] { 140, 180 },
                new[] { 160, 220 },
                new[] { 180, 260 },
                new[] { 200, 300 },
            }));
        }

        [PlaywrightTest("page-mouse.spec.ts", "should always round down")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAlwaysRoundDown()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            await page.EvaluateAsync(@"(() => {
                document.addEventListener('mousedown', event => {
                    window['result'] = [event.clientX, event.clientY];
                });
            })()").ConfigureAwait(false);
            await page.Mouse.ClickAsync(50.1f, 50.9f).ConfigureAwait(false);
            int[] result = await page.EvaluateAsync<int[]>("window['result']").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(new[] { 50, 50 }));
        }

        [PlaywrightTest("page-mouse.spec.ts", "should not crash on mouse drag with any button")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotCrashOnMouseDragWithAnyButton()
        {
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            await page.EvaluateAsync(@"(() => {
                window.addEventListener('contextmenu', e => e.preventDefault(), false);
            })()").ConfigureAwait(false);
            MouseButton[] buttons = new[] { MouseButton.Left, MouseButton.Middle, MouseButton.Right };
            foreach (MouseButton button in buttons)
            {
                await page.Mouse.MoveAsync(50, 50).ConfigureAwait(false);
                await page.Mouse.DownAsync(button: button).ConfigureAwait(false);
                await page.Mouse.MoveAsync(100, 100).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("page-mouse.spec.ts", "should dispatch mouse move after context menu was opened")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchMouseMoveAfterContextMenuWasOpened()
        {
            if (TestConstants.IsChromium && TestConstants.IsWindows)
            {
                Assert.Ignore("context menu support is best-effort for Linux and MacOS");
            }

            if (TestConstants.IsWebKit && TestConstants.IsWindows)
            {
                Assert.Ignore("WebKit on Windows does not dispatch move after context menu");
            }

            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            await page.EvaluateAsync(@"(() => {
                window['contextMenuPromise'] = new Promise(x => {
                    window.addEventListener('contextmenu', x, false);
                });
            })()").ConfigureAwait(false);
            const float cx = 100;
            const float cy = 100;
            await page.Mouse.MoveAsync(cx, cy).ConfigureAwait(false);
            await page.Mouse.DownAsync(button: MouseButton.Right).ConfigureAwait(false);
            await page.EvaluateAsync("window['contextMenuPromise']").ConfigureAwait(false);
            const int n = 20;
            int[] radii = new[] { 10, 30, 60, 90 };
            foreach (int radius in radii)
            {
                for (int i = 0; i < n; i++)
                {
                    double angle = 2 * Math.PI * i / n;
                    float x = cx + (float)Math.Round(radius * Math.Cos(angle));
                    float y = cy + (float)Math.Round(radius * Math.Sin(angle));
                    await page.Mouse.MoveAsync(x, y).ConfigureAwait(false);
                }
            }
        }

        [PlaywrightTest("page-mouse.spec.ts", "should track hover across iframe boundaries")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTrackHoverAcrossIframeBoundaries()
        {
            SkipIfHeaded("headed messes up with hover");
            await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <style>
      body, html { margin: 0; padding: 0; }
      #parentBox { position: absolute; left: 10px; top: 10px; width: 100px; height: 100px; }
      iframe { position: absolute; left: 200px; top: 10px; width: 200px; height: 200px; border: none; }
    </style>
    <div id=""parentBox""></div>
    <iframe srcdoc=""
      <style>body, html { margin: 0; padding: 0; } #childBox { width: 180px; height: 180px; }</style>
      <div id='childBox'></div>
      <script>
        const box = document.querySelector('#childBox');
        box.addEventListener('mouseenter', () => window.top.__log.push('child:enter'));
        box.addEventListener('mouseleave', () => window.top.__log.push('child:leave'));
      </script>
    ""></iframe>
    <script>
      window.__log = [];
      const box = document.querySelector('#parentBox');
      box.addEventListener('mouseenter', () => window.__log.push('parent:enter'));
      box.addEventListener('mouseleave', () => window.__log.push('parent:leave'));
    </script>
").ConfigureAwait(false);
            await page.WaitForSelectorAsync("iframe").ConfigureAwait(false);
            IFrame child = page.MainFrame.ChildFrames.FirstOrDefault();
            Assert.That(child, Is.Not.Null);
            await child.WaitForSelectorAsync("#childBox").ConfigureAwait(false);
            var parentBox = await page.Locator("#parentBox").BoundingBoxAsync().ConfigureAwait(false);
            var iframeBox = await page.Locator("iframe").BoundingBoxAsync().ConfigureAwait(false);
            Assert.That(parentBox, Is.Not.Null);
            Assert.That(iframeBox, Is.Not.Null);
            float parentX = parentBox.X + (parentBox.Width / 2);
            float parentY = parentBox.Y + (parentBox.Height / 2);
            float childX = iframeBox.X + 90;
            float childY = iframeBox.Y + 90;

            await page.Mouse.MoveAsync(parentX, parentY).ConfigureAwait(false);
            await AssertLogEqualsAsync(page, "parent:enter").ConfigureAwait(false);

            await page.Mouse.MoveAsync(childX, childY).ConfigureAwait(false);
            await AssertLogEqualsAsync(page, "parent:enter", "parent:leave", "child:enter").ConfigureAwait(false);

            await page.Mouse.MoveAsync(parentX, parentY).ConfigureAwait(false);
            await AssertLogEqualsAsync(page, "parent:enter", "parent:leave", "child:enter", "child:leave", "parent:enter").ConfigureAwait(false);
        }
    }
}
