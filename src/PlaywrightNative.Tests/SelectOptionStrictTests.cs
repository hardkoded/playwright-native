/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page.selectOption({ strict })</c>.
    /// </summary>
    [TestFixture]
    public class SelectOptionStrictTests : PageTestEx
    {
        private static SelectOptionValue WaveOption()
            => new SelectOptionValue { Value = "wave653" };

        [PlaywrightTest("page-select-option.spec.ts", "strict true throws when two selects match")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictTrueShouldThrowWhenTwoSelectsMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<select><option value='wave653'>a</option></select><select><option value='wave653'>b</option></select>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.SelectOptionAsync("select", new[] { WaveOption() }, strict: true));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
            Assert.That(ex.Message, Does.Contain("2 elements"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "strict true accepts a unique selector")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictTrueShouldAcceptAUniqueSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<select id='only'><option value='wave653'>a</option></select><select><option value='wave653'>b</option></select>").ConfigureAwait(false);

            await page.SelectOptionAsync("#only", new[] { WaveOption() }, strict: true).ConfigureAwait(false);
            string value = await page.EvaluateAsync<string>("document.getElementById('only').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("wave653"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "strict false overrides context StrictSelectors")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictFalseShouldOverrideContextStrictSelectors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                StrictSelectors = true,
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<select id='first'><option value='wave653'>a</option></select><select><option value='wave653'>b</option></select>").ConfigureAwait(false);

            await page.SelectOptionAsync("select", new[] { WaveOption() }, strict: false).ConfigureAwait(false);
            string value = await page.EvaluateAsync<string>("document.getElementById('first').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("wave653"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "frame SelectOption honors strict")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameSelectOptionShouldHonorStrict()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<iframe></iframe>").ConfigureAwait(false);
            IFrame frame = null;
            foreach (IFrame child in page.MainFrame.ChildFrames)
            {
                frame = child;
                break;
            }

            Assert.That(frame, Is.Not.Null);
            await frame.SetContentAsync("<select><option value='wave653'>a</option></select><select><option value='wave653'>b</option></select>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => frame.SelectOptionAsync("select", new[] { WaveOption() }, strict: true));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
