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
    /// <see cref="IPage.PauseAsync"/> without an inspector overlay.
    /// </summary>
    [TestFixture]
    public class PagePauseTests : PageTestEx
    {
        [PlaywrightTest("pause.spec.ts", "PauseAsync completes when the page is closed")]
        [Test]
        [Timeout(30_000)]
        public async Task PauseAsyncShouldCompleteWhenThePageIsClosed()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">paused</div>").ConfigureAwait(false);

            Task pauseTask = page.PauseAsync();
            await Task.Delay(100).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
            await pauseTask.ConfigureAwait(false);
        }

        [PlaywrightTest("pause.spec.ts", "PauseAsync throws when the page is already closed")]
        [Test]
        [Timeout(30_000)]
        public async Task PauseAsyncShouldThrowWhenThePageIsAlreadyClosed()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(() => page.PauseAsync());
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("page.pause"));
            Assert.That(ex.Message, Does.Contain("closed"));
        }

        [PlaywrightTest("pause.spec.ts", "PauseAsync times out using DefaultTimeout")]
        [Test]
        [Timeout(30_000)]
        public async Task PauseAsyncShouldTimeoutUsingDefaultTimeout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            page.SetDefaultTimeout(200);

            TimeoutException ex = Assert.CatchAsync<TimeoutException>(() => page.PauseAsync());
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("page.pause"));
            Assert.That(ex.Message, Does.Contain("200"));
        }
    }
}
