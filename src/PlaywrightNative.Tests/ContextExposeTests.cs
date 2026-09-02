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
    /// Context-wide <see cref="IBrowserContext.ExposeFunctionAsync(string, System.Action)"/>
    /// and handle-mode <see cref="IBrowserContext.ExposeBindingAsync(string, System.Func{BindingSource, IJSHandle, object})"/>.
    /// </summary>
    [TestFixture]
    public class ContextExposeTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-expose-function.spec.ts", "new pages inherit ExposeFunction")]
        [Test]
        [Timeout(30_000)]
        public async Task NewPagesShouldInheritExposeFunction()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            await context.ExposeFunctionAsync("wave69", () => 69).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<int>("window.wave69()").ConfigureAwait(false), Is.EqualTo(69));
        }

        [PlaywrightTest("browsercontext-expose-function.spec.ts", "existing page receives ExposeFunction")]
        [Test]
        [Timeout(30_000)]
        public async Task ExistingPageShouldReceiveExposeFunction()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            int calls = 0;
            await context.ExposeFunctionAsync("mark", () =>
            {
                calls++;
                return calls;
            }).ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<int>("window.mark()").ConfigureAwait(false), Is.EqualTo(1));
            IPage other = await context.NewPageAsync().ConfigureAwait(false);
            Assert.That(await other.EvaluateAsync<int>("window.mark()").ConfigureAwait(false), Is.EqualTo(2));
        }

        [PlaywrightTest("browsercontext-expose-function.spec.ts", "handle binding reports context")]
        [Test]
        [Timeout(30_000)]
        public async Task HandleBindingShouldReportContext()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            BindingSource source = null;
            IJSHandle captured = null;
            await context.ExposeBindingAsync("logme", (BindingSource caller, IJSHandle handle) =>
            {
                source = caller;
                captured = handle;
                return 17;
            }).ConfigureAwait(false);

            int result = await page.EvaluateAsync<int>("window.logme({ n: 7 })").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(17));
            Assert.That(captured, Is.Not.Null);
            Assert.That(await captured.EvaluateAsync<int>("x => x.n").ConfigureAwait(false), Is.EqualTo(7));
            Assert.That(source, Is.Not.Null);
            Assert.That(source.Context, Is.SameAs(context));
            Assert.That(source.Page, Is.SameAs(page));
        }
    }
}
