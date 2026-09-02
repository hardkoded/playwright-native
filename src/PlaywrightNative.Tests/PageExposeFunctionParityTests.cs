/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-expose-function.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class PageExposeFunctionParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

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
            int basePort = 19735;
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

        [PlaywrightTest("page-expose-function.spec.ts", "exposeBinding should work")]
        [PlaywrightTest("page-expose-function.spec.ts", "exposeBinding should work @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ExposeBindingShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            BindingSource bindingSource = null;
            await page.ExposeBindingAsync("add", (BindingSource source, int a, int b) =>
            {
                bindingSource = source;
                return a + b;
            }).ConfigureAwait(false);

            int result = await page.EvaluateAsync<int>(
                "(async function() { return window['add'](5, 6); })()").ConfigureAwait(false);

            Assert.That(bindingSource, Is.Not.Null);
            Assert.That(bindingSource.Context, Is.SameAs(context));
            Assert.That(bindingSource.Page, Is.SameAs(page));
            Assert.That(bindingSource.Frame, Is.SameAs(page.MainFrame));
            Assert.That(result, Is.EqualTo(11));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.ExposeFunctionAsync("compute", (int a, int b) => a * b).ConfigureAwait(false);
            int result = await page.EvaluateAsync<int>(
                "(async function() { return await window['compute'](9, 4); })()").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(36));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should dispose")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispose()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IAsyncDisposable binding = await page.ExposeFunctionAsync("compute", (int a, int b) => a * b).ConfigureAwait(false);
            int result = await page.EvaluateAsync<int>(
                "(async function() { return await window['compute'](9, 4); })()").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(36));

            await binding.DisposeAsync().ConfigureAwait(false);

            PlaywrightNativeException exception = Assert.ThrowsAsync<PlaywrightNativeException>(
                async () => await page.EvaluateAsync<int>(
                    "(async function() { return await window['compute'](9, 4); })()").ConfigureAwait(false));
            Assert.That(exception.Message, Does.Contain("is not a function"));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should work with handles and complex objects")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithHandlesAndComplexObjects()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle fooHandle = await page.EvaluateHandleAsync(@"() => {
                window['fooValue'] = { bar: 2 };
                return window['fooValue'];
            }").ConfigureAwait(false);

            await page.ExposeFunctionAsync("handle", () => new[] { new { foo = fooHandle } }).ConfigureAwait(false);

            bool equals = await page.EvaluateAsync<bool>(@"(async function() {
                const value = await window['handle']();
                const [{ foo }] = value;
                return foo === window['fooValue'];
            })()").ConfigureAwait(false);
            Assert.That(equals, Is.True);
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should throw exception in page context")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowExceptionInPageContext()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.ExposeFunctionAsync("woof", (Action)(() => throw new PlaywrightNativeException("WOOF WOOF"))).ConfigureAwait(false);
            JsonElement result = await page.EvaluateAsync<JsonElement>(@"(async () => {
                try {
                    await window['woof']();
                } catch (e) {
                    return { message: e.message, stack: e.stack };
                }
            })()").ConfigureAwait(false);

            Assert.That(result.GetProperty("message").GetString(), Is.EqualTo("WOOF WOOF"));
            Assert.That(result.GetProperty("stack").GetString(), Does.Contain(nameof(PageExposeFunctionParityTests)));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should support throwing \"null\"")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportThrowingNull()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.ExposeFunctionAsync("woof", (Action)(() =>
            {
                throw null;
            })).ConfigureAwait(false);

            object thrown = await page.EvaluateAsync<object>(@"(async () => {
                try {
                    await window['woof']();
                } catch (e) {
                    return e;
                }
            })()").ConfigureAwait(false);
            Assert.That(thrown, Is.Null);
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should be callable from-inside addInitScript")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldBeCallableFromInsideAddInitScript()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            bool called = false;
            await page.ExposeFunctionAsync("woof", () =>
            {
                called = true;
            }).ConfigureAwait(false);
            await page.AddInitScriptAsync("window['woof']()").ConfigureAwait(false);
            await page.ReloadAsync().ConfigureAwait(false);
            Assert.That(called, Is.True);
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should survive navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSurviveNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.ExposeFunctionAsync("compute", (int a, int b) => a * b).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            int result = await page.EvaluateAsync<int>(
                "(async function() { return await window['compute'](9, 4); })()").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(36));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should await returned promise")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAwaitReturnedPromise()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.ExposeFunctionAsync("compute", (int a, int b) => Task.FromResult(a * b)).ConfigureAwait(false);
            int result = await page.EvaluateAsync<int>(
                "(async function() { return await window['compute'](3, 5); })()").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(15));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should work on frames")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkOnFrames()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.ExposeFunctionAsync("compute", (int a, int b) => Task.FromResult(a * b)).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/frames/nested-frames.html").ConfigureAwait(false);
            IFrame frame = page.Frames.ElementAt(1);
            int result = await frame.EvaluateAsync<int>(
                "(async function() { return await window['compute'](3, 5); })()").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(15));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should work on frames before navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkOnFramesBeforeNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/frames/nested-frames.html").ConfigureAwait(false);
            await page.ExposeFunctionAsync("compute", (int a, int b) => Task.FromResult(a * b)).ConfigureAwait(false);
            IFrame frame = page.Frames.ElementAt(1);
            int result = await frame.EvaluateAsync<int>(
                "(async function() { return await window['compute'](3, 5); })()").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(15));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should work after cross origin navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkAfterCrossOriginNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.ExposeFunctionAsync("compute", (int a, int b) => a * b).ConfigureAwait(false);
            await page.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            int result = await page.EvaluateAsync<int>(
                "(async function() { return await window['compute'](9, 4); })()").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(36));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should work with complex objects")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithComplexObjects()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.ExposeFunctionAsync("complexObject", (ComplexObject a, ComplexObject b) =>
            {
                return new ComplexObject { x = a.x + b.x };
            }).ConfigureAwait(false);

            JsonElement result = await page.EvaluateAsync<JsonElement>(
                "(async () => window['complexObject']({ x: 5 }, { x: 2 }))()").ConfigureAwait(false);
            Assert.That(result.GetProperty("x").GetInt32(), Is.EqualTo(7));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should throw for duplicate registrations")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowForDuplicateRegistrations()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.ExposeFunctionAsync("foo", () => { }).ConfigureAwait(false);
            PlaywrightNativeException error = Assert.ThrowsAsync<PlaywrightNativeException>(
                async () => await page.ExposeFunctionAsync("foo", () => { }).ConfigureAwait(false));
            Assert.That(error.Message, Does.Contain("page.exposeFunction: Function \"foo\" has been already registered"));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should work with setContent")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithSetContent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.ExposeFunctionAsync("compute", (int a, int b) => Task.FromResult(a * b)).ConfigureAwait(false);
            await page.SetContentAsync("<script>window.result = compute(3, 2)</script>").ConfigureAwait(false);
            int result = await page.EvaluateAsync<int>("(() => window.result)()").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(6));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should alias Window, Document and Node")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAliasWindowDocumentAndNode()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            object logged = null;
            await page.ExposeBindingAsync("log", (BindingSource _, object obj) =>
            {
                logged = obj;
                return (object)null;
            }).ConfigureAwait(false);

            await page.EvaluateAsync<object>("(() => window.log([window, document, document.body]))()").ConfigureAwait(false);
            Assert.That(logged, Is.EqualTo(new object[] { "ref: <Window>", "ref: <Document>", "ref: <Node>" }));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should serialize cycles")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSerializeCycles()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            object logged = null;
            await page.ExposeBindingAsync("log", (BindingSource _, object obj) =>
            {
                logged = obj;
                return (object)null;
            }).ConfigureAwait(false);

            await page.EvaluateAsync<object>("(() => { const a = {}; a.b = a; return window.log(a); })()").ConfigureAwait(false);
            Assert.That(logged, Is.Not.Null);
            Assert.That(logged, Is.InstanceOf<IDictionary<string, object>>());
            IDictionary<string, object> cycle = (IDictionary<string, object>)logged;
            Assert.That(cycle["b"], Is.SameAs(logged));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should work with overridden console object")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithOverriddenConsoleObject()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.EvaluateAsync<object>("(() => { window.console = null; })()").ConfigureAwait(false);
            bool consoleIsNull = await page.EvaluateAsync<bool>("(() => window.console === null)()").ConfigureAwait(false);
            Assert.That(consoleIsNull, Is.True);

            await page.ExposeFunctionAsync("add", (int a, int b) => a + b).ConfigureAwait(false);
            int result = await page.EvaluateAsync<int>("(() => add(5, 6))()").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(11));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should work with busted Array.prototype.map/push")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithBustedArrayPrototypeMapPush()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Server.SetRoute("/test", async http =>
            {
                http.Response.StatusCode = 200;
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync(@"<script>
      Array.prototype.map = null;
      Array.prototype.push = null;
    </script>").ConfigureAwait(false);
            });

            await page.GoToAsync(Prefix + "/test").ConfigureAwait(false);
            await page.ExposeFunctionAsync("add", (int a, int b) => a + b).ConfigureAwait(false);
            int result = await page.EvaluateAsync<int>("(() => add(5, 6))()").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(11));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should fail with busted Array.prototype.toJSON")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailWithBustedArrayPrototypeToJson()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.EvaluateHandleAsync("() => (Array.prototype).toJSON = () => '\"[]\"'").ConfigureAwait(false);
            await page.ExposeFunctionAsync("add", (int a, int b) => a + b).ConfigureAwait(false);

            PlaywrightNativeException exception = Assert.ThrowsAsync<PlaywrightNativeException>(
                async () => await page.EvaluateAsync<int>("(() => add(5, 6))()").ConfigureAwait(false));
            Assert.That(
                exception.Message,
                Does.Contain("serializedArgs is not an array. This can happen when Array.prototype.toJSON is defined incorrectly"));

            string toJson = await page.EvaluateAsync<string>("(() => ([]).toJSON())()").ConfigureAwait(false);
            Assert.That(toJson, Is.EqualTo("\"[]\""));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "exposeBinding should work in parallel")]
        [Test]
        [Timeout(30_000)]
        public async Task ExposeBindingShouldWorkInParallel()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await Task.WhenAll(
                page.ExposeBindingAsync("foo", () => 42),
                page.ExposeBindingAsync("bar", () => 42)).ConfigureAwait(false);

            await page.EvaluateAsync<object>("(() => { window.foo(); window.bar(); })()").ConfigureAwait(false);
        }

        private sealed class ComplexObject
        {
            public int x { get; set; }
        }
    }
}
