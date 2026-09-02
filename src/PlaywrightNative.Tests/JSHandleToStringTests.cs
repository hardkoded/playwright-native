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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>jshandle-to-string.spec.ts</c>.
    /// </summary>
    [TestFixture]
    public class JSHandleToStringTests : PageTestEx
    {
        [PlaywrightTest("jshandle-to-string.spec.ts", "should work for primitives")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForPrimitives()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle numberHandle = await page.EvaluateHandleAsync("() => 2").ConfigureAwait(false);
            Assert.That(numberHandle.ToString(), Is.EqualTo("2"));
            IJSHandle stringHandle = await page.EvaluateHandleAsync("() => 'a'").ConfigureAwait(false);
            Assert.That(stringHandle.ToString(), Is.EqualTo("a"));
        }

        [PlaywrightTest("jshandle-to-string.spec.ts", "should work for complicated objects")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForComplicatedObjects()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle aHandle = await page.EvaluateHandleAsync("() => window").ConfigureAwait(false);
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("Firefox Juggler previews Window as JSHandle@object.");
            }

            Assert.That(aHandle.ToString(), Is.EqualTo("Window"));
        }

        [PlaywrightTest("jshandle-to-string.spec.ts", "should beautifully render sparse arrays")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldBeautifullyRenderSparseArrays()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("Firefox console preview for sparse arrays is 'Array'.");
            }

            Task<IConsoleMessage> waitTask = page.WaitForConsoleMessageAsync();
            await page.EvaluateHandleAsync(@"() => {
                const a = [];
                a[1] = 1;
                a[10] = 2;
                a[100] = 3;
                console.log(a);
            }").ConfigureAwait(false);
            IConsoleMessage msg = await waitTask.ConfigureAwait(false);
            Assert.That(msg.Text, Is.EqualTo("[empty, 1, empty x 8, 2, empty x 89, 3]"));
        }

        [PlaywrightTest("jshandle-to-string.spec.ts", "should work for promises")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForPromises()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle wrapperHandle = await page.EvaluateHandleAsync("() => ({ b: Promise.resolve(123) })").ConfigureAwait(false);
            IJSHandle bHandle = await wrapperHandle.GetPropertyAsync("b").ConfigureAwait(false);
            Assert.That(bHandle.ToString(), Is.EqualTo("Promise"));
        }

        [PlaywrightTest("jshandle-to-string.spec.ts", "should work with different subtypes")]
        [PlaywrightTest("jshandle-to-string.spec.ts", "should work with different subtypes @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithDifferentSubtypes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That((await page.EvaluateHandleAsync("(function(){})").ConfigureAwait(false)).ToString(), Does.Contain("function"));
            Assert.That((await page.EvaluateHandleAsync("12").ConfigureAwait(false)).ToString(), Is.EqualTo("12"));
            Assert.That((await page.EvaluateHandleAsync("true").ConfigureAwait(false)).ToString(), Is.EqualTo("true"));
            Assert.That((await page.EvaluateHandleAsync("undefined").ConfigureAwait(false)).ToString(), Is.EqualTo("undefined"));
            Assert.That((await page.EvaluateHandleAsync("\"foo\"").ConfigureAwait(false)).ToString(), Is.EqualTo("foo"));
            Assert.That((await page.EvaluateHandleAsync("Symbol()").ConfigureAwait(false)).ToString(), Is.EqualTo("Symbol()"));
            Assert.That((await page.EvaluateHandleAsync("new Map()").ConfigureAwait(false)).ToString(), Does.Contain("Map"));
            Assert.That((await page.EvaluateHandleAsync("new Set()").ConfigureAwait(false)).ToString(), Does.Contain("Set"));
            Assert.That((await page.EvaluateHandleAsync("[]").ConfigureAwait(false)).ToString(), Does.Contain("Array"));
            Assert.That((await page.EvaluateHandleAsync("null").ConfigureAwait(false)).ToString(), Is.EqualTo("null"));

            IJSHandle bodyHandle = await page.EvaluateHandleAsync("document.body").ConfigureAwait(false);
            string bodyPreview = bodyHandle.ToString();
            for (int i = 0; i < 50 && bodyPreview != "JSHandle@<body></body>"; i++)
            {
                await Task.Delay(50).ConfigureAwait(false);
                bodyPreview = bodyHandle.ToString();
            }

            Assert.That(bodyPreview, Is.EqualTo("JSHandle@<body></body>"));
            Assert.That((await page.EvaluateHandleAsync("new WeakMap()").ConfigureAwait(false)).ToString(), Is.EqualTo("WeakMap"));
            Assert.That((await page.EvaluateHandleAsync("new WeakSet()").ConfigureAwait(false)).ToString(), Is.EqualTo("WeakSet"));
            Assert.That((await page.EvaluateHandleAsync("new Error()").ConfigureAwait(false)).ToString(), Does.Contain("Error"));

            string proxyPreview = (await page.EvaluateHandleAsync("new Proxy({}, {})").ConfigureAwait(false)).ToString();
            Assert.That(proxyPreview, Is.EqualTo(TestConstants.IsChromium ? "Proxy(Object)" : "Proxy"));
        }

        [PlaywrightTest("jshandle-to-string.spec.ts", "should work with previewable subtypes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithPreviewableSubtypes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("Official spec skips Firefox (non-BiDi) for previewable subtypes.");
            }

            Assert.That((await page.EvaluateHandleAsync("/foo/").ConfigureAwait(false)).ToString(), Is.EqualTo("/foo/"));
            Assert.That((await page.EvaluateHandleAsync("new Date(0)").ConfigureAwait(false)).ToString(), Does.Contain("GMT"));
            Assert.That((await page.EvaluateHandleAsync("new Int32Array()").ConfigureAwait(false)).ToString(), Does.Contain("Int32Array"));
        }
    }
}
