/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-event-console.spec.ts</c>.
    /// </summary>
    [TestFixture]
    public class PageEventConsoleTests : PageTestEx
    {
        private const string OfficialConsoleLogHtml =
            "<!DOCTYPE html>\n" +
            "<html>\n" +
            "  <head>\n" +
            "    <title>console.log test</title>\n" +
            "  </head>\n" +
            "  <body>\n" +
            "    <script>\n" +
            "      console.log('here:' + location.href)\n" +
            "    </script>\n" +
            "  </body>\n" +
            "</html>\n";

        private static async Task PollAsync(Func<bool> condition)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (!condition() && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.That(condition(), Is.True);
        }

        [PlaywrightTest("page-event-console.spec.ts", "should work")]
        [PlaywrightTest("page-event-console.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IConsoleMessage message = null;
            page.Console += (_, received) => message = received;
            await Task.WhenAll(
                page.EvaluateAsync<object>("console.log('hello', 5, { foo: 'bar' })"),
                page.WaitForConsoleMessageAsync()).ConfigureAwait(false);

            Assert.That(message, Is.Not.Null);
            if (TestConstants.IsFirefox)
            {
                Assert.That(message.Text, Is.EqualTo("hello 5 JSHandle@object"));
            }
            else
            {
                Assert.That(message.Text, Is.EqualTo("hello 5 {foo: bar}"));
            }

            Assert.That(message.Type, Is.EqualTo("log"));
            IJSHandle[] args = message.Args.ToArray();
            Assert.That(args, Has.Length.EqualTo(3));
            Assert.That(await args[0].JsonValueAsync<string>().ConfigureAwait(false), Is.EqualTo("hello"));
            Assert.That(await args[1].JsonValueAsync<int>().ConfigureAwait(false), Is.EqualTo(5));
            JsonElement obj = await args[2].JsonValueAsync<JsonElement>().ConfigureAwait(false);
            Assert.That(obj.GetProperty("foo").GetString(), Is.EqualTo("bar"));
        }

        [PlaywrightTest("page-event-console.spec.ts", "should emit same log twice")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEmitSameLogTwice()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<string> messages = new List<string>();
            page.Console += (_, received) => messages.Add(received.Text);
            await page.EvaluateAsync<object>(@"(() => {
                for (let i = 0; i < 2; ++i)
                    console.log('hello');
            })()").ConfigureAwait(false);
            Assert.That(messages, Is.EqualTo(new[] { "hello", "hello" }));
        }

        [PlaywrightTest("page-event-console.spec.ts", "should use text() for inspection")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldUseTextForInspection()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            string text = null;
            page.Console += (_, received) => text = received.ToString();
            await page.EvaluateAsync<object>("console.log('Hello world')").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("Hello world"));
        }

        [PlaywrightTest("page-event-console.spec.ts", "should work for different console API calls")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForDifferentConsoleApiCalls()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<IConsoleMessage> messages = new List<IConsoleMessage>();
            page.Console += (_, received) => messages.Add(received);
            await page.EvaluateAsync<object>(@"(() => {
                console.time('calling console.time');
                console.timeEnd('calling console.time');
                console.trace('calling console.trace');
                console.dir('calling console.dir');
                console.warn('calling console.warn');
                console.error('calling console.error');
                console.info('calling console.info');
                console.debug('calling console.debug');
                console.log(Promise.resolve('should not wait until resolved!'));
            })()").ConfigureAwait(false);

            string boundValue = null;
            await page.ExposeFunctionAsync("foobar", (string value) =>
            {
                boundValue = value;
            }).ConfigureAwait(false);
            await page.EvaluateAsync<object>("window['foobar']('Using bindings')").ConfigureAwait(false);
            await page.EvaluateAsync<object>("v => console.log(v)", boundValue).ConfigureAwait(false);

            Assert.That(messages.Select(item => item.Type), Is.EqualTo(new[]
            {
                "timeEnd", "trace", "dir", "warning", "error", "info", "debug", "log", "log",
            }));
            Assert.That(messages[0].Text, Does.Contain("calling console.time"));
            Assert.That(messages.Skip(1).Select(item => item.Text), Is.EqualTo(new[]
            {
                "calling console.trace",
                "calling console.dir",
                "calling console.warn",
                "calling console.error",
                "calling console.info",
                "calling console.debug",
                "Promise",
                "Using bindings",
            }));
        }

        [PlaywrightTest("page-event-console.spec.ts", "should format the message correctly with time/timeLog/timeEnd")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFormatTheMessageCorrectlyWithTimeTimeLogTimeEnd()
        {
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("https://github.com/microsoft/playwright/issues/10580");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<IConsoleMessage> messages = new List<IConsoleMessage>();
            page.Console += (_, received) => messages.Add(received);
            await page.EvaluateAsync<object>(@"(async () => {
                console.time('foo time');
                await new Promise(x => setTimeout(x, 100));
                console.timeLog('foo time');
                await new Promise(x => setTimeout(x, 100));
                console.timeEnd('foo time');
            })()").ConfigureAwait(false);

            Assert.That(messages, Has.Count.EqualTo(2));
            if (TestConstants.IsWebKit)
            {
                Assert.That(messages[0].Type, Is.EqualTo("timeEnd"));
            }
            else if (TestConstants.IsChromium)
            {
                Assert.That(messages[0].Type, Is.EqualTo("log"));
            }
            else if (TestConstants.IsFirefox)
            {
                Assert.That(messages[0].Type, Is.EqualTo("timeLog"));
            }

            Assert.That(messages[1].Type, Is.EqualTo("timeEnd"));
            Regex timing = new Regex(@"foo time: \d+(.\d+)? ?ms");
            Assert.That(messages[0].Text, Does.Match(timing));
            Assert.That(messages[1].Text, Does.Match(timing));
        }

        [PlaywrightTest("page-event-console.spec.ts", "should not fail for window object")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotFailForWindowObject()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IConsoleMessage message = null;
            page.Console += (_, received) => message = received;
            await Task.WhenAll(
                page.EvaluateAsync<object>("console.error(window)"),
                page.WaitForConsoleMessageAsync()).ConfigureAwait(false);

            Assert.That(message, Is.Not.Null);
            if (TestConstants.IsFirefox)
            {
                Assert.That(message.Text, Is.EqualTo("JSHandle@object"));
            }
            else
            {
                Assert.That(message.Text, Is.EqualTo("Window"));
            }
        }

        [PlaywrightTest("page-event-console.spec.ts", "should trigger correct Log")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTriggerCorrectLog()
        {
            if (TestConstants.IsWebKit && TestConstants.IsWindows)
            {
                Assert.Ignore("Upstream issue https://bugs.webkit.org/show_bug.cgi?id=229515");
            }

            await using LocalHtmlServer server = LocalHtmlServer.Start();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            Task<IConsoleMessage> waitTask = page.WaitForConsoleMessageAsync();
            await page.EvaluateAsync<object>(
                "async url => fetch(url).catch(e => {})",
                server.EmptyPage).ConfigureAwait(false);
            IConsoleMessage message = await waitTask.ConfigureAwait(false);
            Assert.That(message.Text, Does.Match("Access-Control-Allow-Origin|CORS"));
            Assert.That(message.Type, Is.EqualTo("error"));
        }

        [PlaywrightTest("page-event-console.spec.ts", "should have location for console API calls")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHaveLocationForConsoleApiCalls()
        {
            await using LocalHtmlServer server = LocalHtmlServer.Start();
            server.SetHtml("/consolelog.html", OfficialConsoleLogHtml);

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(server.EmptyPage).ConfigureAwait(false);
            string url = server.Prefix + "/consolelog.html";
            Task<IConsoleMessage> waitTask = page.WaitForConsoleMessageAsync(
                received => received.Text != null && received.Text.StartsWith("here:", StringComparison.Ordinal));
            await page.GoToAsync(url).ConfigureAwait(false);
            IConsoleMessage message = await waitTask.ConfigureAwait(false);

            Assert.That(message.Type, Is.EqualTo("log"));
            Assert.That(message.Location, Does.StartWith(url + ":7:"));
        }

        [PlaywrightTest("page-event-console.spec.ts", "should not throw when there are console messages in detached iframes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotThrowWhenThereAreConsoleMessagesInDetachedIframes()
        {
            await using LocalHtmlServer server = LocalHtmlServer.Start();
            server.SetHtml("/consolelog.html", OfficialConsoleLogHtml);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(server.EmptyPage).ConfigureAwait(false);
            Task<IPage> popupTask = page.WaitForPopupAsync();
            await page.EvaluateAsync<object>(@"(async () => {
                const win = window.open('');
                window['_popup'] = win;
                if (window.document.readyState !== 'complete')
                    await new Promise(f => window.addEventListener('load', f));
                win.document.body.innerHTML = `<iframe src='/consolelog.html'></iframe>`;
                const frame = win.document.querySelector('iframe');
                if (!frame.contentDocument || frame.contentDocument.readyState !== 'complete')
                    await new Promise(f => frame.addEventListener('load', f));
                frame.remove();
            })()").ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            Assert.That(await popup.EvaluateAsync<int>("1 + 1").ConfigureAwait(false), Is.EqualTo(2));
        }

        [PlaywrightTest("page-event-console.spec.ts", "should use object previews for arrays and objects")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldUseObjectPreviewsForArraysAndObjects()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            string text = null;
            page.Console += (_, received) => text = received.Text;
            await page.EvaluateAsync<object>("console.log([1, 2, 3], { a: 1 }, window)").ConfigureAwait(false);

            if (TestConstants.IsFirefox)
            {
                Assert.That(text, Is.EqualTo("Array JSHandle@object JSHandle@object"));
            }
            else
            {
                Assert.That(text, Is.EqualTo("[1, 2, 3] {a: 1} Window"));
            }
        }

        [PlaywrightTest("page-event-console.spec.ts", "should use object previews for errors")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldUseObjectPreviewsForErrors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            string text = null;
            page.Console += (_, received) => text = received.Text;
            // Official page.evaluate(() => ...) appears in Chromium's error
            // preview as ".evaluate". Invoke through a named evaluate call.
            await page.EvaluateAsync<object>(
                "({evaluate() { console.log(new Error('Exception')); }}).evaluate()").ConfigureAwait(false);

            if (TestConstants.IsFirefox)
            {
                Assert.That(text, Is.EqualTo("Error"));
            }
            else if (TestConstants.IsChromium)
            {
                Assert.That(text, Does.Contain(".evaluate"));
            }
            else if (TestConstants.IsWebKit)
            {
                Assert.That(text, Is.EqualTo("Error: Exception"));
            }
        }

        [PlaywrightTest("page-event-console.spec.ts", "do not update console count on unhandled rejections")]
        [Test]
        [Timeout(30_000)]
        public async Task DoNotUpdateConsoleCountOnUnhandledRejections()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<string> messages = new List<string>();
            page.Console += (_, received) => messages.Add(received.Text);
            await page.EvaluateAsync<object>(@"(() => {
                const fail = async () => Promise.reject(new Error('error'));
                console.log('begin');
                void fail();
                void fail();
                fail().catch(() => {
                    console.log('end');
                });
            })()").ConfigureAwait(false);

            await PollAsync(() => messages.Count >= 2 && messages[0] == "begin" && messages[messages.Count - 1] == "end").ConfigureAwait(false);
            Assert.That(messages, Is.EqualTo(new[] { "begin", "end" }));
        }

        [PlaywrightTest("page-event-console.spec.ts", "should have timestamp")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHaveTimestamp()
        {
            if (OperatingSystem.IsAndroid())
            {
                Assert.Ignore("there is a time difference between android emulator and host machine");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 100;
            Task<IConsoleMessage> waitTask = page.WaitForConsoleMessageAsync();
            await page.EvaluateAsync<object>("console.log('timestamp test')").ConfigureAwait(false);
            IConsoleMessage message = await waitTask.ConfigureAwait(false);
            long after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 100;
            Assert.That(message.Timestamp, Is.GreaterThanOrEqualTo(before));
            Assert.That(message.Timestamp, Is.LessThanOrEqualTo(after));
        }

        [PlaywrightTest("page-event-console.spec.ts", "should have increasing timestamps")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHaveIncreasingTimestamps()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<IConsoleMessage> messages = new List<IConsoleMessage>();
            page.Console += (_, received) => messages.Add(received);
            await page.EvaluateAsync<object>(@"(() => {
                console.log('first');
                console.log('second');
                console.log('third');
            })()").ConfigureAwait(false);
            Assert.That(messages, Has.Count.EqualTo(3));
            for (int i = 1; i < messages.Count; i++)
            {
                Assert.That(messages[i].Timestamp, Is.GreaterThanOrEqualTo(messages[i - 1].Timestamp));
            }
        }

        [PlaywrightTest("page-event-console.spec.ts", "should have timestamp in consoleMessages")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHaveTimestampInConsoleMessages()
        {
            if (OperatingSystem.IsAndroid())
            {
                Assert.Ignore("there is a time difference between android emulator and host machine");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 100;
            await page.EvaluateAsync<object>("console.log('stored message')").ConfigureAwait(false);
            long after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 100;
            IReadOnlyList<IConsoleMessage> messages = await page.ConsoleMessagesAsync().ConfigureAwait(false);
            Assert.That(messages.Count, Is.GreaterThanOrEqualTo(1));
            IConsoleMessage last = messages[messages.Count - 1];
            Assert.That(last.Text, Is.EqualTo("stored message"));
            Assert.That(last.Timestamp, Is.GreaterThanOrEqualTo(before));
            Assert.That(last.Timestamp, Is.LessThanOrEqualTo(after));
        }

        [PlaywrightTest("page-event-console.spec.ts", "consoleMessages should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ConsoleMessagesShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.EvaluateAsync<object>(@"(() => {
                for (let i = 0; i < 301; i++)
                    console.log('message' + i);
            })()").ConfigureAwait(false);

            IReadOnlyList<IConsoleMessage> messages = await page.ConsoleMessagesAsync().ConfigureAwait(false);
            Assert.That(messages.Count, Is.GreaterThanOrEqualTo(100), "should be at least 100 messages");

            IConsoleMessage[] last = messages.Skip(Math.Max(0, messages.Count - 100)).ToArray();
            Assert.That(last, Has.Length.EqualTo(100), "should return last messages");
            for (int i = 0; i < 100; i++)
            {
                Assert.That(last[i].Text, Is.EqualTo("message" + (201 + i)));
                Assert.That(last[i].Type, Is.EqualTo("log"));
                Assert.That(last[i].Page, Is.SameAs(page));
            }
        }

        [PlaywrightTest("page-event-console.spec.ts", "clearConsoleMessages should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ClearConsoleMessagesShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.EvaluateAsync<object>(@"(() => {
                console.log('message1');
                console.log('message2');
            })()").ConfigureAwait(false);

            IReadOnlyList<IConsoleMessage> messages = await page.ConsoleMessagesAsync().ConfigureAwait(false);
            Assert.That(messages.Select(item => item.Text), Does.Contain("message1"));
            Assert.That(messages.Select(item => item.Text), Does.Contain("message2"));

            await page.ClearConsoleMessagesAsync().ConfigureAwait(false);

            messages = await page.ConsoleMessagesAsync().ConfigureAwait(false);
            Assert.That(messages, Is.Empty);

            await page.EvaluateAsync<object>("console.log('message3')").ConfigureAwait(false);
            messages = await page.ConsoleMessagesAsync().ConfigureAwait(false);
            Assert.That(messages.Count, Is.EqualTo(1));
            Assert.That(messages[0].Text, Is.EqualTo("message3"));
        }

        [PlaywrightTest("page-event-console.spec.ts", "consoleMessages since-navigation filter should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ConsoleMessagesSinceNavigationFilterShouldWork()
        {
            await using LocalHtmlServer server = LocalHtmlServer.Start();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.EvaluateAsync<object>("console.log('before navigation')").ConfigureAwait(false);
            await page.GoToAsync(server.EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync<object>("console.log('after navigation')").ConfigureAwait(false);

            IReadOnlyList<IConsoleMessage> all = await page.ConsoleMessagesAsync(new() { Filter = ConsoleMessagesFilter.All }).ConfigureAwait(false);
            Assert.That(all.Select(item => item.Text), Does.Contain("before navigation"));
            Assert.That(all.Select(item => item.Text), Does.Contain("after navigation"));

            IReadOnlyList<IConsoleMessage> sinceNav = await page.ConsoleMessagesAsync().ConfigureAwait(false);
            Assert.That(sinceNav.Select(item => item.Text), Does.Not.Contain("before navigation"));
            Assert.That(sinceNav.Select(item => item.Text), Does.Contain("after navigation"));
        }

        [PlaywrightTest("page-event-console.spec.ts", "pageErrors since-navigation filter should work")]
        [Test]
        [Timeout(30_000)]
        public async Task PageErrorsSinceNavigationFilterShouldWork()
        {
            await using LocalHtmlServer server = LocalHtmlServer.Start();
            server.SetHtml("/page1", "<script>throw new Error('page1 error');</script>");
            server.SetHtml("/page2", "<script>throw new Error('page2 error');</script>");

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(server.Prefix + "/page1").ConfigureAwait(false);
            await page.GoToAsync(server.Prefix + "/page2").ConfigureAwait(false);

            IReadOnlyList<string> all = await page.PageErrorsAsync(PageErrorsFilter.All).ConfigureAwait(false);
            Assert.That(string.Join("\n", all), Does.Contain("page1 error"));
            Assert.That(string.Join("\n", all), Does.Contain("page2 error"));

            IReadOnlyList<string> sinceNav = await page.PageErrorsAsync().ConfigureAwait(false);
            Assert.That(string.Join("\n", sinceNav), Does.Not.Contain("page1 error"));
            Assert.That(string.Join("\n", sinceNav), Does.Contain("page2 error"));
        }

        /// <summary>
        /// In-process HTML server on an ephemeral port so Wave 709
        /// does not fight siblings for <c>8081</c>.
        /// </summary>
        private sealed class LocalHtmlServer : IAsyncDisposable
        {
            private readonly HttpListener _listener = new HttpListener();
            private readonly ConcurrentDictionary<string, string> _pages = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();
            private readonly Task _loop;

            private LocalHtmlServer(int port)
            {
                Prefix = "http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _pages["/empty.html"] = "<html><body>empty</body></html>";
                _listener.Prefixes.Add(Prefix + "/");
                _listener.Start();
                _loop = LoopAsync();
            }

            internal string Prefix { get; }

            internal string EmptyPage => Prefix + "/empty.html";

            internal static LocalHtmlServer Start()
            {
                TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
                probe.Start();
                int port = ((IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();
                return new LocalHtmlServer(port);
            }

            internal void SetHtml(string path, string html)
            {
                _pages[path] = html;
            }

            public async ValueTask DisposeAsync()
            {
                _cts.Cancel();
                _listener.Close();
                try
                {
                    await _loop.ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                }
                catch (HttpListenerException)
                {
                }

                _cts.Dispose();
            }

            private async Task LoopAsync()
            {
                while (!_cts.IsCancellationRequested)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = await _listener.GetContextAsync().ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }
                    catch (HttpListenerException)
                    {
                        return;
                    }

                    string path = context.Request.Url == null ? "/" : context.Request.Url.AbsolutePath;
                    string html = _pages.TryGetValue(path, out string body)
                        ? body
                        : "<html><body></body></html>";
                    byte[] bytes = Encoding.UTF8.GetBytes(html);
                    context.Response.ContentType = "text/html; charset=utf-8";
                    context.Response.ContentLength64 = bytes.Length;
                    await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
                    context.Response.Close();
                }
            }
        }
    }
}
