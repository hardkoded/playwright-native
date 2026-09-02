/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
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
