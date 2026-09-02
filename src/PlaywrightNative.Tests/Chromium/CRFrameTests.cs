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
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.Chromium;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests.Chromium
{
    /// <summary>
    /// Integration tests for frame management via the direct Chromium CDP layer.
    /// Tests frame tree structure, FrameManager, and navigation effects on frames.
    /// </summary>
    [TestFixture]
    public class CRFrameTests : CRTestBase
    {
        [PlaywrightTest("locator-frame.spec.ts", "should have main frame")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveMainFrame()
        {
            Assert.That(Page.MainFrame, Is.Not.Null);
            Assert.That(Page.FrameManager.MainFrame, Is.SameAs(Page.MainFrame));
        }

        [PlaywrightTest("locator-frame.spec.ts", "should detect iframe attachment")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDetectIframeAttachment()
        {
            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            // Set up a watcher for the FrameAttached event.
            TaskCompletionSource<Frame> frameAttachedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Page.FrameManager.FrameAttached += frame => frameAttachedTcs.TrySetResult(frame);

            // Inject an iframe via JavaScript.
            await Page.EvaluateAsync<bool>(@"
                const iframe = document.createElement('iframe');
                iframe.src = 'about:blank';
                document.body.appendChild(iframe);
                true
            ").ConfigureAwait(false);

            // Wait for the frame attached event with timeout.
            using CancellationTokenSource cts = new(5_000);
            cts.Token.Register(() => frameAttachedTcs.TrySetCanceled());

            Frame childFrame = await frameAttachedTcs.Task.ConfigureAwait(false);

            Assert.That(childFrame, Is.Not.Null);
            Assert.That(Page.FrameManager.Frames.Count, Is.GreaterThanOrEqualTo(2),
                "Should have at least 2 frames (main + iframe)");
            Assert.That(Page.MainFrame.ChildFrames, Has.Count.GreaterThanOrEqualTo(1),
                "Main frame should have at least 1 child");
        }

        [PlaywrightTest("locator-frame.spec.ts", "should detect iframe detachment")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDetectIframeDetachment()
        {
            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            // Attach an iframe.
            TaskCompletionSource<Frame> frameAttachedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Page.FrameManager.FrameAttached += frame => frameAttachedTcs.TrySetResult(frame);

            await Page.EvaluateAsync<bool>(@"
                const iframe = document.createElement('iframe');
                iframe.id = 'temp';
                iframe.src = 'about:blank';
                document.body.appendChild(iframe);
                true
            ").ConfigureAwait(false);

            using CancellationTokenSource cts1 = new(5_000);
            cts1.Token.Register(() => frameAttachedTcs.TrySetCanceled());
            await frameAttachedTcs.Task.ConfigureAwait(false);

            int countBefore = Page.FrameManager.Frames.Count;

            // Set up a watcher for the FrameDetached event.
            TaskCompletionSource<Frame> frameDetachedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Page.FrameManager.FrameDetached += frame => frameDetachedTcs.TrySetResult(frame);

            await Page.EvaluateAsync<bool>(@"
                document.getElementById('temp').remove();
                true
            ").ConfigureAwait(false);

            using CancellationTokenSource cts2 = new(5_000);
            cts2.Token.Register(() => frameDetachedTcs.TrySetCanceled());
            await frameDetachedTcs.Task.ConfigureAwait(false);

            int countAfter = Page.FrameManager.Frames.Count;

            Assert.That(countAfter, Is.LessThan(countBefore),
                "Frame count should decrease after iframe removal");
        }

        [PlaywrightTest("locator-frame.spec.ts", "should clear child frames on navigation")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClearChildFramesOnNavigation()
        {
            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            TaskCompletionSource<Frame> frameAttachedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Page.FrameManager.FrameAttached += frame => frameAttachedTcs.TrySetResult(frame);

            await Page.EvaluateAsync<bool>(@"
                const iframe = document.createElement('iframe');
                iframe.src = 'about:blank';
                document.body.appendChild(iframe);
                true
            ").ConfigureAwait(false);

            using CancellationTokenSource cts = new(5_000);
            cts.Token.Register(() => frameAttachedTcs.TrySetCanceled());
            await frameAttachedTcs.Task.ConfigureAwait(false);

            Assert.That(Page.MainFrame.ChildFrames, Has.Count.GreaterThanOrEqualTo(1));

            // Navigate to a new page - child frames should be cleared.
            await Page.GoToAsync("data:text/html,<div>fresh</div>").ConfigureAwait(false);
            Assert.That(Page.MainFrame.ChildFrames, Has.Count.EqualTo(0),
                "Child frames should be cleared after navigation");
        }

        [PlaywrightTest("locator-frame.spec.ts", "Main frame should have correct url after navigation")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task MainFrameShouldHaveCorrectUrlAfterNavigation()
        {
            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(Page.MainFrame.Url, Is.EqualTo(TestConstants.EmptyPage));
        }

        [PlaywrightTest("locator-frame.spec.ts", "should track frame manager frame count")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTrackFrameManagerFrameCount()
        {
            // Initially just the main frame.
            Assert.That(Page.FrameManager.Frames.Count, Is.GreaterThanOrEqualTo(1));

            Frame found = Page.FrameManager.FrameById(Page.MainFrame.FrameId);
            Assert.That(found, Is.SameAs(Page.MainFrame));
        }
    }
}
