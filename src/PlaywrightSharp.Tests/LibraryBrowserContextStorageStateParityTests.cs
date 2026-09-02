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
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.Helpers;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-storage-state.spec.ts</c> parity.
    /// Do not edit leftover <c>ContextStorageStateTests</c>,
    /// <c>StorageStateCredentialsTests</c>, or persistent storage-state tests.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextStorageStateParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19851;
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
                    CrossProcessPrefix = "http://127.0.0.1:" + portText;
                    return;
                }
                catch (Exception)
                {
                }
            }

            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
                return;
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

            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            _ownedServer?.Reset();
            TestServerSetup.Server?.Reset();
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "should capture local storage")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCaptureLocalStorage()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page1 = await context.NewPageAsync().ConfigureAwait(false);
            await page1.RouteAsync("**/*", route =>
            {
                _ = route.FulfillAsync(new() { Body = "<html></html>" });
            }).ConfigureAwait(false);
            await page1.GoToAsync("https://www.example.com").ConfigureAwait(false);
            await page1.EvaluateAsync("(() => { localStorage[\"name1\"] = \"value1\"; })()").ConfigureAwait(false);
            await page1.GoToAsync("https://www.domain.com").ConfigureAwait(false);
            await page1.EvaluateAsync("(() => { localStorage[\"name2\"] = \"value2\"; })()").ConfigureAwait(false);
            JsonElement origins = Origins(await context.StorageStateAsync().ConfigureAwait(false));
            Assert.That(origins.GetArrayLength(), Is.EqualTo(2));
            Assert.That(origins[0].GetProperty("origin").GetString(), Is.EqualTo("https://www.domain.com"));
            Assert.That(origins[0].GetProperty("localStorage")[0].GetProperty("name").GetString(), Is.EqualTo("name2"));
            Assert.That(origins[0].GetProperty("localStorage")[0].GetProperty("value").GetString(), Is.EqualTo("value2"));
            Assert.That(origins[1].GetProperty("origin").GetString(), Is.EqualTo("https://www.example.com"));
            Assert.That(origins[1].GetProperty("localStorage")[0].GetProperty("name").GetString(), Is.EqualTo("name1"));
            Assert.That(origins[1].GetProperty("localStorage")[0].GetProperty("value").GetString(), Is.EqualTo("value1"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "should set local storage")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetLocalStorage()
        {
            string state = "{\"cookies\":[],\"origins\":[{\"origin\":\"https://www.example.com\",\"localStorage\":[{\"name\":\"name1\",\"value\":\"value1\"}]}]}";
            IBrowserContext context = await _browser.NewContextAsync(new() { StorageState = state }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.RouteAsync("**/*", route =>
            {
                _ = route.FulfillAsync(new() { Body = "<html></html>" });
            }).ConfigureAwait(false);
            await page.GoToAsync("https://www.example.com").ConfigureAwait(false);
            JsonElement localStorage = await page.EvaluateAsync<JsonElement>("() => window.localStorage").ConfigureAwait(false);
            Assert.That(localStorage.GetProperty("name1").GetString(), Is.EqualTo("value1"));

            await context.SetStorageStateAsync("{\"cookies\":[],\"origins\":[{\"origin\":\"https://www.example.com\",\"localStorage\":[{\"name\":\"name2\",\"value\":\"value2\"}]}]}").ConfigureAwait(false);
            Assert.That(context.Pages.Count, Is.EqualTo(1));
            await page.GoToAsync("https://www.example.com").ConfigureAwait(false);
            JsonElement localStorage2 = await page.EvaluateAsync<JsonElement>("() => window.localStorage").ConfigureAwait(false);
            Assert.That(localStorage2.GetProperty("name2").GetString(), Is.EqualTo("value2"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "should report good error if the url is not valid")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldReportGoodErrorIfTheUrlIsNotValid()
        {
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(() =>
                _browser.NewContextAsync(new() { StorageState = "{\"cookies\":[],\"origins\":[{\"origin\":\"foo\",\"localStorage\":[{\"name\":\"name1\",\"value\":\"value1\"}]}]}" }));
            Assert.That(error.Message, Does.Contain("Error setting storage state:"));
            Assert.That(error.Message, Does.Contain("foo"));
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "should capture cookies")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCaptureCookies()
        {
            EnsureServer();
            Server.SetRoute("/setcookie.html", http =>
            {
                http.Response.Headers.Append("Set-Cookie", "a=b");
                http.Response.Headers.Append("Set-Cookie", "empty=");
                return http.Response.WriteAsync(string.Empty);
            });

            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/setcookie.html").ConfigureAwait(false);
            JsonElement cookies = await page.EvaluateAsync<JsonElement>(
                @"(() => {
                    const cookies = document.cookie.split(';');
                    return cookies.map(cookie => cookie.trim()).sort();
                })()").ConfigureAwait(false);
            Assert.That(cookies[0].GetString(), Is.EqualTo("a=b"));
            Assert.That(cookies[1].GetString(), Is.EqualTo("empty="));

            JsonElement storageState = JsonDocument.Parse(await context.StorageStateAsync().ConfigureAwait(false)).RootElement;
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement cookie in storageState.GetProperty("cookies").EnumerateArray())
            {
                names.Add(cookie.GetProperty("name").GetString() + "=" + cookie.GetProperty("value").GetString());
            }

            Assert.That(names.Contains("a=b"), Is.True);
            Assert.That(names.Contains("empty="), Is.True);

            IBrowserContext context2 = await _browser.NewContextAsync(new() { StorageState = storageState.GetRawText() }).ConfigureAwait(false);
            IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
            await page2.GoToAsync(EmptyPage).ConfigureAwait(false);
            JsonElement restored = await page2.EvaluateAsync<JsonElement>(
                @"(() => {
                    const cookies = document.cookie.split(';');
                    return cookies.map(cookie => cookie.trim()).sort();
                })()").ConfigureAwait(false);
            Assert.That(restored[0].GetString(), Is.EqualTo("a=b"));
            Assert.That(restored[1].GetString(), Is.EqualTo("empty="));
            await context2.CloseAsync().ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "should not emit events about internal page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotEmitEventsAboutInternalPage()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.RouteAsync("**/*", route =>
            {
                _ = route.FulfillAsync(new() { Body = "<html></html>" });
            }).ConfigureAwait(false);
            await page.GoToAsync("https://www.example.com").ConfigureAwait(false);
            await page.EvaluateAsync("(() => { localStorage[\"name1\"] = \"value1\"; })()").ConfigureAwait(false);
            await page.GoToAsync("https://www.domain.com").ConfigureAwait(false);
            await page.EvaluateAsync("(() => { localStorage[\"name2\"] = \"value2\"; })()").ConfigureAwait(false);

            List<object> events = new List<object>();
            context.Page += (_, e) => events.Add(e);
            context.Request += (_, e) => events.Add(e);
            context.RequestFailed += (_, e) => events.Add(e);
            context.RequestFinished += (_, e) => events.Add(e);
            context.Response += (_, e) => events.Add(e);
            await context.StorageStateAsync().ConfigureAwait(false);
            Assert.That(events, Is.Empty);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "should not restore localStorage twice")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotRestoreLocalStorageTwice()
        {
            string state = "{\"cookies\":[],\"origins\":[{\"origin\":\"https://www.example.com\",\"localStorage\":[{\"name\":\"name1\",\"value\":\"value1\"}]}]}";
            IBrowserContext context = await _browser.NewContextAsync(new() { StorageState = state }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.RouteAsync("**/*", route =>
            {
                _ = route.FulfillAsync(new() { Body = "<html></html>" });
            }).ConfigureAwait(false);
            await page.GoToAsync("https://www.example.com").ConfigureAwait(false);
            JsonElement localStorage1 = await page.EvaluateAsync<JsonElement>("() => window.localStorage").ConfigureAwait(false);
            Assert.That(localStorage1.GetProperty("name1").GetString(), Is.EqualTo("value1"));
            await page.EvaluateAsync("(() => { window.localStorage[\"name1\"] = \"value2\"; })()").ConfigureAwait(false);
            await page.GoToAsync("https://www.example.com").ConfigureAwait(false);
            JsonElement localStorage2 = await page.EvaluateAsync<JsonElement>("() => window.localStorage").ConfigureAwait(false);
            Assert.That(localStorage2.GetProperty("name1").GetString(), Is.EqualTo("value2"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "should handle missing file")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldHandleMissingFile()
        {
            string file = Path.Combine(Path.GetTempPath(), "pwsharp-does-not-exist-" + Guid.NewGuid().ToString("N") + ".json");
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => _browser.NewContextAsync(new BrowserContextOptions { StorageStatePath = file }));
            Assert.That(error.Message, Does.Contain("Error reading storage state from " + file + ":\nENOENT"));
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "should handle malformed file")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldHandleMalformedFile()
        {
            string file = Path.Combine(Path.GetTempPath(), "pwsharp-state-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(file, "not-json");
            try
            {
                PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                    () => _browser.NewContextAsync(new BrowserContextOptions { StorageStatePath = file }));
                Assert.That(
                    error.Message,
                    Does.Contain("Error reading storage state from " + file + ":\nUnexpected token 'o', \"not-json\" is not valid JSON"));
            }
            finally
            {
                File.Delete(file);
            }
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "should serialize storageState with lone surrogates")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSerializeStorageStateWithLoneSurrogates()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync("(() => window.localStorage.setItem('foo', String.fromCharCode(55934)))()").ConfigureAwait(false);
            string json = await context.StorageStateAsync().ConfigureAwait(false);
            StorageState state = StorageStateHelper.Load(json, null);
            string value = null;
            foreach (StorageStateOrigin origin in state.Origins)
            {
                foreach (NameValueEntry item in origin.LocalStorage)
                {
                    if (item.Name == "foo")
                    {
                        value = item.Value;
                    }
                }
            }

            Assert.That(value, Is.EqualTo(((char)55934).ToString()));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "should work when service worker is intefering")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWhenServiceWorkerIsIntefering()
        {
            EnsureServer();
            Server.SetRoute("/", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync(
                    "<script>window.localStorage.foo = 'bar'; window.registrationPromise = navigator.serviceWorker.register('sw.js'); window.activationPromise = new Promise(resolve => navigator.serviceWorker.oncontrollerchange = resolve);</script>");
            });
            Server.SetRoute("/sw.js", http =>
            {
                http.Response.ContentType = "application/javascript";
                return http.Response.WriteAsync("self.addEventListener('activate', event => event.waitUntil(clients.claim()));");
            });

            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix).ConfigureAwait(false);
            await page.EvaluateAsync("(() => window[\"activationPromise\"])()").ConfigureAwait(false);
            JsonElement origins = Origins(await context.StorageStateAsync().ConfigureAwait(false));
            Assert.That(origins[0].GetProperty("localStorage")[0].GetProperty("name").GetString(), Is.EqualTo("foo"));
            Assert.That(origins[0].GetProperty("localStorage")[0].GetProperty("value").GetString(), Is.EqualTo("bar"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "should set local storage in third-party context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetLocalStorageInThirdPartyContext()
        {
            EnsureServer();
            string state = "{\"cookies\":[],\"origins\":[{\"origin\":\"" + CrossProcessPrefix + "\",\"localStorage\":[{\"name\":\"name1\",\"value\":\"value1\"}]}]}";
            IBrowserContext context = await _browser.NewContextAsync(new() { StorageState = state }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame frame = await AttachFrameAsync(page, "frame1", CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            JsonElement localStorage = await frame.EvaluateAsync<JsonElement>("() => window.localStorage").ConfigureAwait(false);
            Assert.That(localStorage.GetProperty("name1").GetString(), Is.EqualTo("value1"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "should roundtrip local storage in third-party context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRoundtripLocalStorageInThirdPartyContext()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame frame = await AttachFrameAsync(page, "frame1", CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            await frame.EvaluateAsync("(() => window.localStorage.setItem('name1', 'value1'))()").ConfigureAwait(false);
            string storageState = await context.StorageStateAsync().ConfigureAwait(false);

            IBrowserContext context2 = await _browser.NewContextAsync(new() { StorageState = storageState }).ConfigureAwait(false);
            IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
            await page2.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame frame2 = await AttachFrameAsync(page2, "frame1", CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            JsonElement localStorage = await frame2.EvaluateAsync<JsonElement>("() => window.localStorage").ConfigureAwait(false);
            Assert.That(localStorage.GetProperty("name1").GetString(), Is.EqualTo("value1"));
            await context2.CloseAsync().ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "should round-trip through the file")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRoundTripThroughTheFile()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page1 = await context.NewPageAsync().ConfigureAwait(false);
            await page1.RouteAsync("**/*", route =>
            {
                _ = route.FulfillAsync(new() { Body = "<html></html>" });
            }).ConfigureAwait(false);
            await page1.GoToAsync("https://www.example.com").ConfigureAwait(false);
            await page1.EvaluateAsync(
                @"(async () => {
                    localStorage['name1'] = 'value1';
                    document.cookie = 'username=John Doe';
                    await new Promise((resolve, reject) => {
                        const openRequest = indexedDB.open('db', 42);
                        openRequest.onupgradeneeded = () => {
                            openRequest.result.createObjectStore('store', { keyPath: 'name' });
                            openRequest.result.createObjectStore('store2');
                        };
                        openRequest.onsuccess = () => {
                            const transaction = openRequest.result.transaction(['store', 'store2'], 'readwrite');
                            transaction.objectStore('store').put({ name: 'foo', date: new Date(0), null: null });
                            transaction.objectStore('store2').put(new TextEncoder().encode('bar'), 'foo');
                            transaction.addEventListener('complete', resolve);
                            transaction.addEventListener('error', reject);
                        };
                    });
                    return document.cookie;
                })()").ConfigureAwait(false);

            string path = Path.Combine(Path.GetTempPath(), "pwsharp-storage-state-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                string state = await context.StorageStateAsync(path, indexedDB: true).ConfigureAwait(false);
                string written = File.ReadAllText(path);
                Assert.That(StorageStateHelper.PrettyPrint(state), Is.EqualTo(written));

                async Task CheckContextAsync(IBrowserContext target)
                {
                    IPage page = await target.NewPageAsync().ConfigureAwait(false);
                    await page.RouteAsync("**/*", route =>
                    {
                        _ = route.FulfillAsync(new() { Body = "<html></html>" });
                    }).ConfigureAwait(false);
                    await page.GoToAsync("https://www.example.com").ConfigureAwait(false);
                    JsonElement localStorage = await page.EvaluateAsync<JsonElement>("() => window.localStorage").ConfigureAwait(false);
                    Assert.That(localStorage.GetProperty("name1").GetString(), Is.EqualTo("value1"));
                    string cookie = await page.EvaluateAsync<string>("() => document.cookie").ConfigureAwait(false);
                    Assert.That(cookie, Is.EqualTo("username=John Doe"));
                    bool idbOk = await page.EvaluateAsync<bool>(
                        @"(async () => {
                            return await new Promise((resolve, reject) => {
                                const openRequest = indexedDB.open('db', 42);
                                openRequest.addEventListener('success', async () => {
                                    const db = openRequest.result;
                                    const transaction = db.transaction(['store', 'store2'], 'readonly');
                                    const request1 = transaction.objectStore('store').get('foo');
                                    const request2 = transaction.objectStore('store2').get('foo');
                                    const [result1, result2] = await Promise.all([request1, request2].map(request => new Promise((resolve, reject) => {
                                        request.addEventListener('success', () => resolve(request.result));
                                        request.addEventListener('error', () => reject(request.error));
                                    })));
                                    resolve(
                                        result1
                                        && result1.name === 'foo'
                                        && result1.date instanceof Date
                                        && result1.date.getTime() === 0
                                        && result1.null === null
                                        && new TextDecoder().decode(result2) === 'bar');
                                });
                                openRequest.addEventListener('error', () => reject(openRequest.error));
                            });
                        })()").ConfigureAwait(false);
                    Assert.That(idbOk, Is.True);
                }

                IBrowserContext context2 = await _browser.NewContextAsync(new BrowserContextOptions { StorageStatePath = path }).ConfigureAwait(false);
                await CheckContextAsync(context2).ConfigureAwait(false);
                await context2.CloseAsync().ConfigureAwait(false);

                IBrowserContext context3 = await _browser.NewContextAsync().ConfigureAwait(false);
                await context3.SetStorageStateAsync(storageStatePath: path).ConfigureAwait(false);
                Assert.That(context3.Pages.Count, Is.EqualTo(0));
                await CheckContextAsync(context3).ConfigureAwait(false);
                await context3.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "should support IndexedDB")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportIndexedDB()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/to-do-notifications/index.html").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#notifications")).ToMatchAriaSnapshotAsync(@"
                - list:
                  - listitem: Database initialised.
            ").ConfigureAwait(false);
            await page.GetByLabel("Task title").FillAsync("Pet the cat").ConfigureAwait(false);
            await page.GetByLabel("Hours").FillAsync("1").ConfigureAwait(false);
            await page.GetByLabel("Mins").FillAsync("1").ConfigureAwait(false);
            await page.GetByText("Add Task").ClickAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#notifications")).ToMatchAriaSnapshotAsync(@"
                - list:
                  - listitem: ""Transaction completed: database modification finished.""
            ").ConfigureAwait(false);

            JsonElement storageState = JsonDocument.Parse(await context.StorageStateAsync(new() { IndexedDB = true }).ConfigureAwait(false)).RootElement;
            AssertIndexedDBTodoDump(storageState, Prefix);

            IBrowserContext restored = await _browser.NewContextAsync(new() { StorageState = storageState.GetRawText() }).ConfigureAwait(false);
            JsonElement again = JsonDocument.Parse(await restored.StorageStateAsync(new() { IndexedDB = true }).ConfigureAwait(false)).RootElement;
            AssertIndexedDBTodoDump(again, Prefix);

            IPage recreatedPage = await restored.NewPageAsync().ConfigureAwait(false);
            await recreatedPage.GoToAsync(Prefix + "/to-do-notifications/index.html").ConfigureAwait(false);
            await Assertions.Expect(recreatedPage.Locator("#task-list")).ToMatchAriaSnapshotAsync(@"
                - list:
                  - listitem:
                    - text: /Pet the cat \[Pet the cat\]/
            ").ConfigureAwait(false);

            JsonElement withoutIndexed = JsonDocument.Parse(await restored.StorageStateAsync().ConfigureAwait(false)).RootElement;
            Assert.That(withoutIndexed.GetProperty("cookies").GetArrayLength(), Is.EqualTo(0));
            Assert.That(withoutIndexed.GetProperty("origins").GetArrayLength(), Is.EqualTo(0));
            await restored.CloseAsync().ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "should support empty indexedDB")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportEmptyIndexedDB()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync(
                @"(async () => {
                    await new Promise(resolve => {
                        const openRequest = indexedDB.open('unused-db');
                        openRequest.onsuccess = () => resolve();
                        openRequest.onerror = () => resolve();
                    });
                })()").ConfigureAwait(false);
            JsonElement storageState = JsonDocument.Parse(await context.StorageStateAsync(new() { IndexedDB = true }).ConfigureAwait(false)).RootElement;
            AssertEmptyUnusedDb(storageState, Prefix);

            IBrowserContext restored = await _browser.NewContextAsync(new() { StorageState = storageState.GetRawText() }).ConfigureAwait(false);
            JsonElement again = JsonDocument.Parse(await restored.StorageStateAsync(new() { IndexedDB = true }).ConfigureAwait(false)).RootElement;
            AssertEmptyUnusedDb(again, Prefix);
            await restored.CloseAsync().ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "should not leave IndexedDB connections open")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotLeaveIndexedDBConnectionsOpen()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync(
                @"(async () => {
                    const openRequest = indexedDB.open('db', 1);
                    openRequest.onupgradeneeded = () => openRequest.result.createObjectStore('store');
                    await new Promise((resolve, reject) => {
                        openRequest.onsuccess = () => {
                            const db = openRequest.result;
                            const transaction = db.transaction('store', 'readwrite');
                            transaction.objectStore('store').put('value', 'key');
                            transaction.oncomplete = () => {
                                db.close();
                                resolve();
                            };
                            transaction.onerror = () => reject(transaction.error);
                        };
                        openRequest.onerror = () => reject(openRequest.error);
                    });
                })()").ConfigureAwait(false);

            string state = await context.StorageStateAsync(new() { IndexedDB = true }).ConfigureAwait(false);
            await page.EvaluateAsync(
                @"(async () => {
                    await new Promise((resolve, reject) => {
                        const request = indexedDB.deleteDatabase('db');
                        request.onsuccess = () => resolve();
                        request.onerror = () => reject(request.error);
                        request.onblocked = () => reject(new Error('deleteDatabase was blocked'));
                    });
                })()").ConfigureAwait(false);

            await context.SetStorageStateAsync(state).ConfigureAwait(false);
            Assert.That(await context.StorageStateAsync(new() { IndexedDB = true }).ConfigureAwait(false), Is.EqualTo(state));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "should round-trip WebAuthn credentials with storageState")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRoundTripWebAuthnCredentialsWithStorageState()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            VirtualCredential credential = await context.Credentials.CreateAsync(new Uri(Prefix).Host).ConfigureAwait(false);

            JsonElement omitted = JsonDocument.Parse(await context.StorageStateAsync().ConfigureAwait(false)).RootElement;
            Assert.That(omitted.GetProperty("cookies").GetArrayLength(), Is.EqualTo(0));
            Assert.That(omitted.GetProperty("origins").GetArrayLength(), Is.EqualTo(0));
            Assert.That(omitted.TryGetProperty("credentials", out _), Is.False);

            string storageState = await context.StorageStateAsync(true).ConfigureAwait(false);
            AssertCredentialState(storageState, credential);

            IBrowserContext context2 = await _browser.NewContextAsync(new() { StorageState = storageState }).ConfigureAwait(false);
            IReadOnlyList<VirtualCredential> list = await context2.Credentials.GetAsync().ConfigureAwait(false);
            Assert.That(list, Has.Exactly(1).Items);
            AssertCredential(list[0], credential);
            Assert.That(await context2.StorageStateAsync(true).ConfigureAwait(false), Is.EqualTo(storageState));
            await context2.CloseAsync().ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "setStorageState should replace credentials")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SetStorageStateShouldReplaceCredentials()
        {
            IBrowserContext ctxA = await _browser.NewContextAsync().ConfigureAwait(false);
            VirtualCredential credA = await ctxA.Credentials.CreateAsync("a.example.com").ConfigureAwait(false);
            string stateA = await ctxA.StorageStateAsync(true).ConfigureAwait(false);

            IBrowserContext ctxB = await _browser.NewContextAsync().ConfigureAwait(false);
            VirtualCredential credB = await ctxB.Credentials.CreateAsync("b.example.com").ConfigureAwait(false);
            string stateB = await ctxB.StorageStateAsync(true).ConfigureAwait(false);

            IBrowserContext context = await _browser.NewContextAsync(new() { StorageState = stateA }).ConfigureAwait(false);
            IReadOnlyList<VirtualCredential> first = await context.Credentials.GetAsync().ConfigureAwait(false);
            Assert.That(first, Has.Exactly(1).Items);
            AssertCredential(first[0], credA);

            await context.SetStorageStateAsync(stateB).ConfigureAwait(false);
            IReadOnlyList<VirtualCredential> second = await context.Credentials.GetAsync().ConfigureAwait(false);
            Assert.That(second, Has.Exactly(1).Items);
            AssertCredential(second[0], credB);

            await context.SetStorageStateAsync("{\"cookies\":[],\"origins\":[]}").ConfigureAwait(false);
            Assert.That(await context.Credentials.GetAsync().ConfigureAwait(false), Is.Empty);

            await context.SetStorageStateAsync(stateA).ConfigureAwait(false);
            IReadOnlyList<VirtualCredential> third = await context.Credentials.GetAsync().ConfigureAwait(false);
            Assert.That(third, Has.Exactly(1).Items);
            AssertCredential(third[0], credA);

            await context.CloseAsync().ConfigureAwait(false);
            await ctxB.CloseAsync().ConfigureAwait(false);
            await ctxA.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "setStorageState should handle missing file")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SetStorageStateShouldHandleMissingFile()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            string file = Path.Combine(Path.GetTempPath(), "pwsharp-does-not-exist-" + Guid.NewGuid().ToString("N") + ".json");
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => context.SetStorageStateAsync(storageStatePath: file));
            Assert.That(error.Message, Does.Contain("Error reading storage state from " + file));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private static void AssertIndexedDBTodoDump(JsonElement storageState, string origin)
        {
            JsonElement origins = storageState.GetProperty("origins");
            Assert.That(origins.GetArrayLength(), Is.EqualTo(1));
            Assert.That(origins[0].GetProperty("origin").GetString(), Is.EqualTo(origin));
            Assert.That(origins[0].GetProperty("localStorage").GetArrayLength(), Is.EqualTo(0));
            JsonElement indexed = origins[0].GetProperty("indexedDB");
            Assert.That(indexed.GetArrayLength(), Is.EqualTo(1));
            Assert.That(indexed[0].GetProperty("name").GetString(), Is.EqualTo("toDoList"));
            Assert.That(indexed[0].GetProperty("version").GetInt32(), Is.EqualTo(4));
            JsonElement stores = indexed[0].GetProperty("stores");
            Assert.That(stores.GetArrayLength(), Is.EqualTo(1));
            Assert.That(stores[0].GetProperty("name").GetString(), Is.EqualTo("toDoList"));
            Assert.That(stores[0].GetProperty("autoIncrement").GetBoolean(), Is.False);
            Assert.That(stores[0].GetProperty("keyPath").GetString(), Is.EqualTo("taskTitle"));
            JsonElement records = stores[0].GetProperty("records");
            Assert.That(records.GetArrayLength(), Is.EqualTo(1));
            JsonElement encoded = records[0].GetProperty("valueEncoded");
            Assert.That(encoded.GetProperty("id").GetInt32(), Is.EqualTo(1));
            JsonElement fields = encoded.GetProperty("o");
            Assert.That(Field(fields, "taskTitle"), Is.EqualTo("Pet the cat"));
            Assert.That(Field(fields, "hours"), Is.EqualTo("1"));
            Assert.That(Field(fields, "minutes"), Is.EqualTo("1"));
            Assert.That(Field(fields, "day"), Is.EqualTo("01"));
            Assert.That(Field(fields, "month"), Is.EqualTo("January"));
            Assert.That(Field(fields, "year"), Is.EqualTo("2025"));
            Assert.That(Field(fields, "notified"), Is.EqualTo("no"));
            JsonElement binary = default;
            foreach (JsonElement entry in fields.EnumerateArray())
            {
                if (entry.GetProperty("k").GetString() == "binaryTitle")
                {
                    binary = entry.GetProperty("v");
                }
            }

            Assert.That(binary.ValueKind, Is.EqualTo(JsonValueKind.Object));
            Assert.That(binary.GetProperty("ab").GetProperty("b").GetString(), Is.EqualTo("UGV0IHRoZSBjYXQ="));

            string[] expectedIndexes = { "day", "hours", "minutes", "month", "notified", "year" };
            JsonElement indexes = stores[0].GetProperty("indexes");
            Assert.That(indexes.GetArrayLength(), Is.EqualTo(expectedIndexes.Length));
            for (int i = 0; i < expectedIndexes.Length; i++)
            {
                Assert.That(indexes[i].GetProperty("name").GetString(), Is.EqualTo(expectedIndexes[i]));
                Assert.That(indexes[i].GetProperty("keyPath").GetString(), Is.EqualTo(expectedIndexes[i]));
                Assert.That(indexes[i].GetProperty("multiEntry").GetBoolean(), Is.False);
                Assert.That(indexes[i].GetProperty("unique").GetBoolean(), Is.False);
            }
        }

        private static string Field(JsonElement fields, string name)
        {
            foreach (JsonElement entry in fields.EnumerateArray())
            {
                if (entry.GetProperty("k").GetString() == name)
                {
                    return entry.GetProperty("v").GetString();
                }
            }

            Assert.Fail("Missing encoded field " + name);
            return null;
        }

        private static void AssertEmptyUnusedDb(JsonElement storageState, string origin)
        {
            JsonElement origins = storageState.GetProperty("origins");
            Assert.That(origins.GetArrayLength(), Is.EqualTo(1));
            Assert.That(origins[0].GetProperty("origin").GetString(), Is.EqualTo(origin));
            Assert.That(origins[0].GetProperty("localStorage").GetArrayLength(), Is.EqualTo(0));
            JsonElement indexed = origins[0].GetProperty("indexedDB");
            Assert.That(indexed.GetArrayLength(), Is.EqualTo(1));
            Assert.That(indexed[0].GetProperty("name").GetString(), Is.EqualTo("unused-db"));
            Assert.That(indexed[0].GetProperty("version").GetInt32(), Is.EqualTo(1));
            Assert.That(indexed[0].GetProperty("stores").GetArrayLength(), Is.EqualTo(0));
        }

        private static void AssertCredentialState(string json, VirtualCredential credential)
        {
            JsonElement root = JsonDocument.Parse(json).RootElement;
            Assert.That(root.GetProperty("cookies").GetArrayLength(), Is.EqualTo(0));
            Assert.That(root.GetProperty("origins").GetArrayLength(), Is.EqualTo(0));
            JsonElement credentials = root.GetProperty("credentials");
            Assert.That(credentials.GetArrayLength(), Is.EqualTo(1));
            Assert.That(credentials[0].GetProperty("id").GetString(), Is.EqualTo(credential.Id));
            Assert.That(credentials[0].GetProperty("rpId").GetString(), Is.EqualTo(credential.RpId));
            Assert.That(credentials[0].GetProperty("userHandle").GetString(), Is.EqualTo(credential.UserHandle));
            Assert.That(credentials[0].GetProperty("privateKey").GetString(), Is.EqualTo(credential.PrivateKey));
            Assert.That(credentials[0].GetProperty("publicKey").GetString(), Is.EqualTo(credential.PublicKey));
        }

        private static void AssertCredential(VirtualCredential actual, VirtualCredential expected)
        {
            Assert.That(actual.Id, Is.EqualTo(expected.Id));
            Assert.That(actual.RpId, Is.EqualTo(expected.RpId));
            Assert.That(actual.UserHandle, Is.EqualTo(expected.UserHandle));
            Assert.That(actual.PrivateKey, Is.EqualTo(expected.PrivateKey));
            Assert.That(actual.PublicKey, Is.EqualTo(expected.PublicKey));
        }

        private static JsonElement Origins(string json)
            => JsonDocument.Parse(json).RootElement.GetProperty("origins").Clone();

        private static async Task<IFrame> AttachFrameAsync(IPage page, string frameId, string url)
        {
            string frameIdJson = JsonSerializer.Serialize(frameId);
            string urlJson = JsonSerializer.Serialize(url);
            await page.EvaluateAsync<object>(
                "(async () => { const frame = document.createElement('iframe'); frame.src = " +
                urlJson + "; frame.id = " + frameIdJson + "; document.body.appendChild(frame); await new Promise(x => frame.onload = x); })()")
                .ConfigureAwait(false);

            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                IFrame named = page.Frame(frameId);
                if (named != null && !named.IsDetached)
                {
                    return named;
                }

                foreach (IFrame frame in page.Frames)
                {
                    if (!ReferenceEquals(frame, page.MainFrame) && !frame.IsDetached)
                    {
                        return frame;
                    }
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            Assert.Fail("Timed out waiting for frame " + frameId);
            return null;
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
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
