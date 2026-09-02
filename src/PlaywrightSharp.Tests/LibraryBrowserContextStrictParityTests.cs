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
    /// Official <c>library/browsercontext-strict.spec.ts</c> parity.
    /// Do not edit leftover <c>StrictSelectorsTests</c> or
    /// per-action leftover <c>*StrictTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextStrictParityTests : PageTestEx
    {
        private IBrowser _browser;

        [SetUp]
        public async Task SetUpAsync()
        {
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }

            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }
        }

        [PlaywrightTest("browsercontext-strict.spec.ts", "should not fail page.textContent in non-strict mode")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotFailPageTextContentInNonStrictMode()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<span>span1</span><div><span>target</span></div>").ConfigureAwait(false);
            Assert.That(await page.TextContentAsync("span", new() { Strict = false }).ConfigureAwait(false), Is.EqualTo("span1"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-strict.spec.ts", "should fail page.textContent in strict mode")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldFailPageTextContentInStrictMode()
        {
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(async () =>
            {
                IBrowserContext context = await _browser.NewContextAsync(new BrowserContextOptions { StrictSelectors = true }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync("<span>span1</span><div><span>target</span></div>").ConfigureAwait(false);
                await page.TextContentAsync("span").ConfigureAwait(false);
            });
            Assert.That(error.Message, Does.Contain("strict mode violation"));
        }

        [PlaywrightTest("browsercontext-strict.spec.ts", "should fail page.click in strict mode")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldFailPageClickInStrictMode()
        {
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(async () =>
            {
                IBrowserContext context = await _browser.NewContextAsync(new BrowserContextOptions { StrictSelectors = true }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync("<button>button1</button><button>target</button>").ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
            });
            Assert.That(error.Message, Does.Contain("strict mode violation"));
        }

        [PlaywrightTest("browsercontext-strict.spec.ts", "should opt out of strict mode")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOptOutOfStrictMode()
        {
            IBrowserContext context = await _browser.NewContextAsync(new BrowserContextOptions { StrictSelectors = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<span>span1</span><div><span>target</span></div>").ConfigureAwait(false);
            Assert.That(await page.TextContentAsync("span", new() { Strict = false }).ConfigureAwait(false), Is.EqualTo("span1"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private static async Task DisposeQuietlyAsync(IAsyncDisposable disposable)
        {
            try
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }
    }
}
