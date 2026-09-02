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
using PlaywrightNative.Chromium;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests.Chromium
{
    /// <summary>
    /// Base class for Chromium protocol tests (<c>CRPage</c> / CDP internals the public
    /// <c>IPage</c> suite cannot see). Provides browser, context, page, and test servers.
    /// </summary>
    [TestFixture]
    public class CRTestBase
    {
        private CRBrowser _browser;

        /// <summary>
        /// Gets the Chromium browser instance shared across tests in this fixture.
        /// </summary>
        private protected CRBrowser Browser => _browser;

        /// <summary>
        /// Gets the browser context for the current test. Created fresh per test.
        /// </summary>
        private protected CRBrowserContext Context { get; private set; }

        /// <summary>
        /// Gets the page for the current test. Created fresh per test.
        /// </summary>
        private protected CRPage Page { get; private set; }

        /// <summary>
        /// Gets the test HTTP server (port 8081).
        /// </summary>
        private protected static SimpleServer Server => TestServerSetup.Server;

        /// <summary>
        /// Gets the test HTTPS server (port 8082).
        /// </summary>
        private protected static SimpleServer HttpsServer => TestServerSetup.HttpsServer;

        [OneTimeSetUp]
        public async Task CROneTimeSetUp()
        {
            await BrowserExecutable.EnsureAsync("chromium").ConfigureAwait(false);
            string executablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            if (executablePath == null)
            {
                Assert.Ignore("Chromium executable not found. Skipping direct connection tests.");
            }

            _browser = await ChromiumBrowserType.LaunchAsync(executablePath, headless: true).ConfigureAwait(false);
        }

        [OneTimeTearDown]
        public async Task CROneTimeTearDown()
        {
            if (_browser != null)
            {
                await _browser.DisposeAsync().ConfigureAwait(false);
            }
        }

        [SetUp]
        public async Task CRSetUp()
        {
            Context = await Browser.NewContextAsync().ConfigureAwait(false);
            Page = await Context.NewPageAsync().ConfigureAwait(false);
            await Page.InitializedTask.ConfigureAwait(false);
            Server?.Reset();
            HttpsServer?.Reset();
        }

        [TearDown]
        public async Task CRTearDown()
        {
            if (Context != null)
            {
                try
                {
                    await Context.DisposeAsync().ConfigureAwait(false);
                }
                catch (TargetClosedException)
                {
                    // Browser session may already be closed.
                }
                catch (PlaywrightNativeException)
                {
                    // Connection may already be closed.
                }
            }
        }
    }
}
