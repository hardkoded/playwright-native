/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page-evaluate-handle.spec.ts</c> parity for
    /// <see cref="IPage.EvaluateHandleAsync"/> and passing <see cref="IJSHandle"/>
    /// arguments to <see cref="IPage.EvaluateAsync"/>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class PageEvaluateHandleParityTests : PageTestEx
    {
        [PlaywrightTest("page-evaluate-handle.spec.ts", "should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle windowHandle = await page.EvaluateHandleAsync("() => window").ConfigureAwait(false);
            Assert.That(windowHandle, Is.Not.Null);
        }

        [PlaywrightTest("page-evaluate-handle.spec.ts", "should accept object handle as an argument")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAcceptObjectHandleAsAnArgument()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle navigatorHandle = await page.EvaluateHandleAsync("() => navigator").ConfigureAwait(false);
            string text = await page.EvaluateAsync<string>("e => e.userAgent", navigatorHandle).ConfigureAwait(false);
            Assert.That(text, Does.Contain("Mozilla"));
        }

        [PlaywrightTest("page-evaluate-handle.spec.ts", "should accept object handle to primitive types")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAcceptObjectHandleToPrimitiveTypes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle aHandle = await page.EvaluateHandleAsync("() => 5").ConfigureAwait(false);
            bool isFive = await page.EvaluateAsync<bool>("e => Object.is(e, 5)", aHandle).ConfigureAwait(false);
            Assert.That(isFive, Is.True);
        }

        [PlaywrightTest("page-evaluate-handle.spec.ts", "should accept nested handle")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAcceptNestedHandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle foo = await page.EvaluateHandleAsync("() => ({ x: 1, y: 'foo' })").ConfigureAwait(false);
            JsonElement result = await page.EvaluateAsync<JsonElement>("({ foo }) => foo", new { foo })
                .ConfigureAwait(false);
            Assert.That(result.GetProperty("x").GetInt32(), Is.EqualTo(1));
            Assert.That(result.GetProperty("y").GetString(), Is.EqualTo("foo"));
        }

        [PlaywrightTest("page-evaluate-handle.spec.ts", "should accept nested window handle")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAcceptNestedWindowHandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle foo = await page.EvaluateHandleAsync("() => window").ConfigureAwait(false);
            bool isWindow = await page.EvaluateAsync<bool>("({ foo }) => foo === window", new { foo })
                .ConfigureAwait(false);
            Assert.That(isWindow, Is.True);
        }

        [PlaywrightTest("page-evaluate-handle.spec.ts", "should accept multiple nested handles")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAcceptMultipleNestedHandles()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle foo = await page.EvaluateHandleAsync("() => ({ x: 1, y: 'foo' })").ConfigureAwait(false);
            IJSHandle bar = await page.EvaluateHandleAsync("() => 5").ConfigureAwait(false);
            IJSHandle baz = await page.EvaluateHandleAsync("() => ['baz']").ConfigureAwait(false);
            string result = await page.EvaluateAsync<string>(
                "x => JSON.stringify(x)",
                new
                {
                    a1 = new { foo },
                    a2 = new
                    {
                        bar,
                        arr = new[] { new { baz } },
                    },
                }).ConfigureAwait(false);

            JsonElement json = JsonDocument.Parse(result).RootElement;
            Assert.That(json.GetProperty("a1").GetProperty("foo").GetProperty("x").GetInt32(), Is.EqualTo(1));
            Assert.That(json.GetProperty("a1").GetProperty("foo").GetProperty("y").GetString(), Is.EqualTo("foo"));
            Assert.That(json.GetProperty("a2").GetProperty("bar").GetInt32(), Is.EqualTo(5));
            Assert.That(
                json.GetProperty("a2").GetProperty("arr")[0].GetProperty("baz")[0].GetString(),
                Is.EqualTo("baz"));
        }

        [PlaywrightTest("page-evaluate-handle.spec.ts", "should accept same handle multiple times")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAcceptSameHandleMultipleTimes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle foo = await page.EvaluateHandleAsync("() => 1").ConfigureAwait(false);
            JsonElement result = await page.EvaluateAsync<JsonElement>(
                "x => x",
                new { foo, bar = new[] { foo }, baz = new { foo } }).ConfigureAwait(false);

            Assert.That(result.GetProperty("foo").GetInt32(), Is.EqualTo(1));
            Assert.That(result.GetProperty("bar")[0].GetInt32(), Is.EqualTo(1));
            Assert.That(result.GetProperty("baz").GetProperty("foo").GetInt32(), Is.EqualTo(1));
        }

        [PlaywrightTest("page-evaluate-handle.spec.ts", "should accept same nested object multiple times")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAcceptSameNestedObjectMultipleTimes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            object foo = new { x = 1 };
            JsonElement result = await page.EvaluateAsync<JsonElement>(
                "x => x",
                new { foo, bar = new[] { foo }, baz = new { foo } }).ConfigureAwait(false);

            Assert.That(result.GetProperty("foo").GetProperty("x").GetInt32(), Is.EqualTo(1));
            Assert.That(result.GetProperty("bar")[0].GetProperty("x").GetInt32(), Is.EqualTo(1));
            Assert.That(result.GetProperty("baz").GetProperty("foo").GetProperty("x").GetInt32(), Is.EqualTo(1));
        }

        [PlaywrightTest("page-evaluate-handle.spec.ts", "should accept object handle to unserializable value")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAcceptObjectHandleToUnserializableValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle aHandle = await page.EvaluateHandleAsync("() => Infinity").ConfigureAwait(false);
            bool isInfinity = await page.EvaluateAsync<bool>("e => Object.is(e, Infinity)", aHandle)
                .ConfigureAwait(false);
            Assert.That(isInfinity, Is.True);
        }

        [PlaywrightTest("page-evaluate-handle.spec.ts", "should pass configurable args")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldPassConfigurableArgs()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            JsonElement result = await page.EvaluateAsync<JsonElement>(
                @"arg => {
                    if (arg.foo !== 42)
                      throw new Error('Not a 42');
                    arg.foo = 17;
                    if (arg.foo !== 17)
                      throw new Error('Not 17');
                    delete arg.foo;
                    if (arg.foo === 17)
                      throw new Error('Still 17');
                    return arg;
                }",
                new { foo = 42 }).ConfigureAwait(false);

            Assert.That(result.GetRawText(), Is.EqualTo("{}"));
        }

        [PlaywrightTest("page-evaluate-handle.spec.ts", "should work with primitives")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithPrimitives()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle aHandle = await page.EvaluateHandleAsync(@"() => {
                window['FOO'] = 123;
                return window;
            }").ConfigureAwait(false);
            int value = await page.EvaluateAsync<int>("e => e['FOO']", aHandle).ConfigureAwait(false);
            Assert.That(value, Is.EqualTo(123));
        }
    }
}
