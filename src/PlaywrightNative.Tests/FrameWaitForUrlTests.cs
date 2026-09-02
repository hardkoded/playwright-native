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
    /// Direct-connection tests for <see cref="IFrame.WaitForURLAsync(string, float?, WaitUntilState)"/>.
    /// </summary>
    [TestFixture]
    public class FrameWaitForUrlTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("page-wait-for-url.spec.ts", "resolves immediately when the child URL already matches")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldResolveImmediatelyWhenUrlAlreadyMatches()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);

            await child.WaitForURLAsync("about:blank").ConfigureAwait(false);
            Assert.That(child.Url, Does.Contain("about:blank"));
        }

        [PlaywrightTest("page-wait-for-url.spec.ts", "wait then child GoTo matches glob")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitUntilChildUrlMatchesGlob()
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

            Task waitTask = child.WaitForURLAsync("**/empty.html");
            await child.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await waitTask.ConfigureAwait(false);

            Assert.That(child.Url, Does.Contain("empty.html"));
            Assert.That(page.Url, Does.Contain("about:blank"));
        }

        [PlaywrightTest("page-wait-for-url.spec.ts", "wait times out")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForUrl()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await child.WaitForURLAsync("**/never-this-url", new() { Timeout = 200 }).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("frame.waitForURL"));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
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
