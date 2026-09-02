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
    /// Official <c>elementHandle.waitForSelector({ strict })</c>.
    /// </summary>
    [TestFixture]
    public class ElementWaitForSelectorStrictTests : PageTestEx
    {
        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "strict true throws when two descendants match")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictTrueShouldThrowWhenTwoDescendantsMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id='root'><button>one</button><button>two</button></div>").ConfigureAwait(false);
            IElementHandle root = await page.QuerySelectorAsync("#root").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => root.WaitForSelectorAsync("button", new() { Strict = true }));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
            Assert.That(ex.Message, Does.Contain("2 elements"));
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "strict true accepts a unique selector")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictTrueShouldAcceptAUniqueSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id='root'><button id='only'>one</button><button>two</button></div>").ConfigureAwait(false);
            IElementHandle root = await page.QuerySelectorAsync("#root").ConfigureAwait(false);

            IElementHandle handle = await root.WaitForSelectorAsync("#only", new() { Strict = true }).ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("only"));
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "strict false uses the first descendant")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictFalseShouldUseTheFirstDescendant()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                StrictSelectors = true,
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id='root'><button id='first'>one</button><button>two</button></div>").ConfigureAwait(false);
            IElementHandle root = await page.QuerySelectorAsync("#root").ConfigureAwait(false);

            IElementHandle handle = await root.WaitForSelectorAsync("button", new() { Strict = false }).ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("first"));
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "frame element honors strict")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameElementShouldHonorStrict()
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
            await frame.SetContentAsync("<div id='root'><button>one</button><button>two</button></div>").ConfigureAwait(false);
            IElementHandle root = await frame.QuerySelectorAsync("#root").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => root.WaitForSelectorAsync("button", new() { Strict = true }));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
