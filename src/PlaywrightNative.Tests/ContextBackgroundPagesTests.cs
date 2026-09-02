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
    /// Official <c>browserContext.backgroundPages()</c>.
    /// </summary>
    [TestFixture]
    public class ContextBackgroundPagesTests : PageTestEx
    {
        [PlaywrightTest("chromium.spec.ts", "BackgroundPages is empty without extensions")]
        [Test]
        [Timeout(30_000)]
        public async Task BackgroundPagesShouldBeEmptyWithoutExtensions()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("data:text/html,<html><body>wave459</body></html>").ConfigureAwait(false);

            Assert.That(context.Pages, Does.Contain(page));
            Assert.That(context.BackgroundPages, Is.Empty);
        }
    }
}
