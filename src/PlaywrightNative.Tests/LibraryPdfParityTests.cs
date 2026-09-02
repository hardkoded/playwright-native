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
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/pdf.spec.ts</c> parity. Official skip when
    /// <c>browserName !== 'chromium'</c>. Do not edit leftover
    /// <c>PagePdfTests</c>, <c>PagePathTests</c>, or
    /// <c>CRPdfTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryPdfParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19887;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    Prefix = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    return;
                }
                catch (Exception)
                {
                }
            }

            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                return;
            }

            Assert.Ignore("Test server is unavailable.");
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }
        }

        [SetUp]
        public async Task SetUpAsync()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Printing to pdf is currently only supported in chromium.");
            }

            Server?.Reset();
            await DisposeBrowserAsync().ConfigureAwait(false);
            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            Server?.Reset();
            await DisposeBrowserAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("pdf.spec.ts", "should be able to save file")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToSaveFile()
        {
            await using IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            string outputFile = Path.Combine(Path.GetTempPath(), "pwsharp-wave887-output-" + Guid.NewGuid().ToString("N") + ".pdf");
            try
            {
                await page.PdfAsync(new() { Path = outputFile }).ConfigureAwait(false);
                Assert.That(File.ReadAllBytes(outputFile).Length, Is.GreaterThan(0));
            }
            finally
            {
                if (File.Exists(outputFile))
                {
                    File.Delete(outputFile);
                }
            }
        }

        [PlaywrightTest("pdf.spec.ts", "should be able to generate outline")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToGenerateOutline()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            await using IBrowserContext context = await _browser.NewContextAsync(new BrowserContextOptions
            {
                BaseURL = Prefix,
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("/headings.html").ConfigureAwait(false);
            string outputFileNoOutline = Path.Combine(Path.GetTempPath(), "pwsharp-wave887-no-outline-" + Guid.NewGuid().ToString("N") + ".pdf");
            string outputFileOutline = Path.Combine(Path.GetTempPath(), "pwsharp-wave887-outline-" + Guid.NewGuid().ToString("N") + ".pdf");
            try
            {
                await page.PdfAsync(new() { Path = outputFileNoOutline }).ConfigureAwait(false);
                await page.PdfAsync(new() { Path = outputFileOutline, Tagged = true, Outline = true }).ConfigureAwait(false);
                Assert.That(
                    File.ReadAllBytes(outputFileOutline).Length,
                    Is.GreaterThan(File.ReadAllBytes(outputFileNoOutline).Length));
            }
            finally
            {
                if (File.Exists(outputFileNoOutline))
                {
                    File.Delete(outputFileNoOutline);
                }

                if (File.Exists(outputFileOutline))
                {
                    File.Delete(outputFileOutline);
                }
            }
        }

        private async Task DisposeBrowserAsync()
        {
            if (_browser != null)
            {
                try
                {
                    await _browser.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                _browser = null;
            }
        }
    }
}
