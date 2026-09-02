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
    /// Direct-connection tests for <see cref="IPage.WaitForCloseAsync"/>.
    /// </summary>
    [TestFixture]
    public class WaitForCloseTests : PageTestEx
    {
        [PlaywrightTest("page-basic.spec.ts", "WaitForCloseAsync resolves on CloseAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldResolveWhenPageCloses()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task waitTask = page.WaitForCloseAsync();
            await page.CloseAsync().ConfigureAwait(false);
            await waitTask.ConfigureAwait(false);

            Assert.That(page.IsClosed, Is.True);
        }

        [PlaywrightTest("page-basic.spec.ts", "CloseAsync reason is surfaced on later errors")]
        [Test]
        [Timeout(30_000)]
        public async Task CloseReasonShouldSurfaceOnLaterErrors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.CloseAsync(new() { Reason = "wave-375-reason" }).ConfigureAwait(false);

            TargetClosedException ex = Assert.ThrowsAsync<TargetClosedException>(
                () => page.EvaluateAsync("1 + 1"));
            Assert.That(ex.Message, Does.Contain("wave-375-reason"));
            Assert.That(ex.CloseReason, Is.EqualTo("wave-375-reason"));
        }
    }
}
