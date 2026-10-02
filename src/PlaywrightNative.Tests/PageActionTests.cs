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
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for page-level click/fill/focus/check and <see cref="IPage.TitleAsync"/>.
    /// </summary>
    [TestFixture]
    public class PageActionTests : PageTestEx
    {
        [PlaywrightTest("page-click.spec.ts", "should click the button")]
        [Test]
        [Timeout(30_000)]
        public async Task ClickShouldFireDomHandler()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<button id=\"b\" onclick=\"window.clicked=true\">Go</button>").ConfigureAwait(false);
            await page.ClickAsync("#b").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.clicked === true").ConfigureAwait(false), Is.True);
        }
        [PlaywrightTest("page-fill.spec.ts", "should fill input")]
        [Test]
        [Timeout(30_000)]
        public async Task FillShouldSetInputValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input id=\"n\" />").ConfigureAwait(false);
            await page.FillAsync("#n", "Ada").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.querySelector('#n').value").ConfigureAwait(false), Is.EqualTo("Ada"));
        }
        [PlaywrightTest("page-click.spec.ts", "should double click the button")]
        [Test]
        [Timeout(30_000)]
        public async Task DblClickShouldFireDblClick()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<button id=\"b\" ondblclick=\"window.dbl=true\">Go</button>").ConfigureAwait(false);
            await page.DblClickAsync("#b").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.dbl === true").ConfigureAwait(false), Is.True);
        }
    }
}
