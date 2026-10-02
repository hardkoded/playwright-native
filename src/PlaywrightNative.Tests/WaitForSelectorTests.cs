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
    /// Direct-connection tests for <see cref="IPage.WaitForSelectorAsync"/>,
    /// <see cref="IPage.IsVisibleAsync"/>, and <see cref="IPage.IsHiddenAsync"/>.
    /// Mirrors a first-match subset of upstream <c>page-wait-for-selector-1.spec.ts</c>.
    /// </summary>
    [TestFixture]
    public class WaitForSelectorTests : PageTestEx
    {
        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "should immediately resolve promise if node exists")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldResolveImmediatelyWhenAlreadyVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=\"ready\">hello</div>").ConfigureAwait(false);
            IElementHandle handle = await page.WaitForSelectorAsync("#ready").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Is.EqualTo("hello"));
        }
    }
}
