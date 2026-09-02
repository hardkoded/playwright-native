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
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-fetch-algorithms.spec.ts</c> parity.
    /// Do not edit leftover <c>LibraryBrowserContextFetchParityTests</c>
    /// or leftover <c>ApiRequestTests</c>.
    /// Skip Node-only <c>library/browsercontext-reuse.spec.ts</c> and
    /// <c>library/browsercontext-fetch-happy-eyeballs.spec.ts</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextFetchAlgorithmsParityTests : BrowserTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19960;
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

            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
            }
        }

        [SetUp]
        public async Task SetUpAsync()
        {
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }

            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            Server?.Reset();
            TestServerSetup.Server?.Reset();
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }
        }

        [PlaywrightTest("browsercontext-fetch-algorithms.spec.ts", "should support decompression")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task GzipShouldSupportDecompression()
            => ShouldSupportDecompressionAsync("gzip");

        [PlaywrightTest("browsercontext-fetch-algorithms.spec.ts", "should support decompression")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task DeflateShouldSupportDecompression()
            => ShouldSupportDecompressionAsync("deflate");

        [PlaywrightTest("browsercontext-fetch-algorithms.spec.ts", "should support decompression")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task BrShouldSupportDecompression()
            => ShouldSupportDecompressionAsync("br");

        [PlaywrightTest("browsercontext-fetch-algorithms.spec.ts", "should not fail if response content-length header is missing (gzip)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldNotFailIfResponseContentLengthHeaderIsMissingGzip()
            => ShouldNotFailIfContentLengthMissingAsync("gzip");

        [PlaywrightTest("browsercontext-fetch-algorithms.spec.ts", "should not fail if response content-length header is missing (deflate)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldNotFailIfResponseContentLengthHeaderIsMissingDeflate()
            => ShouldNotFailIfContentLengthMissingAsync("deflate");

        [PlaywrightTest("browsercontext-fetch-algorithms.spec.ts", "should not fail if response content-length header is missing (br)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldNotFailIfResponseContentLengthHeaderIsMissingBr()
            => ShouldNotFailIfContentLengthMissingAsync("br");

        [PlaywrightTest("browsercontext-fetch-algorithms.spec.ts", "should not fail with chunked responses (without Content-Length header)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task GzipShouldNotFailWithChunkedResponses()
            => ShouldNotFailWithChunkedResponsesAsync("gzip");

        [PlaywrightTest("browsercontext-fetch-algorithms.spec.ts", "should not fail with chunked responses (without Content-Length header)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task DeflateShouldNotFailWithChunkedResponses()
            => ShouldNotFailWithChunkedResponsesAsync("deflate");

        [PlaywrightTest("browsercontext-fetch-algorithms.spec.ts", "should not fail with chunked responses (without Content-Length header)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task BrShouldNotFailWithChunkedResponses()
            => ShouldNotFailWithChunkedResponsesAsync("br");

        [PlaywrightTest("browsercontext-fetch-algorithms.spec.ts", "should not fail with an empty response without content-length header (Z_BUF_ERROR)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task GzipShouldNotFailWithEmptyResponseWithoutContentLength()
            => ShouldNotFailWithEmptyResponseAsync("gzip", omitContentLength: true);

        [PlaywrightTest("browsercontext-fetch-algorithms.spec.ts", "should not fail with an empty response without content-length header (Z_BUF_ERROR)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task DeflateShouldNotFailWithEmptyResponseWithoutContentLength()
            => ShouldNotFailWithEmptyResponseAsync("deflate", omitContentLength: true);

        [PlaywrightTest("browsercontext-fetch-algorithms.spec.ts", "should not fail with an empty response without content-length header (Z_BUF_ERROR)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task BrShouldNotFailWithEmptyResponseWithoutContentLength()
            => ShouldNotFailWithEmptyResponseAsync("br", omitContentLength: true);

        [PlaywrightTest("browsercontext-fetch-algorithms.spec.ts", "should not fail with an empty response with content-length header (Z_BUF_ERROR)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task GzipShouldNotFailWithEmptyResponseWithContentLength()
            => ShouldNotFailWithEmptyResponseAsync("gzip", omitContentLength: false);

        [PlaywrightTest("browsercontext-fetch-algorithms.spec.ts", "should not fail with an empty response with content-length header (Z_BUF_ERROR)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task DeflateShouldNotFailWithEmptyResponseWithContentLength()
            => ShouldNotFailWithEmptyResponseAsync("deflate", omitContentLength: false);

        [PlaywrightTest("browsercontext-fetch-algorithms.spec.ts", "should not fail with an empty response with content-length header (Z_BUF_ERROR)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task BrShouldNotFailWithEmptyResponseWithContentLength()
            => ShouldNotFailWithEmptyResponseAsync("br", omitContentLength: false);

        private async Task ShouldSupportDecompressionAsync(string type)
        {
            EnsureServer();
            byte[] zipped = Compress(type, Encoding.UTF8.GetBytes("str"));
            Server.SetRoute("/compressed", async http =>
            {
                http.Response.Headers["Content-Encoding"] = type;
                await http.Response.Body.WriteAsync(zipped).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(Prefix + "/compressed").ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("str"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private async Task ShouldNotFailIfContentLengthMissingAsync(string type)
        {
            EnsureServer();
            byte[] zipped = Compress(type, Encoding.UTF8.GetBytes("str"));
            Server.SetRoute("/compressed", async http =>
            {
                http.Response.Headers["Content-Encoding"] = type;
                http.Response.Headers.Remove("Content-Length");
                http.Response.ContentLength = null;
                await http.Response.Body.WriteAsync(zipped).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(Prefix + "/compressed").ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("str"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private async Task ShouldNotFailWithChunkedResponsesAsync(string type)
        {
            EnsureServer();
            byte[] zipped = Compress(type, Encoding.UTF8.GetBytes("str"));
            Server.SetRoute("/compressed", async http =>
            {
                http.Response.Headers["Content-Encoding"] = type;
                http.Response.ContentLength = null;
                await http.Response.StartAsync().ConfigureAwait(false);
                await http.Response.Body.WriteAsync(zipped).ConfigureAwait(false);
                await http.Response.CompleteAsync().ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(Prefix + "/compressed").ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("str"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private async Task ShouldNotFailWithEmptyResponseAsync(string type, bool omitContentLength)
        {
            EnsureServer();
            Server.SetRoute("/compressed", async http =>
            {
                http.Response.Headers["Content-Encoding"] = type;
                if (omitContentLength)
                {
                    http.Response.Headers.Remove("Content-Length");
                    http.Response.ContentLength = null;
                }

                await http.Response.CompleteAsync().ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(Prefix + "/compressed").ConfigureAwait(false);
            if (omitContentLength)
            {
                Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo(string.Empty));
            }

            await context.CloseAsync().ConfigureAwait(false);
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static byte[] Compress(string type, byte[] payload)
        {
            using MemoryStream stream = new MemoryStream();
            if (string.Equals(type, "gzip", StringComparison.Ordinal))
            {
                using (GZipStream gzip = new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true))
                {
                    gzip.Write(payload, 0, payload.Length);
                }
            }
            else if (string.Equals(type, "deflate", StringComparison.Ordinal))
            {
                using (ZLibStream deflate = new ZLibStream(stream, CompressionLevel.Optimal, leaveOpen: true))
                {
                    deflate.Write(payload, 0, payload.Length);
                }
            }
            else
            {
                using (BrotliStream brotli = new BrotliStream(stream, CompressionLevel.Optimal, leaveOpen: true))
                {
                    brotli.Write(payload, 0, payload.Length);
                }
            }

            return stream.ToArray();
        }

        private static async Task DisposeQuietlyAsync(IAsyncDisposable disposable)
        {
            try
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }
    }
}
