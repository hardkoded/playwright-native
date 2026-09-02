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
    /// Direct-connection tests for <see cref="IPage.RequestGCAsync"/>.
    /// </summary>
    [TestFixture]
    public class PageRequestGCTests : PageTestEx
    {
        [PlaywrightTest("page-request-gc.spec.ts", "reachable WeakRef survives requestGC")]
        [Test]
        [Timeout(30_000)]
        public async Task ReachableObjectSurvivesRequestGC()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EvaluateAsync(
                "globalThis.objectToDestroy = { hello: 'world' }; globalThis.weakRef = new WeakRef(globalThis.objectToDestroy);").ConfigureAwait(false);

            await page.RequestGCAsync().ConfigureAwait(false);

            bool alive = await page.EvaluateAsync<bool>(
                "globalThis.weakRef.deref() !== undefined").ConfigureAwait(false);
            Assert.That(alive, Is.True);
        }

        [PlaywrightTest("page-request-gc.spec.ts", "unreachable WeakRef is collected")]
        [Test]
        [Timeout(30_000)]
        public async Task UnreachableObjectIsCollected()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EvaluateAsync(
                "globalThis.objectToDestroy = { hello: 'world' }; globalThis.weakRef = new WeakRef(globalThis.objectToDestroy);").ConfigureAwait(false);
            await page.EvaluateAsync("globalThis.objectToDestroy = null").ConfigureAwait(false);
            await page.RequestGCAsync().ConfigureAwait(false);

            bool collected = await page.EvaluateAsync<bool>(
                "globalThis.weakRef.deref() === undefined").ConfigureAwait(false);
            Assert.That(collected, Is.True);
        }
    }
}
