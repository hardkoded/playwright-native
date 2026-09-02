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
    /// IFrame.EvaluateHandleAsync and IPage.GoToAsync waitUntil / IResponse.
    /// </summary>
    [TestFixture]
    public class GoToAndFrameHandleTests : PageTestEx
    {
        [PlaywrightTest("page-goto.spec.ts", "main frame evaluate handle")]
        [Test]
        [Timeout(30_000)]
        public async Task MainFrameEvaluateHandleShouldReturnDocument()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=\"d\">handle</div>").ConfigureAwait(false);
            IJSHandle handle = await page.MainFrame.EvaluateHandleAsync("document").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);

            string nodeName = await handle.EvaluateAsync<string>("node => node.nodeName").ConfigureAwait(false);
            Assert.That(nodeName, Is.EqualTo("#document"));
        }

        [PlaywrightTest("page-goto.spec.ts", "child frame evaluate handle")]
        [Test]
        [Timeout(30_000)]
        public async Task ChildFrameEvaluateHandleShouldRunInOwnWorld()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);

            await child.SetContentAsync("<div id=\"c\">child</div>").ConfigureAwait(false);
            IJSHandle handle = await child.EvaluateHandleAsync("document.getElementById('c')").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(handle.AsElement(), Is.Not.Null);
            Assert.That(await handle.AsElement().GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("c"));
            Assert.That(
                await page.EvaluateAsync<bool>("document.getElementById('c') === null").ConfigureAwait(false),
                Is.True);
        }

        [PlaywrightTest("page-goto.spec.ts", "goto returns response")]
        [Test]
        [Timeout(30_000)]
        public async Task GoToShouldReturnNavigationResponse()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync(TestConstants.EmptyPage, WaitUntilState.DOMContentLoaded).ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(response.Url, Does.Contain("/empty.html"));
            Assert.That(response.Request, Is.Not.Null);
            Assert.That(response.Request.IsNavigationRequest, Is.True);
        }

        [PlaywrightTest("page-goto.spec.ts", "goto waitUntil commit resolves after navigation commit")]
        [Test]
        [Timeout(30_000)]
        public async Task GoToCommitShouldResolveAfterNavigationCommit()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync(TestConstants.EmptyPage, WaitUntilState.Commit).ConfigureAwait(false);
            Assert.That(page.Url, Does.Contain("/empty.html"));
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Status, Is.EqualTo(200));
        }

        [PlaywrightTest("page-goto.spec.ts", "frame goto returns response")]
        [Test]
        [Timeout(30_000)]
        public async Task ChildFrameGoToShouldReturnNavigationResponse()
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

            IResponse response = await child.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(response.Frame, Is.SameAs(child));
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
