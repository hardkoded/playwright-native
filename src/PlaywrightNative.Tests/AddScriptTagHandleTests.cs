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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for AddScriptTag / AddStyleTag returning element handles.
    /// </summary>
    [TestFixture]
    public class AddScriptTagHandleTests : PageTestEx
    {
        [PlaywrightTest("page-add-script-tag.spec.ts", "AddScriptTagAsync returns SCRIPT handle")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnScriptElementHandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            IElementHandle handle = await page.AddScriptTagAsync(new() { Content = "window.__wave91 = 91;" }).ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            string tag = await handle.EvaluateAsync<string>("node => node.tagName").ConfigureAwait(false);
            Assert.That(tag, Is.EqualTo("SCRIPT"));
            int marker = await page.EvaluateAsync<int>("window.__wave91").ConfigureAwait(false);
            Assert.That(marker, Is.EqualTo(91));
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "frame AddScriptTagAsync returns SCRIPT handle")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameAddScriptTagAsyncShouldReturnScriptElementHandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            IElementHandle handle = await page.MainFrame.AddScriptTagAsync(new() { Content = "window.__wave325 = 325;" }).ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            string tag = await handle.EvaluateAsync<string>("node => node.tagName").ConfigureAwait(false);
            Assert.That(tag, Is.EqualTo("SCRIPT"));
            int marker = await page.EvaluateAsync<int>("window.__wave325").ConfigureAwait(false);
            Assert.That(marker, Is.EqualTo(325));
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "AddStyleTagAsync returns STYLE handle")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnStyleElementHandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            IElementHandle handle = await page.AddStyleTagAsync(new() { Content = "body { color: red; }" }).ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            string tag = await handle.EvaluateAsync<string>("node => node.tagName").ConfigureAwait(false);
            Assert.That(tag, Is.EqualTo("STYLE"));
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "frame AddStyleTagAsync returns STYLE handle")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameAddStyleTagAsyncShouldReturnStyleElementHandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            IElementHandle handle = await page.MainFrame.AddStyleTagAsync(new() { Content = "body { color: blue; }" }).ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            string tag = await handle.EvaluateAsync<string>("node => node.tagName").ConfigureAwait(false);
            Assert.That(tag, Is.EqualTo("STYLE"));
        }
    }
}
