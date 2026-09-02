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
    /// Official <c>page.dblclick({ strict })</c>.
    /// </summary>
    [TestFixture]
    public class DblClickStrictTests : PageTestEx
    {
        [PlaywrightTest("page-click.spec.ts", "strict true throws when two buttons match")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictTrueShouldThrowWhenTwoButtonsMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button>one</button><button>two</button>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.DblClickAsync("button", new() { Strict = true }));

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
            await page.SetContentAsync("<button id='only' ondblclick=\"this.dataset.hit='1'\">one</button><button>two</button>").ConfigureAwait(false);

            await page.DblClickAsync("#only", new() { Strict = true }).ConfigureAwait(false);
            string hit = await page.EvaluateAsync<string>("document.getElementById('only').dataset.hit").ConfigureAwait(false);
            Assert.That(hit, Is.EqualTo("1"));
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
            await page.SetContentAsync("<button id='first' ondblclick=\"this.dataset.hit='1'\">one</button><button>two</button>").ConfigureAwait(false);

            await page.DblClickAsync("button", new() { Strict = false }).ConfigureAwait(false);
            string hit = await page.EvaluateAsync<string>("document.getElementById('first').dataset.hit").ConfigureAwait(false);
            Assert.That(hit, Is.EqualTo("1"));
        }

        [PlaywrightTest("page-click.spec.ts", "frame DblClick honors strict")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameDblClickShouldHonorStrict()
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
            await frame.SetContentAsync("<button>one</button><button>two</button>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => frame.DblClickAsync("button", new() { Strict = true }));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
