/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
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
