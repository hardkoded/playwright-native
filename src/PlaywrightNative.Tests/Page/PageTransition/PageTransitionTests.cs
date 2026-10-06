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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-transition.spec.ts</c>.
    /// </summary>
    [TestFixture]
    public class PageTransitionTests : PageTestEx
    {
        [PlaywrightTest("page-transition.spec.ts", "should not crash when filter transition completes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCrashWhenFilterTransitionCompletes()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("official fixme(browserName === 'webkit'): Web process crashes on the compositor thread when a filter transitions to none");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <style>
      div { transition: filter 0.1s; }
      .dim { filter: brightness(0.95); }
    </style>
    " + string.Concat(Enumerable.Repeat("<div>hello</div>", 10)) + @"
  ").ConfigureAwait(false);
            bool crashed = false;
            page.Crash += (_, _) => crashed = true;
            for (int i = 0; i < 10 && !crashed; i++)
            {
                try
                {
                    await page.EvaluateAsync(@"async () => {
      const divs = [...document.querySelectorAll('div')];
      const settled = () => Promise.all(divs.map(div => new Promise(f => div.addEventListener('transitionend', f, { once: true }))));
      let promise = settled();
      divs.forEach(div => div.classList.add('dim'));
      await promise;
      promise = settled();
      divs.forEach(div => div.classList.remove('dim'));
      await promise;
    }").ConfigureAwait(false);
                }
                catch (PlaywrightException)
                {
                }
            }

            Assert.That(crashed, Is.False);
        }
    }
}
