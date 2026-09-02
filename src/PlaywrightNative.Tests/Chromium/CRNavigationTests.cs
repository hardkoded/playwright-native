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
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests.Chromium
{
    /// <summary>
    /// Integration tests for navigation via the direct Chromium CDP layer.
    /// Tests GoToAsync with lifecycle waiting, error handling, and frame URL tracking.
    /// </summary>
    [TestFixture]
    public class CRNavigationTests : CRTestBase
    {
        [PlaywrightTest("page-navigation.spec.ts", "Go to should work")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GoToShouldWork()
        {
            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(Page.FrameManager.MainFrame.Url, Is.EqualTo(TestConstants.EmptyPage));
        }

        [PlaywrightTest("page-navigation.spec.ts", "Go to should work with data url")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GoToShouldWorkWithDataUrl()
        {
            await Page.GoToAsync("data:text/html,<h1>Hello</h1>").ConfigureAwait(false);

            string text = await Page.EvaluateAsync<string>("document.querySelector('h1').textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("Hello"));
        }

        [PlaywrightTest("page-navigation.spec.ts", "Go to should wait for dom content loaded")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GoToShouldWaitForDomContentLoaded()
        {
            await Page.GoToAsync(TestConstants.EmptyPage, WaitUntilState.DOMContentLoaded).ConfigureAwait(false);
            Assert.That(Page.FrameManager.MainFrame.LifecycleEvents, Does.Contain("DOMContentLoaded"));
        }

        [PlaywrightTest("page-navigation.spec.ts", "Go to should wait for load")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GoToShouldWaitForLoad()
        {
            await Page.GoToAsync(TestConstants.EmptyPage, WaitUntilState.Load).ConfigureAwait(false);
            Assert.That(Page.FrameManager.MainFrame.LifecycleEvents, Does.Contain("load"));
        }

        [PlaywrightTest("page-navigation.spec.ts", "Go to should update main frame url")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GoToShouldUpdateMainFrameUrl()
        {
            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(Page.FrameManager.MainFrame.Url, Is.EqualTo(TestConstants.EmptyPage));

            await Page.GoToAsync("data:text/html,<div>second</div>").ConfigureAwait(false);
            Assert.That(Page.FrameManager.MainFrame.Url, Does.StartWith("data:text/html"));
        }

        [PlaywrightTest("page-navigation.spec.ts", "Go to should update document id")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GoToShouldUpdateDocumentId()
        {
            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            string firstDocId = Page.FrameManager.MainFrame.DocumentId;
            Assert.That(firstDocId, Is.Not.Null.And.Not.Empty);

            await Page.GoToAsync("data:text/html,<div>new</div>").ConfigureAwait(false);
            string secondDocId = Page.FrameManager.MainFrame.DocumentId;
            Assert.That(secondDocId, Is.Not.Null.And.Not.Empty);
            Assert.That(secondDocId, Is.Not.EqualTo(firstDocId));
        }

        [PlaywrightTest("page-navigation.spec.ts", "Go to should allow evaluation after navigation")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GoToShouldAllowEvaluationAfterNavigation()
        {
            await Page.GoToAsync("data:text/html,<div id='test'>content</div>").ConfigureAwait(false);

            string content = await Page.EvaluateAsync<string>("document.getElementById('test').textContent").ConfigureAwait(false);
            Assert.That(content, Is.EqualTo("content"));
        }

        [PlaywrightTest("page-navigation.spec.ts", "Go to should clear lifecycle on new navigation")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GoToShouldClearLifecycleOnNewNavigation()
        {
            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(Page.FrameManager.MainFrame.LifecycleEvents, Does.Contain("load"));

            // Navigating to a new page should clear lifecycle events before re-adding them.
            await Page.GoToAsync("data:text/html,<div>new</div>").ConfigureAwait(false);
            // After GoToAsync returns, load should have fired again.
            Assert.That(Page.FrameManager.MainFrame.LifecycleEvents, Does.Contain("load"));
        }

        [PlaywrightTest("page-navigation.spec.ts", "Go to should throw on navigation error")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public void GoToShouldThrowOnNavigationError()
        {
            NavigationException ex = Assert.ThrowsAsync<NavigationException>(
                () => Page.GoToAsync("https://localhost:44444/nonexistent"));
            Assert.That(ex.Message, Does.Contain("Navigation failed"));
        }

        [PlaywrightTest("page-navigation.spec.ts", "Go to should handle multiple navigations sequentially")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GoToShouldHandleMultipleNavigationsSequentially()
        {
            await Page.GoToAsync("data:text/html,<div>1</div>").ConfigureAwait(false);
            string first = await Page.EvaluateAsync<string>("document.querySelector('div').textContent").ConfigureAwait(false);
            Assert.That(first, Is.EqualTo("1"));

            await Page.GoToAsync("data:text/html,<div>2</div>").ConfigureAwait(false);
            string second = await Page.EvaluateAsync<string>("document.querySelector('div').textContent").ConfigureAwait(false);
            Assert.That(second, Is.EqualTo("2"));

            await Page.GoToAsync("data:text/html,<div>3</div>").ConfigureAwait(false);
            string third = await Page.EvaluateAsync<string>("document.querySelector('div').textContent").ConfigureAwait(false);
            Assert.That(third, Is.EqualTo("3"));
        }

        [PlaywrightTest("page-navigation.spec.ts", "should navigate to test server page")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNavigateToTestServerPage()
        {
            Server.SetRoute("/title.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync("<title>Woof</title><body>Dogs are the best</body>");
            });

            await Page.GoToAsync(TestConstants.ServerUrl + "/title.html").ConfigureAwait(false);

            string title = await Page.EvaluateAsync<string>("document.title").ConfigureAwait(false);
            Assert.That(title, Is.EqualTo("Woof"));
        }
    }
}
