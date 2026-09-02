/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IElementHandle.WaitForElementStateAsync"/>.
    /// </summary>
    [TestFixture]
    public class WaitForElementStateTests : PageTestEx
    {
        [PlaywrightTest("elementhandle-wait-for-element-state.spec.ts", "WaitForElementStateAsync resolves when already visible")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldResolveWhenAlreadyVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">ready</div>").ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            await handle.WaitForElementStateAsync(ElementState.Visible).ConfigureAwait(false);
            Assert.That(await handle.IsVisibleAsync().ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("elementhandle-wait-for-element-state.spec.ts", "WaitForElementStateAsync waits until visible")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitUntilVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\" style=\"display:none\">late</div>").ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            Task waitTask = handle.WaitForElementStateAsync(ElementState.Visible);
            await page.EvaluateAsync<bool>(
                @"(() => {
                    setTimeout(() => { document.getElementById('t').style.display = 'block'; }, 50);
                    return true;
                })()").ConfigureAwait(false);
            await waitTask.ConfigureAwait(false);
            Assert.That(await handle.IsVisibleAsync().ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("elementhandle-wait-for-element-state.spec.ts", "WaitForElementStateAsync waits until enabled")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitUntilEnabled()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"n\" disabled />").ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#n").ConfigureAwait(false);
            Task waitTask = handle.WaitForElementStateAsync(ElementState.Enabled);
            await page.EvaluateAsync<bool>(
                @"(() => {
                    setTimeout(() => { document.getElementById('n').disabled = false; }, 50);
                    return true;
                })()").ConfigureAwait(false);
            await waitTask.ConfigureAwait(false);
            Assert.That(await handle.IsEnabledAsync().ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("elementhandle-wait-for-element-state.spec.ts", "WaitForElementStateAsync waits until hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitUntilHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">hide me</div>").ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            Task waitTask = handle.WaitForElementStateAsync(ElementState.Hidden);
            await page.EvaluateAsync<bool>(
                @"(() => {
                    setTimeout(() => { document.getElementById('t').style.display = 'none'; }, 50);
                    return true;
                })()").ConfigureAwait(false);
            await waitTask.ConfigureAwait(false);
            Assert.That(await handle.IsHiddenAsync().ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("elementhandle-wait-for-element-state.spec.ts", "WaitForElementStateAsync times out")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeOutWhenStateNeverChanges()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\" style=\"display:none\">nope</div>").ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => handle.WaitForElementStateAsync(ElementState.Visible, timeout: 200));
            Assert.That(ex.Message, Does.Contain("waitForElementState"));
        }
    }
}
