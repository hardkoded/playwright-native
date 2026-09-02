/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NUnit.Framework;
using PlaywrightNative.Helpers;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-network-response.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android, BiDi-only):
    /// Electron-only version skips on "should report all headers" and
    /// "should report multiple set-cookie headers"; Android skips on
    /// "should provide a Response with a file URL",
    /// "should report if request was fromServiceWorker", and
    /// "should return uncompressed text for brotli encoding".
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageNetworkResponseParityTests : PageTestEx
    {
        private const string SimpleJsonBody = "{\"foo\": \"bar\"}\n";
        private static readonly byte[] PngBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        private static readonly byte[] PixelGifBytes = Convert.FromBase64String("R0lGODlhAQABAAAAACw=");

        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            Server.Reset();
        }

        private static void IgnoreWebKitWin32()
        {
            if (TestConstants.IsWebKit && TestConstants.IsWindows)
            {
                Assert.Ignore("libcurl does not support non-set-cookie multivalue headers");
            }
        }

        private static void SetSimpleJsonRoute()
        {
            Server.SetRoute("/simple.json", http =>
            {
                http.Response.ContentType = "application/json";
                return http.Response.WriteAsync(SimpleJsonBody);
            });
        }

        private static void SetGzipJsonRoute()
        {
            byte[] compressed = CompressGzip(Encoding.UTF8.GetBytes(SimpleJsonBody));
            Server.SetRoute("/simple.json", async http =>
            {
                http.Response.ContentType = "application/json";
                http.Response.Headers["Content-Encoding"] = "gzip";
                await http.Response.Body.WriteAsync(compressed).ConfigureAwait(false);
            });
        }

        private static byte[] CompressGzip(byte[] data)
        {
            using MemoryStream output = new MemoryStream();
            using (GZipStream gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                gzip.Write(data, 0, data.Length);
            }

            return output.ToArray();
        }

        private static byte[] CompressBrotli(byte[] data)
        {
            using MemoryStream output = new MemoryStream();
            using (BrotliStream brotli = new BrotliStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                brotli.Write(data, 0, data.Length);
            }

            return output.ToArray();
        }

        private static int[] ToIntList(JsonElement? value)
        {
            if (!value.HasValue || value.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<int>();
            }

            List<int> list = new List<int>();
            foreach (JsonElement item in value.Value.EnumerateArray())
            {
                list.Add(item.GetInt32());
            }

            return list.ToArray();
        }

        private static int GetChromiumMajorVersion()
        {
            string path = Environment.GetEnvironmentVariable("CHROMIUM_PATH");
            if (string.IsNullOrEmpty(path))
            {
                if (File.Exists("/opt/google/chrome/chrome"))
                {
                    path = "/opt/google/chrome/chrome";
                }
                else if (File.Exists("/usr/local/bin/chrome"))
                {
                    path = "/usr/local/bin/chrome";
                }
            }

            if (string.IsNullOrEmpty(path))
            {
                return int.MaxValue;
            }

            try
            {
                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                };
                using System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo);
                if (process == null)
                {
                    return int.MaxValue;
                }

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                Match match = Regex.Match(output, @"(\d+)\.");
                if (match.Success)
                {
                    return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                }
            }
            catch (Exception)
            {
            }

            return int.MaxValue;
        }

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19759;
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

        [PlaywrightTest("page-network-response.spec.ts", "should work")]
        [PlaywrightTest("page-network-response.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", http =>
            {
                http.Response.Headers["foo"] = "bar";
                http.Response.Headers["BaZ"] = "bAz";
                return Task.CompletedTask;
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Dictionary<string, string> headers = await response.AllHeadersAsync().ConfigureAwait(false);
            Assert.That(headers["foo"], Is.EqualTo("bar"));
            Assert.That(headers["baz"], Is.EqualTo("bAz"));
            Assert.That(headers.ContainsKey("BaZ"), Is.False);
        }

        [PlaywrightTest("page-network-response.spec.ts", "should return multiple header value")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnMultipleHeaderValue()
        {
            IgnoreWebKitWin32();
            EnsureServer();
            Server.SetRoute("/headers", async http =>
            {
                http.Response.Headers.Append("Name-A", "v1");
                http.Response.Headers.Append("Name-a", "v2");
                http.Response.Headers.Append("name-A", "v3");
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(Prefix + "/headers").ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(HeaderMap.All(response.Headers)["name-a"], Is.EqualTo("v1, v2, v3"));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should return text")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnText()
        {
            EnsureServer();
            SetSimpleJsonRoute();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(Prefix + "/simple.json").ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo(SimpleJsonBody));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should return uncompressed text")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnUncompressedText()
        {
            EnsureServer();
            SetGzipJsonRoute();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(Prefix + "/simple.json").ConfigureAwait(false);
            Assert.That(HeaderMap.All(response.Headers)["content-encoding"], Is.EqualTo("gzip"));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo(SimpleJsonBody));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should return uncompressed text for brotli encoding")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnUncompressedTextForBrotliEncoding()
        {
            if (OperatingSystem.IsAndroid())
            {
                Assert.Ignore("net::ERR_CONTENT_DECODING_FAILED");
            }

            EnsureServer();
            byte[] compressed = CompressBrotli(Encoding.UTF8.GetBytes(SimpleJsonBody));
            Server.SetRoute("/brotli.json", async http =>
            {
                http.Response.StatusCode = 200;
                http.Response.Headers["Content-Type"] = "application/json";
                http.Response.Headers["Content-Encoding"] = "br";
                await http.Response.Body.WriteAsync(compressed).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(Prefix + "/brotli.json").ConfigureAwait(false);
            Assert.That(HeaderMap.All(response.Headers)["content-encoding"], Is.EqualTo("br"));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo(SimpleJsonBody));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should throw when requesting body of redirected response")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowWhenRequestingBodyOfRedirectedResponse()
        {
            EnsureServer();
            Server.SetRedirect("/foo.html", "/empty.html");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(Prefix + "/foo.html").ConfigureAwait(false);
            IRequest redirectedFrom = response.Request.RedirectedFrom;
            Assert.That(redirectedFrom, Is.Not.Null);
            IResponse redirected = await redirectedFrom.ResponseAsync().ConfigureAwait(false);
            Assert.That(redirected.Status, Is.EqualTo(302));
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(() => redirected.TextAsync());
            Assert.That(error.Message, Does.Contain("Response body is unavailable for redirect responses"));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should wait until response completes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitUntilResponseCompletes()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            TaskCompletionSource<bool> serverResponseCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            HttpResponse serverResponse = null;
            Server.SetRoute("/get", http =>
            {
                http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
                serverResponse = http.Response;
                http.Response.Headers["Content-Type"] = "text/plain; charset=utf-8";
                _ = http.Response.WriteAsync("hello ");
                return serverResponseCompletion.Task;
            });

            bool requestFinished = false;
            page.RequestFinished += (_, request) =>
                requestFinished = requestFinished || request.Url.Contains("/get", StringComparison.Ordinal);

            Task<IResponse> responseTask = page.WaitForResponseAsync(r => r.Url.Contains("/get", StringComparison.Ordinal));
            Task waitForServer = Server.WaitForRequest("/get");
            await page.EvaluateAsync("void fetch('./get', { method: 'GET' })").ConfigureAwait(false);
            await waitForServer.ConfigureAwait(false);
            IResponse pageResponse = await responseTask.ConfigureAwait(false);

            Assert.That(serverResponse, Is.Not.Null);
            Assert.That(pageResponse, Is.Not.Null);
            Assert.That(pageResponse.Status, Is.EqualTo(200));
            Assert.That(requestFinished, Is.False);

            Task<string> responseText = pageResponse.TextAsync();
            await serverResponse.WriteAsync("wor").ConfigureAwait(false);
            await serverResponse.Body.FlushAsync().ConfigureAwait(false);
            await serverResponse.WriteAsync("ld!").ConfigureAwait(false);
            await serverResponse.Body.FlushAsync().ConfigureAwait(false);
            serverResponseCompletion.TrySetResult(true);
            Assert.That(await responseText.ConfigureAwait(false), Is.EqualTo("hello world!"));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should return json")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnJson()
        {
            EnsureServer();
            SetSimpleJsonRoute();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(Prefix + "/simple.json").ConfigureAwait(false);
            using JsonDocument document = await response.GetJsonAsync().ConfigureAwait(false);
            Assert.That(document.RootElement.GetProperty("foo").GetString(), Is.EqualTo("bar"));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should return body")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnBody()
        {
            EnsureServer();
            Server.SetRoute("/pptr.png", async http =>
            {
                http.Response.ContentType = "image/png";
                await http.Response.Body.WriteAsync(PngBytes).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(Prefix + "/pptr.png").ConfigureAwait(false);
            Assert.That(await response.BodyAsync().ConfigureAwait(false), Is.EqualTo(PngBytes));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should return body with compression")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnBodyWithCompression()
        {
            EnsureServer();
            byte[] compressed = CompressGzip(PngBytes);
            Server.SetRoute("/pptr.png", async http =>
            {
                http.Response.ContentType = "image/png";
                http.Response.Headers["Content-Encoding"] = "gzip";
                await http.Response.Body.WriteAsync(compressed).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(Prefix + "/pptr.png").ConfigureAwait(false);
            Assert.That(await response.BodyAsync().ConfigureAwait(false), Is.EqualTo(PngBytes));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should return non-utf8 body even when content-type says utf8")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnNonUtf8BodyEvenWhenContentTypeSaysUtf8()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("webkit encodes the response body");
            }

            if (TestConstants.IsChromium && GetChromiumMajorVersion() < 151)
            {
                Assert.Ignore("older chromium re-encodes the non-utf8 response body and lacks Response.bytes()");
            }

            EnsureServer();
            byte[] buffer = new byte[] { 0x80, 0x81, 0x82, 0xFF, 0xFE, 0x00, 0x01, 0x02 };
            Server.SetRoute("/binary-as-text", async http =>
            {
                http.Response.StatusCode = 200;
                http.Response.Headers["Content-Type"] = "text/plain;charset=UTF-8";
                http.Response.Headers["Content-Length"] = buffer.Length.ToString(CultureInfo.InvariantCulture);
                await http.Response.Body.WriteAsync(buffer).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string url = Prefix + "/binary-as-text";
            Task<IResponse> responseTask = page.WaitForResponseAsync(url);
            JsonElement? bytesReceived = await page.EvaluateAsync(
                "url => fetch(url).then(r => r.bytes()).then(b => Array.from(b))",
                url).ConfigureAwait(false);
            IResponse response = await responseTask.ConfigureAwait(false);
            Assert.That(await response.BodyAsync().ConfigureAwait(false), Is.EqualTo(buffer));
            Assert.That(ToIntList(bytesReceived), Is.EqualTo(new[] { 128, 129, 130, 255, 254, 0, 1, 2 }));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should return status text")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnStatusText()
        {
            EnsureServer();
            Server.SetRoute("/cool", http =>
            {
                http.Response.StatusCode = 200;
                IHttpResponseFeature feature = http.Features.Get<IHttpResponseFeature>();
                if (feature != null)
                {
                    feature.ReasonPhrase = "cool!";
                }

                return Task.CompletedTask;
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(Prefix + "/cool").ConfigureAwait(false);
            Assert.That(response.StatusText, Is.EqualTo("cool!"));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should report all headers")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportAllHeaders()
        {
            IgnoreWebKitWin32();
            EnsureServer();
            Dictionary<string, List<string>> expectedHeaders = new Dictionary<string, List<string>>(StringComparer.Ordinal)
            {
                ["header-a"] = new List<string> { "value-a", "value-a-1", "value-a-2" },
                ["header-b"] = new List<string> { "value-b" },
            };
            Server.SetRoute("/headers", async http =>
            {
                http.Response.Headers.Append("header-a", "value-a");
                http.Response.Headers.Append("header-a", "value-a-1");
                http.Response.Headers.Append("header-a", "value-a-2");
                http.Response.Headers.Append("header-b", "value-b");
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IResponse> responseTask = page.WaitForResponseAsync(r => r.Url.Contains("/headers", StringComparison.Ordinal));
            await page.EvaluateAsync("void fetch('/headers')").ConfigureAwait(false);
            IResponse response = await responseTask.ConfigureAwait(false);
            IReadOnlyList<Header> headers = await response.HeadersArrayAsync().ConfigureAwait(false);
            Dictionary<string, List<string>> actualHeaders = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (Header entry in headers)
            {
                if (!actualHeaders.TryGetValue(entry.Name, out List<string> values))
                {
                    values = new List<string>();
                    actualHeaders[entry.Name] = values;
                }

                values.Add(entry.Value);
            }

            actualHeaders.Remove("Keep-Alive");
            actualHeaders.Remove("keep-alive");
            actualHeaders.Remove("Connection");
            actualHeaders.Remove("connection");
            actualHeaders.Remove("Date");
            actualHeaders.Remove("date");
            actualHeaders.Remove("Transfer-Encoding");
            actualHeaders.Remove("transfer-encoding");
            actualHeaders.Remove("Server");
            actualHeaders.Remove("server");
            actualHeaders.Remove("Content-Length");
            actualHeaders.Remove("content-length");
            Assert.That(actualHeaders, Is.EqualTo(expectedHeaders));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should report multiple set-cookie headers")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportMultipleSetCookieHeaders()
        {
            EnsureServer();
            Server.SetRoute("/headers", async http =>
            {
                http.Response.Headers.Append("Set-Cookie", "a=b");
                http.Response.Headers.Append("Set-Cookie", "c=d");
                await http.Response.WriteAsync("\r\n").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IResponse> responseTask = page.WaitForResponseAsync(r => r.Url.Contains("/headers", StringComparison.Ordinal));
            await page.EvaluateAsync("void fetch('/headers')").ConfigureAwait(false);
            IResponse response = await responseTask.ConfigureAwait(false);
            IReadOnlyList<Header> headers = await response.HeadersArrayAsync().ConfigureAwait(false);
            string[] cookies = headers
                .Where(entry => string.Equals(entry.Name, "set-cookie", StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Value)
                .ToArray();
            Assert.That(cookies, Is.EqualTo(new[] { "a=b", "c=d" }));
            Assert.That(await response.HeaderValueAsync("not-there").ConfigureAwait(false), Is.Null);
            Assert.That(await response.HeaderValueAsync("set-cookie").ConfigureAwait(false), Is.EqualTo("a=b\nc=d"));
            Assert.That(await response.HeaderValuesAsync("set-cookie").ConfigureAwait(false), Is.EqualTo(new[] { "a=b", "c=d" }));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should behave the same way for headers and allHeaders")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldBehaveTheSameWayForHeadersAndAllHeaders()
        {
            IgnoreWebKitWin32();
            EnsureServer();
            Server.SetRoute("/headers", async http =>
            {
                if (!TestConstants.IsChromium)
                {
                    http.Response.Headers.Append("Set-Cookie", "a=b");
                    http.Response.Headers.Append("Set-Cookie", "c=d");
                }

                http.Response.Headers.Append("header-a", "a=b");
                http.Response.Headers.Append("header-a", "c=d");
                http.Response.Headers.Append("Name-A", "v1");
                http.Response.Headers.Append("name-b", "v4");
                http.Response.Headers.Append("Name-a", "v2");
                http.Response.Headers.Append("name-A", "v3");
                await http.Response.WriteAsync("\r\n").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IResponse> responseTask = page.WaitForResponseAsync(r => r.Url.Contains("/headers", StringComparison.Ordinal));
            await page.EvaluateAsync("void fetch('/headers')").ConfigureAwait(false);
            IResponse response = await responseTask.ConfigureAwait(false);
            Dictionary<string, string> allHeaders = await response.AllHeadersAsync().ConfigureAwait(false);
            Assert.That(HeaderMap.All(response.Headers), Is.EqualTo(allHeaders));
            Assert.That(allHeaders["header-a"], Is.EqualTo("a=b, c=d"));
            Assert.That(allHeaders["name-a"], Is.EqualTo("v1, v2, v3"));
            Assert.That(allHeaders["name-b"], Is.EqualTo("v4"));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should provide a Response with a file URL")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldProvideAResponseWithAFileUrl()
        {
            if (OperatingSystem.IsAndroid())
            {
                Assert.Ignore("No files on Android");
            }

            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("Firefox does return null for file:// URLs");
            }

            if (string.Equals(Environment.GetEnvironmentVariable("CHANNEL"), "webkit-wsl", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore("webkit-wsl");
            }

            string assetPath = TestUtils.GetWebServerFile("frames/two-frames.html");
            string fileurl = new Uri(assetPath).AbsoluteUri;
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(fileurl).ConfigureAwait(false);
            if (TestConstants.IsChromium || (TestConstants.IsWebKit && TestConstants.IsWindows))
            {
                Assert.That(response.Status, Is.EqualTo(200));
            }
            else
            {
                Assert.That(response.Status, Is.EqualTo(0));
            }

            Assert.That(response.Ok, Is.True);
        }

        [PlaywrightTest("page-network-response.spec.ts", "should return set-cookie header after route.fulfill")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnSetCookieHeaderAfterRouteFulfill()
        {
            if (TestConstants.IsWebKit || TestConstants.IsChromium)
            {
                Assert.Ignore("https://github.com/microsoft/playwright/issues/11035");
            }

            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.RouteAsync("**/*", route => route.FulfillAsync(200, headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["set-cookie"] = "a=b",
            },
                contentType: "text/plain",
                body: string.Empty)).ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Dictionary<string, string> headers = await response.AllHeadersAsync().ConfigureAwait(false);
            Assert.That(headers["set-cookie"], Is.EqualTo("a=b"));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should return headers after route.fulfill")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnHeadersAfterRouteFulfill()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.RouteAsync("**/*", route => route.FulfillAsync(200, headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["foo"] = "bar",
                ["content-language"] = "en",
            },
                contentType: "text/plain",
                body: "done")).ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(
                await response.AllHeadersAsync().ConfigureAwait(false),
                Is.EqualTo(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["foo"] = "bar",
                    ["content-type"] = "text/plain",
                    ["content-length"] = "4",
                    ["content-language"] = "en",
                }));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should report if request was fromServiceWorker")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportIfRequestWasFromServiceWorker()
        {
            if (OperatingSystem.IsAndroid())
            {
                Assert.Ignore("isAndroid || isElectron");
            }

            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse first = await page.GoToAsync(Prefix + "/serviceworkers/fetch/sw.html").ConfigureAwait(false);
            Assert.That(first.FromServiceWorker, Is.False);
            await page.EvaluateAsync("window['activationPromise']").ConfigureAwait(false);
            Task<IResponse> responseTask = page.WaitForResponseAsync(new Regex("example\\.txt"));
            await page.EvaluateAsync("void fetch('/example.txt')").ConfigureAwait(false);
            IResponse res = await responseTask.ConfigureAwait(false);
            Assert.That(res.FromServiceWorker, Is.True);
        }

        [PlaywrightTest("page-network-response.spec.ts", "should return body for prefetch script")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnBodyForPrefetchScript()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("No prefetch in WebKit: https://caniuse.com/link-rel-prefetch");
            }

            if (TestConstants.IsChromium && GetChromiumMajorVersion() < 138)
            {
                Assert.Ignore("Requires Sec-Purpose header, shipped in Chrome 138");
            }

            EnsureServer();
            Server.SetRoute("/prefetch.js", http =>
            {
                http.Response.ContentType = "application/javascript";
                return http.Response.WriteAsync("// Scripts will be pre-fetched");
            });
            Server.SetRoute("/prefetch.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<link rel='prefetch' href='prefetch.js'>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IResponse> responseTask = page.WaitForResponseAsync("**/prefetch.js");
            await page.GoToAsync(Prefix + "/prefetch.html").ConfigureAwait(false);
            IResponse response = await responseTask.ConfigureAwait(false);
            byte[] body = await response.BodyAsync().ConfigureAwait(false);
            Assert.That(Encoding.UTF8.GetString(body), Is.EqualTo("// Scripts will be pre-fetched"));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should return body for image with evicted body")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnBodyForImageWithEvictedBody()
        {
            if (TestConstants.IsWebKit && TestConstants.IsMacOSX)
            {
                Assert.Ignore("WebKit on Mac evicts the body and returns empty buffer");
            }

            EnsureServer();
            Server.SetRoute("/pixel.gif", async http =>
            {
                http.Response.Headers["content-type"] = "image/gif";
                await http.Response.Body.WriteAsync(PixelGifBytes).ConfigureAwait(false);
            });
            Server.SetRoute("/page.html", http =>
            {
                http.Response.Headers["content-type"] = "text/html";
                return http.Response.WriteAsync("<img src='/pixel.gif'>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IResponse> responseTask = page.WaitForResponseAsync("**/pixel.gif");
            await page.GoToAsync(Prefix + "/page.html").ConfigureAwait(false);
            IResponse response = await responseTask.ConfigureAwait(false);
            byte[] body = await response.BodyAsync().ConfigureAwait(false);
            Assert.That(Convert.ToBase64String(body), Is.EqualTo("R0lGODlhAQABAAAAACw="));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should bypass disk cache when page interception is enabled")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldBypassDiskCacheWhenPageInterceptionIsEnabled()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);
            await page.RouteAsync("**/api*", route => route.ContinueAsync()).ConfigureAwait(false);

            List<HttpRequest> mainRequests = new List<HttpRequest>();
            Server.SetRoute("/api", async http =>
            {
                mainRequests.Add(http.Request);
                http.Response.StatusCode = 200;
                http.Response.Headers["content-type"] = "text/plain";
                http.Response.Headers["cache-control"] = "public, max-age=31536000";
                await http.Response.WriteAsync("Hello").ConfigureAwait(false);
            });
            for (int i = 0; i < 3; i++)
            {
                Task<IResponse> respPromise = page.WaitForResponseAsync("**/api");
                await page.EvaluateAsync("(async () => { const response = await fetch('/api'); return response.status; })()")
                    .ConfigureAwait(false);
                IResponse response = await respPromise.ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(200));
                Assert.That(mainRequests.Count, Is.EqualTo(i + 1));
            }

            List<HttpRequest> frameRequests = new List<HttpRequest>();
            Server.SetRoute("/frame/api", async http =>
            {
                frameRequests.Add(http.Request);
                http.Response.StatusCode = 200;
                http.Response.Headers["content-type"] = "text/plain";
                http.Response.Headers["cache-control"] = "public, max-age=31536000";
                await http.Response.WriteAsync("Hello").ConfigureAwait(false);
            });
            IFrame frame = page.FrameByUrl("**/frame.html");
            Assert.That(frame, Is.Not.Null);
            for (int i = 0; i < 3; i++)
            {
                Task<IResponse> respPromise = page.WaitForResponseAsync("**/frame/api");
                await frame.EvaluateAsync("(async () => { const response = await fetch('/frame/api'); return response.status; })()")
                    .ConfigureAwait(false);
                IResponse response = await respPromise.ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(200));
                Assert.That(frameRequests.Count, Is.EqualTo(i + 1));
            }
        }

        [PlaywrightTest("page-network-response.spec.ts", "request.existingResponse should return null before response is received")]
        [Test]
        [Timeout(30_000)]
        public async Task RequestExistingResponseShouldReturnNullBeforeResponseIsReceived()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            TaskCompletionSource<HttpResponse> serverResponseTcs = new TaskCompletionSource<HttpResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            Server.SetRoute("/get", http =>
            {
                serverResponseTcs.TrySetResult(http.Response);
                return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
            });

            Task<IRequest> requestTask = page.WaitForRequestAsync(r => r.Url.Contains("/get", StringComparison.Ordinal));
            Task waitForServer = Server.WaitForRequest("/get");
            await page.EvaluateAsync("void fetch('./get', { method: 'GET' })").ConfigureAwait(false);
            await waitForServer.ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);
            Assert.That(request.ExistingResponse, Is.Null);

            HttpResponse serverResponse = await serverResponseTcs.Task.ConfigureAwait(false);
            serverResponse.Headers["Content-Type"] = "text/plain; charset=utf-8";
            Task<IResponse> responseTask = page.WaitForResponseAsync(r => r.Url.Contains("/get", StringComparison.Ordinal));
            await serverResponse.WriteAsync("done").ConfigureAwait(false);
            await serverResponse.CompleteAsync().ConfigureAwait(false);
            await responseTask.ConfigureAwait(false);
            IResponse existingResponse = request.ExistingResponse;
            Assert.That(existingResponse, Is.Not.Null);
            Assert.That(existingResponse.Status, Is.EqualTo(200));
        }

        [PlaywrightTest("page-network-response.spec.ts", "request.existingResponse should return the response after it is received")]
        [Test]
        [Timeout(30_000)]
        public async Task RequestExistingResponseShouldReturnTheResponseAfterItIsReceived()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IRequest request = response.Request;
            Assert.That(request.ExistingResponse, Is.SameAs(response));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should return http version")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnHttpVersion()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(await response.HttpVersionAsync().ConfigureAwait(false), Is.EqualTo("HTTP/1.1"));
        }

        [PlaywrightTest("page-network-response.spec.ts", "Response.formData() should parse multipart/form-data in page context")]
        [Test]
        [Timeout(30_000)]
        public async Task ResponseFormDataShouldParseMultipartFormDataInPageContext()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            JsonElement result = await page.EvaluateAsync<JsonElement>(@"(async () => {
                const boundary = '----WebKitFormBoundary1234';
                const body = [
                  `--${boundary}`,
                  'Content-Disposition: form-data; name=""field1""',
                  '',
                  'value1',
                  `--${boundary}`,
                  'Content-Disposition: form-data; name=""file1""; filename=""test.txt""',
                  'Content-Type: text/plain',
                  '',
                  'hello',
                  `--${boundary}--`,
                ].join('\r\n');
                const response = new Response(body, {
                  headers: { 'Content-Type': `multipart/form-data; boundary=${boundary}` },
                });
                const fd = await response.formData();
                const file = fd.get('file1');
                return {
                  field1: fd.get('field1'),
                  filename: file instanceof File ? file.name : null,
                  fileContent: file instanceof File ? await file.text() : null,
                };
            })()").ConfigureAwait(false);
            Assert.That(result.ValueKind, Is.EqualTo(JsonValueKind.Object), result.ToString());
            Assert.That(result.GetProperty("field1").GetString(), Is.EqualTo("value1"));
            Assert.That(result.GetProperty("filename").GetString(), Is.EqualTo("test.txt"));
            Assert.That(result.GetProperty("fileContent").GetString(), Is.EqualTo("hello"));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should give a readable error when response.body() races with navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldGiveAReadableErrorWhenResponseBodyRacesWithNavigation()
        {
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("Firefox keeps the response body available after navigating away, so it never throws");
            }

            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IResponse> responseTask = page.WaitForResponseAsync(Prefix + "/title.html");
            await page.GoToAsync(Prefix + "/title.html").ConfigureAwait(false);
            IResponse response = await responseTask.ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(() => response.BodyAsync());
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("navigated away"));
        }
    }
}
