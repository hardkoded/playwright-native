/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/capabilities.spec.ts</c> parity. All 29 titles
    /// are ported. Chromium skips <c>webkit should define window.safari</c>;
    /// WebKit skips <c>should not crash on showDirectoryPicker</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryCapabilitiesParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static SimpleServer _ownedHttps;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string HttpsPrefix = TestConstants.HttpsPrefix;
        private static string Host = "localhost:8081";
        private static int Port = 8081;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private static SimpleServer HttpsServer => _ownedHttps ?? TestServerSetup.HttpsServer;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            await StartOwnedHttpAsync(contentRoot).ConfigureAwait(false);
            await StartOwnedHttpsAsync(contentRoot).ConfigureAwait(false);
            if (Server == null && TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                Uri fallback = new(Prefix);
                Host = fallback.Authority;
                Port = fallback.Port;
            }

            if (HttpsServer == null && TestServerSetup.HttpsServer != null)
            {
                HttpsPrefix = TestConstants.HttpsPrefix;
            }
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }

            if (_ownedHttps != null)
            {
                await _ownedHttps.StopAsync().ConfigureAwait(false);
                _ownedHttps = null;
            }

            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
            }
        }

        [SetUp]
        public async Task SetUpAsync()
        {
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }

            Server?.Reset();
            HttpsServer?.Reset();
            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            Server?.Reset();
            HttpsServer?.Reset();
            TestServerSetup.Server?.Reset();
            TestServerSetup.HttpsServer?.Reset();
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }
        }

        [PlaywrightTest("capabilities.spec.ts", "SharedArrayBuffer should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SharedArrayBufferShouldWork()
        {
            EnsureHttps();
            IBrowserContext context = await _browser.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            HttpsServer.SetRoute("/sharedarraybuffer", http =>
            {
                http.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
                http.Response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
                return http.Response.WriteAsync(
                    "<div>Hello there!</div>\n<script>window.onload = () => console.log('onload')</script>\n");
            });
            await page.GoToAsync(HttpsPrefix + "/sharedarraybuffer").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("() => typeof SharedArrayBuffer").ConfigureAwait(false), Is.EqualTo("function"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("capabilities.spec.ts", "Web Assembly should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task WebAssemblyShouldWork()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/wasm/table2.html").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("loadTable()").ConfigureAwait(false), Is.EqualTo("42, 83"));
        }

        [PlaywrightTest("capabilities.spec.ts", "WebSocket should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task WebSocketShouldWork()
        {
            EnsureServer();
            Server.SendOnWebSocketConnection("incoming");
            IPage page = await NewPageAsync().ConfigureAwait(false);
            string value = await page.EvaluateAsync<string>(
                @"host => {
                    let cb;
                    const result = new Promise(f => cb = f);
                    const ws = new WebSocket('ws://' + host + '/ws');
                    ws.addEventListener('message', data => { ws.close(); cb(data.data); });
                    ws.addEventListener('error', error => cb('Error'));
                    return result;
                }",
                Host).ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("incoming"));
        }

        [PlaywrightTest("capabilities.spec.ts", "should respect CSP @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRespectCsp()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", http =>
            {
                http.Response.Headers["Content-Security-Policy"] = "script-src 'unsafe-inline';";
                return http.Response.WriteAsync(
                    "<script>\n  window.testStatus = 'SUCCESS';\n  window.testStatus = eval(\"'FAILED'\");\n</script>");
            });
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("() => window['testStatus']").ConfigureAwait(false), Is.EqualTo("SUCCESS"));
        }

        [PlaywrightTest("capabilities.spec.ts", "should play video @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPlayVideo()
        {
            if (TestConstants.IsWebKit && TestConstants.IsWindows)
            {
                Assert.Ignore("passes locally but fails on GitHub Action bot, apparently due to a Media Pack issue in the Windows Server");
            }

            IPage page = await NewPageAsync().ConfigureAwait(false);
            string fileName = TestConstants.IsWebKit ? "video_mp4.html" : "video.html";
            await page.GoToAsync(FileUrl(fileName)).ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("video", "v => v.play()").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("video", "v => v.pause()").ConfigureAwait(false);
        }

        [PlaywrightTest("capabilities.spec.ts", "should play webm video @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPlayWebmVideo()
        {
            if (TestConstants.IsWebKit && TestConstants.IsWindows)
            {
                Assert.Ignore("not supported");
            }

            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(FileUrl("video_webm.html")).ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("video", "v => v.play()").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("video", "v => v.pause()").ConfigureAwait(false);
        }

        [PlaywrightTest("capabilities.spec.ts", "should play audio @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPlayAudio()
        {
            if (TestConstants.IsWebKit && TestConstants.IsWindows)
            {
                Assert.Ignore("https://github.com/microsoft/playwright/issues/10892");
            }

            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<audio src=\"" + Prefix + "/example.mp3\"></audio>").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("audio", "e => e.play()").ConfigureAwait(false);
            await Task.Delay(3000).ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("audio", "e => e.pause()").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<double>("audio", "e => e.currentTime").ConfigureAwait(false), Is.GreaterThan(0.1d));
        }

        [PlaywrightTest("capabilities.spec.ts", "should support webgl @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportWebgl()
        {
            IPage page = await NewPageAsync().ConfigureAwait(false);
            bool hasWebGl = await page.EvaluateAsync<bool>(
                @"() => {
                    const canvas = document.createElement('canvas');
                    return !!canvas.getContext('webgl');
                }").ConfigureAwait(false);
            Assert.That(hasWebGl, Is.True);
        }

        [PlaywrightTest("capabilities.spec.ts", "should support webgl 2 @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportWebgl2()
        {
            IPage page = await NewPageAsync().ConfigureAwait(false);
            bool hasWebGl2 = await page.EvaluateAsync<bool>(
                @"() => {
                    const canvas = document.createElement('canvas');
                    return !!canvas.getContext('webgl2');
                }").ConfigureAwait(false);
            Assert.That(hasWebGl2, Is.True);
        }

        [PlaywrightTest("capabilities.spec.ts", "should not crash on page with mp4 @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCrashOnPageWithMp4()
        {
            if (TestConstants.IsWebKit && TestConstants.IsWindows)
            {
                Assert.Ignore("https://github.com/microsoft/playwright/issues/11009, times out in setContent");
            }

            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<video><source src=\"" + Prefix + "/movie.mp4\"/></video>").ConfigureAwait(false);
            await Task.Delay(1000).ConfigureAwait(false);
        }

        [PlaywrightTest("capabilities.spec.ts", "should not crash on showDirectoryPicker")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCrashOnShowDirectoryPicker()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("showDirectoryPicker is only available in Chromium");
            }

            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.Locator("body").ClickAsync().ConfigureAwait(false);
            _ = page.EvaluateAsync(
                @"async () => {
                    const dir = await window.showDirectoryPicker();
                    return dir.name;
                }");
            await Task.Delay(3000).ConfigureAwait(false);
        }

        [PlaywrightTest("capabilities.spec.ts", "should not crash on storage.getDirectory()")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCrashOnStorageGetDirectory()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            object error;
            try
            {
                error = await page.EvaluateAsync<string>(
                    @"async () => {
                        const dir = await navigator.storage.getDirectory();
                        return dir.name;
                    }").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            if (TestConstants.IsWebKit)
            {
                Assert.That(error, Is.InstanceOf<Exception>());
                Assert.That(((Exception)error).Message, Does.Contain("UnknownError: The operation failed for an unknown transient reason"));
            }
            else
            {
                Assert.That(error, Is.Not.InstanceOf<Exception>());
            }
        }

        [PlaywrightTest("capabilities.spec.ts", "navigator.clipboard should be present")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task NavigatorClipboardShouldBePresent()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<object>("() => navigator.clipboard").ConfigureAwait(false), Is.Not.Null);
        }

        [PlaywrightTest("capabilities.spec.ts", "should set CloseEvent.wasClean to false when the server terminates a WebSocket connection")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetCloseEventWasCleanToFalseWhenTheServerTerminatesAWebSocketConnection()
        {
            EnsureServer();
            Server.OnceWebSocketConnection(ws => ws.Abort());
            IPage page = await NewPageAsync().ConfigureAwait(false);
            bool wasClean = await page.EvaluateAsync<bool>(
                @"port => new Promise(resolve => {
                    const ws = new WebSocket('ws://localhost:' + port + '/ws');
                    ws.addEventListener('close', error => resolve(error.wasClean));
                })",
                Port).ConfigureAwait(false);
            Assert.That(wasClean, Is.False);
        }

        [PlaywrightTest("capabilities.spec.ts", "serviceWorker should intercept document request")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ServiceWorkerShouldInterceptDocumentRequest()
        {
            EnsureServer();
            Server.SetRoute("/sw.js", http =>
            {
                http.Response.ContentType = "application/javascript";
                return http.Response.WriteAsync(
                    "self.addEventListener('fetch', event => {\n" +
                    "  event.respondWith(new Response('intercepted'));\n" +
                    "});\n" +
                    "self.addEventListener('activate', event => {\n" +
                    "  event.waitUntil(clients.claim());\n" +
                    "});\n");
            });
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync(
                @"async () => {
                    await navigator.serviceWorker.register('/sw.js');
                    await new Promise(resolve => navigator.serviceWorker.oncontrollerchange = resolve);
                }").ConfigureAwait(false);
            await page.ReloadAsync().ConfigureAwait(false);
            Assert.That(await page.TextContentAsync("body").ConfigureAwait(false), Is.EqualTo("intercepted"));
        }

        [PlaywrightTest("capabilities.spec.ts", "webkit should define window.safari")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task WebkitShouldDefineWindowSafari()
        {
            if (!TestConstants.IsWebKit)
            {
                Assert.Ignore("official skip: browserName !== 'webkit'");
            }

            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("() => !!window.safari").ConfigureAwait(false), Is.True);
            Assert.That(await page.EvaluateAsync<string>("() => typeof window.safari.pushNotification").ConfigureAwait(false), Is.EqualTo("object"));
            Assert.That(await page.EvaluateAsync<string>("() => window.safari.pushNotification.toString()").ConfigureAwait(false), Is.EqualTo("[object SafariRemoteNotification]"));
        }

        [PlaywrightTest("capabilities.spec.ts", "make sure that XMLHttpRequest upload events are emitted correctly")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task MakeSureThatXmlHttpRequestUploadEventsAreEmittedCorrectly()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string[] events = await page.EvaluateAsync<string[]>(
                @"async () => {
                    const events = [];
                    const xhr = new XMLHttpRequest();
                    xhr.upload.addEventListener('loadstart', () => events.push('loadstart'));
                    xhr.upload.addEventListener('progress', () => events.push('progress'));
                    xhr.upload.addEventListener('load', () => events.push('load'));
                    xhr.upload.addEventListener('loadend', () => events.push('loadend'));
                    xhr.open('POST', '/simple.json');
                    xhr.send('hello');
                    await new Promise(f => xhr.onload = f);
                    return events;
                }").ConfigureAwait(false);
            Assert.That(events, Is.EqualTo(new[] { "loadstart", "progress", "load", "loadend" }));
        }

        [PlaywrightTest("capabilities.spec.ts", "loading in HTMLImageElement.prototype")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task LoadingInHtmlImageElementPrototype()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("() => 'loading' in HTMLImageElement.prototype").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("capabilities.spec.ts", "window.GestureEvent in WebKit")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task WindowGestureEventInWebKit()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            bool defined = await page.EvaluateAsync<bool>("() => 'GestureEvent' in window").ConfigureAwait(false);
            Assert.That(defined, Is.EqualTo(TestConstants.IsWebKit));
            string type = await page.EvaluateAsync<string>("() => typeof window.GestureEvent").ConfigureAwait(false);
            Assert.That(type, Is.EqualTo(TestConstants.IsWebKit ? "function" : "undefined"));
        }

        [PlaywrightTest("capabilities.spec.ts", "requestFullscreen")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task RequestFullscreen()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync(
                @"() => {
                    const result = new Promise(resolve => document.addEventListener('fullscreenchange', resolve));
                    void document.documentElement.requestFullscreen().then(() => console.log('success')).catch(e => console.log(e));
                    return result;
                }").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("() => document.fullscreenElement === document.documentElement").ConfigureAwait(false), Is.True);
            await page.EvaluateAsync(
                @"() => {
                    const result = new Promise(resolve => document.addEventListener('fullscreenchange', resolve));
                    void document.exitFullscreen().then(() => console.log('success')).catch(e => console.log(e));
                    return result;
                }").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("() => !!document.fullscreenElement").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("capabilities.spec.ts", "should send no Content-Length header for GET requests with a Content-Type")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendNoContentLengthHeaderForGetRequestsWithAContentType()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<string> requestTask = Server.WaitForRequest("/empty.html", request => request.Headers["content-length"].ToString());
            Task evalTask = page.EvaluateAsync(
                @"() => fetch('/empty.html', {
                    'headers': { 'Content-Type': 'application/json' },
                    'method': 'GET'
                })");
            await evalTask.ConfigureAwait(false);
            string length = await requestTask.ConfigureAwait(false);
            Assert.That(string.IsNullOrEmpty(length), Is.True);
        }

        [PlaywrightTest("capabilities.spec.ts", "Intl.ListFormat should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task IntlListFormatShouldWork()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string formatted = await page.EvaluateAsync<string>(
                @"() => {
                    const data = ['first', 'second', 'third'];
                    const listFormat = new Intl.ListFormat('en', {
                      type: 'disjunction',
                      style: 'short',
                    });
                    return listFormat.format(data);
                }").ConfigureAwait(false);
            Assert.That(formatted, Is.EqualTo("first, second, or third"));
        }

        [PlaywrightTest("capabilities.spec.ts", "service worker should cover the iframe")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ServiceWorkerShouldCoverTheIframe()
        {
            EnsureServer();
            Server.SetRoute("/sw.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync(
                    "<script>\n" +
                    "  window.registrationPromise = navigator.serviceWorker.register('sw.js');\n" +
                    "  window.activationPromise = new Promise(resolve => navigator.serviceWorker.oncontrollerchange = resolve);\n" +
                    "</script>\n");
            });
            Server.SetRoute("/iframe.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<div>from the server</div>");
            });
            Server.SetRoute("/sw.js", http =>
            {
                http.Response.ContentType = "application/javascript";
                return http.Response.WriteAsync(
                    "const kIframeHtml = \"<div>from the service worker</div>\";\n" +
                    "self.addEventListener('fetch', event => {\n" +
                    "  if (event.request.url.endsWith('iframe.html')) {\n" +
                    "    const blob = new Blob([kIframeHtml], { type: 'text/html' });\n" +
                    "    const response = new Response(blob, { status: 200 , statusText: 'OK' });\n" +
                    "    event.respondWith(response);\n" +
                    "    return;\n" +
                    "  }\n" +
                    "  event.respondWith(fetch(event.request));\n" +
                    "});\n" +
                    "self.addEventListener('activate', event => {\n" +
                    "  event.waitUntil(clients.claim());\n" +
                    "});\n");
            });
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/sw.html").ConfigureAwait(false);
            await page.EvaluateAsync("() => window['activationPromise']").ConfigureAwait(false);
            await page.EvaluateAsync(
                @"() => {
                    const iframe = document.createElement('iframe');
                    iframe.src = '/iframe.html';
                    document.body.appendChild(iframe);
                }").ConfigureAwait(false);
            await Assertions.Expect(page.FrameLocator("iframe").Locator("div")).ToHaveTextAsync("from the service worker").ConfigureAwait(false);
        }

        [PlaywrightTest("capabilities.spec.ts", "service worker should register in an iframe")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ServiceWorkerShouldRegisterInAnIframe()
        {
            EnsureServer();
            Server.SetRoute("/main.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<iframe src='/dir/iframe.html'></iframe>\n");
            });
            Server.SetRoute("/dir/iframe.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync(
                    "<script>\n" +
                    "  window.registrationPromise = navigator.serviceWorker.register('sw.js');\n" +
                    "  window.activationPromise = new Promise(resolve => navigator.serviceWorker.oncontrollerchange = resolve);\n" +
                    "</script>\n");
            });
            Server.SetRoute("/dir/sw.js", http =>
            {
                http.Response.ContentType = "application/javascript";
                return http.Response.WriteAsync(
                    "self.addEventListener('fetch', event => {\n" +
                    "  if (event.request.url.endsWith('html')) {\n" +
                    "    event.respondWith(fetch(event.request));\n" +
                    "    return;\n" +
                    "  }\n" +
                    "  const blob = new Blob(['responseFromServiceWorker'], { type: 'text/plain' });\n" +
                    "  const response = new Response(blob, { status: 200 , statusText: 'OK' });\n" +
                    "  event.respondWith(response);\n" +
                    "});\n" +
                    "self.addEventListener('activate', event => {\n" +
                    "  event.waitUntil(clients.claim());\n" +
                    "});\n");
            });
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/main.html").ConfigureAwait(false);
            IFrame iframe = new List<IFrame>(page.Frames)[1];
            await iframe.EvaluateAsync("() => window['activationPromise']").ConfigureAwait(false);
            string response = await iframe.EvaluateAsync<string>(
                @"async () => {
                    const response = await fetch('foo.txt');
                    return response.text();
                }").ConfigureAwait(false);
            Assert.That(response, Is.EqualTo("responseFromServiceWorker"));
        }

        [PlaywrightTest("capabilities.spec.ts", "should be able to render avif images")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToRenderAvifImages()
        {
            if (TestConstants.IsWebKit && TestConstants.IsWindows)
            {
                Assert.Ignore("official skip: browserName === 'webkit' && platform === 'win32'");
            }

            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<img src=\"" + Prefix + "/rgb.avif\" onerror=\"window.error = true\">").ConfigureAwait(false);
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            ElementHandleBoundingBoxResult box = null;
            while (DateTime.UtcNow < deadline)
            {
                box = (await page.Locator("img").BoundingBoxAsync().ConfigureAwait(false))?.AsElementHandleBoundingBox();
                if (box != null && Math.Abs(box.Width - 128) < 0.5 && Math.Abs(box.Height - 128) < 0.5)
                {
                    break;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.That(box, Is.Not.Null);
            Assert.That(box.Width, Is.EqualTo(128).Within(0.5));
            Assert.That(box.Height, Is.EqualTo(128).Within(0.5));
            Assert.That(await page.EvaluateAsync<object>("() => window.error").ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("capabilities.spec.ts", "should not crash when clicking a label with a <input type=\"file\"/>")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCrashWhenClickingALabelWithAInputTypeFile()
        {
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<form>\n  <label>\n    A second file\n    <input type=\"file\" />\n  </label>\n</form>\n").ConfigureAwait(false);
            Task<IFileChooser> chooserTask = page.WaitForFileChooserAsync();
            await page.GetByText("A second file").ClickAsync().ConfigureAwait(false);
            IFileChooser fileChooser = await chooserTask.ConfigureAwait(false);
            Assert.That(fileChooser.Page, Is.SameAs(page));
        }

        [PlaywrightTest("capabilities.spec.ts", "should not crash when clicking a color input")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCrashWhenClickingAColorInput()
        {
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=\"color\">").ConfigureAwait(false);
            ILocator input = page.Locator("input");
            await Assertions.Expect(input).ToBeVisibleAsync().ConfigureAwait(false);
            await input.ClickAsync().ConfigureAwait(false);
            await Assertions.Expect(input).ToBeVisibleAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("capabilities.spec.ts", "should not auto play audio")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotAutoPlayAudio()
        {
            if (TestConstants.IsWebKit && TestConstants.IsWindows)
            {
                Assert.Ignore("audio does not play at all");
            }

            if (string.Equals(Environment.GetEnvironmentVariable("PW_CLOCK"), "frozen", StringComparison.Ordinal))
            {
                Assert.Ignore("no way to inject real setTimeout");
            }

            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.RouteAsync("**/*", route => route.FulfillAsync(new()
            {
                Status = 200,
                ContentType = "text/html",
                Body = "<script>\n" +
                    "  async function onLoad() {\n" +
                    "    const log = document.getElementById('log');\n" +
                    "    const audioContext = new AudioContext();\n" +
                    "    const gainNode = new GainNode(audioContext);\n" +
                    "    gainNode.connect(audioContext.destination);\n" +
                    "    gainNode.gain.value = 0.025;\n" +
                    "    const sineNode = new OscillatorNode(audioContext);\n" +
                    "    sineNode.connect(gainNode);\n" +
                    "    sineNode.start();\n" +
                    "    await new Promise((resolve) => setTimeout(resolve, 1000));\n" +
                    "    log.innerHTML = 'State: ' + audioContext.state;\n" +
                    "  }\n" +
                    "</script>\n" +
                    "<body onload=\"onLoad()\">\n" +
                    "<div id=\"log\"></div>\n" +
                    "</body>"
            })).ConfigureAwait(false);
            await page.GoToAsync("http://127.0.0.1/audio.html").ConfigureAwait(false);
            if (TestConstants.IsWebKit)
            {
                await Assertions.Expect(page.Locator("#log")).ToHaveTextAsync(new Regex("State: (interrupted|suspended)")).ConfigureAwait(false);
            }
            else
            {
                await Assertions.Expect(page.Locator("#log")).ToHaveTextAsync("State: suspended").ConfigureAwait(false);
            }
        }

        [PlaywrightTest("capabilities.spec.ts", "should not crash on feature detection for PublicKeyCredential")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCrashOnFeatureDetectionForPublicKeyCredential()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync(
                @"async () => {
                    await PublicKeyCredential.getClientCapabilities();
                    await PublicKeyCredential.isConditionalMediationAvailable();
                    await PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable();
                }").ConfigureAwait(false);
        }

        private async Task<IPage> NewPageAsync()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            return await context.NewPageAsync().ConfigureAwait(false);
        }

        private static async Task StartOwnedHttpAsync(string contentRoot)
        {
            int basePort = 19968;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    string portText = port.ToString(CultureInfo.InvariantCulture);
                    Prefix = "http://localhost:" + portText;
                    EmptyPage = Prefix + "/empty.html";
                    Host = "localhost:" + portText;
                    Port = port;
                    return;
                }
                catch (Exception)
                {
                }
            }
        }

        private static async Task StartOwnedHttpsAsync(string contentRoot)
        {
            if (TestServerSetup.HttpsServer != null)
            {
                HttpsPrefix = TestConstants.HttpsPrefix;
                return;
            }

            string certPath = EnsureTestCertificate(contentRoot);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PATH", certPath);
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PASSWORD")))
            {
                Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PASSWORD", "playwright");
            }

            int basePort = 19988;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer https = SimpleServer.CreateHttps(port, contentRoot);
                    await https.StartAsync().ConfigureAwait(false);
                    _ownedHttps = https;
                    HttpsPrefix = "https://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    return;
                }
                catch (Exception)
                {
                }
            }
        }

        private static string EnsureTestCertificate(string contentRoot)
        {
            string certPath = Path.Combine(contentRoot, "key.pfx");
            if (File.Exists(certPath))
            {
                return certPath;
            }

            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new(
                "CN=localhost",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            SubjectAlternativeNameBuilder san = new();
            san.AddDnsName("localhost");
            san.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(san.Build());
            using X509Certificate2 cert = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(10));
            File.WriteAllBytes(certPath, cert.Export(X509ContentType.Pfx, "playwright"));
            return certPath;
        }

        private static string FileUrl(string name)
        {
            string root = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            string path = Path.Combine(root, "wwwroot", name);
            return new Uri(path).AbsoluteUri;
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static void EnsureHttps()
        {
            if (HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
            }
        }

        private static async Task DisposeQuietlyAsync(IAsyncDisposable disposable)
        {
            try
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }
    }
}
