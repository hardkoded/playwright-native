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
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// IRequest.Timing from Network.requestWillBeSent / response.timing / loadingFinished.
    /// </summary>
    [TestFixture]
    public class RequestTimingTests : PageTestEx
    {
        [PlaywrightTest("page-network-request.spec.ts", "should expose start time after navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task TimingShouldExposeStartTimeAfterNavigation()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<IRequest> finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.RequestFinished += (_, request) =>
            {
                if (request.Url.Contains("/empty.html", StringComparison.Ordinal))
                {
                    finished.TrySetResult(request);
                }
            };

            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            using CancellationTokenSource cts = new(10_000);
            cts.Token.Register(() => finished.TrySetCanceled());
            IRequest request = await finished.Task.ConfigureAwait(false);

            Assert.That(request.Timing, Is.Not.Null);
            Assert.That(request.Timing.StartTime, Is.GreaterThan(0));
            Assert.That(
                request.Timing.ResponseEnd >= 0 || request.Timing.RequestStart >= 0,
                Is.True,
                "Expected responseEnd or requestStart after the document request finished.");
        }
    }
}
