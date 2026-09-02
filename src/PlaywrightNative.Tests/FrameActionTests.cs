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
    /// IFrame QuerySelector, ElementQuery actions, GoTo, and SetContent.
    /// </summary>
    [TestFixture]
    public class FrameActionTests : PageTestEx
    {
        [PlaywrightTest("frame-evaluate.spec.ts", "main frame query and fill")]
        [Test]
        [Timeout(30_000)]
        public async Task MainFrameQuerySelectorAndFillShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input id=\"n\" value=\"old\" />").ConfigureAwait(false);
            IElementHandle handle = await page.MainFrame.QuerySelectorAsync("#n").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("n"));

            await page.MainFrame.FillAsync("#n", "Ada").ConfigureAwait(false);
            Assert.That(
                await page.MainFrame.EvaluateAsync<string>("document.querySelector('#n').value").ConfigureAwait(false),
                Is.EqualTo("Ada"));
        }

        [PlaywrightTest("frame-evaluate.spec.ts", "InputValueAsync reads the input value")]
        [Test]
        [Timeout(30_000)]
        public async Task MainFrameInputValueShouldReadTheFilledValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input id=\"n\" />").ConfigureAwait(false);
            await page.MainFrame.FillAsync("#n", "wave164").ConfigureAwait(false);
            Assert.That(await page.MainFrame.InputValueAsync("#n").ConfigureAwait(false), Is.EqualTo("wave164"));
        }

        [PlaywrightTest("frame-evaluate.spec.ts", "main frame click")]
        [Test]
        [Timeout(30_000)]
        public async Task MainFrameClickShouldFireDomHandler()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<button id=\"b\" onclick=\"window.clicked=true\">Go</button>").ConfigureAwait(false);
            await page.MainFrame.ClickAsync("#b").ConfigureAwait(false);
            Assert.That(await page.MainFrame.EvaluateAsync<bool>("window.clicked === true").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("frame-evaluate.spec.ts", "child frame query fill and attribute")]
        [Test]
        [Timeout(30_000)]
        public async Task ChildFrameQuerySelectorAndFillShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);

            await child.SetContentAsync("<input id=\"n\" data-kind=\"child\" />").ConfigureAwait(false);
            IElementHandle handle = await child.QuerySelectorAsync("#n").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await child.GetAttributeAsync("#n", "data-kind").ConfigureAwait(false), Is.EqualTo("child"));

            await child.FillAsync("#n", "iframe-ada").ConfigureAwait(false);
            Assert.That(
                await child.EvaluateAsync<string>("document.querySelector('#n').value").ConfigureAwait(false),
                Is.EqualTo("iframe-ada"));
            Assert.That(
                await page.EvaluateAsync<bool>("document.querySelector('#n') === null").ConfigureAwait(false),
                Is.True);
        }

        [PlaywrightTest("frame-evaluate.spec.ts", "child frame click")]
        [Test]
        [Timeout(30_000)]
        public async Task ChildFrameClickShouldFireDomHandler()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);

            await child.SetContentAsync(
                "<button id=\"b\" style=\"position:absolute;left:0;top:0;width:80px;height:30px\" onclick=\"window.clicked=true\">Go</button>")
                .ConfigureAwait(false);
            await child.ClickAsync("#b").ConfigureAwait(false);
            Assert.That(await child.EvaluateAsync<bool>("window.clicked === true").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("frame-evaluate.spec.ts", "child frame goto")]
        [Test]
        [Timeout(30_000)]
        public async Task ChildFrameGoToShouldNavigate()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);

            await child.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(child.Url, Does.Contain("/empty.html"));
            Assert.That(
                await child.EvaluateAsync<string>("location.pathname").ConfigureAwait(false),
                Is.EqualTo("/empty.html"));
            Assert.That(page.Url, Does.Not.Contain("/empty.html"));
        }

        private static async Task<IFrame> AttachBlankChildFrameAsync(IPage page)
        {
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EvaluateAsync<bool>(@"
                const iframe = document.createElement('iframe');
                iframe.style.position = 'absolute';
                iframe.style.left = '0';
                iframe.style.top = '0';
                iframe.style.border = '0';
                iframe.style.width = '400px';
                iframe.style.height = '200px';
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
                    bool isTop = await child.EvaluateAsync<bool>("window === window.top").ConfigureAwait(false);
                    if (!isTop)
                    {
                        return child;
                    }
                }
                catch (PlaywrightNativeException)
                {
                    // Execution context is not ready yet.
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            throw new TimeoutException("Child frame execution context did not become ready.");
        }
    }
}
