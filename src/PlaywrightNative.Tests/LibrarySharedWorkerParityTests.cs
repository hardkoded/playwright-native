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
    /// Official <c>library/shared-worker.spec.ts</c> parity.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibrarySharedWorkerParityTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("shared-worker.spec.ts", "should survive shared worker restart")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSurviveSharedWorkerRestart()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            string url = TestConstants.ServerUrl + "/shared-worker/shared-worker.html";

            IPage page1 = await context.NewPageAsync().ConfigureAwait(false);
            await page1.GoToAsync(url).ConfigureAwait(false);
            Assert.That(
                await page1.EvaluateAsync<string>("window.sharedWorkerResponsePromise").ConfigureAwait(false),
                Is.EqualTo("echo:hello"));
            await page1.CloseAsync().ConfigureAwait(false);

            IPage page2 = await context.NewPageAsync().ConfigureAwait(false);
            await page2.GoToAsync(url).ConfigureAwait(false);
            Assert.That(
                await page2.EvaluateAsync<string>("window.sharedWorkerResponsePromise").ConfigureAwait(false),
                Is.EqualTo("echo:hello"));
            await page2.CloseAsync().ConfigureAwait(false);
        }
    }
}
