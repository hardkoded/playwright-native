/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// End-to-end smoke tests for the direct entry point. Exercises the
    /// retained-surface subset: launch -> context -> page -> goto -> evaluate -> close.
    /// </summary>
    [TestFixture]
    public class PlaywrightTests : PageTestEx
    {
        [PlaywrightTest("browser.spec.ts", "LaunchChromiumDirectShouldReturnUsableBrowser")]
        [Test]
        [Timeout(30_000)]
        public void LaunchChromiumDirectShouldReturnUsableBrowser()
        {
            Assert.That(Browser, Is.Not.Null);
            Assert.That(Browser.IsConnected, Is.True);
            Assert.That(Browser.Version, Is.Not.Null.And.Not.Empty);
        }

        [PlaywrightTest("browser.spec.ts", "NewContextShouldReturnUsableContext")]
        [Test]
        [Timeout(30_000)]
        public void NewContextShouldReturnUsableContext()
        {
            Assert.That(Context, Is.Not.Null);
        }

        [PlaywrightTest("browser.spec.ts", "NewPageShouldReturnUsablePage")]
        [Test]
        [Timeout(30_000)]
        public void NewPageShouldReturnUsablePage()
        {
            Assert.That(Page, Is.Not.Null);
            Assert.That(Page.IsClosed, Is.False);
            Assert.That(Page.MainFrame, Is.Not.Null);
        }

        [PlaywrightTest("browser.spec.ts", "GotoAndEvaluateShouldRoundTrip")]
        [Test]
        [Timeout(30_000)]
        public async Task GotoAndEvaluateShouldRoundTrip()
        {
            await Page.GoToAsync("data:text/html,<div id='d'>hello</div>").ConfigureAwait(false);

            string text = await Page.EvaluateAsync<string>("document.querySelector('#d').textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("hello"));
        }

        [PlaywrightTest("browser.spec.ts", "PageCloseShouldTransitionIsClosed")]
        [Test]
        [Timeout(30_000)]
        public async Task PageCloseShouldTransitionIsClosed()
        {
            await Page.CloseAsync().ConfigureAwait(false);

            // IsClosed may need the detach event to fire; allow a brief delay.
            await Task.Delay(300).ConfigureAwait(false);

            Assert.That(Page.IsClosed, Is.True);
        }
    }
}
