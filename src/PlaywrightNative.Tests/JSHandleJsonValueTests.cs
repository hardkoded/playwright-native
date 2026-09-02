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
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>jshandle-json-value.spec.ts</c>.
    /// </summary>
    [TestFixture]
    public class JSHandleJsonValueTests : PageTestEx
    {
        [PlaywrightTest("jshandle-json-value.spec.ts", "should work")]
        [PlaywrightTest("jshandle-json-value.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle aHandle = await page.EvaluateHandleAsync("(() => ({ foo: 'bar' }))()").ConfigureAwait(false);
            JsonElement json = await aHandle.JsonValueAsync<JsonElement>().ConfigureAwait(false);
            Assert.That(json.GetProperty("foo").GetString(), Is.EqualTo("bar"));
            await aHandle.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("jshandle-json-value.spec.ts", "should work with dates")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithDates()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle dateHandle = await page.EvaluateHandleAsync("(() => new Date('2017-09-26T00:00:00.000Z'))()").ConfigureAwait(false);
            DateTime date = await dateHandle.JsonValueAsync<DateTime>().ConfigureAwait(false);
            string iso = date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            Assert.That(iso, Is.EqualTo("2017-09-26T00:00:00.000Z"));
            await dateHandle.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("jshandle-json-value.spec.ts", "should handle circular objects")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHandleCircularObjects()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle handle = await page.EvaluateHandleAsync("const a = {}; a.b = a; a").ConfigureAwait(false);
            CircularJsonObject a = await handle.JsonValueAsync<CircularJsonObject>().ConfigureAwait(false);
            Assert.That(a, Is.Not.Null);
            Assert.That(a.B, Is.SameAs(a));
            await handle.DisposeAsync().ConfigureAwait(false);
        }

        private sealed class CircularJsonObject
        {
            public CircularJsonObject B { get; set; }
        }
    }
}
