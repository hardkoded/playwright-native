/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IFrame.WaitForLoadStateAsync"/>.
    /// </summary>
    [TestFixture]
    public class FrameWaitForLoadStateTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "resolves immediately when the child is already loaded")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldResolveImmediatelyWhenAlreadyLoaded()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);

            await child.WaitForLoadStateAsync(LoadState.Load).ConfigureAwait(false);
            await child.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
            Assert.That(child.Url, Does.Contain("about:blank"));
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "after child GoTo the frame is loaded")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReachLoadAfterChildGoTo()
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

            await child.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await child.WaitForLoadStateAsync(LoadState.Load).ConfigureAwait(false);
            await child.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
            Assert.That(child.Url, Does.Contain("empty.html"));
            Assert.That(page.Url, Does.Contain("about:blank"));
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "reaches networkidle after child GoTo")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReachNetworkIdleAfterChildGoTo()
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

            await child.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await child.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);
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
