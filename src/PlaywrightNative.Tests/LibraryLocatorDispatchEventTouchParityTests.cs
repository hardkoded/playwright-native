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
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/locator-dispatchevent-touch.spec.ts</c> parity.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryLocatorDispatchEventTouchParityTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("locator-dispatchevent-touch.spec.ts", "should support touch points in touch event arguments")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportTouchPointsInTouchEventArguments()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { HasTouch = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync(@"
      <div data-testid='outer' style=""position: absolute; width: 120px; height: 120px; background-color: red;"">
        <div data-testid='inner' style=""position: absolute; width: 100px; height: 100px; top: 10px; left: 10px; background-color: green; z-index: 3;"">inner</div>
      </div>").ConfigureAwait(false);
            ILocator outer = page.GetByTestId("outer");
            await outer.EvaluateAsync<object>(@"(el) => {
    const events = [];
    window.events = events;
    el.addEventListener('touchstart', (e) => events.push('touchstart: ' + [...e.touches].map(t => `${t.constructor.name}(id: ${t.identifier}, clientX: ${t.clientX}, clientY: ${t.clientY})`)));
    el.addEventListener('touchmove', (e) => events.push('touchmove: ' + [...e.touches].map(t => `${t.constructor.name}(id: ${t.identifier}, clientX: ${t.clientX}, clientY: ${t.clientY})`)));
    el.addEventListener('touchend', (e) => events.push('touchend: ' + [...e.touches].map(t => `${t.constructor.name}(id: ${t.identifier}, clientX: ${t.clientX}, clientY: ${t.clientY})`)));
  }").ConfigureAwait(false);

            object[] touches =
            {
                new { identifier = 0, clientX = 61, clientY = 60 },
                new { identifier = 1, clientX = 59, clientY = 60 },
            };
            ILocator inner = page.GetByTestId("inner");
            await inner.DispatchEventAsync("touchstart", new
            {
                touches,
                changedTouches = touches,
                targetTouches = touches,
            }).ConfigureAwait(false);
            await inner.DispatchEventAsync("touchmove", new
            {
                touches,
                changedTouches = touches,
                targetTouches = touches,
            }).ConfigureAwait(false);
            await inner.DispatchEventAsync("touchend", new
            {
                touches = Array.Empty<object>(),
                changedTouches = touches,
                targetTouches = Array.Empty<object>(),
            }).ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string[]>("() => window.events").ConfigureAwait(false),
                Is.EqualTo(new[]
                {
                    "touchstart: Touch(id: 0, clientX: 61, clientY: 60),Touch(id: 1, clientX: 59, clientY: 60)",
                    "touchmove: Touch(id: 0, clientX: 61, clientY: 60),Touch(id: 1, clientX: 59, clientY: 60)",
                    "touchend: ",
                }));
        }
    }
}
