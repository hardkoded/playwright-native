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
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IFrame.WaitForNavigationAsync()"/>.
    /// </summary>
    [TestFixture]
    public class FrameWaitForNavigationTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "wait then child GoTo returns the document response")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnDocumentResponseForChildGoTo()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);

            Task<IResponse> waitTask = child.WaitForNavigationAsync();
            IResponse gotoResponse = await child.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IResponse waited = await waitTask.ConfigureAwait(false);

            Assert.That(waited, Is.Not.Null);
            Assert.That(waited.Status, Is.EqualTo(200));
            Assert.That(waited.Frame, Is.SameAs(child));
            Assert.That(child.Url, Does.Contain("empty.html"));
            Assert.That(page.Url, Does.Contain("about:blank"));
            Assert.That(gotoResponse?.Status, Is.EqualTo(200));
        }

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "RunAndWaitForNavigationAsync waits for child GoTo")]
        [Test]
        [Timeout(30_000)]
        public async Task RunAndWaitForNavigationAsyncShouldReturnTheResponse()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);

            IResponse waited = await child.RunAndWaitForNavigationAsync(
                () => child.GoToAsync(TestConstants.EmptyPage)).ConfigureAwait(false);

            Assert.That(waited, Is.Not.Null);
            Assert.That(waited.Status, Is.EqualTo(200));
            Assert.That(waited.Frame, Is.SameAs(child));
            Assert.That(child.Url, Does.Contain("empty.html"));
        }

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "url glob ignores a non-matching child GoTo")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFilterChildNavigationByGlob()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);

            Task<IResponse> waitTask = child.WaitForNavigationAsync("**/title.html");
            await child.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(waitTask.IsCompleted, Is.False);

            await child.GoToAsync($"{TestConstants.ServerUrl}/title.html").ConfigureAwait(false);
            IResponse waited = await waitTask.ConfigureAwait(false);

            Assert.That(waited, Is.Not.Null);
            Assert.That(waited.Status, Is.EqualTo(200));
            Assert.That(child.Url, Does.Contain("title.html"));
            Assert.That(page.Url, Does.Contain("about:blank"));
        }

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "parent hash change does not resolve child wait")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldIgnoreParentSameDocumentNavigation()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);

            Task<IResponse> waitTask = child.WaitForNavigationAsync();
            await page.EvaluateAsync<object>("location.hash = 'wave73'").ConfigureAwait(false);
            Assert.That(waitTask.IsCompleted, Is.False);

            IResponse gotoResponse = await child.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IResponse waited = await waitTask.ConfigureAwait(false);

            Assert.That(waited, Is.Not.Null);
            Assert.That(waited.Status, Is.EqualTo(200));
            Assert.That(gotoResponse?.Status, Is.EqualTo(200));
            Assert.That(child.Url, Does.Contain("empty.html"));
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
