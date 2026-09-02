/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.IO.Pipelines;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page-network-sizes.spec.ts</c> parity for
    /// <see cref="IRequest.SizesAsync"/> / <see cref="IRequest.GetSizesAsync"/>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android):
    /// file-level <c>it.skip(isElectron &amp;&amp; browserMajorVersion &lt; 99)</c>
    /// (<c>This needs Chromium &gt;= 99</c>).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageNetworkSizesParityTests : PageTestEx
    {
        private static readonly byte[] SimpleZipBytes = CreateSimpleZipBytes();

        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19761;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    string origin = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    Prefix = origin;
                    EmptyPage = origin + "/empty.html";
                    return;
                }
                catch (Exception)
                {
                }
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

        [TearDown]
        public void ResetServerRoutes()
        {
            Server?.Reset();
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            Server.Reset();
        }

        private static byte[] CreateSimpleZipBytes()
        {
            byte[] bytes = new byte[5100];
            byte[] line = Encoding.UTF8.GetBytes("{\"foo\": \"bar\"}\n");
            int offset = 0;
            while (offset < bytes.Length)
            {
                int copy = Math.Min(line.Length, bytes.Length - offset);
                Buffer.BlockCopy(line, 0, bytes, offset, copy);
                offset += copy;
            }

            return bytes;
        }

        private static byte[] GzipBytes(byte[] content)
        {
            using MemoryStream stream = new MemoryStream();
            using (GZipStream compressionStream = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true))
            {
                compressionStream.Write(content, 0, content.Length);
            }

            return stream.ToArray();
        }

        private static async Task WriteSimpleZipAsync(HttpContext http)
        {
            http.Response.ContentType = "application/json";
            http.Response.ContentLength = SimpleZipBytes.Length;
            await http.Response.Body.WriteAsync(SimpleZipBytes.AsMemory()).ConfigureAwait(false);
        }

        [PlaywrightTest("page-network-sizes.spec.ts", "should set bodySize and headersSize")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSetBodySizeAndHeadersSize()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IRequest> requestTask = page.WaitForEventAsync(PageEvent.Request);
            Task evalTask = page.EvaluateAsync("(() => fetch('./get', { method: 'POST', body: '12345' }).then(r => r.text()))()");
            await Task.WhenAll(requestTask, evalTask).ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);

            RequestSizesResult sizes = await request.SizesAsync().ConfigureAwait(false);
            Assert.That(sizes.RequestBodySize, Is.EqualTo(5));
            Assert.That(sizes.RequestHeadersSize, Is.GreaterThanOrEqualTo(250));
        }

        [PlaywrightTest("page-network-sizes.spec.ts", "should set bodySize to 0 if there was no body")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSetBodySizeTo0IfThereWasNoBody()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IRequest> requestTask = page.WaitForEventAsync(PageEvent.Request);
            Task evalTask = page.EvaluateAsync("(() => fetch('./get').then(r => r.text()))()");
            await Task.WhenAll(requestTask, evalTask).ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);

            RequestSizesResult sizes = await request.SizesAsync().ConfigureAwait(false);
            Assert.That(sizes.RequestBodySize, Is.EqualTo(0));
            Assert.That(sizes.RequestHeadersSize, Is.GreaterThanOrEqualTo(190));
        }

        [PlaywrightTest("page-network-sizes.spec.ts", "should set bodySize, headersSize, and transferSize")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSetBodySizeHeadersSizeAndTransferSize()
        {
            EnsureServer();
            Server.SetRoute("/get", http =>
            {
                http.Response.ContentType = "text/plain; charset=utf-8";
                http.Response.ContentLength = 6;
                return http.Response.WriteAsync("abc134");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IResponse> responseTask = page.WaitForEventAsync(PageEvent.Response);
            Task evalTask = page.EvaluateAsync("(() => fetch('./get').then(r => r.text()))()");
            Task serverTask = Server.WaitForRequest("/get");
            await Task.WhenAll(responseTask, evalTask, serverTask).ConfigureAwait(false);
            IResponse response = await responseTask.ConfigureAwait(false);

            RequestSizesResult sizes = await response.Request.SizesAsync().ConfigureAwait(false);
            Assert.That(sizes.ResponseBodySize, Is.EqualTo(6));
            Assert.That(sizes.ResponseHeadersSize, Is.GreaterThanOrEqualTo(100));
        }

        [PlaywrightTest("page-network-sizes.spec.ts", "should set bodySize to 0 when there was no response body")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSetBodySizeTo0WhenThereWasNoResponseBody()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            RequestSizesResult sizes = await response.Request.SizesAsync().ConfigureAwait(false);
            Assert.That(sizes.ResponseBodySize, Is.EqualTo(0));
            Assert.That(sizes.ResponseHeadersSize, Is.GreaterThanOrEqualTo(150));
        }

        [PlaywrightTest("page-network-sizes.spec.ts", "should have the correct responseBodySize")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHaveTheCorrectResponseBodySize()
        {
            EnsureServer();
            Server.SetRoute("/simplezip.json", WriteSimpleZipAsync);

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync(Prefix + "/simplezip.json").ConfigureAwait(false);
            RequestSizesResult sizes = await response.Request.SizesAsync().ConfigureAwait(false);
            Assert.That(sizes.ResponseBodySize, Is.EqualTo(SimpleZipBytes.Length));
        }

        [PlaywrightTest("page-network-sizes.spec.ts", "should have the correct responseBodySize for chunked request")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHaveTheCorrectResponseBodySizeForChunkedRequest()
        {
            EnsureServer();
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("upstream test.fixme(firefox)");
            }

            if (TestConstants.IsWebKit && !TestConstants.IsMacOSX)
            {
                Assert.Ignore("upstream test.fixme(webkit && platform !== darwin)");
            }

            const int amountOfChunks = 10;
            int chunkSize = (int)Math.Ceiling(SimpleZipBytes.Length / (double)amountOfChunks);
            Server.SetRoute("/chunked-simplezip.json", async http =>
            {
                http.Response.StatusCode = 200;
                http.Response.ContentType = "text/html; charset=utf-8";
                IHttpResponseBodyFeature bodyFeature = http.Features.Get<IHttpResponseBodyFeature>();
                bodyFeature?.DisableBuffering();
                for (int start = 0; start < SimpleZipBytes.Length; start += chunkSize)
                {
                    int end = Math.Min(start + chunkSize, SimpleZipBytes.Length);
                    await http.Response.BodyWriter.WriteAsync(SimpleZipBytes.AsMemory(start, end - start)).ConfigureAwait(false);
                    FlushResult flush = await http.Response.BodyWriter.FlushAsync().ConfigureAwait(false);
                    if (flush.IsCompleted)
                    {
                        return;
                    }
                }

                await http.Response.CompleteAsync().ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync(Prefix + "/chunked-simplezip.json").ConfigureAwait(false);
            RequestSizesResult sizes = await response.Request.SizesAsync().ConfigureAwait(false);

            // The actual file size is 5100 bytes. The extra 75 bytes are coming from the chunked encoding headers and end bytes.
            if (TestConstants.IsWebKit)
            {
                // WebKit on macOS reports 5173 with the legacy CFNetwork loader (builds <= 2346) and the
                // correct 5175 with NWLoader. TODO: expect 5175 once the NWLoader-based build ships.
                Assert.That(new[] { 5173, 5175 }, Does.Contain(sizes.ResponseBodySize));
            }
            else
            {
                Assert.That(sizes.ResponseBodySize, Is.EqualTo(5175));
            }
        }

        [PlaywrightTest("page-network-sizes.spec.ts", "should have the correct responseBodySize with gzip compression")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHaveTheCorrectResponseBodySizeWithGzipCompression()
        {
            EnsureServer();
            // SimpleCompressionMiddleware only wraps static files (SetRoute
            // returns before next()). Serve the gzip payload from the route.
            byte[] gzipped = GzipBytes(SimpleZipBytes);
            Server.SetRoute("/simplezip.json", async http =>
            {
                http.Response.Headers["Content-Encoding"] = "gzip";
                http.Response.ContentType = "application/json";
                http.Response.ContentLength = gzipped.Length;
                await http.Response.Body.WriteAsync(gzipped.AsMemory()).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IResponse> responseTask = page.WaitForEventAsync(PageEvent.Response);
            Task evalTask = page.EvaluateAsync("(() => fetch('./simplezip.json').then(r => r.text()))()");
            await Task.WhenAll(responseTask, evalTask).ConfigureAwait(false);
            IResponse response = await responseTask.ConfigureAwait(false);

            RequestSizesResult sizes = await response.Request.SizesAsync().ConfigureAwait(false);
            Assert.That(sizes.ResponseBodySize, Is.EqualTo(gzipped.Length));
        }

        [PlaywrightTest("page-network-sizes.spec.ts", "should handle redirects")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHandleRedirects()
        {
            EnsureServer();
            Server.SetRedirect("/foo", "/bar");
            Server.SetRoute("/bar", http =>
            {
                http.Response.ContentLength = 3;
                return http.Response.WriteAsync("bar");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IResponse> responseTask = page.WaitForEventAsync(PageEvent.Response);
            Task evalTask = page.EvaluateAsync("(() => fetch('/foo', { method: 'POST', body: '12345' }).then(r => r.text()))()");
            await Task.WhenAll(responseTask, evalTask).ConfigureAwait(false);
            IResponse response = await responseTask.ConfigureAwait(false);

            Assert.That((await response.Request.SizesAsync().ConfigureAwait(false)).RequestBodySize, Is.EqualTo(5));
            IRequest newRequest = response.Request.RedirectedTo;
            Assert.That((await newRequest.SizesAsync().ConfigureAwait(false)).ResponseBodySize, Is.EqualTo(3));
        }

        [PlaywrightTest("page-network-sizes.spec.ts", "should throw for failed requests")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowForFailedRequests()
        {
            EnsureServer();
            Server.SetRoute("/one-style.css", async http =>
            {
                http.Response.ContentType = "text/css";
                http.Response.ContentLength = 64;
                await http.Response.StartAsync().ConfigureAwait(false);
                http.Abort();
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IRequest> requestTask = page.WaitForEventAsync(PageEvent.RequestFailed);
            Task navTask = page.GoToAsync(Prefix + "/one-style.html");
            await Task.WhenAll(requestTask, navTask).ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(() => request.SizesAsync());
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Unable to fetch sizes for failed request"));
        }

        [PlaywrightTest("page-network-sizes.spec.ts", "should work with 200 status code")]
        [Test]
        [Timeout(30_000)]
        public Task ShouldWorkWith200StatusCode() => ShouldWorkWithStatusCodeAsync(200);

        [PlaywrightTest("page-network-sizes.spec.ts", "should work with 401 status code")]
        [Test]
        [Timeout(30_000)]
        public Task ShouldWorkWith401StatusCode() => ShouldWorkWithStatusCodeAsync(401);

        [PlaywrightTest("page-network-sizes.spec.ts", "should work with 404 status code")]
        [Test]
        [Timeout(30_000)]
        public Task ShouldWorkWith404StatusCode() => ShouldWorkWithStatusCodeAsync(404);

        [PlaywrightTest("page-network-sizes.spec.ts", "should work with 500 status code")]
        [Test]
        [Timeout(30_000)]
        public Task ShouldWorkWith500StatusCode() => ShouldWorkWithStatusCodeAsync(500);

        [PlaywrightTest("page-network-sizes.spec.ts", "should have correct responseBodySize for 404 with content")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHaveCorrectResponseBodySizeFor404WithContent()
        {
            EnsureServer();
            if (TestConstants.IsChromium)
            {
                Assert.Ignore("upstream test.fixme(chromium)");
            }

            Server.SetRoute("/broken-image.png", async http =>
            {
                http.Response.StatusCode = 404;
                await http.Response.WriteAsync(" this should have a non-negative size ").ConfigureAwait(false);
            });
            Server.SetRoute("/page-with-404-image.html", http =>
                http.Response.WriteAsync("<!DOCTYPE html><html><head><title>Page with Broken Image</title></head><body><img src=\"broken-image.png\" /></body></html>"));

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> requestTask = page.WaitForRequestAsync(new Regex("broken-image\\.png$"));
            Task navTask = page.GoToAsync(Prefix + "/page-with-404-image.html");
            await Task.WhenAll(requestTask, navTask).ConfigureAwait(false);
            IRequest req = await requestTask.ConfigureAwait(false);

            RequestSizesResult sizes = await req.SizesAsync().ConfigureAwait(false);
            Assert.That(sizes.ResponseBodySize, Is.GreaterThanOrEqualTo(0));
        }

        [PlaywrightTest("page-network-sizes.spec.ts", "should return sizes without hanging")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnSizesWithoutHanging()
        {
            EnsureServer();
            if (TestConstants.IsChromium)
            {
                Assert.Ignore("upstream test.fixme(chromium)");
            }

            Server.SetRoute("/has-abandoned-fetch", http =>
                http.Response.WriteAsync("<!DOCTYPE html><html><head><script>fetch(\"./404\");</script></head><body>t</body></html>"));

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> requestTask = page.WaitForRequestAsync(new Regex("404$"));
            Task navTask = page.GoToAsync(Prefix + "/has-abandoned-fetch");
            await Task.WhenAll(requestTask, navTask).ConfigureAwait(false);
            IRequest req = await requestTask.ConfigureAwait(false);

            await req.SizesAsync().ConfigureAwait(false);
        }

        private async Task ShouldWorkWithStatusCodeAsync(int statusCode)
        {
            EnsureServer();
            Server.SetRoute("/foo", async http =>
            {
                http.Response.StatusCode = statusCode;
                http.Response.ContentType = "text/plain; charset=utf-8";
                http.Response.ContentLength = 3;
                await http.Response.WriteAsync("bar").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IResponse> responseTask = page.WaitForEventAsync(PageEvent.Response);
            Task evalTask = page.EvaluateAsync("(() => fetch('/foo', { method: 'POST', body: '12345' }).then(r => r.text()))()");
            await Task.WhenAll(responseTask, evalTask).ConfigureAwait(false);
            IResponse response = await responseTask.ConfigureAwait(false);

            Assert.That(response.Status, Is.EqualTo(statusCode));
            RequestSizesResult sizes = await response.Request.SizesAsync().ConfigureAwait(false);
            Assert.That(sizes.RequestBodySize, Is.EqualTo(5));
            Assert.That(sizes.ResponseBodySize, Is.EqualTo(3));
        }
    }
}
