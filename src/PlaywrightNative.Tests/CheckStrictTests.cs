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
    /// Official <c>page.check({ strict })</c>.
    /// </summary>
    [TestFixture]
    public class CheckStrictTests : PageTestEx
    {
        [PlaywrightTest("page-check.spec.ts", "strict true throws when two nodes match")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictTrueShouldThrowWhenTwoNodesMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type='checkbox'><input type='checkbox'>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.CheckAsync("input", new() { Strict = true }));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
            Assert.That(ex.Message, Does.Contain("2 elements"));
        }

        [PlaywrightTest("page-check.spec.ts", "strict true accepts a unique selector")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictTrueShouldAcceptAUniqueSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id='only' type='checkbox'><input type='checkbox'>").ConfigureAwait(false);

            await page.CheckAsync("#only", new() { Strict = true }).ConfigureAwait(false);
            bool actual = await page.EvaluateAsync<bool>("document.getElementById('only').checked").ConfigureAwait(false);
            Assert.That(actual, Is.EqualTo(true));
        }

        [PlaywrightTest("page-check.spec.ts", "strict false overrides context StrictSelectors")]
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
            await page.SetContentAsync("<input id='first' type='checkbox'><input id='second' type='checkbox'>").ConfigureAwait(false);

            await page.CheckAsync("input", new() { Strict = false }).ConfigureAwait(false);
            bool actual = await page.EvaluateAsync<bool>("document.getElementById('first').checked").ConfigureAwait(false);
            Assert.That(actual, Is.EqualTo(true));
        }

        [PlaywrightTest("page-check.spec.ts", "frame honors strict")]
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
            await frame.SetContentAsync("<input type='checkbox'><input type='checkbox'>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => frame.CheckAsync("input", new() { Strict = true }));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
