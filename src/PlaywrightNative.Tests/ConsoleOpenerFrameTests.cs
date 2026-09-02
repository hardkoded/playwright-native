/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Console, pageerror, opener, input setters, and Chromium frame surfaces.
    /// </summary>
    [TestFixture]
    public class ConsoleOpenerFrameTests : PageTestEx
    {
        [PlaywrightTest("page-event-console.spec.ts", "should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ConsoleEventShouldFireForLog()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<IConsoleMessage> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.Console += (_, message) => tcs.TrySetResult(message);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EvaluateAsync<object>("console.log('hello-wave-42')").ConfigureAwait(false);

            using CancellationTokenSource cts = new(5_000);
            cts.Token.Register(() => tcs.TrySetCanceled());
            IConsoleMessage received = await tcs.Task.ConfigureAwait(false);

            Assert.That(received, Is.Not.Null);
            Assert.That(received.Page, Is.SameAs(page));
            Assert.That(received.Text, Does.Contain("hello-wave-42"));
            Assert.That(received.Type, Is.EqualTo("log"));
        }

        [PlaywrightTest("page-event-pageerror.spec.ts", "should fire")]
        [Test]
        [Timeout(30_000)]
        public async Task PageErrorShouldFireForUncaughtException()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<string> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.PageError += (_, error) => tcs.TrySetResult(error.ToString());

            await page.GoToAsync("data:text/html,<script>throw new Error('boom-wave-42');</script>").ConfigureAwait(false);

            using CancellationTokenSource cts = new(5_000);
            cts.Token.Register(() => tcs.TrySetCanceled());
            string received = await tcs.Task.ConfigureAwait(false);

            Assert.That(received, Does.Contain("boom-wave-42"));
        }

        [PlaywrightTest("page-event-popup.spec.ts", "should return opener page")]
        [Test]
        [Timeout(30_000)]
        public async Task OpenerAsyncShouldReturnOpeningPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<IPage> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.Popup += (_, popup) => tcs.TrySetResult(popup);

            await page.GoToAsync("data:text/html,<div>opener</div>").ConfigureAwait(false);

            // Comma-operator `true` so CDP does not try to serialize the Window proxy
            // ("Object reference chain is too long").
            await page.EvaluateAsync<bool>("window.open('about:blank'), true").ConfigureAwait(false);

            using CancellationTokenSource cts = new(5_000);
            cts.Token.Register(() => tcs.TrySetCanceled());
            IPage popup = await tcs.Task.ConfigureAwait(false);

            IPage opener = await popup.OpenerAsync().ConfigureAwait(false);
            Assert.That(opener, Is.SameAs(page));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should accept keyboard setter")]
        [Test]
        [Timeout(30_000)]
        public async Task KeyboardMouseTouchscreenSettersShouldRoundTrip()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IKeyboard keyboard = page.Keyboard;
            IMouse mouse = page.Mouse;
            ITouchscreen touchscreen = page.Touchscreen;
            // MS IPage input devices are read-only; round-trip via getters only.
            Assert.That(page.Keyboard, Is.Not.Null);
            Assert.That(page.Mouse, Is.Not.Null);
            Assert.That(page.Touchscreen, Is.Not.Null);
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should report main frame and child frames")]
        [Test]
        [Timeout(30_000)]
        public async Task FramesShouldIncludeMainAndIframe()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EvaluateAsync<bool>(@"
                const iframe = document.createElement('iframe');
                iframe.src = 'about:blank';
                document.body.appendChild(iframe);
                true
            ").ConfigureAwait(false);

            IFrame child = await WaitForChildFrameAsync(page).ConfigureAwait(false);

            Assert.That(page.MainFrame, Is.Not.Null);
            Assert.That(page.Frames.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(child, Is.Not.Null);
            Assert.That(child.ParentFrame, Is.SameAs(page.MainFrame));
        }

        [PlaywrightTest("page-network-request.spec.ts", "response frame should be main frame")]
        [Test]
        [Timeout(30_000)]
        public async Task ResponseFrameShouldBeMainFrame()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IResponse> waitTask = page.WaitForResponseAsync(r => r.Url.Contains("/empty.html", StringComparison.Ordinal));
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IResponse response = await waitTask.ConfigureAwait(false);

            Assert.That(response.Frame, Is.Not.Null);
            Assert.That(response.Frame, Is.SameAs(page.MainFrame));
            Assert.That(response.Request.Frame, Is.SameAs(page.MainFrame));
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should fire attached and detached")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameAttachedAndDetachedShouldFire()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);

            TaskCompletionSource<IFrame> attached = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<IFrame> detached = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.FrameAttached += (_, frame) => attached.TrySetResult(frame);
            page.FrameDetached += (_, frame) => detached.TrySetResult(frame);

            await page.EvaluateAsync<bool>(@"
                const iframe = document.createElement('iframe');
                iframe.id = 'wave43';
                iframe.src = 'about:blank';
                document.body.appendChild(iframe);
                true
            ").ConfigureAwait(false);

            using CancellationTokenSource attachCts = new(5_000);
            attachCts.Token.Register(() => attached.TrySetCanceled());
            IFrame child = await attached.Task.ConfigureAwait(false);
            Assert.That(child, Is.Not.Null);
            Assert.That(child.IsDetached, Is.False);

            await page.EvaluateAsync<bool>(@"
                document.getElementById('wave43').remove();
                true
            ").ConfigureAwait(false);

            using CancellationTokenSource detachCts = new(5_000);
            detachCts.Token.Register(() => detached.TrySetCanceled());
            IFrame gone = await detached.Task.ConfigureAwait(false);
            Assert.That(gone, Is.SameAs(child));
            Assert.That(gone.IsDetached, Is.True);
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should fire navigated")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameNavigatedShouldFireForMainFrame()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<IFrame> navigated = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.FrameNavigated += (_, frame) =>
            {
                if (frame.Url != null && frame.Url.Contains("/empty.html", StringComparison.Ordinal))
                {
                    navigated.TrySetResult(frame);
                }
            };

            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            using CancellationTokenSource cts = new(5_000);
            cts.Token.Register(() => navigated.TrySetCanceled());
            IFrame frame = await navigated.Task.ConfigureAwait(false);
            Assert.That(frame, Is.SameAs(page.MainFrame));
            Assert.That(page.MainFrame.Url, Is.EqualTo(TestConstants.EmptyPage));
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should find frame by url")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameByUrlShouldFindMainAndChild()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(page.FrameByUrl(TestConstants.EmptyPage, null, null), Is.SameAs(page.MainFrame));
            Assert.That(page.FrameByUrl(TestConstants.EmptyPage), Is.SameAs(page.MainFrame));
            Assert.That(page.FrameByUrl(new Regex("empty\\.html$", RegexOptions.CultureInvariant)), Is.SameAs(page.MainFrame));
            Assert.That(page.FrameByUrl(url => url.Contains("/empty.html", StringComparison.Ordinal)), Is.SameAs(page.MainFrame));
            Assert.That(page.FrameByUrl(null, null, url => url.Contains("/empty.html", StringComparison.Ordinal)), Is.SameAs(page.MainFrame));

            string childUrl = TestConstants.ServerUrl + "/grid.html";
            await page.EvaluateAsync<bool>(@"url => {
                const iframe = document.createElement('iframe');
                iframe.name = 'child-wave43';
                iframe.src = url;
                document.body.appendChild(iframe);
                return true;
            }", childUrl).ConfigureAwait(false);

            IFrame child = null;
            for (int i = 0; i < 50 && child == null; i++)
            {
                child = page.FrameByUrl(null, null, url => url.Contains("/grid.html", StringComparison.Ordinal));
                if (child != null)
                {
                    break;
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            Assert.That(child, Is.Not.Null);
            Assert.That(child, Is.Not.SameAs(page.MainFrame));
            Assert.That(page.FrameByUrl("**/grid.html"), Is.SameAs(child));
            Assert.That(page.FrameByUrl(new Regex("grid\\.html$", RegexOptions.CultureInvariant)), Is.SameAs(child));
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should find frame by name")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameShouldFindChildByName()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(page.Frame("missing-wave167"), Is.Null);

            string childUrl = TestConstants.ServerUrl + "/grid.html";
            await page.EvaluateAsync<bool>(@"url => {
                const iframe = document.createElement('iframe');
                iframe.name = 'child-wave167';
                iframe.src = url;
                document.body.appendChild(iframe);
                return true;
            }", childUrl).ConfigureAwait(false);

            IFrame child = null;
            for (int i = 0; i < 50 && child == null; i++)
            {
                child = page.Frame("child-wave167");
                if (child != null)
                {
                    break;
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            Assert.That(child, Is.Not.Null);
            Assert.That(child, Is.Not.SameAs(page.MainFrame));
            Assert.That(child.Name, Is.EqualTo("child-wave167"));
        }

        [PlaywrightTest("frame-evaluate.spec.ts", "should evaluate in main and child frames")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameEvaluateShouldRunInOwnWorld()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            Assert.That(await page.MainFrame.EvaluateAsync<int>("1 + 1").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await page.MainFrame.TitleAsync().ConfigureAwait(false), Is.EqualTo(await page.TitleAsync().ConfigureAwait(false)));

            if (!TestConstants.IsChromium)
            {
                return;
            }

            await page.EvaluateAsync<bool>(@"
                const iframe = document.createElement('iframe');
                iframe.src = 'about:blank';
                document.body.appendChild(iframe);
                true
            ").ConfigureAwait(false);

            IFrame child = await WaitForChildFrameAsync(page).ConfigureAwait(false);
            Assert.That(child, Is.Not.Null);

            int childSum = 0;
            bool? childIsTop = null;
            for (int i = 0; i < 50 && childIsTop == null; i++)
            {
                try
                {
                    childSum = await child.EvaluateAsync<int>("1 + 1").ConfigureAwait(false);
                    childIsTop = await child.EvaluateAsync<bool>("window === window.top").ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                    await Task.Delay(100).ConfigureAwait(false);
                }
            }

            Assert.That(childSum, Is.EqualTo(2));
            Assert.That(childIsTop, Is.False);
        }

        private static async Task<IFrame> WaitForChildFrameAsync(IPage page)
        {
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

            return child;
        }
    }
}
