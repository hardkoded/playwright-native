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
    /// Official <c>page.querySelector({ strict })</c>.
    /// </summary>
    [TestFixture]
    public class QuerySelectorStrictTests : PageTestEx
    {
        [PlaywrightTest("queryselector.spec.ts", "strict true throws when two nodes match")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictTrueShouldThrowWhenTwoNodesMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button>one</button><button>two</button>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.QuerySelectorAsync("button", new() { Strict = true }));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
            Assert.That(ex.Message, Does.Contain("2 elements"));
        }

        [PlaywrightTest("queryselector.spec.ts", "strict true accepts a unique selector")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictTrueShouldAcceptAUniqueSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='only'>one</button><button>two</button>").ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#only", new() { Strict = true }).ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("only"));
        }

        [PlaywrightTest("queryselector.spec.ts", "strict false keeps first match under context StrictSelectors")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictFalseShouldKeepFirstMatchUnderContextStrictSelectors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                StrictSelectors = true,
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='first'>one</button><button>two</button>").ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("button", new() { Strict = false }).ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("first"));
        }

        [PlaywrightTest("queryselector.spec.ts", "frame honors strict")]
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
            await frame.SetContentAsync("<button>one</button><button>two</button>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => frame.QuerySelectorAsync("button", new() { Strict = true }));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
