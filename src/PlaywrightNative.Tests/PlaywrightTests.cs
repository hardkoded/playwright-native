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
