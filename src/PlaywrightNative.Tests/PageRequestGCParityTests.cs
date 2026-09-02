/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-request-gc.spec.ts</c> parity for
    /// <see cref="IPage.RequestGCAsync"/>.
    /// Upstream marks
    /// <c>should collect element retained by locator hit-target interceptor after detach</c>
    /// as <c>test.fixme</c> on Chromium and Firefox. Chromium is still ported and
    /// run. If Chromium retains a native reference to the last hit-tested element
    /// (upstream #41575), that title is ignored with the official fixme reason.
    /// WebKit is expected to pass both titles when the locator interceptor
    /// releases its handle on detach.
    /// </summary>
    [TestFixture]
    public class PageRequestGCParityTests : PageTestEx
    {
        [PlaywrightTest("page-request-gc.spec.ts", "should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            await page.EvaluateAsync<object>(@"(() => {
                globalThis.objectToDestroy = { hello: 'world' };
                globalThis.weakRef = new WeakRef(globalThis.objectToDestroy);
            })()").ConfigureAwait(false);

            await page.RequestGCAsync().ConfigureAwait(false);
            string first = await page.EvaluateAsync<string>(
                "(() => JSON.stringify(globalThis.weakRef.deref()))()").ConfigureAwait(false);
            Assert.That(first, Is.EqualTo("{\"hello\":\"world\"}"));

            await page.RequestGCAsync().ConfigureAwait(false);
            string second = await page.EvaluateAsync<string>(
                "(() => JSON.stringify(globalThis.weakRef.deref()))()").ConfigureAwait(false);
            Assert.That(second, Is.EqualTo("{\"hello\":\"world\"}"));

            await page.EvaluateAsync<object>("(() => { globalThis.objectToDestroy = null; })()").ConfigureAwait(false);
            await page.RequestGCAsync().ConfigureAwait(false);
            bool collected = await page.EvaluateAsync<bool>(
                "(() => globalThis.weakRef.deref() === undefined)()").ConfigureAwait(false);
            Assert.That(collected, Is.True);
        }

        [PlaywrightTest("page-request-gc.spec.ts", "should collect element retained by locator hit-target interceptor after detach")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCollectElementRetainedByLocatorHitTargetInterceptorAfterDetach()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<button id=\"btn\">click me</button>").ConfigureAwait(false);
            await page.Locator("#btn").ClickAsync().ConfigureAwait(false);
            await page.EvaluateAsync<object>(@"(() => {
                globalThis.weakRef = new WeakRef(document.getElementById('btn'));
                document.getElementById('btn').remove();
            })()").ConfigureAwait(false);
            await page.RequestGCAsync().ConfigureAwait(false);
            bool collected = await page.EvaluateAsync<bool>(
                "(() => globalThis.weakRef.deref() === undefined)()").ConfigureAwait(false);
            if (!collected && TestConstants.IsChromium)
            {
                Assert.Ignore("Chromium retains a native reference to the last hit-tested element; fix pending upstream discussion, see #41575");
            }

            Assert.That(collected, Is.True);
        }
    }
}
