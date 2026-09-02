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
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>APIResponse.timing</c>.
    /// </summary>
    [TestFixture]
    public class ApiResponseTimingTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        private static void AssertTiming(RequestTimingResult timing)
        {
            Assert.That(timing, Is.Not.Null);
            Assert.That(timing.StartTime, Is.GreaterThan(1_600_000_000_000f));
            Assert.That(timing.StartTime, Is.LessThan(2_100_000_000_000f));
            Assert.That(timing.RequestStart, Is.EqualTo(0).Or.EqualTo(-1));
            AssertUnknownOrAfter(timing.DomainLookupStart, -1);
            AssertUnknownOrAfter(timing.DomainLookupEnd, timing.DomainLookupStart);
            AssertUnknownOrAfter(timing.ConnectStart, timing.DomainLookupEnd);
            AssertUnknownOrAfter(timing.SecureConnectionStart, timing.ConnectStart);
            AssertUnknownOrAfter(timing.ConnectEnd, timing.SecureConnectionStart);
            AssertUnknownOrAfter(timing.ResponseStart, timing.RequestStart);
            Assert.That(timing.ResponseEnd, Is.GreaterThanOrEqualTo(0));
            Assert.That(timing.ResponseEnd, Is.LessThan(10_000));
            if (timing.ResponseStart >= 0)
            {
                Assert.That(timing.ResponseEnd, Is.GreaterThanOrEqualTo(timing.ResponseStart));
            }
        }

        private static void AssertUnknownOrAfter(float value, float previous)
        {
            if (value == -1 || previous == -1)
            {
                return;
            }

            Assert.That(value, Is.GreaterThanOrEqualTo(previous));
        }

        [PlaywrightTest("global-fetch.spec.ts", "should return resource timing from response")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnResourceTimingFromResponse()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            AssertTiming(response.Timing);
        }

        [PlaywrightTest("global-fetch.spec.ts", "context APIRequest reports resource timing")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextApiRequestShouldReportResourceTiming()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            AssertTiming(response.Timing);
        }
    }
}
