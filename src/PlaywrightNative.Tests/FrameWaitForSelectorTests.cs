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
    /// Direct-connection tests for <see cref="IFrame.WaitForSelectorAsync"/>.
    /// </summary>
    [TestFixture]
    public class FrameWaitForSelectorTests : PageTestEx
    {
        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "resolves immediately when the child node is visible")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldResolveImmediatelyWhenAlreadyVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);

            await child.SetContentAsync("<div id=\"ready\">hello</div>").ConfigureAwait(false);
            IElementHandle handle = await child.WaitForSelectorAsync("#ready").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Is.EqualTo("hello"));
            Assert.That(await page.EvaluateAsync<bool>("document.getElementById('ready') === null").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "waits until an element appears in the child")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitUntilElementAppears()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);

            await child.SetContentAsync("<div></div>").ConfigureAwait(false);
            Task<IElementHandle> waitTask = child.WaitForSelectorAsync("#late");
            await child.EvaluateAsync("setTimeout(() => { const e = document.createElement('div'); e.id = 'late'; e.textContent = 'ok'; document.body.appendChild(e); }, 50)").ConfigureAwait(false);
            IElementHandle handle = await waitTask.ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Is.EqualTo("ok"));
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "wait times out")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await child.WaitForSelectorAsync("#never", new() { Timeout = 200 }).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("frame.waitForSelector"));
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
