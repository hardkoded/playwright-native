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
    /// Direct-connection tests for <see cref="IElementHandle.ContentFrameAsync"/>,
    /// <see cref="IElementHandle.OwnerFrameAsync"/>, and <see cref="IFrame.FrameElementAsync"/>.
    /// </summary>
    [TestFixture]
    public class ElementFrameTests : PageTestEx
    {
        [PlaywrightTest("elementhandle-content-frame.spec.ts", "ContentFrameAsync returns the iframe's frame")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnContentFrameForIframe()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);

            IElementHandle iframe = await page.QuerySelectorAsync("iframe").ConfigureAwait(false);
            IFrame content = await iframe.ContentFrameAsync().ConfigureAwait(false);
            Assert.That(content, Is.Not.Null);
            Assert.That(content, Is.SameAs(child));
        }

        [PlaywrightTest("elementhandle-content-frame.spec.ts", "ContentFrameAsync is null for a regular element")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnNullContentFrameForRegularElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"m\">main</div>").ConfigureAwait(false);

            IElementHandle div = await page.QuerySelectorAsync("#m").ConfigureAwait(false);
            Assert.That(await div.ContentFrameAsync().ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("elementhandle-content-frame.spec.ts", "OwnerFrameAsync returns the main frame")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnOwnerFrameOnMainPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"m\">main</div>").ConfigureAwait(false);

            IElementHandle div = await page.QuerySelectorAsync("#m").ConfigureAwait(false);
            IFrame owner = await div.OwnerFrameAsync().ConfigureAwait(false);
            Assert.That(owner, Is.SameAs(page.MainFrame));
        }

        [PlaywrightTest("elementhandle-content-frame.spec.ts", "OwnerFrameAsync returns the child frame")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnOwnerFrameInsideChild()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);
            await child.SetContentAsync("<div id=\"c\">child</div>").ConfigureAwait(false);

            IElementHandle inner = await child.QuerySelectorAsync("#c").ConfigureAwait(false);
            IFrame owner = await inner.OwnerFrameAsync().ConfigureAwait(false);
            Assert.That(owner, Is.SameAs(child));
        }

        [PlaywrightTest("elementhandle-content-frame.spec.ts", "FrameElementAsync returns the iframe element")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnHostingIframeElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);

            IElementHandle iframe = await child.FrameElementAsync().ConfigureAwait(false);
            Assert.That(iframe, Is.Not.Null);
            Assert.That(await iframe.ContentFrameAsync().ConfigureAwait(false), Is.SameAs(child));
        }

        [PlaywrightTest("elementhandle-content-frame.spec.ts", "FrameElementAsync throws for the main frame")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowForMainFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.ThrowsAsync<PlaywrightSharpException>(
                () => page.MainFrame.FrameElementAsync());
            Assert.That(ex.Message, Does.Contain("detached").IgnoreCase);
        }

        private static async Task<IFrame> AttachBlankChildFrameAsync(IPage page)
        {
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EvaluateAsync<bool>(@"
                const iframe = document.createElement('iframe');
                iframe.src = 'about:blank';
                document.body.appendChild(iframe);
                true
            ").ConfigureAwait(false);

            IFrame child = null;
            for (int i = 0; i < 50 && child == null; i++)
            {
                foreach (IFrame frame in page.MainFrame.ChildFrames)
                {
                    child = frame;
                    break;
                }

                if (child != null)
                {
                    break;
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            Assert.That(child, Is.Not.Null);

            for (int i = 0; i < 50; i++)
            {
                try
                {
                    if (!await child.EvaluateAsync<bool>("window === window.top").ConfigureAwait(false))
                    {
                        return child;
                    }
                }
                catch (PlaywrightSharpException)
                {
                    // Execution context is not ready yet.
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            throw new TimeoutException("Child frame execution context did not become ready.");
        }
    }
}
