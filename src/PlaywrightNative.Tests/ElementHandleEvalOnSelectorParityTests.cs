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
    /// Official <c>elementhandle-eval-on-selector.spec.ts</c> parity for
    /// <see cref="IElementHandle.EvalOnSelectorAsync{T}(string, string, object)"/>
    /// and <see cref="IElementHandle.EvalOnSelectorAllAsync{T}(string, string, object)"/>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class ElementHandleEvalOnSelectorParityTests : PageTestEx
    {
        [PlaywrightTest("elementhandle-eval-on-selector.spec.ts", "should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<html><body><div class=\"tweet\"><div class=\"like\">100</div><div class=\"retweets\">10</div></div></body></html>").ConfigureAwait(false);
            IElementHandle tweet = await page.QuerySelectorAsync(".tweet").ConfigureAwait(false);
            string content = await tweet.EvalOnSelectorAsync<string>(".like", "node => node.innerText").ConfigureAwait(false);
            Assert.That(content, Is.EqualTo("100"));
        }

        [PlaywrightTest("elementhandle-eval-on-selector.spec.ts", "should retrieve content from subtree")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRetrieveContentFromSubtree()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            string htmlContent = "<div class=\"a\">not-a-child-div</div><div id=\"myId\"><div class=\"a\">a-child-div</div></div>";
            await page.SetContentAsync(htmlContent).ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync("#myId").ConfigureAwait(false);
            string content = await elementHandle.EvalOnSelectorAsync<string>(".a", "node => node.innerText").ConfigureAwait(false);
            Assert.That(content, Is.EqualTo("a-child-div"));
        }

        [PlaywrightTest("elementhandle-eval-on-selector.spec.ts", "should throw in case of missing selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowInCaseOfMissingSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            string htmlContent = "<div class=\"a\">not-a-child-div</div><div id=\"myId\"></div>";
            await page.SetContentAsync(htmlContent).ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync("#myId").ConfigureAwait(false);
            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => elementHandle.EvalOnSelectorAsync<string>(".a", "node => node.innerText"));
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("Failed to find element matching selector \".a\""));
        }

        [PlaywrightTest("elementhandle-eval-on-selector.spec.ts", "should work for all")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForAll()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<html><body><div class=\"tweet\"><div class=\"like\">100</div><div class=\"like\">10</div></div></body></html>").ConfigureAwait(false);
            IElementHandle tweet = await page.QuerySelectorAsync(".tweet").ConfigureAwait(false);
            string[] content = await tweet.EvalOnSelectorAllAsync<string[]>(".like", "nodes => nodes.map(n => n.innerText)").ConfigureAwait(false);
            Assert.That(content, Is.EqualTo(new[] { "100", "10" }));
        }

        [PlaywrightTest("elementhandle-eval-on-selector.spec.ts", "should retrieve content from subtree for all")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRetrieveContentFromSubtreeForAll()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            string htmlContent = "<div class=\"a\">not-a-child-div</div><div id=\"myId\"><div class=\"a\">a1-child-div</div><div class=\"a\">a2-child-div</div></div>";
            await page.SetContentAsync(htmlContent).ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync("#myId").ConfigureAwait(false);
            string[] content = await elementHandle.EvalOnSelectorAllAsync<string[]>(".a", "nodes => nodes.map(n => n.innerText)").ConfigureAwait(false);
            Assert.That(content, Is.EqualTo(new[] { "a1-child-div", "a2-child-div" }));
        }

        [PlaywrightTest("elementhandle-eval-on-selector.spec.ts", "should not throw in case of missing selector for all")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotThrowInCaseOfMissingSelectorForAll()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            string htmlContent = "<div class=\"a\">not-a-child-div</div><div id=\"myId\"></div>";
            await page.SetContentAsync(htmlContent).ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync("#myId").ConfigureAwait(false);
            int nodesLength = await elementHandle.EvalOnSelectorAllAsync<int>(".a", "nodes => nodes.length").ConfigureAwait(false);
            Assert.That(nodesLength, Is.EqualTo(0));
        }
    }
}
