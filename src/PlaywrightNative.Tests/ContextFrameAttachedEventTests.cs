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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>browserContext.on('frameattached')</c>.
    /// </summary>
    [TestFixture]
    public class ContextFrameAttachedEventTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-events.spec.ts", "FrameAttached fires when an iframe is added")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextFrameAttachedShouldFireOnIframe()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            IFrame frame = await context.RunAndWaitForFrameAttachedAsync(
                () => page.EvaluateAsync<bool>(@"
                    const iframe = document.createElement('iframe');
                    iframe.src = 'about:blank';
                    document.body.appendChild(iframe);
                    true
                ")).ConfigureAwait(false);

            Assert.That(frame, Is.Not.Null);
            Assert.That(frame.Page, Is.SameAs(page));
            Assert.That(frame.IsDetached, Is.False);
        }
    }
}
