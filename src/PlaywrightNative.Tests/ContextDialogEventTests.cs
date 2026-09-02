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
    /// Official <c>browserContext.on('dialog')</c>.
    /// </summary>
    [TestFixture]
    public class ContextDialogEventTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-events.spec.ts", "WaitForDialogAsync resolves on page alert")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextWaitForDialogShouldResolveOnAlert()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IDialog> waitTask = context.WaitForDialogAsync();
            Task evaluateTask = page.EvaluateAsync("alert('wave446')");
            IDialog dialog = await waitTask.ConfigureAwait(false);

            Assert.That(dialog, Is.Not.Null);
            Assert.That(dialog.Page, Is.SameAs(page));
            Assert.That(dialog.Message, Is.EqualTo("wave446"));
            await dialog.AcceptAsync(null).ConfigureAwait(false);
            await evaluateTask.ConfigureAwait(false);
        }
    }
}
