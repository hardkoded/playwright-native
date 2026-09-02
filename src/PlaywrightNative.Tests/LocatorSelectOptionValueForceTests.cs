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
    /// Official <c>locator.selectOption(SelectOptionValue, { force })</c>.
    /// </summary>
    [TestFixture]
    public class LocatorSelectOptionValueForceTests : PageTestEx
    {
        [PlaywrightTest("page-select-option.spec.ts", "force true selects a hidden select")]
        [Test]
        [Timeout(30_000)]
        public async Task ForceTrueShouldSelectAHiddenSelect()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<select id='only' style='display:none'><option value='wave689'>a</option></select>").ConfigureAwait(false);
            SelectOptionValue values = new SelectOptionValue { Value = "wave689" };

            await page.Locator("#only").SelectOptionAsync(values, options: new() { Force = true }).ConfigureAwait(false);
            string actual = await page.EvaluateAsync<string>("document.getElementById('only').value").ConfigureAwait(false);
            Assert.That(actual, Is.EqualTo("wave689"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "omitted force times out on a hidden select")]
        [Test]
        [Timeout(30_000)]
        public async Task OmittedForceShouldTimeoutOnAHiddenSelect()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<select id='only' style='display:none'><option value='wave689'>a</option></select>").ConfigureAwait(false);
            SelectOptionValue values = new SelectOptionValue { Value = "wave689" };

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.Locator("#only").SelectOptionAsync(values, options: new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "force true accepts a visible unique selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ForceTrueShouldAcceptAVisibleUniqueSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<select id='only'><option value='wave689'>a</option></select>").ConfigureAwait(false);
            SelectOptionValue values = new SelectOptionValue { Value = "wave689" };

            await page.Locator("#only").SelectOptionAsync(values, options: new() { Force = true }).ConfigureAwait(false);
            string actual = await page.EvaluateAsync<string>("document.getElementById('only').value").ConfigureAwait(false);
            Assert.That(actual, Is.EqualTo("wave689"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "frame locator honors force")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameLocatorShouldHonorForce()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<iframe></iframe>").ConfigureAwait(false);
            IFrame frame = null;
            foreach (IFrame child in page.MainFrame.ChildFrames)
            {
                frame = child;
                break;
            }

            Assert.That(frame, Is.Not.Null);
            await frame.SetContentAsync("<select id='only' style='display:none'><option value='wave689'>a</option></select>").ConfigureAwait(false);
            SelectOptionValue values = new SelectOptionValue { Value = "wave689" };

            await frame.Locator("#only").SelectOptionAsync(values, options: new() { Force = true }).ConfigureAwait(false);
            string actual = await frame.EvaluateAsync<string>("document.getElementById('only').value").ConfigureAwait(false);
            Assert.That(actual, Is.EqualTo("wave689"));
        }
    }
}
