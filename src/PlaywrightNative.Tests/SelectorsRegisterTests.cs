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
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>selectors.register</c>.
    /// </summary>
    [TestFixture]
    public class SelectorsRegisterTests : PageTestEx
    {
        private const string TagEngine = @"{
  query(root, selector) {
    return root.querySelector(selector);
  },
  queryAll(root, selector) {
    return Array.from(root.querySelectorAll(selector));
  }
}";

        [PlaywrightTest("selectors-register.spec.ts", "should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            await Playwright.Selectors.RegisterAsync("tag635work", TagEngine).ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><button>Click me</button></div>").ConfigureAwait(false);

            Assert.That(await page.Locator("tag635work=button").CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            string name = await page.EvalOnSelectorAsync<string>("tag635work=button", "el => el.nodeName").ConfigureAwait(false);
            Assert.That(name, Is.EqualTo("BUTTON"));
        }
        [PlaywrightTest("selectors-register.spec.ts", "should work with path")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithPath()
        {
            string path = Path.Combine(Path.GetTempPath(), "pw-tag635-" + Guid.NewGuid().ToString("N") + ".js");
            await File.WriteAllTextAsync(path, TagEngine).ConfigureAwait(false);
            try
            {
                await Playwright.Selectors.RegisterAsync("tag635path", new() { Path = path }).ConfigureAwait(false);
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync("<section>here</section>").ConfigureAwait(false);
                Assert.That(await page.Locator("tag635path=section").CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            }
            finally
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                }
            }
        }
    }
}
