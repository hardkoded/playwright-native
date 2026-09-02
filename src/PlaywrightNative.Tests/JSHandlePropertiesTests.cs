/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.Helpers;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>jshandle-properties.spec.ts</c>.
    /// </summary>
    [TestFixture]
    public class JSHandlePropertiesTests : PageTestEx
    {
        /// <summary>
        /// Mirrors JavaScript <c>String(value)</c> for <c>jsonValue</c> assertions
        /// (<c>String(undefined) === 'undefined'</c>, <c>String(NaN) === 'NaN'</c>).
        /// </summary>
        /// <param name="value">The deserialized <c>jsonValue</c>.</param>
        /// <returns>The JS <c>String(...)</c> equivalent.</returns>
        private static string StringifyJsValue(object value)
        {
            if (value == null)
            {
                return "undefined";
            }

            if (value is double d)
            {
                if (double.IsNaN(d))
                {
                    return "NaN";
                }

                if (double.IsPositiveInfinity(d))
                {
                    return "Infinity";
                }

                if (double.IsNegativeInfinity(d))
                {
                    return "-Infinity";
                }
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        [PlaywrightTest("jshandle-properties.spec.ts", "should work")]
        [PlaywrightTest("jshandle-properties.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle aHandle = await page.EvaluateHandleAsync(@"() => ({
                one: 1,
                two: 2,
                three: 3
            })").ConfigureAwait(false);
            IJSHandle twoHandle = await aHandle.GetPropertyAsync("two").ConfigureAwait(false);
            Assert.That(await twoHandle.JsonValueAsync<int>().ConfigureAwait(false), Is.EqualTo(2));
        }

        [PlaywrightTest("jshandle-properties.spec.ts", "should work with undefined, null, and empty")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithUndefinedNullAndEmpty()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle aHandle = await page.EvaluateHandleAsync(@"() => ({
                undefined: undefined,
                null: null,
            })").ConfigureAwait(false);
            IJSHandle undefinedHandle = await aHandle.GetPropertyAsync("undefined").ConfigureAwait(false);
            Assert.That(StringifyJsValue(await undefinedHandle.JsonValueAsync<object>().ConfigureAwait(false)), Is.EqualTo("undefined"));
            IJSHandle nullHandle = await aHandle.GetPropertyAsync("null").ConfigureAwait(false);
            Assert.That(await nullHandle.JsonValueAsync<object>().ConfigureAwait(false), Is.Null);
            IJSHandle emptyHandle = await aHandle.GetPropertyAsync("empty").ConfigureAwait(false);
            Assert.That(StringifyJsValue(await emptyHandle.JsonValueAsync<object>().ConfigureAwait(false)), Is.EqualTo("undefined"));
        }

        [PlaywrightTest("jshandle-properties.spec.ts", "should work with unserializable values")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithUnserializableValues()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle aHandle = await page.EvaluateHandleAsync(@"() => ({
                infinity: Infinity,
                nInfinity: -Infinity,
                nan: NaN,
                nzero: -0
            })").ConfigureAwait(false);
            IJSHandle infinityHandle = await aHandle.GetPropertyAsync("infinity").ConfigureAwait(false);
            Assert.That(await infinityHandle.JsonValueAsync<double>().ConfigureAwait(false), Is.EqualTo(double.PositiveInfinity));
            IJSHandle nInfinityHandle = await aHandle.GetPropertyAsync("nInfinity").ConfigureAwait(false);
            Assert.That(await nInfinityHandle.JsonValueAsync<double>().ConfigureAwait(false), Is.EqualTo(double.NegativeInfinity));
            IJSHandle nanHandle = await aHandle.GetPropertyAsync("nan").ConfigureAwait(false);
            Assert.That(StringifyJsValue(await nanHandle.JsonValueAsync<object>().ConfigureAwait(false)), Is.EqualTo("NaN"));
            IJSHandle nzeroHandle = await aHandle.GetPropertyAsync("nzero").ConfigureAwait(false);
            Assert.That((await nzeroHandle.JsonValueAsync<double>().ConfigureAwait(false)).IsNegativeZero(), Is.True);
        }

        [PlaywrightTest("jshandle-properties.spec.ts", "getProperties should work")]
        [Test]
        [Timeout(30_000)]
        public async Task GetPropertiesShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle aHandle = await page.EvaluateHandleAsync(@"() => ({
                foo: 'bar'
            })").ConfigureAwait(false);
            Dictionary<string, IJSHandle> properties = await aHandle.GetPropertiesAsync().ConfigureAwait(false);
            Assert.That(properties.TryGetValue("foo", out IJSHandle foo), Is.True);
            Assert.That(foo, Is.Not.Null);
            Assert.That(await foo.JsonValueAsync<string>().ConfigureAwait(false), Is.EqualTo("bar"));
        }

        [PlaywrightTest("jshandle-properties.spec.ts", "getProperties should return empty map for non-objects")]
        [Test]
        [Timeout(30_000)]
        public async Task GetPropertiesShouldReturnEmptyMapForNonObjects()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle aHandle = await page.EvaluateHandleAsync("() => 123").ConfigureAwait(false);
            Dictionary<string, IJSHandle> properties = await aHandle.GetPropertiesAsync().ConfigureAwait(false);
            Assert.That(properties, Is.Empty);
        }

        [PlaywrightTest("jshandle-properties.spec.ts", "getProperties should return even non-own properties")]
        [Test]
        [Timeout(30_000)]
        public async Task GetPropertiesShouldReturnEvenNonOwnProperties()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle aHandle = await page.EvaluateHandleAsync(@"() => {
                class A {
                    constructor() {
                        this.a = '1';
                    }
                }
                class B extends A {
                    constructor() {
                        super();
                        this.b = '2';
                    }
                }
                return new B();
            }").ConfigureAwait(false);
            Dictionary<string, IJSHandle> properties = await aHandle.GetPropertiesAsync().ConfigureAwait(false);
            Assert.That(await properties["a"].JsonValueAsync<string>().ConfigureAwait(false), Is.EqualTo("1"));
            Assert.That(await properties["b"].JsonValueAsync<string>().ConfigureAwait(false), Is.EqualTo("2"));
        }

        [PlaywrightTest("jshandle-properties.spec.ts", "getProperties should work with elements")]
        [Test]
        [Timeout(30_000)]
        public async Task GetPropertiesShouldWorkWithElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>Hello</div>").ConfigureAwait(false);
            IJSHandle handle = await page.EvaluateHandleAsync("() => ({ body: document.body })").ConfigureAwait(false);
            Dictionary<string, IJSHandle> properties = await handle.GetPropertiesAsync().ConfigureAwait(false);
            Assert.That(properties.TryGetValue("body", out IJSHandle bodyHandle), Is.True);
            Assert.That(bodyHandle, Is.Not.Null);
            IElementHandle body = bodyHandle as IElementHandle ?? bodyHandle.AsElement();
            Assert.That(body, Is.Not.Null);
            Assert.That(await body.TextContentAsync().ConfigureAwait(false), Is.EqualTo("Hello"));
        }
    }
}
