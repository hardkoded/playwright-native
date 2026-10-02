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
using Microsoft.Playwright;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page.screencast</c> start / stop.
    /// </summary>
    [TestFixture]
    public class PageScreencastTests : PageTestEx
    {
        [PlaywrightTest("screencast.spec.ts", "start throws if screencast is already started")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowIfScreencastIsAlreadyStarted()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.Screencast.StartAsync(_ => Task.CompletedTask).ConfigureAwait(false);
            PlaywrightException ex = Assert.CatchAsync<PlaywrightException>(
                () => page.Screencast.StartAsync(_ => Task.CompletedTask));
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("already started"));
            await page.Screencast.StopAsync().ConfigureAwait(false);
        }
    }
}
