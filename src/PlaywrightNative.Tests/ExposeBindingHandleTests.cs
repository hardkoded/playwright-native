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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// ExposeBindingAsync handle mode: the page argument arrives as an IJSHandle.
    /// </summary>
    [TestFixture]
    public class ExposeBindingHandleTests : PageTestEx
    {
        [PlaywrightTest("page-expose-function.spec.ts", "handle argument is not serialized")]
        [Test]
        [Timeout(30_000)]
        public async Task ExposeBindingHandleShouldDeliverArgumentAsHandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle captured = null;
            BindingSource source = null;
            await page.ExposeBindingAsync("logme", (BindingSource caller, IJSHandle handle) =>
            {
                source = caller;
                captured = handle;
                return 17;
            }).ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            int result = await page.EvaluateAsync<int>("window.logme({ foo: 42 })").ConfigureAwait(false);

            Assert.That(result, Is.EqualTo(17));
            Assert.That(captured, Is.Not.Null);
            Assert.That(await captured.EvaluateAsync<int>("x => x.foo").ConfigureAwait(false), Is.EqualTo(42));
            Assert.That(source, Is.Not.Null);
            Assert.That(source.Page, Is.SameAs(page));
            Assert.That(source.Context, Is.SameAs(context));
            Assert.That(source.Frame, Is.SameAs(page.MainFrame));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "handle binding rejects extra arguments")]
        [Test]
        [Timeout(30_000)]
        public async Task ExposeBindingHandleShouldRejectMultipleArguments()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.ExposeBindingAsync("logme", (BindingSource _, IJSHandle _) => 0).ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            PlaywrightNativeException exception = Assert.ThrowsAsync<PlaywrightNativeException>(
                async () => await page.EvaluateAsync<object>("window.logme({ a: 1 }, { b: 2 })").ConfigureAwait(false));
            Assert.That(exception.Message, Does.Contain("exposeBindingHandle supports a single argument"));
        }
    }
}
