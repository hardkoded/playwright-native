/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for promise-aware evaluation. WebKit's
    /// <c>Runtime.evaluate</c> does not honor <c>awaitPromise</c>, so the execution
    /// context unwraps promises by piping the result through <c>Runtime.callFunctionOn</c>.
    /// Mirrors upstream <c>page-evaluate.spec.ts</c> "should await promise".
    /// </summary>
    [TestFixture]
    public class PageEvaluateTests : PageTestEx
    {
        [PlaywrightTest("page-evaluate.spec.ts", "should await promise")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAwaitPromise()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);

            int result = await page.EvaluateAsync<int>("Promise.resolve(8 * 7)").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(56));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should await asynchronously resolved promise")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAwaitAsynchronouslyResolvedPromise()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);

            int result = await page
                .EvaluateAsync<int>("new Promise(resolve => setTimeout(() => resolve(7), 50))")
                .ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(7));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should return non-promise values unchanged")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnNonPromiseValuesUnchanged()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);

            int sum = await page.EvaluateAsync<int>("1 + 2").ConfigureAwait(false);
            Assert.That(sum, Is.EqualTo(3));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should work with argument")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEvaluateFunctionWithArgument()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);

            int result = await page.EvaluateAsync<int>("x => x * 2", 7).ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(14));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should return a handle")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnHandleForDocumentBody()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=\"box\">x</div>").ConfigureAwait(false);
            IJSHandle handle = await page.EvaluateHandleAsync("document.body").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(handle.AsElement(), Is.Not.Null);
            await handle.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "nested handle AsElement returns an element")]
        [Test]
        [Timeout(30_000)]
        public async Task NestedHandleAsElementShouldReturnAnElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=\"box\">wave317</div>").ConfigureAwait(false);
            IJSHandle document = await page.EvaluateHandleAsync("document").ConfigureAwait(false);
            IJSHandle body = await document.EvaluateHandleAsync("doc => doc.body").ConfigureAwait(false);

            Assert.That(body, Is.Not.Null);
            Assert.That(body.AsElement(), Is.Not.Null);
            string text = await body.AsElement().InnerTextAsync().ConfigureAwait(false);
            Assert.That(text, Does.Contain("wave317"));

            await body.DisposeAsync().ConfigureAwait(false);
            await document.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should work with jsonValue")]
        [Test]
        [Timeout(30_000)]
        public async Task JsonValueShouldReturnSerializedHandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            IJSHandle handle = await page.EvaluateHandleAsync("({ foo: 42 })").ConfigureAwait(false);
            JsonElement json = await handle.JsonValueAsync<JsonElement>().ConfigureAwait(false);
            Assert.That(json.GetProperty("foo").GetInt32(), Is.EqualTo(42));
            await handle.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "JsonAsync aliases JsonValueAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task JsonAsyncShouldAliasJsonValueAsync()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            IJSHandle handle = await page.EvaluateHandleAsync("({ foo: 42 })").ConfigureAwait(false);
            JsonElement viaValue = await handle.JsonValueAsync<JsonElement>().ConfigureAwait(false);
            JsonElement viaAlias = await handle.JsonAsync<JsonElement>().ConfigureAwait(false);
            Assert.That(viaAlias.GetProperty("foo").GetInt32(), Is.EqualTo(viaValue.GetProperty("foo").GetInt32()));
            await handle.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should evaluate on handle with argument")]
        [Test]
        [Timeout(30_000)]
        public async Task HandleEvaluateShouldAcceptArgument()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            IJSHandle handle = await page.EvaluateHandleAsync("({ n: 3 })").ConfigureAwait(false);
            int result = await handle.EvaluateAsync<int>("(obj, m) => obj.n * m", 4).ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(12));
            await handle.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("jshandle-properties.spec.ts", "should work")]
        [PlaywrightTest("jshandle-properties.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task GetPropertyAsyncShouldReturnNestedObjectHandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            IJSHandle handle = await page.EvaluateHandleAsync("({ nested: { value: 42 } })").ConfigureAwait(false);
            IJSHandle nested = await handle.GetPropertyAsync("nested").ConfigureAwait(false);
            Assert.That(nested, Is.Not.Null);
            int value = await nested.EvaluateAsync<int>("o => o.value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo(42));
            await nested.DisposeAsync().ConfigureAwait(false);
            await handle.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("jshandle-properties.spec.ts", "PropertyAsync aliases GetPropertyAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task PropertyAsyncShouldAliasGetPropertyAsync()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            IJSHandle handle = await page.EvaluateHandleAsync("({ nested: { value: 42 } })").ConfigureAwait(false);
            IJSHandle nested = await handle.PropertyAsync("nested").ConfigureAwait(false);
            Assert.That(nested, Is.Not.Null);
            int value = await nested.EvaluateAsync<int>("o => o.value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo(42));
            await nested.DisposeAsync().ConfigureAwait(false);
            await handle.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("jshandle-properties.spec.ts", "should return map with property names")]
        [Test]
        [Timeout(30_000)]
        public async Task GetPropertiesAsyncShouldReturnNamedHandles()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            IJSHandle handle = await page.EvaluateHandleAsync("({ a: { n: 1 }, b: { n: 2 } })").ConfigureAwait(false);
            Dictionary<string, IJSHandle> properties = await handle.GetPropertiesAsync().ConfigureAwait(false);
            Assert.That(properties.ContainsKey("a"), Is.True);
            Assert.That(properties.ContainsKey("b"), Is.True);
            Assert.That(await properties["a"].EvaluateAsync<int>("o => o.n").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await properties["b"].EvaluateAsync<int>("o => o.n").ConfigureAwait(false), Is.EqualTo(2));
            foreach (IJSHandle property in properties.Values)
            {
                await property.DisposeAsync().ConfigureAwait(false);
            }

            await handle.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("jshandle-properties.spec.ts", "PropertiesAsync aliases GetPropertiesAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task PropertiesAsyncShouldAliasGetPropertiesAsync()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            IJSHandle handle = await page.EvaluateHandleAsync("({ a: { n: 1 }, b: { n: 2 } })").ConfigureAwait(false);
            Dictionary<string, IJSHandle> viaGet = await handle.GetPropertiesAsync().ConfigureAwait(false);
            Dictionary<string, IJSHandle> viaAlias = await handle.PropertiesAsync().ConfigureAwait(false);
            Assert.That(viaAlias.ContainsKey("a"), Is.True);
            Assert.That(viaAlias.ContainsKey("b"), Is.True);
            Assert.That(await viaAlias["a"].EvaluateAsync<int>("o => o.n").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(viaGet.Count, Is.EqualTo(viaAlias.Count));
            foreach (IJSHandle property in viaGet.Values)
            {
                await property.DisposeAsync().ConfigureAwait(false);
            }

            foreach (IJSHandle property in viaAlias.Values)
            {
                await property.DisposeAsync().ConfigureAwait(false);
            }

            await handle.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("jshandle-evaluate.spec.ts", "should return handle from handle evaluate")]
        [Test]
        [Timeout(30_000)]
        public async Task EvaluateHandleAsyncOnHandleShouldReturnObject()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            IJSHandle handle = await page.EvaluateHandleAsync("({ n: 3 })").ConfigureAwait(false);
            IJSHandle doubled = await handle.EvaluateHandleAsync("(obj, m) => ({ n: obj.n * m })", 4).ConfigureAwait(false);
            Assert.That(doubled, Is.Not.Null);
            int n = await doubled.EvaluateAsync<int>("o => o.n").ConfigureAwait(false);
            Assert.That(n, Is.EqualTo(12));
            await doubled.DisposeAsync().ConfigureAwait(false);
            await handle.DisposeAsync().ConfigureAwait(false);
        }
    }
}
