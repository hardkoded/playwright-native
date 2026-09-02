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
    /// Official <c>page.click({ strict })</c>.
    /// </summary>
    [TestFixture]
    public class ClickStrictTests : PageTestEx
    {
        [PlaywrightTest("page-click.spec.ts", "strict true throws when two buttons match")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictTrueShouldThrowWhenTwoButtonsMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><button>one</button><button>two</button></div>").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.ClickAsync("button", new() { Strict = true }));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
            Assert.That(ex.Message, Does.Contain("2 elements"));
        }

        [PlaywrightTest("page-click.spec.ts", "strict true accepts a unique selector")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictTrueShouldAcceptAUniqueSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><button id=\"only\">one</button><button>two</button></div>").ConfigureAwait(false);

            await page.ClickAsync("#only", new() { Strict = true }).ConfigureAwait(false);
            string id = await page.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);
            Assert.That(id, Is.EqualTo("only"));
        }

        [PlaywrightTest("page-click.spec.ts", "strict false overrides context StrictSelectors")]
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
            await page.SetContentAsync("<div><button id=\"first\">one</button><button>two</button></div>").ConfigureAwait(false);

            await page.ClickAsync("button", new() { Strict = false }).ConfigureAwait(false);
            string id = await page.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);
            Assert.That(id, Is.EqualTo("first"));
        }

        [PlaywrightTest("page-click.spec.ts", "frame Click honors strict")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameClickShouldHonorStrict()
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
            await frame.SetContentAsync("<div><button>one</button><button>two</button></div>").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => frame.ClickAsync("button", new() { Strict = true }));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
