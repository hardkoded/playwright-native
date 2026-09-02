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
    /// Official <c>browserContext.on('weberror')</c>.
    /// </summary>
    [TestFixture]
    public class ContextWebErrorEventTests : PageTestEx
    {
        [PlaywrightTest("page-event-pageerror.spec.ts", "WebError fires on an uncaught page exception")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextWebErrorShouldFireOnUncaughtException()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IWebError error = await context.RunAndWaitForWebErrorAsync(
                () => page.GoToAsync("data:text/html,<script>throw new Error('wave452');</script>")).ConfigureAwait(false);

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Page, Is.SameAs(page));
            Assert.That(error.Error, Does.Contain("wave452"));
        }
    }
}
