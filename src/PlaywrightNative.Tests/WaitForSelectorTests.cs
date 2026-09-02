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

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IPage.WaitForSelectorAsync"/>,
    /// <see cref="IPage.IsVisibleAsync"/>, and <see cref="IPage.IsHiddenAsync"/>.
    /// Mirrors a first-match subset of upstream <c>page-wait-for-selector-1.spec.ts</c>.
    /// </summary>
    [TestFixture]
    public class WaitForSelectorTests : PageTestEx
    {
        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "should immediately resolve promise if node exists")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldResolveImmediatelyWhenAlreadyVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=\"ready\">hello</div>").ConfigureAwait(false);
            IElementHandle handle = await page.WaitForSelectorAsync("#ready").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Is.EqualTo("hello"));
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "should wait for selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitUntilElementAppears()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div></div>").ConfigureAwait(false);
            Task<IElementHandle> waitTask = page.WaitForSelectorAsync("#late");
            await page.EvaluateAsync("setTimeout(() => { const e = document.createElement('div'); e.id = 'late'; e.textContent = 'ok'; document.body.appendChild(e); }, 50)").ConfigureAwait(false);
            IElementHandle handle = await waitTask.ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Is.EqualTo("ok"));
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "should wait for hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldResolveWhenHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=\"box\" style=\"display:none\">hidden</div>").ConfigureAwait(false);
            IElementHandle handle = await page.WaitForSelectorAsync("#box", WaitForSelectorState.Hidden).ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("box"));
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "should return null if waiting for hidden and node is not present")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnNullWhenWaitingForDetached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IElementHandle handle = await page.WaitForSelectorAsync("#gone", WaitForSelectorState.Detached).ConfigureAwait(false);
            Assert.That(handle, Is.Null);
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "should timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await page.WaitForSelectorAsync("#never", new() { Timeout = 200 }).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("page.waitForSelector"));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "should work with visible and hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task IsVisibleAndIsHiddenShouldMatchDisplay()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=\"on\">shown</div><div id=\"off\" style=\"display:none\">hidden</div>").ConfigureAwait(false);

            Assert.That(await page.IsVisibleAsync("#on").ConfigureAwait(false), Is.True);
            Assert.That(await page.IsHiddenAsync("#on").ConfigureAwait(false), Is.False);
            Assert.That(await page.IsVisibleAsync("#off").ConfigureAwait(false), Is.False);
            Assert.That(await page.IsHiddenAsync("#off").ConfigureAwait(false), Is.True);
            Assert.That(await page.IsVisibleAsync("#missing").ConfigureAwait(false), Is.False);
            Assert.That(await page.IsHiddenAsync("#missing").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "element waitForSelector is scoped")]
        [Test]
        [Timeout(30_000)]
        public async Task ElementWaitForSelectorShouldBeScoped()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=\"root\"></div><div id=\"late\">outside</div>").ConfigureAwait(false);
            IElementHandle root = await page.QuerySelectorAsync("#root").ConfigureAwait(false);

            Task<IElementHandle> waitTask = root.WaitForSelectorAsync("#late");
            await page.EvaluateAsync("setTimeout(() => { const e = document.createElement('div'); e.id = 'late'; e.textContent = 'inside'; document.getElementById('root').appendChild(e); }, 50)").ConfigureAwait(false);
            IElementHandle handle = await waitTask.ConfigureAwait(false);

            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Is.EqualTo("inside"));
        }
    }
}
