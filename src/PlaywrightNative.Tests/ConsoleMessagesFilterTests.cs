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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page.consoleMessages({ filter })</c> (Playwright v1.59).
    /// </summary>
    [TestFixture]
    public class ConsoleMessagesFilterTests : PageTestEx
    {
        [PlaywrightTest("page-event-console.spec.ts", "since-navigation is the default")]
        [Test]
        [Timeout(30_000)]
        public async Task SinceNavigationFilterShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            await page.EvaluateAsync<object>("console.log('before navigation')").ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync<object>("console.log('after navigation')").ConfigureAwait(false);

            IReadOnlyList<IConsoleMessage> all = await page.ConsoleMessagesAsync(new() { Filter = ConsoleMessagesFilter.All }).ConfigureAwait(false);
            Assert.That(all.Select(item => item.Text), Does.Contain("before navigation"));
            Assert.That(all.Select(item => item.Text), Does.Contain("after navigation"));

            IReadOnlyList<IConsoleMessage> sinceNav = await page.ConsoleMessagesAsync().ConfigureAwait(false);
            Assert.That(sinceNav.Select(item => item.Text), Does.Not.Contain("before navigation"));
            Assert.That(sinceNav.Select(item => item.Text), Does.Contain("after navigation"));

            IReadOnlyList<IConsoleMessage> explicitSince = await page.ConsoleMessagesAsync(new() { Filter = ConsoleMessagesFilter.SinceNavigation }).ConfigureAwait(false);
            Assert.That(explicitSince.Select(item => item.Text), Does.Not.Contain("before navigation"));
            Assert.That(explicitSince.Select(item => item.Text), Does.Contain("after navigation"));
        }
    }
}
