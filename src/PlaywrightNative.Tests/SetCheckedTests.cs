/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IPage.SetCheckedAsync"/> and
    /// <see cref="IElementHandle.SetCheckedAsync"/>.
    /// </summary>
    [TestFixture]
    public class SetCheckedTests : PageTestEx
    {
        [PlaywrightTest("page-check.spec.ts", "SetCheckedAsync checks an unchecked box")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldCheckAnUncheckedBox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" />").ConfigureAwait(false);

            IElementHandle box = await page.QuerySelectorAsync("#c").ConfigureAwait(false);
            await box.SetCheckedAsync(true).ConfigureAwait(false);
            Assert.That(await box.IsCheckedAsync().ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "SetCheckedAsync unchecks a checked box")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldUncheckACheckedBox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" checked />").ConfigureAwait(false);

            IElementHandle box = await page.QuerySelectorAsync("#c").ConfigureAwait(false);
            await box.SetCheckedAsync(false).ConfigureAwait(false);
            Assert.That(await box.IsCheckedAsync().ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("page-check.spec.ts", "SetCheckedAsync is a no-op when already matching")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNoOpWhenAlreadyMatching()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" checked />").ConfigureAwait(false);

            await page.SetCheckedAsync("#c", true).ConfigureAwait(false);
            Assert.That(await page.IsCheckedAsync("#c").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "page SetCheckedAsync checks by selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldCheckFromPageSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" />").ConfigureAwait(false);

            await page.SetCheckedAsync("#c", true).ConfigureAwait(false);
            Assert.That(await page.IsCheckedAsync("#c").ConfigureAwait(false), Is.True);

            await page.SetCheckedAsync("#c", false).ConfigureAwait(false);
            Assert.That(await page.IsCheckedAsync("#c").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("page-check.spec.ts", "frame SetCheckedAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldCheckFromMainFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" />").ConfigureAwait(false);

            await page.MainFrame.SetCheckedAsync("#c", true).ConfigureAwait(false);
            Assert.That(await page.MainFrame.IsCheckedAsync("#c").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "SetCheckedAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<p>only</p>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.SetCheckedAsync(".nope", true, new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-check.spec.ts", "SetCheckedAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task checkTask = page.SetCheckedAsync("#c", true, new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                @"document.getElementById('host').insertAdjacentHTML('beforeend', '<input id=""c"" type=""checkbox"" style=""width:20px;height:20px"" />');")
                .ConfigureAwait(false);
            await checkTask.ConfigureAwait(false);
            Assert.That(await page.IsCheckedAsync("#c").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "SetCheckedAsync times out while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWhileHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" style=\"display:none\" />").ConfigureAwait(false);

            IElementHandle box = await page.QuerySelectorAsync("#c").ConfigureAwait(false);
            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => box.SetCheckedAsync(true, new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-check.spec.ts", "SetCheckedAsync waits until visible")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitUntilVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" style=\"display:none\" />").ConfigureAwait(false);

            IElementHandle box = await page.QuerySelectorAsync("#c").ConfigureAwait(false);
            Task checkTask = box.SetCheckedAsync(true, new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#c').style.display = 'inline-block'").ConfigureAwait(false);
            await checkTask.ConfigureAwait(false);
            Assert.That(await box.IsCheckedAsync().ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "SetCheckedAsync honors force")]
        [Test]
        [Timeout(30_000)]
        public async Task SetCheckedAsyncShouldHonorForce()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" />").ConfigureAwait(false);

            IElementHandle box = await page.QuerySelectorAsync("#c").ConfigureAwait(false);
            await box.SetCheckedAsync(true, new() { Force = true }).ConfigureAwait(false);
            Assert.That(await box.IsCheckedAsync().ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "SetCheckedAsync trial does not check")]
        [Test]
        [Timeout(30_000)]
        public async Task SetCheckedTrialShouldNotChangeTheBox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" />").ConfigureAwait(false);

            await page.SetCheckedAsync("#c", true, new() { Trial = true }).ConfigureAwait(false);
            Assert.That(await page.IsCheckedAsync("#c").ConfigureAwait(false), Is.False);
            await page.SetCheckedAsync("#c", true).ConfigureAwait(false);
            Assert.That(await page.IsCheckedAsync("#c").ConfigureAwait(false), Is.True);
        }
    }
}
