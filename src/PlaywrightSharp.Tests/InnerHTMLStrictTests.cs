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
    /// Official <c>page.innerHTML({ strict })</c>.
    /// </summary>
    [TestFixture]
    public class InnerHTMLStrictTests : PageTestEx
    {
        [PlaywrightTest("elementhandle-convenience.spec.ts", "strict true throws when two nodes match")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictTrueShouldThrowWhenTwoNodesMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>one</div><div>two</div>").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.InnerHTMLAsync("div", new() { Strict = true }));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
            Assert.That(ex.Message, Does.Contain("2 elements"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "strict true accepts a unique selector")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictTrueShouldAcceptAUniqueSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id='only'>wave656</div><div>two</div>").ConfigureAwait(false);

            await page.InnerHTMLAsync("#only", new() { Strict = true }).ConfigureAwait(false);
            string actual = await page.EvaluateAsync<string>("document.getElementById('only').innerHTML").ConfigureAwait(false);
            Assert.That(actual, Is.EqualTo("wave656"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "strict false overrides context StrictSelectors")]
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
            await page.SetContentAsync("<div id='first'>wave656</div><div>two</div>").ConfigureAwait(false);

            await page.InnerHTMLAsync("div", new() { Strict = false }).ConfigureAwait(false);
            string actual = await page.EvaluateAsync<string>("document.getElementById('first').innerHTML").ConfigureAwait(false);
            Assert.That(actual, Is.EqualTo("wave656"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "frame honors strict")]
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
            await frame.SetContentAsync("<div>one</div><div>two</div>").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => frame.InnerHTMLAsync("div", new() { Strict = true }));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
