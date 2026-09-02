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
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// WaitFor, Clear, and SelectOption on <see cref="ILocator"/>.
    /// </summary>
    [TestFixture]
    public class LocatorWaitForTests : PageTestEx
    {
        [PlaywrightTest("locator-misc-1.spec.ts", "WaitFor visible waits for a match")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForVisibleShouldWaitForAMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task wait = page.Locator("#late").WaitForAsync();
            await page.EvaluateAsync<object>(
                "document.getElementById('host').innerHTML = '<span id=\"late\">ok</span>'").ConfigureAwait(false);
            await wait.ConfigureAwait(false);

            Assert.That(await page.Locator("#late").TextContentAsync().ConfigureAwait(false), Is.EqualTo("ok"));
        }

        [PlaywrightTest("locator-misc-1.spec.ts", "WaitFor hidden waits until gone")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForHiddenShouldWaitUntilGone()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"gone\">x</div>").ConfigureAwait(false);

            Task wait = page.Locator("#gone").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
            await page.EvaluateAsync<object>("document.getElementById('gone').remove()").ConfigureAwait(false);
            await wait.ConfigureAwait(false);

            Assert.That(await page.Locator("#gone").CountAsync().ConfigureAwait(false), Is.EqualTo(0));
        }

        [PlaywrightTest("locator-misc-1.spec.ts", "WaitFor visible is strict")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForVisibleShouldThrowWhenTwoMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div class=\"x\">a</div><div class=\"x\">b</div>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.Locator(".x").WaitForAsync());

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "Clear empties an input")]
        [Test]
        [Timeout(30_000)]
        public async Task ClearShouldEmptyAnInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"n\" value=\"Ada\" />").ConfigureAwait(false);

            await page.Locator("#n").ClearAsync().ConfigureAwait(false);

            Assert.That(await page.Locator("#n").InputValueAsync().ConfigureAwait(false), Is.EqualTo(string.Empty));
        }

        [PlaywrightTest("page-select-option.spec.ts", "SelectOption selects by value")]
        [Test]
        [Timeout(30_000)]
        public async Task SelectOptionShouldSelectByValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<select id=\"s\"><option value=\"a\">A</option><option value=\"b\">B</option></select>").ConfigureAwait(false);

            IReadOnlyList<string> selected = await page.Locator("#s").SelectOptionAsync("b").ConfigureAwait(false);

            Assert.That(selected, Is.EqualTo(new[] { "b" }));
            Assert.That(await page.EvaluateAsync<string>("document.querySelector('#s').value").ConfigureAwait(false), Is.EqualTo("b"));
        }
    }
}
