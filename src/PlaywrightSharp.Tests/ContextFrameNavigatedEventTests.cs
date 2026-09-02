/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>browserContext.on('framenavigated')</c>.
    /// </summary>
    [TestFixture]
    public class ContextFrameNavigatedEventTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-events.spec.ts", "FrameNavigated fires on main-frame navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextFrameNavigatedShouldFireOnGoTo()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IFrame frame = await context.RunAndWaitForFrameNavigatedAsync(
                () => page.GoToAsync(TestConstants.EmptyPage),
                timeout: 10_000).ConfigureAwait(false);

            Assert.That(frame, Is.SameAs(page.MainFrame));
            Assert.That(frame.Url, Does.Contain("/empty.html").IgnoreCase);
        }
    }
}
