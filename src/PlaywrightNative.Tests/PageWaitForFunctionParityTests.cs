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
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.Helpers;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-wait-for-function.spec.ts</c> parity for
    /// <see cref="IPage.WaitForFunctionAsync"/> and <see cref="IPage.WaitForTimeoutAsync"/>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class PageWaitForFunctionParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static async Task<IFrame> AttachFrameAsync(IPage page, string name, string url)
        {
            string nameJson = JsonSerializer.Serialize(name);
            string urlJson = JsonSerializer.Serialize(url);
            string script =
                "(() => { const f = document.createElement('iframe'); f.name = " +
                nameJson +
                "; f.id = " +
                nameJson +
                "; f.src = " +
                urlJson +
                "; document.body.appendChild(f); })()";
            await page.EvaluateAsync<object>(script).ConfigureAwait(false);

            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                IFrame named = page.Frame(name);
                if (named == null)
                {
                    foreach (IFrame frame in page.Frames)
                    {
                        if (!ReferenceEquals(frame, page.MainFrame))
                        {
                            named = frame;
                            break;
                        }
                    }
                }

                if (named != null)
                {
                    try
                    {
                        await named.WaitForLoadStateAsync(LoadState.Load, new() { Timeout = 5000 }).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                    }

                    return named;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.Fail("Timed out waiting for frame " + name);
            return null;
        }

        private static async Task DetachFrameAsync(IPage page, string name)
        {
            string nameJson = JsonSerializer.Serialize(name);
            await page.EvaluateAsync<object>(
                "(() => { const f = document.getElementById(" + nameJson + "); if (f) f.remove(); })()")
                .ConfigureAwait(false);
        }

        private static async Task<IPage> NewPageAsync(IBrowserContext context)
        {
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            return page;
        }

        private static IFrame ChildFrame(IPage page)
        {
            foreach (IFrame frame in page.Frames)
            {
                if (!ReferenceEquals(frame, page.MainFrame))
                {
                    return frame;
                }
            }

            Assert.Fail("Expected a child frame.");
            return null;
        }

        [SetUp]
        public void ResetWaitForFunctionAmbient()
        {
            WaitForFunctionHelper.SetAmbientTimeout(null);
            WaitForFunctionHelper.ClearPendingArg();
        }

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
            int basePort = 19739;
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

        [PlaywrightTest("page-wait-for-function.spec.ts", "should timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);

            Stopwatch sw = Stopwatch.StartNew();
            int timeout = 42;
            await page.WaitForTimeoutAsync(timeout).ConfigureAwait(false);
            sw.Stop();
            Assert.That(sw.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(timeout / 2));
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should accept a string")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAcceptAString()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);

            Task<IJSHandle> watchdog = page.WaitForFunctionAsync("window.__FOO === 1");
            await page.EvaluateAsync("(() => { window.__FOO = 1; })()").ConfigureAwait(false);
            await watchdog.ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should work when resolved right before execution context disposal")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWhenResolvedRightBeforeExecutionContextDisposal()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);

            await page.AddInitScriptAsync("window.__RELOADED = true").ConfigureAwait(false);
            await page.WaitForFunctionAsync(@"() => {
                if (!window.__RELOADED)
                    window.location.reload();
                return true;
            }").ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should poll on interval")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldPollOnInterval()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);

            int polling = 100;
            IJSHandle timeDelta = await page.WaitForFunctionAsync(
                @"() => {
                    if (!window.__startTime) {
                        window.__startTime = Date.now();
                        return false;
                    }
                    return Date.now() - window.__startTime;
                }",
                null,
                polling).ConfigureAwait(false);
            int value = await timeDelta.JsonValueAsync<int>().ConfigureAwait(false);
            Assert.That(value, Is.GreaterThanOrEqualTo(polling));
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should avoid side effects after timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAvoidSideEffectsAfterTimeout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);

            int counter = 0;
            page.Console += (_, _) => ++counter;

            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                async () => await page.WaitForFunctionAsync(
                    @"() => {
                        window.counter = (window.counter || 0) + 1;
                        console.log(window.counter);
                    }",
                    null,
                    1,
                    1000).ConfigureAwait(false));

            int savedCounter = counter;
            await page.WaitForTimeoutAsync(2000).ConfigureAwait(false);

            Assert.That(error.Message, Does.Contain("page.waitForFunction"));
            Assert.That(error.Message, Does.Contain("Timeout 1000ms exceeded"));
            Assert.That(counter, Is.EqualTo(savedCounter));
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should throw on polling:mutation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowOnPollingMutation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);

            PlaywrightNativeException error = Assert.ThrowsAsync<PlaywrightNativeException>(
                () => page.WaitForFunctionAsync("() => true", new { }, "mutation"));
            Assert.That(error.Message, Does.Contain("Unknown polling option: mutation"));
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should poll on raf")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldPollOnRaf()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);

            Task<IJSHandle> watchdog = page.WaitForFunctionAsync("() => window.__FOO === 'hit'");
            await page.EvaluateAsync("(() => { window.__FOO = 'hit'; })()").ConfigureAwait(false);
            await watchdog.ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should fail with predicate throwing on first call")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailWithPredicateThrowingOnFirstCall()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);

            PlaywrightNativeException error = Assert.ThrowsAsync<PlaywrightNativeException>(
                () => page.WaitForFunctionAsync("() => { throw new Error('oh my'); }"));
            Assert.That(error.Message, Does.Contain("oh my"));
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should fail with predicate throwing sometimes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailWithPredicateThrowingSometimes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);

            PlaywrightNativeException error = Assert.ThrowsAsync<PlaywrightNativeException>(
                () => page.WaitForFunctionAsync(@"() => {
                    window.counter = (window.counter || 0) + 1;
                    if (window.counter === 3)
                        throw new Error('Bad counter!');
                    return window.counter === 5 ? 'result' : false;
                }"));
            Assert.That(error.Message, Does.Contain("Bad counter!"));
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should fail with ReferenceError on wrong page")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailWithReferenceErrorOnWrongPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);

            PlaywrightNativeException error = Assert.ThrowsAsync<PlaywrightNativeException>(
                () => page.WaitForFunctionAsync("() => globalVar === 123"));
            Assert.That(error.Message, Does.Contain("globalVar"));
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should work with strict CSP policy")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithStrictCspPolicy()
        {
            EnsureServer();
            try
            {
                Server.SetCSP("/empty.html", "script-src " + Prefix);
            }
            catch (ArgumentException)
            {
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            Exception error = null;
            Task p = page.WaitForFunctionAsync("() => window.__FOO === 'hit'").ContinueWith(
                t =>
                {
                    if (t.IsFaulted)
                    {
                        error = t.Exception?.GetBaseException();
                    }
                },
                TaskScheduler.Default);
            await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            await page.EvaluateAsync("(() => { window.__FOO = 'hit'; })()").ConfigureAwait(false);
            await p.ConfigureAwait(false);
            Assert.That(error, Is.Null);
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should throw on bad polling value")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowOnBadPollingValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);

            PlaywrightNativeException error = Assert.ThrowsAsync<PlaywrightNativeException>(
                () => page.WaitForFunctionAsync("() => !!document.body", new { }, "unknown"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("polling"));
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should throw negative polling interval")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowNegativePollingInterval()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);

            PlaywrightNativeException error = Assert.ThrowsAsync<PlaywrightNativeException>(
                () => page.WaitForFunctionAsync("() => !!document.body", null, -10));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Cannot poll with non-positive interval"));
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should return the success value as a JSHandle")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnTheSuccessValueAsAJSHandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);

            IJSHandle handle = await page.WaitForFunctionAsync("() => 5").ConfigureAwait(false);
            Assert.That(await handle.JsonValueAsync<int>().ConfigureAwait(false), Is.EqualTo(5));
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should return the window as a success value")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnTheWindowAsASuccessValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);

            Assert.That(await page.WaitForFunctionAsync("() => window").ConfigureAwait(false), Is.Not.Null);
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should accept ElementHandle arguments")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAcceptElementHandleArguments()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            WaitForFunctionHelper.SetPendingArg(div);
            bool resolved = false;
            Task waitForFunction = page.WaitForFunctionAsync("element => !element.parentElement", div)
                .ContinueWith(_ => resolved = true, TaskScheduler.Default);
            Assert.That(resolved, Is.False);
            await page.EvaluateAsync("(() => { const el = document.querySelector('div'); if (el) el.remove(); })()").ConfigureAwait(false);
            await waitForFunction.ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should respect timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRespectTimeout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);

            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => page.WaitForFunctionAsync("false", null, timeout: 10));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("page.waitForFunction"));
            Assert.That(error.Message, Does.Contain("Timeout 10ms exceeded"));
            Assert.That(error, Is.InstanceOf<TimeoutException>());
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should respect default timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRespectDefaultTimeout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);
            page.SetDefaultTimeout(1);

            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => page.WaitForFunctionAsync("false"));
            Assert.That(error, Is.InstanceOf<TimeoutException>());
            Assert.That(error.Message, Does.Contain("page.waitForFunction"));
            Assert.That(error.Message, Does.Contain("Timeout 1ms exceeded"));
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should disable timeout when its set to 0")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDisableTimeoutWhenItsSetTo0()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);

            Task<IJSHandle> watchdog = page.WaitForFunctionAsync(
                @"() => {
                    window.__counter = (window.__counter || 0) + 1;
                    return window.__injected;
                }",
                null,
                10,
                0);
            await page.WaitForFunctionAsync("() => window.__counter > 10").ConfigureAwait(false);
            await page.EvaluateAsync("(() => { window.__injected = true; })()").ConfigureAwait(false);
            await watchdog.ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should survive cross-process navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSurviveCrossProcessNavigation()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);

            bool fooFound = false;
            Task waitForFunction = page.WaitForFunctionAsync("window.__FOO === 1")
                .ContinueWith(
                    t =>
                    {
                        if (t.Status == TaskStatus.RanToCompletion)
                        {
                            fooFound = true;
                        }
                    },
                    TaskScheduler.Default);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(fooFound, Is.False);
            await page.ReloadAsync().ConfigureAwait(false);
            Assert.That(fooFound, Is.False);
            await page.GoToAsync(CrossProcessPrefix + "/grid.html").ConfigureAwait(false);
            Assert.That(fooFound, Is.False);
            await page.EvaluateAsync("(() => { window.__FOO = 1; })()").ConfigureAwait(false);
            await waitForFunction.ConfigureAwait(false);
            Assert.That(fooFound, Is.True);
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should survive navigations")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSurviveNavigations()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);

            Task<IJSHandle> watchdog = page.WaitForFunctionAsync("() => window.__done");
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/consolelog.html").ConfigureAwait(false);
            await page.EvaluateAsync("(() => { window.__done = true; })()").ConfigureAwait(false);
            await watchdog.ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should work with multiline body")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithMultilineBody()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);

            IJSHandle result = await page.WaitForFunctionAsync(@"
                (() => true)()
            ").ConfigureAwait(false);
            Assert.That(await result.JsonValueAsync<bool>().ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should wait for predicate with arguments")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForPredicateWithArguments()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);

            var arg = new { arg1 = 1, arg2 = 2 };
            WaitForFunctionHelper.SetPendingArg(arg);
            await page.WaitForFunctionAsync("({ arg1, arg2 }) => arg1 + arg2 === 3", arg).ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should not be called after finishing successfully")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotBeCalledAfterFinishingSuccessfully()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            List<string> messages = new List<string>();
            page.Console += (_, msg) =>
            {
                if (msg.Text.StartsWith("waitForFunction", StringComparison.Ordinal))
                {
                    messages.Add(msg.Text);
                }
            };

            await page.WaitForFunctionAsync("() => { console.log('waitForFunction1'); return true; }").ConfigureAwait(false);
            await page.ReloadAsync().ConfigureAwait(false);
            await page.WaitForFunctionAsync("() => { console.log('waitForFunction2'); return true; }").ConfigureAwait(false);
            await page.ReloadAsync().ConfigureAwait(false);
            await page.WaitForFunctionAsync("() => { console.log('waitForFunction3'); return true; }").ConfigureAwait(false);

            Assert.That(string.Join("|", messages), Is.EqualTo("waitForFunction1|waitForFunction2|waitForFunction3"));
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should not be called after finishing unsuccessfully")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotBeCalledAfterFinishingUnsuccessfully()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            List<string> messages = new List<string>();
            page.Console += (_, msg) =>
            {
                if (msg.Text.StartsWith("waitForFunction", StringComparison.Ordinal))
                {
                    messages.Add(msg.Text);
                }
            };

            Assert.CatchAsync<PlaywrightNativeException>(
                () => page.WaitForFunctionAsync("() => { console.log('waitForFunction1'); throw new Error('waitForFunction1'); }"));
            await page.ReloadAsync().ConfigureAwait(false);
            Assert.CatchAsync<PlaywrightNativeException>(
                () => page.WaitForFunctionAsync("() => { console.log('waitForFunction2'); throw new Error('waitForFunction2'); }"));
            await page.ReloadAsync().ConfigureAwait(false);
            Assert.CatchAsync<PlaywrightNativeException>(
                () => page.WaitForFunctionAsync("() => { console.log('waitForFunction3'); throw new Error('waitForFunction3'); }"));

            Assert.That(string.Join("|", messages), Is.EqualTo("waitForFunction1|waitForFunction2|waitForFunction3"));
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "should throw when frame is detached")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowWhenFrameIsDetached()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await NewPageAsync(context).ConfigureAwait(false);

            await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            IFrame frame = ChildFrame(page);
            Task<Exception> promise = frame.WaitForFunctionAsync("() => false")
                .ContinueWith(t => t.Exception?.GetBaseException(), TaskScheduler.Default);
            await DetachFrameAsync(page, "frame1").ConfigureAwait(false);
            Exception error = await promise.ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            Assert.That(
                error.Message,
                Does.Match(@"frame\.waitForFunction: (Frame was detached|Execution context was destroyed)")
                    .Or.Contain("Frame was detached")
                    .Or.Contain("detached")
                    .Or.Contain("destroyed"),
                error.Message);
        }
    }
}
