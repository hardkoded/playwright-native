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
    /// Assertions.SetDefaultExpectTimeout.
    /// </summary>
    [TestFixture]
    public class ExpectTimeoutTests : PageTestEx
    {
        [PlaywrightTest("expect-timeout.spec.ts", "SetDefaultExpectTimeout is used when timeout is omitted")]
        [Test]
        [Timeout(30_000)]
        public async Task SetDefaultExpectTimeoutShouldBeUsedWhenTimeoutIsOmitted()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">x</div>").ConfigureAwait(false);

            Assertions.SetDefaultExpectTimeout(400);
            try
            {
                Exception ex = Assert.CatchAsync(() => Assertions.Expect(page.Locator("#missing")).ToBeVisibleAsync());
                Assert.That(ex, Is.InstanceOf<TimeoutException>());
                Assert.That(ex.Message, Does.Contain("400"));
            }
            finally
            {
                Assertions.SetDefaultExpectTimeout(30_000);
            }
        }
    }
}
