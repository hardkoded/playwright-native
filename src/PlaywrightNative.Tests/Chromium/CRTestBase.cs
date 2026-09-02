/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
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
