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
    /// Official <c>browserContext.on('weberror')</c>.
    /// </summary>
    [TestFixture]
    public class ContextWebErrorEventTests : PageTestEx
    {
        [PlaywrightTest("page-event-pageerror.spec.ts", "WebError fires on an uncaught page exception")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextWebErrorShouldFireOnUncaughtException()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IWebError error = await context.RunAndWaitForWebErrorAsync(
                () => page.GoToAsync("data:text/html,<script>throw new Error('wave452');</script>")).ConfigureAwait(false);

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Page, Is.SameAs(page));
            Assert.That(error.Error, Does.Contain("wave452"));
        }
    }
}
