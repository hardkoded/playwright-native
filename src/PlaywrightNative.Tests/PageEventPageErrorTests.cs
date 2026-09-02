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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.Helpers;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-event-pageerror.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageEventPageErrorTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static SimpleServer _server;
        private static string _prefix = TestConstants.ServerUrl;

        private static void EnsureServer()
        {
            if (_server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static async Task<PageErrorEventArgs> CapturePageErrorAsync(IPage page, Func<Task> action)
        {
            Task<PageErrorEventArgs> waitTask = page.WaitForEventAsync(PageEvent.PageError);
            await action().ConfigureAwait(false);
            return await waitTask.ConfigureAwait(false);
        }

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                _server = TestServerSetup.Server;
                _prefix = TestConstants.ServerUrl;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19320;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    _server = server;
                    _prefix = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
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
                _server = null;
            }
        }

        [PlaywrightTest("page-event-pageerror.spec.ts", "should fire")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFire()
        {
            EnsureServer();
            string url = _prefix + "/error.html";
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            PageErrorEventArgs error = await CapturePageErrorAsync(
                page,
                () => page.GoToAsync(url)).ConfigureAwait(false);

            Assert.That(error.Name, Is.EqualTo("Error"));
            Assert.That(error.Message, Is.EqualTo("Fancy error!"));
            if (TestConstants.IsChromium)
            {
                // Official Playwright Chromium reports column 11; newer V8 reports 16
                // for the same `new Error` expression on line 14.
                string official = "Error: Fancy error!\n" +
                    "    at c (myscript.js:14:11)\n" +
                    "    at b (myscript.js:10:5)\n" +
                    "    at a (myscript.js:6:5)\n" +
                    "    at myscript.js:3:1";
                string newerV8 = "Error: Fancy error!\n" +
                    "    at c (myscript.js:14:16)\n" +
                    "    at b (myscript.js:10:5)\n" +
                    "    at a (myscript.js:6:5)\n" +
                    "    at myscript.js:3:1";
                Assert.That(error.Stack, Is.EqualTo(official).Or.EqualTo(newerV8));
            }
            else if (TestConstants.IsWebKit)
            {
                // Official Playwright WebKit reports c() at :14:36 (the `new Error`
                // expression). Current WebKit reports :15:19 (the `throw`).
                string official = "Error: Fancy error!\n" +
                    "    at c (" + url + ":14:36)\n" +
                    "    at b (" + url + ":10:6)\n" +
                    "    at a (" + url + ":6:6)\n" +
                    "    at global code (" + url + ":3:2)";
                string currentWebKit = "Error: Fancy error!\n" +
                    "    at c (" + url + ":15:19)\n" +
                    "    at b (" + url + ":10:6)\n" +
                    "    at a (" + url + ":6:6)\n" +
                    "    at global code (" + url + ":3:2)";
                Assert.That(error.Stack, Is.EqualTo(official).Or.EqualTo(currentWebKit));
            }
            else if (TestConstants.IsFirefox)
            {
                Assert.That(error.Stack, Is.EqualTo(
                    "Error: Fancy error!\n" +
                    "    at c (myscript.js:14:11)\n" +
                    "    at b (myscript.js:10:5)\n" +
                    "    at a (myscript.js:6:5)\n" +
                    "    at  (myscript.js:3:1)"));
            }
        }

        [PlaywrightTest("page-event-pageerror.spec.ts", "should not receive console message for pageError")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotReceiveConsoleMessageForPageError()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<IConsoleMessage> messages = new List<IConsoleMessage>();
            page.Console += (_, received) => messages.Add(received);
            await CapturePageErrorAsync(
                page,
                () => page.GoToAsync(_prefix + "/error.html")).ConfigureAwait(false);
            Assert.That(messages, Has.Count.EqualTo(1));
        }

        [PlaywrightTest("page-event-pageerror.spec.ts", "should contain sourceURL")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldContainSourceURL()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("Upstream marks this as fail on WebKit.");
            }

            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            PageErrorEventArgs error = await CapturePageErrorAsync(
                page,
                () => page.GoToAsync(_prefix + "/error.html")).ConfigureAwait(false);
            Assert.That(error.Stack, Does.Contain("myscript.js"));
        }

        [PlaywrightTest("page-event-pageerror.spec.ts", "should contain the Error.name property")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldContainTheErrorNameProperty()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            PageErrorEventArgs error = await CapturePageErrorAsync(
                page,
                () => page.EvaluateAsync<object>(@"(() => {
                    setTimeout(() => {
                        const error = new Error('my-message');
                        error.name = 'my-name';
                        throw error;
                    }, 0);
                })()")).ConfigureAwait(false);
            Assert.That(error.Name, Is.EqualTo("my-name"));
            Assert.That(error.Message, Is.EqualTo("my-message"));
        }

        [PlaywrightTest("page-event-pageerror.spec.ts", "should support an empty Error.name property")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportAnEmptyErrorNameProperty()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            PageErrorEventArgs error = await CapturePageErrorAsync(
                page,
                () => page.EvaluateAsync<object>(@"(() => {
                    setTimeout(() => {
                        const error = new Error('my-message');
                        error.name = '';
                        throw error;
                    }, 0);
                })()")).ConfigureAwait(false);
            Assert.That(error.Name, Is.EqualTo(string.Empty));
            Assert.That(error.Message, Is.EqualTo("my-message"));
        }

        [PlaywrightTest("page-event-pageerror.spec.ts", "should handle odd values")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHandleOddValues()
        {
            (string Script, string Message)[] cases =
            {
                ("null", "null"),
                ("undefined", "undefined"),
                ("0", "0"),
                ("''", string.Empty),
            };

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            foreach ((string script, string message) in cases)
            {
                PageErrorEventArgs error = await CapturePageErrorAsync(
                    page,
                    () => page.EvaluateAsync<object>(
                        "(() => { setTimeout(() => { throw " + script + "; }, 0); })()")).ConfigureAwait(false);
                Assert.That(error.Message, Is.EqualTo(message));
            }
        }

        [PlaywrightTest("page-event-pageerror.spec.ts", "should handle object")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHandleObject()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            PageErrorEventArgs error = await CapturePageErrorAsync(
                page,
                () => page.EvaluateAsync<object>(
                    "(() => { setTimeout(() => { throw {}; }, 0); })()")).ConfigureAwait(false);
            Assert.That(
                error.Message,
                Is.EqualTo(TestConstants.IsChromium ? "Object" : "[object Object]"));
        }

        [PlaywrightTest("page-event-pageerror.spec.ts", "should handle window")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHandleWindow()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            PageErrorEventArgs error = await CapturePageErrorAsync(
                page,
                () => page.EvaluateAsync<object>(
                    "(() => { setTimeout(() => { throw window; }, 0); })()")).ConfigureAwait(false);
            Assert.That(
                error.Message,
                Is.EqualTo(TestConstants.IsChromium ? "Window" : "[object Window]"));
        }

        [PlaywrightTest("page-event-pageerror.spec.ts", "should remove a listener of a non-existing event handler")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRemoveAListenerOfANonExistingEventHandler()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            EventHandler<PageErrorEventArgs> handler = (_, _) => { };
            page.PageError -= (_, _) => { };
        }

        [PlaywrightTest("page-event-pageerror.spec.ts", "should emit error from unhandled rejects")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEmitErrorFromUnhandledRejects()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            PageErrorEventArgs error = await CapturePageErrorAsync(
                page,
                () => page.SetContentAsync(@"
        <script>
          Promise.reject(new Error('sad :('));
        </script>
    ")).ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("sad :("));
        }

        [PlaywrightTest("page-event-pageerror.spec.ts", "pageErrors should work")]
        [Test]
        [Timeout(30_000)]
        public async Task PageErrorsShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.EvaluateAsync<object>(@"(async () => {
                for (let i = 0; i < 301; i++)
                    setTimeout(() => { throw new Error('error' + i); }, 0);
                await new Promise(f => setTimeout(f, 2000));
            })()").ConfigureAwait(false);

            IReadOnlyList<string> errors = await page.PageErrorsAsync().ConfigureAwait(false);
            List<string> messages = errors.Select(item => PageErrorText.Parse(item).Message).ToList();

            List<string> expected = new List<string>();
            for (int i = 201; i < 301; i++)
            {
                expected.Add("error" + i);
            }

            Assert.That(messages.Count, Is.GreaterThanOrEqualTo(100), "should be at least 100 errors");
            Assert.That(
                messages.Skip(messages.Count - expected.Count).ToArray(),
                Is.EqualTo(expected.ToArray()),
                "should return last errors");
        }

        [PlaywrightTest("page-event-pageerror.spec.ts", "clearPageErrors should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ClearPageErrorsShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.EvaluateAsync<object>(@"(() => {
                setTimeout(() => { throw new Error('error1'); }, 0);
                setTimeout(() => { throw new Error('error2'); }, 0);
            })()").ConfigureAwait(false);
            await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);

            IReadOnlyList<string> errors = await page.PageErrorsAsync().ConfigureAwait(false);
            List<string> messages = errors.Select(item => PageErrorText.Parse(item).Message).ToList();
            Assert.That(messages, Does.Contain("error1"));
            Assert.That(messages, Does.Contain("error2"));

            await page.ClearPageErrorsAsync().ConfigureAwait(false);

            errors = await page.PageErrorsAsync().ConfigureAwait(false);
            Assert.That(errors, Is.EqualTo(Array.Empty<string>()));

            await page.EvaluateAsync<object>(@"(() => {
                setTimeout(() => { throw new Error('error3'); }, 0);
            })()").ConfigureAwait(false);
            await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);

            errors = await page.PageErrorsAsync().ConfigureAwait(false);
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(PageErrorText.Parse(errors[0]).Message, Does.Contain("error3"));
        }

        [PlaywrightTest("page-event-pageerror.spec.ts", "should fire illegal character error")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireIllegalCharacterError()
        {
            EnsureServer();
            _server.SetRoute("/illegal-character.html", async http =>
            {
                http.Response.ContentType = "text/html; charset=utf-8";
                await http.Response.WriteAsync(@"
      <!doctype html>
      <html lang=""en"">
        <head>
          <meta charset=""UTF-8"" />
          <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
          <title>vite-project</title>
        </head>
        <body>
          <div id=""app""></div>
          <script >
            let a=10；//   !!!note: ； not ;
          </script>
        </body>
      </html>
    ").ConfigureAwait(false);
            });

            string url = _prefix + "/illegal-character.html";
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            PageErrorEventArgs error = await CapturePageErrorAsync(
                page,
                () => page.GoToAsync(url)).ConfigureAwait(false);
            if (TestConstants.IsChromium)
            {
                Assert.That(error.Message, Does.Contain("Invalid or unexpected token"));
            }
            else if (TestConstants.IsWebKit && TestConstants.IsWindows)
            {
                Assert.That(error.Message, Does.Contain("No identifiers allowed directly after numeric literal"));
            }
            else if (TestConstants.IsWebKit)
            {
                Assert.That(error.Message, Does.Contain("Invalid character"));
            }
            else
            {
                Assert.That(error.Message, Does.Contain("illegal character"));
            }
        }
    }
}
