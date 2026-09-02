/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page.selectOption(IElementHandle, { strict })</c>.
    /// </summary>
    [TestFixture]
    public class SelectOptionHandleStrictTests : PageTestEx
    {
        [PlaywrightTest("page-select-option.spec.ts", "strict true throws when two selects match")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictTrueShouldThrowWhenTwoSelectsMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<select><option value='wave674'>a</option></select><select><option value='wave674'>b</option></select>").ConfigureAwait(false);
            IElementHandle option = await page.QuerySelectorAsync("option").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.SelectOptionAsync("select", option, strict: true));

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
            await page.SetContentAsync("<select id='only'><option value='wave674'>a</option></select><select><option value='wave674'>b</option></select>").ConfigureAwait(false);
            IElementHandle option = await page.QuerySelectorAsync("#only option").ConfigureAwait(false);

            await page.SelectOptionAsync("#only", option, strict: true).ConfigureAwait(false);
            string actual = await page.EvaluateAsync<string>("document.getElementById('only').value").ConfigureAwait(false);
            Assert.That(actual, Is.EqualTo("wave674"));
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
            await page.SetContentAsync("<select id='first'><option value='wave674'>a</option></select><select><option value='wave674'>b</option></select>").ConfigureAwait(false);
            IElementHandle option = await page.QuerySelectorAsync("#first option").ConfigureAwait(false);

            await page.SelectOptionAsync("select", option, strict: false).ConfigureAwait(false);
            string actual = await page.EvaluateAsync<string>("document.getElementById('first').value").ConfigureAwait(false);
            Assert.That(actual, Is.EqualTo("wave674"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "frame honors strict")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameShouldHonorStrict()
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
            await frame.SetContentAsync("<select><option value='wave674'>a</option></select><select><option value='wave674'>b</option></select>").ConfigureAwait(false);
            IElementHandle option = await frame.QuerySelectorAsync("option").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => frame.SelectOptionAsync("select", option, strict: true));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
