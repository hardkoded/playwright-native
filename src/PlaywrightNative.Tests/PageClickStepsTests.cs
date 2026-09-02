/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page.click({ steps })</c> / <c>frame.click({ steps })</c>.
    /// </summary>
    [TestFixture]
    public class PageClickStepsTests : PageTestEx
    {
        [PlaywrightTest("page-click.spec.ts", "Page.ClickAsync steps emits intermediate mousemove events")]
        [Test]
        [Timeout(30_000)]
        public async Task PageClickStepsShouldEmitIntermediateMouseMoves()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                    "<div style=\"padding:80px 160px\">" +
                    "<button id=\"go\">Go</button></div>" +
                    "<script>window.moves=0;window.clicked=false;" +
                    "document.addEventListener('mousemove',()=>{window.moves++;});" +
                    "document.getElementById('go').addEventListener('click',()=>{window.clicked=true;});" +
                    "</script>")
                .ConfigureAwait(false);

            await page.Mouse.MoveAsync(2, 2).ConfigureAwait(false);
            await page.EvaluateAsync("window.moves=0;window.clicked=false").ConfigureAwait(false);
            await page.ClickAsync("#go", new PageClickOptions { Steps = 1 }).ConfigureAwait(false);
            int one = await page.EvaluateAsync<int>("window.moves").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.clicked").ConfigureAwait(false), Is.True);
            Assert.That(one, Is.GreaterThanOrEqualTo(1));

            await page.Mouse.MoveAsync(2, 2).ConfigureAwait(false);
            await page.EvaluateAsync("window.moves=0;window.clicked=false").ConfigureAwait(false);
            await page.ClickAsync("#go", new PageClickOptions { Steps = 8 }).ConfigureAwait(false);
            int many = await page.EvaluateAsync<int>("window.moves").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.clicked").ConfigureAwait(false), Is.True);
            Assert.That(many, Is.GreaterThan(one));
            Assert.That(many, Is.GreaterThanOrEqualTo(8));
        }

        [PlaywrightTest("page-click.spec.ts", "Frame.ClickAsync steps clicks inside the frame")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameClickStepsShouldClickInsideTheFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);
            await child.SetContentAsync("<button id=\"in\">In</button>").ConfigureAwait(false);

            await child.ClickAsync("#in", new FrameClickOptions { Steps = 4 }).ConfigureAwait(false);

            string id = await child.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);
            Assert.That(id, Is.EqualTo("in"));

            static async Task<IFrame> AttachBlankChildFrameAsync(IPage page)
            {
                await page.GoToAsync("about:blank").ConfigureAwait(false);
                await page.EvaluateAsync<bool>(@"
                    const iframe = document.createElement('iframe');
                    iframe.src = 'about:blank';
                    document.body.appendChild(iframe);
                    true
                ").ConfigureAwait(false);

                IFrame found = null;
                for (int i = 0; i < 50 && found == null; i++)
                {
                    foreach (IFrame frame in page.MainFrame.ChildFrames)
                    {
                        found = frame;
                        break;
                    }

                    if (found != null)
                    {
                        break;
                    }

                    await Task.Delay(100).ConfigureAwait(false);
                }

                Assert.That(found, Is.Not.Null);
                for (int i = 0; i < 50; i++)
                {
                    try
                    {
                        if (!await found.EvaluateAsync<bool>("window === window.top").ConfigureAwait(false))
                        {
                            return found;
                        }
                    }
                    catch (PlaywrightNativeException)
                    {
                    }

                    await Task.Delay(100).ConfigureAwait(false);
                }

                throw new TimeoutException("Child frame execution context did not become ready.");
            }
        }
    }
}
