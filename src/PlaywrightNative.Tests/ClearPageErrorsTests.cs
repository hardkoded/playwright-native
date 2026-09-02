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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page.clearPageErrors()</c> (Playwright v1.59).
    /// </summary>
    [TestFixture]
    public class ClearPageErrorsTests : PageTestEx
    {
        [PlaywrightTest("page-event-pageerror.spec.ts", "ClearPageErrorsAsync drops recorded errors")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClearRecordedPageErrors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<string> firstWait = page.WaitForPageErrorAsync();
            await page.GoToAsync("data:text/html,<script>throw new Error('before-clear');</script>").ConfigureAwait(false);
            await firstWait.ConfigureAwait(false);

            IReadOnlyList<string> before = await page.PageErrorsAsync().ConfigureAwait(false);
            Assert.That(string.Join("\n", before), Does.Contain("before-clear"));

            await page.ClearPageErrorsAsync().ConfigureAwait(false);
            IReadOnlyList<string> cleared = await page.PageErrorsAsync().ConfigureAwait(false);
            Assert.That(string.Join("\n", cleared), Does.Not.Contain("before-clear"));

            Task<string> secondWait = page.WaitForPageErrorAsync();
            await page.GoToAsync("data:text/html,<script>throw new Error('after-clear');</script>").ConfigureAwait(false);
            await secondWait.ConfigureAwait(false);

            IReadOnlyList<string> after = await page.PageErrorsAsync().ConfigureAwait(false);
            Assert.That(string.Join("\n", after), Does.Contain("after-clear"));
            Assert.That(string.Join("\n", after), Does.Not.Contain("before-clear"));
        }
    }
}
