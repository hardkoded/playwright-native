/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IPage.RouteWebSocketAsync(string, System.Action{IWebSocketRoute})"/>.
    /// </summary>
    [TestFixture]
    public class RouteWebSocketTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        private static string EchoUrl =>
            TestConstants.ServerUrl.Replace("http://", "ws://", System.StringComparison.Ordinal) + "/ws";

        [SetUp]
        public void SendOfficialIncomingOnConnect()
        {
            Server?.SendOnWebSocketConnection("incoming");
        }

        [PlaywrightTest("page-route.spec.ts", "page RouteWebSocket mocks a reply")]
        [Test]
        [Timeout(30_000)]
        public async Task PageShouldMockAReply()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await page.RouteWebSocketAsync("**/ws", ws =>
            {
                ws.OnMessage(frame =>
                {
                    if (frame.Text == "request")
                    {
                        ws.Send("response");
                    }
                });
            }).ConfigureAwait(false);

            string text = await page.EvaluateAsync<string>(
                @"url => new Promise(resolve => {
                    const ws = new WebSocket(url);
                    ws.addEventListener('message', e => resolve(e.data));
                    ws.addEventListener('open', () => ws.send('request'));
                })",
                EchoUrl).ConfigureAwait(false);

            Assert.That(text, Is.EqualTo("response"));
        }

        [PlaywrightTest("page-route.spec.ts", "page RouteWebSocket matches a regex")]
        [Test]
        [Timeout(30_000)]
        public async Task PageShouldMockAReplyMatchingRegex()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await page.RouteWebSocketAsync(new Regex("/ws$"), ws =>
            {
                ws.OnMessage(frame =>
                {
                    if (frame.Text == "request")
                    {
                        ws.Send("regex-response");
                    }
                });
            }).ConfigureAwait(false);

            string text = await page.EvaluateAsync<string>(
                @"url => new Promise(resolve => {
                    const ws = new WebSocket(url);
                    ws.addEventListener('message', e => resolve(e.data));
                    ws.addEventListener('open', () => ws.send('request'));
                })",
                EchoUrl).ConfigureAwait(false);

            Assert.That(text, Is.EqualTo("regex-response"));
        }

        [PlaywrightTest("page-route.spec.ts", "page RouteWebSocket matches a predicate")]
        [Test]
        [Timeout(30_000)]
        public async Task PageShouldMockAReplyMatchingPredicate()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await page.RouteWebSocketAsync(
                url => url.Contains("/ws", StringComparison.Ordinal),
                ws =>
                {
                    ws.OnMessage(frame =>
                    {
                        if (frame.Text == "request")
                        {
                            ws.Send("pred-response");
                        }
                    });
                }).ConfigureAwait(false);

            string text = await page.EvaluateAsync<string>(
                @"url => new Promise(resolve => {
                    const ws = new WebSocket(url);
                    ws.addEventListener('message', e => resolve(e.data));
                    ws.addEventListener('open', () => ws.send('request'));
                })",
                EchoUrl).ConfigureAwait(false);

            Assert.That(text, Is.EqualTo("pred-response"));
        }

        [PlaywrightTest("page-route.spec.ts", "page RouteWebSocket can push a message")]
        [Test]
        [Timeout(30_000)]
        public async Task PageShouldPushAMessage()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            TaskCompletionSource<IWebSocketRoute> opened = new TaskCompletionSource<IWebSocketRoute>();
            await page.RouteWebSocketAsync("**/ws", ws => opened.TrySetResult(ws)).ConfigureAwait(false);

            Task<string> received = page.EvaluateAsync<string>(
                @"url => new Promise(resolve => {
                    const ws = new WebSocket(url);
                    ws.addEventListener('message', e => resolve(e.data));
                })",
                EchoUrl);

            IWebSocketRoute route = await opened.Task.ConfigureAwait(false);
            route.Send("hello-from-route");

            Assert.That(await received.ConfigureAwait(false), Is.EqualTo("hello-from-route"));
        }

        [PlaywrightTest("page-route.spec.ts", "context RouteWebSocket mocks a reply")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextShouldMockAReply()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteWebSocketAsync("/ws", ws =>
            {
                ws.OnMessage(frame => ws.Send("from-context:" + frame.Text));
            }).ConfigureAwait(false);

            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            string text = await page.EvaluateAsync<string>(
                @"url => new Promise(resolve => {
                    const ws = new WebSocket(url);
                    ws.addEventListener('message', e => resolve(e.data));
                    ws.addEventListener('open', () => ws.send('ping'));
                })",
                EchoUrl).ConfigureAwait(false);

            Assert.That(text, Is.EqualTo("from-context:ping"));
        }

        [PlaywrightTest("page-route.spec.ts", "context RouteWebSocket matches a regex")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextShouldMockAReplyMatchingRegex()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteWebSocketAsync(new Regex("/ws$"), ws =>
            {
                ws.OnMessage(frame =>
                {
                    if (frame.Text == "request")
                    {
                        ws.Send("context-regex-response");
                    }
                });
            }).ConfigureAwait(false);

            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            string text = await page.EvaluateAsync<string>(
                @"url => new Promise(resolve => {
                    const ws = new WebSocket(url);
                    ws.addEventListener('message', e => resolve(e.data));
                    ws.addEventListener('open', () => ws.send('request'));
                })",
                EchoUrl).ConfigureAwait(false);

            Assert.That(text, Is.EqualTo("context-regex-response"));
        }

        [PlaywrightTest("page-route.spec.ts", "context RouteWebSocket matches a predicate")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextShouldMockAReplyMatchingPredicate()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteWebSocketAsync(
                url => url.Contains("/ws", StringComparison.Ordinal),
                ws =>
                {
                    ws.OnMessage(frame =>
                    {
                        if (frame.Text == "request")
                        {
                            ws.Send("context-pred-response");
                        }
                    });
                }).ConfigureAwait(false);

            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            string text = await page.EvaluateAsync<string>(
                @"url => new Promise(resolve => {
                    const ws = new WebSocket(url);
                    ws.addEventListener('message', e => resolve(e.data));
                    ws.addEventListener('open', () => ws.send('request'));
                })",
                EchoUrl).ConfigureAwait(false);

            Assert.That(text, Is.EqualTo("context-pred-response"));
        }

        [PlaywrightTest("page-route.spec.ts", "non-matching RouteWebSocket uses the server")]
        [Test]
        [Timeout(30_000)]
        public async Task NonMatchingRouteShouldUseTheServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await page.RouteWebSocketAsync("**/no-such-socket", _ => { }).ConfigureAwait(false);

            string text = await page.EvaluateAsync<string>(
                @"url => new Promise(resolve => {
                    const ws = new WebSocket(url);
                    ws.addEventListener('message', e => resolve(e.data));
                })",
                EchoUrl).ConfigureAwait(false);

            Assert.That(text, Is.EqualTo("incoming"));
        }

        [PlaywrightTest("page-route.spec.ts", "context UnrouteWebSocket removes a regex route")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextUnrouteWebSocketRegexShouldUseTheServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Regex pattern = new Regex("/ws$");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteWebSocketAsync(pattern, _ => { }).ConfigureAwait(false);
            await context.UnrouteWebSocketAsync(pattern).ConfigureAwait(false);

            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            string text = await page.EvaluateAsync<string>(
                @"url => new Promise(resolve => {
                    const ws = new WebSocket(url);
                    ws.addEventListener('message', e => resolve(e.data));
                })",
                EchoUrl).ConfigureAwait(false);

            Assert.That(text, Is.EqualTo("incoming"));
        }

        [PlaywrightTest("page-route.spec.ts", "context UnrouteWebSocket removes a predicate route")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextUnrouteWebSocketPredicateShouldUseTheServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Func<string, bool> pattern = url => url.Contains("/ws", StringComparison.Ordinal);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteWebSocketAsync(pattern, _ => { }).ConfigureAwait(false);
            await context.UnrouteWebSocketAsync(pattern).ConfigureAwait(false);

            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            string text = await page.EvaluateAsync<string>(
                @"url => new Promise(resolve => {
                    const ws = new WebSocket(url);
                    ws.addEventListener('message', e => resolve(e.data));
                })",
                EchoUrl).ConfigureAwait(false);

            Assert.That(text, Is.EqualTo("incoming"));
        }

        [PlaywrightTest("page-route.spec.ts", "ConnectToServer forwards the server greeting")]
        [Test]
        [Timeout(30_000)]
        public async Task ConnectToServerShouldForwardServerFrames()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await page.RouteWebSocketAsync("**/ws", ws =>
            {
                ws.ConnectToServer();
            }).ConfigureAwait(false);

            string text = await page.EvaluateAsync<string>(
                @"url => new Promise(resolve => {
                    const ws = new WebSocket(url);
                    ws.addEventListener('message', e => resolve(e.data));
                })",
                EchoUrl).ConfigureAwait(false);

            Assert.That(text, Is.EqualTo("incoming"));
        }

        [PlaywrightTest("page-route.spec.ts", "ConnectToServer can rewrite server frames")]
        [Test]
        [Timeout(30_000)]
        public async Task ConnectToServerShouldRewriteServerFrames()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await page.RouteWebSocketAsync("**/ws", ws =>
            {
                IWebSocketRoute server = ws.ConnectToServer();
                server.OnMessage(frame =>
                {
                    if (frame.Text == "incoming")
                    {
                        ws.Send("rewritten");
                    }
                    else
                    {
                        ws.Send(frame.Text);
                    }
                });
            }).ConfigureAwait(false);

            string text = await page.EvaluateAsync<string>(
                @"url => new Promise(resolve => {
                    const ws = new WebSocket(url);
                    ws.addEventListener('message', e => resolve(e.data));
                })",
                EchoUrl).ConfigureAwait(false);

            Assert.That(text, Is.EqualTo("rewritten"));
        }

        [PlaywrightTest("page-route.spec.ts", "RouteWebSocket exposes constructor protocols")]
        [Test]
        [Timeout(30_000)]
        public async Task RouteShouldExposeConstructorProtocols()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            IReadOnlyList<string> protocols = null;
            await page.RouteWebSocketAsync("**/ws", ws =>
            {
                protocols = ws.Protocols;
                ws.OnMessage(frame => ws.Send("ok:" + frame.Text));
            }).ConfigureAwait(false);

            string text = await page.EvaluateAsync<string>(
                @"url => new Promise(resolve => {
                    const ws = new WebSocket(url, ['chat', 'superchat']);
                    ws.addEventListener('message', e => resolve(e.data));
                    ws.addEventListener('open', () => ws.send('hi'));
                })",
                EchoUrl).ConfigureAwait(false);

            Assert.That(text, Is.EqualTo("ok:hi"));
            Assert.That(protocols, Is.EqualTo(new[] { "chat", "superchat" }));
        }

        [PlaywrightTest("page-route.spec.ts", "page UnrouteWebSocket stops intercepting")]
        [Test]
        [Timeout(30_000)]
        public async Task PageUnrouteWebSocketShouldStopIntercepting()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await page.RouteWebSocketAsync("**/ws", ws =>
            {
                ws.OnMessage(frame => ws.Send("mocked"));
            }).ConfigureAwait(false);
            await page.UnrouteWebSocketAsync("**/ws").ConfigureAwait(false);

            string text = await page.EvaluateAsync<string>(
                @"url => new Promise(resolve => {
                    const ws = new WebSocket(url);
                    ws.addEventListener('message', e => resolve(e.data));
                })",
                EchoUrl).ConfigureAwait(false);

            Assert.That(text, Is.EqualTo("incoming"));
        }

        [PlaywrightTest("page-route.spec.ts", "page UnrouteWebSocket removes only the given handler")]
        [Test]
        [Timeout(30_000)]
        public async Task PageUnrouteWebSocketShouldRemoveOnlyTheGivenHandler()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            Action<IWebSocketRoute> keep = ws => ws.OnMessage(frame => ws.Send("keep:" + frame.Text));
            Action<IWebSocketRoute> drop = ws => ws.OnMessage(frame => ws.Send("drop"));

            await page.RouteWebSocketAsync("**/ws", keep).ConfigureAwait(false);
            await page.RouteWebSocketAsync("**/ws", drop).ConfigureAwait(false);
            await page.UnrouteWebSocketAsync("**/ws", drop).ConfigureAwait(false);

            string text = await page.EvaluateAsync<string>(
                @"url => new Promise(resolve => {
                    const ws = new WebSocket(url);
                    ws.addEventListener('message', e => resolve(e.data));
                    ws.addEventListener('open', () => ws.send('hi'));
                })",
                EchoUrl).ConfigureAwait(false);

            Assert.That(text, Is.EqualTo("keep:hi"));
        }

        [PlaywrightTest("page-route.spec.ts", "context UnrouteWebSocket stops intercepting")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextUnrouteWebSocketShouldStopIntercepting()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteWebSocketAsync("/ws", ws =>
            {
                ws.OnMessage(frame => ws.Send("mocked"));
            }).ConfigureAwait(false);
            await context.UnrouteWebSocketAsync("/ws").ConfigureAwait(false);

            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            string text = await page.EvaluateAsync<string>(
                @"url => new Promise(resolve => {
                    const ws = new WebSocket(url);
                    ws.addEventListener('message', e => resolve(e.data));
                })",
                EchoUrl).ConfigureAwait(false);

            Assert.That(text, Is.EqualTo("incoming"));
        }

        [PlaywrightTest("page-route.spec.ts", "page UnrouteAllAsync stops WebSocket intercepting")]
        [Test]
        [Timeout(30_000)]
        public async Task PageUnrouteAllShouldStopWebSocketIntercepting()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await page.RouteWebSocketAsync("**/ws", ws =>
            {
                ws.OnMessage(frame => ws.Send("mocked"));
            }).ConfigureAwait(false);
            await page.UnrouteAllAsync().ConfigureAwait(false);

            string text = await page.EvaluateAsync<string>(
                @"url => new Promise(resolve => {
                    const ws = new WebSocket(url);
                    ws.addEventListener('message', e => resolve(e.data));
                })",
                EchoUrl).ConfigureAwait(false);

            Assert.That(text, Is.EqualTo("incoming"));
        }

        [PlaywrightTest("page-route.spec.ts", "context UnrouteAllAsync stops WebSocket intercepting")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextUnrouteAllShouldStopWebSocketIntercepting()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteWebSocketAsync("/ws", ws =>
            {
                ws.OnMessage(frame => ws.Send("mocked"));
            }).ConfigureAwait(false);
            await context.UnrouteAllAsync().ConfigureAwait(false);

            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            string text = await page.EvaluateAsync<string>(
                @"url => new Promise(resolve => {
                    const ws = new WebSocket(url);
                    ws.addEventListener('message', e => resolve(e.data));
                })",
                EchoUrl).ConfigureAwait(false);

            Assert.That(text, Is.EqualTo("incoming"));
        }
    }
}
