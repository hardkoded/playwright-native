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
    /// Official <c>strictSelectors</c> on <see cref="BrowserContextOptions"/>.
    /// </summary>
    [TestFixture]
    public class StrictSelectorsTests : PageTestEx
    {
        [PlaywrightTest("page-strict.spec.ts", "strictSelectors throws when the selector matches two nodes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowWhenSelectorMatchesTwoNodes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                StrictSelectors = true,
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><button>one</button><button>two</button></div>").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.ClickAsync("button"));

            Assert.That(context.StrictSelectors, Is.True);
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
            Assert.That(ex.Message, Does.Contain("2 elements"));
        }

        [PlaywrightTest("page-strict.spec.ts", "strictSelectors allows a unique selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAllowAUniqueSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                StrictSelectors = true,
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><button id=\"only\">one</button><button>two</button></div>").ConfigureAwait(false);

            await page.ClickAsync("#only").ConfigureAwait(false);

            string id = await page.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);
            Assert.That(id, Is.EqualTo("only"));
        }

        [PlaywrightTest("page-strict.spec.ts", "querySelector is not strict")]
        [Test]
        [Timeout(30_000)]
        public async Task QuerySelectorShouldReturnTheFirstMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                StrictSelectors = true,
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><button>one</button><button>two</button></div>").ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("button").ConfigureAwait(false);

            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Is.EqualTo("one"));
        }

        [PlaywrightTest("page-strict.spec.ts", "strictSelectors defaults to false")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickTheFirstMatchWhenNotStrict()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><button id=\"first\">one</button><button>two</button></div>").ConfigureAwait(false);

            await page.ClickAsync("button").ConfigureAwait(false);

            string id = await page.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);
            Assert.That(context.StrictSelectors, Is.False);
            Assert.That(id, Is.EqualTo("first"));
        }
    }
}
