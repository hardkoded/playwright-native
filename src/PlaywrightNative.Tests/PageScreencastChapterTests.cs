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
    /// Official <c>screencast.showChapter</c>.
    /// </summary>
    [TestFixture]
    public class PageScreencastChapterTests : PageTestEx
    {
        [PlaywrightTest("screencast.spec.ts", "showChapter displays title and description")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldShowChapterTitleAndDescription()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<body>page</body>").ConfigureAwait(false);

            await page.Screencast.ShowChapterAsync("Chapter title", "Chapter description", 10_000).ConfigureAwait(false);

            Assert.That(
                await page.Locator("[data-pw-screencast-chapter-title]").TextContentAsync().ConfigureAwait(false),
                Is.EqualTo("Chapter title"));
            Assert.That(
                await page.Locator("[data-pw-screencast-chapter-description]").TextContentAsync().ConfigureAwait(false),
                Is.EqualTo("Chapter description"));
        }

        [PlaywrightTest("screencast.spec.ts", "showChapter requires a title")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowWhenChapterTitleIsEmpty()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            ArgumentException ex = Assert.CatchAsync<ArgumentException>(
                () => page.Screencast.ShowChapterAsync(string.Empty));
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.ParamName, Is.EqualTo("title"));
        }
    }
}
