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
    /// Direct-connection tests for <see cref="IPage.DispatchEventAsync"/> and
    /// <see cref="IElementHandle.DispatchEventAsync"/>.
    /// </summary>
    [TestFixture]
    public class DispatchEventTests : PageTestEx
    {
        [PlaywrightTest("page-dispatchevent.spec.ts", "page DispatchEventAsync fires click")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchClickOnPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"b\" onclick=\"window.clicked=true\">Go</button>").ConfigureAwait(false);

            await page.DispatchEventAsync("#b", "click").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.clicked === true").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "element DispatchEventAsync fires click")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchClickOnElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"b\" onclick=\"window.clicked=true\">Go</button>").ConfigureAwait(false);

            IElementHandle button = await page.QuerySelectorAsync("#b").ConfigureAwait(false);
            await button.DispatchEventAsync("click").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.clicked === true").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "DispatchEventAsync passes CustomEvent detail")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchCustomEventWithDetail()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\" style=\"width:10px;height:10px\"></div>").ConfigureAwait(false);
            await page.EvaluateAsync<bool>(
                @"(() => {
                    document.getElementById('t').addEventListener('app-event', e => { window.detail = e.detail; });
                    return true;
                })()").ConfigureAwait(false);

            await page.DispatchEventAsync("#t", "app-event", new { detail = 42 }).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("window.detail").ConfigureAwait(false), Is.EqualTo(42));
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "frame DispatchEventAsync fires click")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchClickOnMainFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"b\" onclick=\"window.clicked=true\">Go</button>").ConfigureAwait(false);

            await page.MainFrame.DispatchEventAsync("#b", "click").ConfigureAwait(false);
            Assert.That(await page.MainFrame.EvaluateAsync<bool>("window.clicked === true").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "page DispatchEventAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageDispatchEventAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<p>only</p>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.DispatchEventAsync(".nope", "click", options: new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "page DispatchEventAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageDispatchEventAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task dispatchTask = page.DispatchEventAsync("#b", "click", options: new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('host').insertAdjacentHTML('beforeend', '<button id=\"b\" onclick=\"window.clicked=true\">Go</button>')")
                .ConfigureAwait(false);
            await dispatchTask.ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.clicked === true").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "DispatchEventAsync times out while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWhileHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"b\" style=\"display:none\" onclick=\"window.clicked=true\">Go</button>").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("#b").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => button.DispatchEventAsync("click", timeout: 200));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "DispatchEventAsync waits until visible")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitUntilVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"b\" style=\"display:none\" onclick=\"window.clicked=true\">Go</button>").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("#b").ConfigureAwait(false);

            Task dispatchTask = button.DispatchEventAsync("click", timeout: 5000);
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#b').style.display = 'block'").ConfigureAwait(false);
            await dispatchTask.ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<bool>("window.clicked === true").ConfigureAwait(false), Is.True);
        }
    }
}
