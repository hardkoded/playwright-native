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
    /// Official <c>browser.on('context')</c>.
    /// </summary>
    [TestFixture]
    public class BrowserContextEventTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-events.spec.ts", "Context fires when NewContextAsync creates a context")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextShouldFireOnNewContext()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            Task<IBrowserContext> waitTask = browser.WaitForContextAsync();
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IBrowserContext received = await waitTask.ConfigureAwait(false);

            Assert.That(received, Is.SameAs(context));
            await context.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-events.spec.ts", "Context fires when NewPageAsync creates an implicit context")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextShouldFireOnNewPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext received = null;
            browser.Context += (_, ctx) => received = ctx;

            IPage page = await browser.NewPageAsync().ConfigureAwait(false);

            Assert.That(received, Is.Not.Null);
            Assert.That(received, Is.SameAs(page.Context));
            await page.CloseAsync().ConfigureAwait(false);
        }
    }
}
