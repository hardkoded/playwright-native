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
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IPage.WaitForRequestAsync"/>,
    /// <see cref="IPage.WaitForRequestFinishedAsync"/>,
    /// <see cref="IPage.WaitForRequestFailedAsync"/>, and
    /// <see cref="IPage.WaitForResponseAsync"/>. First-match subset of upstream
    /// <c>page-wait-for-request</c> / <c>page-wait-for-response</c>.
    /// </summary>
    [TestFixture]
    public class WaitForRequestTests : PageTestEx
    {
        [PlaywrightTest("page-wait-for-request.spec.ts", "should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForMatchingRequest()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            string url = TestConstants.ServerUrl + "/digits/2.png";
            Task<IRequest> waitTask = page.WaitForRequestAsync(url);
            await page.EvaluateAsync(
                @"() => {
                    void fetch('/digits/1.png');
                    void fetch('/digits/2.png');
                    void fetch('/digits/3.png');
                }").ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);
            Assert.That(request, Is.Not.Null);
            Assert.That(request.Url, Is.EqualTo(url));
            Assert.That(request.Method, Is.EqualTo("GET"));
        }
        [PlaywrightTest("page-wait-for-request.spec.ts", "should work with predicate")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForRequestPredicate()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            string url = TestConstants.ServerUrl + "/digits/2.png";
            Task<IRequest> waitTask = page.WaitForRequestAsync(r => r.Url == url);
            await page.EvaluateAsync(
                @"() => {
                    void fetch('/digits/1.png');
                    void fetch('/digits/2.png');
                    void fetch('/digits/3.png');
                }").ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);
            Assert.That(request.Url, Is.EqualTo(url));
        }

        [PlaywrightTest("page-wait-for-response.spec.ts", "should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForMatchingResponse()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            string url = TestConstants.ServerUrl + "/digits/2.png";
            Task<IResponse> waitTask = page.WaitForResponseAsync(url);
            await page.EvaluateAsync(
                @"() => {
                    void fetch('/digits/1.png');
                    void fetch('/digits/2.png');
                    void fetch('/digits/3.png');
                }").ConfigureAwait(false);
            IResponse response = await waitTask.ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Url, Is.EqualTo(url));
            Assert.That(response.Ok, Is.True);
        }
    }
}
