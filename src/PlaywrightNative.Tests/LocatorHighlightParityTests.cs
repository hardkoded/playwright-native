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
    /// Official <c>locator-highlight.spec.ts</c> parity for
    /// <see cref="ILocator.HighlightAsync"/>. Node <c>it.skip</c> when
    /// <c>mode !== 'default'</c> is not applied (this gate is default).
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class LocatorHighlightParityTests : PageTestEx
    {
        private static ElementHandleBoundingBoxResult RoundBox(ElementHandleBoundingBoxResult box)
        {
            return new ElementHandleBoundingBoxResult
            {
                X = (float)Math.Round(box.X),
                Y = (float)Math.Round(box.Y),
                Width = (float)Math.Round(box.Width),
                Height = (float)Math.Round(box.Height),
            };
        }

        [PlaywrightTest("locator-highlight.spec.ts", "should highlight locator")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHighlightLocator()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type='text' />").ConfigureAwait(false);
            await page.Locator("input").HighlightAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("x-pw-tooltip")).ToHaveTextAsync("locator('input')").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("x-pw-highlight")).ToBeVisibleAsync().ConfigureAwait(false);
            ElementHandleBoundingBoxResult box1 = RoundBox((await page.Locator("input").BoundingBoxAsync().ConfigureAwait(false)).AsElementHandleBoundingBox());
            ElementHandleBoundingBoxResult box2 = RoundBox((await page.Locator("x-pw-highlight").BoundingBoxAsync().ConfigureAwait(false)).AsElementHandleBoundingBox());
            Assert.That(box2.X, Is.EqualTo(box1.X));
            Assert.That(box2.Y, Is.EqualTo(box1.Y));
            Assert.That(box2.Width, Is.EqualTo(box1.Width));
            Assert.That(box2.Height, Is.EqualTo(box1.Height));
        }
    }
}
