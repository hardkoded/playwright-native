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
    /// Direct-connection tests for <see cref="IPage.EvalOnSelectorAsync{T}"/> and
    /// <see cref="IPage.EvalOnSelectorAllAsync{T}"/>.
    /// </summary>
    [TestFixture]
    public class EvalOnSelectorTests : PageTestEx
    {
        [PlaywrightTest("eval-on-selector.spec.ts", "EvalOnSelectorAsync returns the function result")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEvaluateOnMatchingElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>hello</div>").ConfigureAwait(false);

            string text = await page.EvalOnSelectorAsync<string>("div", "el => el.textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("hello"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "EvalOnSelectorAsync passes an argument")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldPassArgumentToFunction()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<span>hi</span>").ConfigureAwait(false);

            string text = await page
                .EvalOnSelectorAsync<string>("span", "(el, suffix) => el.textContent + suffix", "!")
                .ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("hi!"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "EvalOnSelectorAsync throws when nothing matches")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowWhenNothingMatches()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<p>only</p>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(
                () => page.EvalOnSelectorAsync<string>(".nope", "el => el.textContent"));
            Assert.That(ex.Message, Does.Contain("No node found for selector"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "EvalOnSelectorAllAsync evaluates the matching array")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEvaluateOnMatchingArray()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>a</div><div>b</div><span>c</span>").ConfigureAwait(false);

            int count = await page.EvalOnSelectorAllAsync<int>("div", "els => els.length").ConfigureAwait(false);
            Assert.That(count, Is.EqualTo(2));

            string[] texts = await page
                .EvalOnSelectorAllAsync<string[]>("div", "(els, suffix) => els.map(e => e.textContent + suffix)", "-x")
                .ConfigureAwait(false);
            Assert.That(texts, Is.EqualTo(new[] { "a-x", "b-x" }));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "EvalOnSelectorAllAsync allows an empty match")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAllowEmptyMatchOnAll()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<p>only</p>").ConfigureAwait(false);

            int count = await page.EvalOnSelectorAllAsync<int>(".nope", "els => els.length").ConfigureAwait(false);
            Assert.That(count, Is.EqualTo(0));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "frame EvalOnSelectorAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEvaluateOnMainFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<li>1</li><li>2</li>").ConfigureAwait(false);

            string text = await page.MainFrame.EvalOnSelectorAsync<string>("li", "el => el.textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("1"));

            int count = await page.MainFrame.EvalOnSelectorAllAsync<int>("li", "els => els.length").ConfigureAwait(false);
            Assert.That(count, Is.EqualTo(2));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "element EvalOnSelectorAsync is scoped")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEvaluateOnElementHandleScope()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page
                .SetContentAsync("<div id=\"outside\"><span>out</span></div><div id=\"root\"><span>a</span><span>b</span></div>")
                .ConfigureAwait(false);

            IElementHandle root = await page.QuerySelectorAsync("#root").ConfigureAwait(false);
            string first = await root.EvalOnSelectorAsync<string>("span", "el => el.textContent").ConfigureAwait(false);
            Assert.That(first, Is.EqualTo("a"));

            int count = await root.EvalOnSelectorAllAsync<int>("span", "els => els.length").ConfigureAwait(false);
            Assert.That(count, Is.EqualTo(2));
        }
    }
}
