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
    /// Official <c>browserContext.on('framedetached')</c>.
    /// </summary>
    [TestFixture]
    public class ContextFrameDetachedEventTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-events.spec.ts", "FrameDetached fires when an iframe is removed")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextFrameDetachedShouldFireOnIframeRemove()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            IFrame attached = await context.RunAndWaitForFrameAttachedAsync(
                () => page.EvaluateAsync<bool>(@"
                    const iframe = document.createElement('iframe');
                    iframe.id = 'wave450';
                    iframe.src = 'about:blank';
                    document.body.appendChild(iframe);
                    true
                ")).ConfigureAwait(false);

            IFrame detached = await context.RunAndWaitForFrameDetachedAsync(
                () => page.EvaluateAsync<bool>(@"
                    document.getElementById('wave450').remove();
                    true
                ")).ConfigureAwait(false);

            Assert.That(detached, Is.SameAs(attached));
            Assert.That(detached.IsDetached, Is.True);
        }
    }
}
