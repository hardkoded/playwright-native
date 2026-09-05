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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IBrowserContext.APIRequest"/>.
    /// </summary>
    [TestFixture]
    public class ApiRequestTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        private static SimpleServer HttpsServer => TestServerSetup.HttpsServer;

        private static async Task EchoBytesAsync(HttpContext http)
        {
            using MemoryStream buffer = new MemoryStream();
            await http.Request.Body.CopyToAsync(buffer).ConfigureAwait(false);
            http.Response.ContentType = "application/octet-stream";
            await http.Response.Body.WriteAsync(buffer.ToArray()).ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "context APIRequest GET returns the body")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextGetShouldReturnTheBody()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-hello", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("hello-api");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IAPIResponse response = await context.APIRequest.GetAsync(TestConstants.ServerUrl + "/api-hello").ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("hello-api"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "page APIRequest shares the context client")]
        [Test]
        [Timeout(30_000)]
        public async Task PageApiRequestShouldBeTheContextClient()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(page.APIRequest, Is.SameAs(context.APIRequest));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest POST sends the body")]
        [Test]
        [Timeout(30_000)]
        public async Task PostShouldSendTheBody()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-echo", async http =>
            {
                using StreamReader reader = new StreamReader(http.Request.Body);
                string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync("echo:" + body).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IAPIResponse response = await context.APIRequest.PostAsync(
                TestConstants.ServerUrl + "/api-echo",
                "wave-128").ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("echo:wave-128"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest GET sends context cookies")]
        [Test]
        [Timeout(30_000)]
        public async Task GetShouldSendContextCookies()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-cookie", http =>
            {
                http.Response.ContentType = "text/plain";
                string cookie = http.Request.Headers["Cookie"].ToString();
                return http.Response.WriteAsync(cookie);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Name = "wave128",
                    Value = "from-context",
                    Url = TestConstants.ServerUrl + "/api-cookie",
                    SameSite = SameSiteAttribute.Lax,
                },
            }).ConfigureAwait(false);

            IAPIResponse response = await context.APIRequest.GetAsync(TestConstants.ServerUrl + "/api-cookie").ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Does.Contain("wave128=from-context"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest HEAD returns OK")]
        [Test]
        [Timeout(30_000)]
        public async Task HeadShouldReturnOk()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-head", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("ignored");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IAPIResponse response = await context.APIRequest.HeadAsync(TestConstants.ServerUrl + "/api-head").ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);
            Assert.That(response.Status, Is.EqualTo(200));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest PUT and DELETE send the method")]
        [Test]
        [Timeout(30_000)]
        public async Task PutAndDeleteShouldSendTheMethod()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-verb", async http =>
            {
                using StreamReader reader = new StreamReader(http.Request.Body);
                string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync(http.Request.Method + ":" + body).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IAPIResponse put = await context.APIRequest.PutAsync(
                TestConstants.ServerUrl + "/api-verb",
                "payload").ConfigureAwait(false);
            Assert.That(await put.TextAsync().ConfigureAwait(false), Is.EqualTo("PUT:payload"));

            IAPIResponse delete = await context.APIRequest.DeleteAsync(TestConstants.ServerUrl + "/api-verb").ConfigureAwait(false);
            Assert.That(await delete.TextAsync().ConfigureAwait(false), Does.StartWith("DELETE:"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest GET sends extra headers")]
        [Test]
        [Timeout(30_000)]
        public async Task GetShouldSendExtraHeaders()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-header", http =>
            {
                http.Response.ContentType = "text/plain";
                string wave = http.Request.Headers["X-Wave"].ToString();
                return http.Response.WriteAsync(wave);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IAPIResponse response = await context.APIRequest.GetAsync(
                TestConstants.ServerUrl + "/api-header",
                new Dictionary<string, string> { ["X-Wave"] = "129" }).ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("129"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest StorageState matches the context")]
        [Test]
        [Timeout(30_000)]
        public async Task StorageStateShouldMatchTheContext()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Name = "wave130",
                    Value = "state",
                    Url = TestConstants.EmptyPage,
                    SameSite = SameSiteAttribute.Lax,
                },
            }).ConfigureAwait(false);

            string fromRequest = await context.APIRequest.StorageStateAsync().ConfigureAwait(false);
            string fromContext = await context.StorageStateAsync().ConfigureAwait(false);
            Assert.That(fromRequest, Does.Contain("wave130"));
            Assert.That(fromRequest, Is.EqualTo(fromContext));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIResponse HeadersArray includes content-type")]
        [Test]
        [Timeout(30_000)]
        public async Task HeadersArrayShouldIncludeContentType()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-headers-array", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("ok");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(TestConstants.ServerUrl + "/api-headers-array").ConfigureAwait(false);

            Assert.That(response.HeadersArray, Is.Not.Empty);
            Assert.That(
                response.HeadersArray.Any(h =>
                    string.Equals(h.Name, "Content-Type", System.StringComparison.OrdinalIgnoreCase)
                    && h.Value.Contains("text/plain", System.StringComparison.OrdinalIgnoreCase)),
                Is.True);
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest returns 404 without failOnStatusCode")]
        [Test]
        [Timeout(30_000)]
        public async Task GetShouldReturnNotFoundWithoutFailOnStatusCode()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-missing", http =>
            {
                http.Response.StatusCode = 404;
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("nope");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(TestConstants.ServerUrl + "/api-missing").ConfigureAwait(false);
            Assert.That(response.Ok, Is.False);
            Assert.That(response.Status, Is.EqualTo(404));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("nope"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest failOnStatusCode throws on 404")]
        [Test]
        [Timeout(30_000)]
        public async Task GetShouldThrowWhenFailOnStatusCode()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-fail", http =>
            {
                http.Response.StatusCode = 404;
                return http.Response.WriteAsync("nope");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(async () =>
            {
                await context.APIRequest.GetAsync(TestConstants.ServerUrl + "/api-fail", new() { FailOnStatusCode = true }).ConfigureAwait(false);
            });
            Assert.That(ex.Message, Does.Contain("404"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest GET sends context extra HTTP headers")]
        [Test]
        [Timeout(30_000)]
        public async Task GetShouldSendContextExtraHttpHeaders()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-context-header", http =>
            {
                http.Response.ContentType = "text/plain";
                string wave = http.Request.Headers["X-Wave"].ToString();
                return http.Response.WriteAsync(wave);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await context.SetExtraHttpHeadersAsync(new Dictionary<string, string> { ["X-Wave"] = "132" }).ConfigureAwait(false);

            IAPIResponse response = await context.APIRequest.GetAsync(TestConstants.ServerUrl + "/api-context-header").ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("132"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest per-request headers override context extra headers")]
        [Test]
        [Timeout(30_000)]
        public async Task GetShouldLetPerRequestHeadersOverrideContext()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-override-header", http =>
            {
                http.Response.ContentType = "text/plain";
                string wave = http.Request.Headers["X-Wave"].ToString();
                return http.Response.WriteAsync(wave);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Wave"] = "context" } }).ConfigureAwait(false);

            IAPIResponse response = await context.APIRequest.GetAsync(
                TestConstants.ServerUrl + "/api-override-header",
                new Dictionary<string, string> { ["X-Wave"] = "request" }).ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("request"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest timeout throws when the server is slow")]
        [Test]
        [Timeout(30_000)]
        public async Task GetShouldThrowWhenTimeoutExceeded()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-slow", async http =>
            {
                await Task.Delay(2000).ConfigureAwait(false);
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync("late").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(async () =>
            {
                await context.APIRequest.GetAsync(TestConstants.ServerUrl + "/api-slow", new() { Timeout = 300 }).ConfigureAwait(false);
            });
            Assert.That(ex.Message, Does.Contain("timeout").IgnoreCase);
            Assert.That(ex.Message, Does.Contain("300"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest succeeds when timeout is long enough")]
        [Test]
        [Timeout(30_000)]
        public async Task GetShouldSucceedWhenTimeoutIsLongEnough()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-brief", async http =>
            {
                await Task.Delay(50).ConfigureAwait(false);
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync("soon").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IAPIResponse response = await context.APIRequest.GetAsync(TestConstants.ServerUrl + "/api-brief", new() { Timeout = 5000 }).ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("soon"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest follows redirects by default")]
        [Test]
        [Timeout(30_000)]
        public async Task GetShouldFollowRedirectsByDefault()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-dest", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("redirected");
            });
            Server.SetRedirect("/api-src", TestConstants.ServerUrl + "/api-dest");

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IAPIResponse response = await context.APIRequest.GetAsync(TestConstants.ServerUrl + "/api-src").ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("redirected"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest maxRedirects 0 returns the redirect")]
        [Test]
        [Timeout(30_000)]
        public async Task GetShouldReturnRedirectWhenMaxRedirectsIsZero()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-dest-zero", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("should-not-follow");
            });
            Server.SetRedirect("/api-src-zero", TestConstants.ServerUrl + "/api-dest-zero");

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IAPIResponse response = await context.APIRequest.GetAsync(TestConstants.ServerUrl + "/api-src-zero", new() { MaxRedirects = 0 }).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(302));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.Not.EqualTo("should-not-follow"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest maxRedirects throws when the chain is longer")]
        [Test]
        [Timeout(30_000)]
        public async Task GetShouldThrowWhenMaxRedirectsExceeded()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-final", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("done");
            });
            Server.SetRedirect("/api-hop2", TestConstants.ServerUrl + "/api-final");
            Server.SetRedirect("/api-hop1", TestConstants.ServerUrl + "/api-hop2");

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(async () =>
            {
                await context.APIRequest.GetAsync(TestConstants.ServerUrl + "/api-hop1", new() { MaxRedirects = 1 }).ConfigureAwait(false);
            });
            Assert.That(ex.Message, Does.Contain("Max redirect count exceeded"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest GET uses context ignoreHTTPSErrors")]
        [Test]
        [Timeout(30_000)]
        public async Task GetShouldFetchHttpsWhenContextIgnoresErrors()
        {
            if (HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
                return;
            }

            HttpsServer.Reset();
            HttpsServer.SetRoute("/api-https-context", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("secure-context");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);

            IAPIResponse response = await context.APIRequest.GetAsync(
                TestConstants.HttpsPrefix + "/api-https-context").ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("secure-context"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest GET uses per-request ignoreHTTPSErrors")]
        [Test]
        [Timeout(30_000)]
        public async Task GetShouldFetchHttpsWhenRequestIgnoresErrors()
        {
            if (HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
                return;
            }

            HttpsServer.Reset();
            HttpsServer.SetRoute("/api-https-request", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("secure-request");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IAPIResponse response = await context.APIRequest.GetAsync(TestConstants.HttpsPrefix + "/api-https-request", new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("secure-request"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIResponse DisposeAsync blocks body reads")]
        [Test]
        [Timeout(30_000)]
        public async Task ResponseDisposeShouldBlockBodyReads()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-dispose-body", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("gone");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IAPIResponse response = await context.APIRequest.GetAsync(
                TestConstants.ServerUrl + "/api-dispose-body").ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("gone"));

            await response.DisposeAsync().ConfigureAwait(false);
            await response.DisposeAsync().ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(
                async () => await response.TextAsync().ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("disposed"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest DisposeAsync blocks further fetches")]
        [Test]
        [Timeout(30_000)]
        public async Task RequestDisposeShouldBlockFurtherFetches()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-dispose-fetch", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("ok");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IAPIRequestContext request = context.APIRequest;
            await request.DisposeAsync().ConfigureAwait(false);
            await request.DisposeAsync().ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(async () =>
            {
                await request.GetAsync(TestConstants.ServerUrl + "/api-dispose-fetch").ConfigureAwait(false);
            });
            Assert.That(ex.Message, Does.Contain("Target page, context or browser has been closed"));

            // Upstream keeps the same disposed context.request; further calls fail.
            PlaywrightNativeException ex2 = Assert.ThrowsAsync<PlaywrightNativeException>(async () =>
            {
                await context.APIRequest.GetAsync(TestConstants.ServerUrl + "/api-dispose-fetch").ConfigureAwait(false);
            });
            Assert.That(ex2.Message, Does.Contain("Target page, context or browser has been closed"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest POST sends a JSON body")]
        [Test]
        [Timeout(30_000)]
        public async Task PostShouldSendJsonBody()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-json", async http =>
            {
                using StreamReader reader = new StreamReader(http.Request.Body);
                string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync(http.Request.ContentType + "|" + body).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IAPIResponse response = await context.APIRequest.PostAsync(TestConstants.ServerUrl + "/api-json", new() { DataObject = new Dictionary<string, int> { ["wave"] = 137 } }).ConfigureAwait(false);
            string text = await response.TextAsync().ConfigureAwait(false);
            Assert.That(text, Does.Contain("application/json"));
            Assert.That(text, Does.Contain("\"wave\":137"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest POST rejects data and json together")]
        [Test]
        [Timeout(30_000)]
        public async Task PostShouldRejectDataAndJsonTogether()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            ArgumentException ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await context.APIRequest.PostAsync(TestConstants.ServerUrl + "/api-json-both", new() { Data = "plain", DataObject = new { wave = 137 } }).ConfigureAwait(false);
            });
            Assert.That(ex.Message, Does.Contain("data").And.Contain("json"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest POST sends a form body")]
        [Test]
        [Timeout(30_000)]
        public async Task PostShouldSendFormBody()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-form", async http =>
            {
                using StreamReader reader = new StreamReader(http.Request.Body);
                string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync(http.Request.ContentType + "|" + body).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IFormData form = context.APIRequest.CreateFormData();
            form.Set("wave", "138").Set("ok", "yes");

            IAPIResponse response = await context.APIRequest.PostAsync(TestConstants.ServerUrl + "/api-form", new() { Form = form }).ConfigureAwait(false);
            string text = await response.TextAsync().ConfigureAwait(false);
            Assert.That(text, Does.Contain("application/x-www-form-urlencoded"));
            Assert.That(text, Does.Contain("wave=138"));
            Assert.That(text, Does.Contain("ok=yes"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest POST rejects json and form together")]
        [Test]
        [Timeout(30_000)]
        public async Task PostShouldRejectJsonAndFormTogether()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IFormData form = context.APIRequest.CreateFormData();
            form.Set("wave", "138");

            ArgumentException ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await context.APIRequest.PostAsync(TestConstants.ServerUrl + "/api-form-both", new() { DataObject = new { wave = 137 }, Form = form }).ConfigureAwait(false);
            });
            Assert.That(ex.Message, Does.Contain("form"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest POST sends multipart when form has a file")]
        [Test]
        [Timeout(30_000)]
        public async Task PostShouldSendMultipartWhenFormHasAFile()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-multipart", async http =>
            {
                using StreamReader reader = new StreamReader(http.Request.Body);
                string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync(http.Request.ContentType + "|" + body).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IFormData form = context.APIRequest.CreateFormData();
            form.Set("wave", "139");
            form.Set("upload", new FilePayload
            {
                Name = "note.txt",
                MimeType = "text/plain",
                Buffer = Encoding.UTF8.GetBytes("hello-wave-139"),
            });

            IAPIResponse response = await context.APIRequest.PostAsync(TestConstants.ServerUrl + "/api-multipart", new() { Form = form }).ConfigureAwait(false);
            string text = await response.TextAsync().ConfigureAwait(false);
            Assert.That(text, Does.Contain("multipart/form-data"));
            Assert.That(text, Does.Contain("wave"));
            Assert.That(text, Does.Contain("139"));
            Assert.That(text, Does.Contain("note.txt"));
            Assert.That(text, Does.Contain("hello-wave-139"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest POST form Append keeps duplicate names")]
        [Test]
        [Timeout(30_000)]
        public async Task PostFormAppendShouldKeepDuplicateNames()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-form-append", async http =>
            {
                using StreamReader reader = new StreamReader(http.Request.Body);
                string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync(http.Request.ContentType + "|" + body).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IFormData form = context.APIRequest.CreateFormData();
            form.Set("wave", "140").Append("tag", "a").Append("tag", "b");

            IAPIResponse response = await context.APIRequest.PostAsync(TestConstants.ServerUrl + "/api-form-append", new() { Form = form }).ConfigureAwait(false);
            string text = await response.TextAsync().ConfigureAwait(false);
            Assert.That(text, Does.Contain("application/x-www-form-urlencoded"));
            Assert.That(text, Does.Contain("wave=140"));
            Assert.That(text, Does.Contain("tag=a"));
            Assert.That(text, Does.Contain("tag=b"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest POST form Set replaces appended names")]
        [Test]
        [Timeout(30_000)]
        public async Task PostFormSetShouldReplaceAppendedNames()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-form-set-replace", async http =>
            {
                using StreamReader reader = new StreamReader(http.Request.Body);
                string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync(http.Request.ContentType + "|" + body).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IFormData form = context.APIRequest.CreateFormData();
            form.Append("tag", "a").Append("tag", "b").Set("tag", "c");

            IAPIResponse response = await context.APIRequest.PostAsync(TestConstants.ServerUrl + "/api-form-set-replace", new() { Form = form }).ConfigureAwait(false);
            string text = await response.TextAsync().ConfigureAwait(false);
            Assert.That(text, Does.Contain("tag=c"));
            Assert.That(text, Does.Not.Contain("tag=a"));
            Assert.That(text, Does.Not.Contain("tag=b"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest POST form Append file sends multipart")]
        [Test]
        [Timeout(30_000)]
        public async Task PostFormAppendFileShouldSendMultipart()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-form-append-file", async http =>
            {
                using StreamReader reader = new StreamReader(http.Request.Body);
                string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync(http.Request.ContentType + "|" + body).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IFormData form = context.APIRequest.CreateFormData();
            form.Append("wave", "140");
            form.Append("upload", new FilePayload
            {
                Name = "note.txt",
                MimeType = "text/plain",
                Buffer = Encoding.UTF8.GetBytes("hello-wave-140"),
            });

            IAPIResponse response = await context.APIRequest.PostAsync(TestConstants.ServerUrl + "/api-form-append-file", new() { Form = form }).ConfigureAwait(false);
            string text = await response.TextAsync().ConfigureAwait(false);
            Assert.That(text, Does.Contain("multipart/form-data"));
            Assert.That(text, Does.Contain("wave"));
            Assert.That(text, Does.Contain("140"));
            Assert.That(text, Does.Contain("note.txt"));
            Assert.That(text, Does.Contain("hello-wave-140"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest POST form Set bool and int")]
        [Test]
        [Timeout(30_000)]
        public async Task PostFormSetShouldSendBoolAndInt()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-form-typed", async http =>
            {
                using StreamReader reader = new StreamReader(http.Request.Body);
                string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync(http.Request.ContentType + "|" + body).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IFormData form = context.APIRequest.CreateFormData();
            form.Set("wave", 141).Set("ok", true).Set("off", false);

            IAPIResponse response = await context.APIRequest.PostAsync(TestConstants.ServerUrl + "/api-form-typed", new() { Form = form }).ConfigureAwait(false);
            string text = await response.TextAsync().ConfigureAwait(false);
            Assert.That(text, Does.Contain("application/x-www-form-urlencoded"));
            Assert.That(text, Does.Contain("wave=141"));
            Assert.That(text, Does.Contain("ok=true"));
            Assert.That(text, Does.Contain("off=false"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest POST form Append bool and int")]
        [Test]
        [Timeout(30_000)]
        public async Task PostFormAppendShouldSendBoolAndInt()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-form-typed-append", async http =>
            {
                using StreamReader reader = new StreamReader(http.Request.Body);
                string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync(http.Request.ContentType + "|" + body).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IFormData form = context.APIRequest.CreateFormData();
            form.Append("n", 1).Append("n", 2).Append("flag", true);

            IAPIResponse response = await context.APIRequest.PostAsync(TestConstants.ServerUrl + "/api-form-typed-append", new() { Form = form }).ConfigureAwait(false);
            string text = await response.TextAsync().ConfigureAwait(false);
            Assert.That(text, Does.Contain("n=1"));
            Assert.That(text, Does.Contain("n=2"));
            Assert.That(text, Does.Contain("flag=true"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest POST multipart sends form-data without files")]
        [Test]
        [Timeout(30_000)]
        public async Task PostMultipartShouldSendFormDataWithoutFiles()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-multipart-param", async http =>
            {
                using StreamReader reader = new StreamReader(http.Request.Body);
                string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync(http.Request.ContentType + "|" + body).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IFormData multipart = context.APIRequest.CreateFormData();
            multipart.Set("wave", 142).Set("ok", true);

            IAPIResponse response = await context.APIRequest.PostAsync(TestConstants.ServerUrl + "/api-multipart-param", new() { Multipart = multipart }).ConfigureAwait(false);
            string text = await response.TextAsync().ConfigureAwait(false);
            Assert.That(text, Does.Contain("multipart/form-data"));
            Assert.That(text, Does.Not.Contain("application/x-www-form-urlencoded"));
            Assert.That(text, Does.Contain("wave"));
            Assert.That(text, Does.Contain("142"));
            Assert.That(text, Does.Contain("ok"));
            Assert.That(text, Does.Contain("true"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest POST rejects form and multipart together")]
        [Test]
        [Timeout(30_000)]
        public async Task PostShouldRejectFormAndMultipartTogether()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IFormData form = context.APIRequest.CreateFormData();
            form.Set("wave", 142);
            IFormData multipart = context.APIRequest.CreateFormData();
            multipart.Set("wave", 142);

            ArgumentException ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await context.APIRequest.PostAsync(TestConstants.ServerUrl + "/api-form-multipart-both", new() { Form = form, Multipart = multipart }).ConfigureAwait(false);
            });
            Assert.That(ex.Message, Does.Contain("multipart"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest GET sends queryParams")]
        [Test]
        [Timeout(30_000)]
        public async Task GetShouldSendQueryParams()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            Task<string> waitTask = Server.WaitForRequest(
                "/api-query",
                request => request.QueryString.Value ?? string.Empty);
            IAPIResponse response = await context.APIRequest.GetAsync(TestConstants.ServerUrl + "/api-query", new APIRequestContextOptions
            {
                Params = new Dictionary<string, string>
                {
                    ["wave"] = "143",
                    ["ok"] = "yes",
                }.AsObjectPairs()
            }).ConfigureAwait(false);
            string query = await waitTask.ConfigureAwait(false);
            Assert.That(query, Does.Contain("wave=143"));
            Assert.That(query, Does.Contain("ok=yes"));
            Assert.That(response.Url, Does.Contain("wave=143"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest GET queryParams keep existing and duplicates")]
        [Test]
        [Timeout(30_000)]
        public async Task GetQueryParamsShouldKeepExistingAndDuplicates()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            Task<string> waitTask = Server.WaitForRequest(
                "/api-query-merge",
                request => request.QueryString.Value ?? string.Empty);
            IAPIResponse response = await context.APIRequest.GetAsync(TestConstants.ServerUrl + "/api-query-merge?keep=1", new APIRequestContextOptions
            {
                Params = new[]
                {
                    new KeyValuePair<string, object>("page", "2"),
                    new KeyValuePair<string, object>("page", "3"),
                }
            }).ConfigureAwait(false);
            string query = await waitTask.ConfigureAwait(false);
            Assert.That(query, Does.Contain("keep=1"));
            Assert.That(query, Does.Contain("page=2"));
            Assert.That(query, Does.Contain("page=3"));
            Assert.That(response.Url, Does.Contain("keep=1"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest maxRetries recovers after connection reset")]
        [Test]
        [Timeout(30_000)]
        public async Task GetShouldRetryConnectionReset()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            int hits = 0;
            Server.SetRoute("/api-reset", http =>
            {
                int n = Interlocked.Increment(ref hits);
                if (n <= 2)
                {
                    http.Abort();
                    return Task.CompletedTask;
                }

                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("recovered");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IAPIResponse response = await context.APIRequest.GetAsync(TestConstants.ServerUrl + "/api-reset", new() { MaxRetries = 2 }).ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("recovered"));
            Assert.That(hits, Is.GreaterThanOrEqualTo(3));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest maxRetries 0 does not retry")]
        [Test]
        [Timeout(30_000)]
        public async Task GetShouldNotRetryWhenMaxRetriesIsZero()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            int hits = 0;
            Server.SetRoute("/api-reset-once", http =>
            {
                Interlocked.Increment(ref hits);
                http.Abort();
                return Task.CompletedTask;
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            PlaywrightNativeException resetError = Assert.ThrowsAsync<PlaywrightNativeException>(async () =>
            {
                await context.APIRequest.GetAsync(
                    TestConstants.ServerUrl + "/api-reset-once").ConfigureAwait(false);
            });
            Assert.That(resetError.Message, Does.Contain("socket hang up"));
            Assert.That(hits, Is.EqualTo(1));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest maxRetries rejects a negative value")]
        [Test]
        [Timeout(30_000)]
        public async Task GetShouldRejectNegativeMaxRetries()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            ArgumentOutOfRangeException ex = Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            {
                await context.APIRequest.GetAsync(TestConstants.ServerUrl + "/api-retries", new() { MaxRetries = -1 }).ConfigureAwait(false);
            });
            Assert.That(ex.ParamName, Is.EqualTo("maxRetries"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "Playwright.APIRequest GET works without a browser")]
        [Test]
        [Timeout(30_000)]
        public async Task StandaloneGetShouldWorkWithoutABrowser()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-standalone", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("no-browser");
            });

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(
                TestConstants.ServerUrl + "/api-standalone").ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("no-browser"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "Playwright.APIRequest sends extraHTTPHeaders")]
        [Test]
        [Timeout(30_000)]
        public async Task StandaloneShouldSendExtraHttpHeaders()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-standalone-headers", http =>
            {
                string wave = http.Request.Headers["X-Wave"].ToString();
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync(wave);
            });

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Wave"] = "145" } }).ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(
                TestConstants.ServerUrl + "/api-standalone-headers").ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("145"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "Playwright.APIRequest StorageState is empty")]
        [Test]
        [Timeout(30_000)]
        public async Task StandaloneStorageStateShouldBeEmpty()
        {
            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            string json = await request.StorageStateAsync().ConfigureAwait(false);
            Assert.That(json, Does.Contain("\"cookies\""));
            Assert.That(json, Does.Contain("\"origins\""));
            Assert.That(json, Does.Contain("[]"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "Playwright.APIRequest DisposeAsync blocks further fetches")]
        [Test]
        [Timeout(30_000)]
        public async Task StandaloneDisposeShouldBlockFurtherFetches()
        {
            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            await request.DisposeAsync().ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(async () =>
            {
                await request.GetAsync(TestConstants.ServerUrl + "/api-standalone-disposed").ConfigureAwait(false);
            });
            Assert.That(ex.Message, Does.Contain("Target page, context or browser has been closed"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "Playwright.APIRequest ignoreHTTPSErrors accepts untrusted TLS")]
        [Test]
        [Timeout(30_000)]
        public async Task StandaloneShouldFetchHttpsWhenIgnoringErrors()
        {
            if (HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
                return;
            }

            HttpsServer.Reset();
            HttpsServer.SetRoute("/api-standalone-https", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("secure-standalone");
            });

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(
                TestConstants.HttpsPrefix + "/api-standalone-https").ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("secure-standalone"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "Playwright.APIRequest baseURL resolves a relative URL")]
        [Test]
        [Timeout(30_000)]
        public async Task StandaloneBaseUrlShouldResolveARelativeUrl()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-base", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("from-base");
            });

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { BaseURL = TestConstants.ServerUrl }).ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync("/api-base").ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("from-base"));
            Assert.That(response.Url, Does.Contain("/api-base"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "Playwright.APIRequest userAgent is sent")]
        [Test]
        [Timeout(30_000)]
        public async Task StandaloneUserAgentShouldBeSent()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-ua", http =>
            {
                string agent = http.Request.Headers["User-Agent"].ToString();
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync(agent);
            });

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { UserAgent = "PlaywrightNative-Wave-147" }).ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(
                TestConstants.ServerUrl + "/api-ua").ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("PlaywrightNative-Wave-147"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "Playwright.APIRequest timeout applies to every request")]
        [Test]
        [Timeout(30_000)]
        public async Task StandaloneTimeoutShouldApplyToEveryRequest()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-standalone-slow", async http =>
            {
                await Task.Delay(2000).ConfigureAwait(false);
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync("late").ConfigureAwait(false);
            });

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { Timeout = 300 }).ConfigureAwait(false);
            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(async () =>
            {
                await request.GetAsync(TestConstants.ServerUrl + "/api-standalone-slow").ConfigureAwait(false);
            });
            Assert.That(ex.Message, Does.Contain("timeout").IgnoreCase);
            Assert.That(ex.Message, Does.Contain("300"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "Playwright.APIRequest failOnStatusCode throws on 404")]
        [Test]
        [Timeout(30_000)]
        public async Task StandaloneFailOnStatusCodeShouldThrow()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-standalone-404", http =>
            {
                http.Response.StatusCode = 404;
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("missing");
            });

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { FailOnStatusCode = true }).ConfigureAwait(false);
            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(async () =>
            {
                await request.GetAsync(TestConstants.ServerUrl + "/api-standalone-404").ConfigureAwait(false);
            });
            Assert.That(ex.Message, Does.Contain("404"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "Playwright.APIRequest maxRedirects throws when the chain is longer")]
        [Test]
        [Timeout(30_000)]
        public async Task StandaloneMaxRedirectsShouldThrowWhenExceeded()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-standalone-final", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("done");
            });
            Server.SetRedirect("/api-standalone-hop2", TestConstants.ServerUrl + "/api-standalone-final");
            Server.SetRedirect("/api-standalone-hop1", TestConstants.ServerUrl + "/api-standalone-hop2");

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { MaxRedirects = 1 }).ConfigureAwait(false);
            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(async () =>
            {
                await request.GetAsync(TestConstants.ServerUrl + "/api-standalone-hop1").ConfigureAwait(false);
            });
            Assert.That(ex.Message, Does.Contain("Max redirect count exceeded"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "Playwright.APIRequest storageState sends cookies")]
        [Test]
        [Timeout(30_000)]
        public async Task StandaloneStorageStateShouldSendCookies()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-standalone-cookie", http =>
            {
                http.Response.ContentType = "text/plain";
                string cookie = http.Request.Headers["Cookie"].ToString();
                return http.Response.WriteAsync(cookie);
            });

            string json =
                "{\"cookies\":[{\"name\":\"wave151\",\"value\":\"from-state\",\"domain\":\"localhost\",\"path\":\"/\",\"expires\":-1,\"httpOnly\":false,\"secure\":false,\"sameSite\":\"Lax\"}],\"origins\":[]}";
            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { StorageState = json }).ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(
                TestConstants.ServerUrl + "/api-standalone-cookie").ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Does.Contain("wave151=from-state"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "Playwright.APIRequest storageStatePath sends cookies")]
        [Test]
        [Timeout(30_000)]
        public async Task StandaloneStorageStatePathShouldSendCookies()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-standalone-cookie-path", http =>
            {
                http.Response.ContentType = "text/plain";
                string cookie = http.Request.Headers["Cookie"].ToString();
                return http.Response.WriteAsync(cookie);
            });

            string path = Path.Combine(Path.GetTempPath(), "pw-wave151-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(
                path,
                "{\"cookies\":[{\"name\":\"wave151p\",\"value\":\"from-file\",\"domain\":\"localhost\",\"path\":\"/\",\"expires\":-1,\"httpOnly\":false,\"secure\":false,\"sameSite\":\"Lax\"}],\"origins\":[]}");
            try
            {
                await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { StorageStatePath = path }).ConfigureAwait(false);
                IAPIResponse response = await request.GetAsync(
                    TestConstants.ServerUrl + "/api-standalone-cookie-path").ConfigureAwait(false);
                Assert.That(await response.TextAsync().ConfigureAwait(false), Does.Contain("wave151p=from-file"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [PlaywrightTest("global-fetch.spec.ts", "Playwright.APIRequest httpCredentials sends Basic auth")]
        [Test]
        [Timeout(30_000)]
        public async Task StandaloneHttpCredentialsShouldSendBasicAuth()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetAuth("/api-standalone-auth", "wave156", "s3cret");
            Server.SetRoute("/api-standalone-auth", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("ok");
            });

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "wave156", Password = "s3cret" } }).ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(
                TestConstants.ServerUrl + "/api-standalone-auth").ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("ok"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "Playwright.APIRequest httpCredentials origin matches")]
        [Test]
        [Timeout(30_000)]
        public async Task StandaloneHttpCredentialsOriginShouldApplyWhenRequestMatches()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetAuth("/api-standalone-auth-origin", "wave404", "s3cret");
            Server.SetRoute("/api-standalone-auth-origin", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("ok");
            });

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new()
            {
                HttpCredentials = new HttpCredentials
                {
                    Username = "wave404",
                    Password = "s3cret",
                    Origin = TestConstants.ServerUrl,
                }
            }).ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(
                TestConstants.ServerUrl + "/api-standalone-auth-origin").ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("ok"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "Playwright.APIRequest httpCredentials origin mismatch")]
        [Test]
        [Timeout(30_000)]
        public async Task StandaloneHttpCredentialsOriginShouldNotApplyToOtherOrigins()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetAuth("/api-standalone-auth-origin-miss", "wave404", "s3cret");
            Server.SetRoute("/api-standalone-auth-origin-miss", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("ok");
            });

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new()
            {
                HttpCredentials = new HttpCredentials
                {
                    Username = "wave404",
                    Password = "s3cret",
                    Origin = TestConstants.ServerUrl,
                }
            }).ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(
                TestConstants.CrossProcessHttpPrefix + "/api-standalone-auth-origin-miss").ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(401));
        }

        [PlaywrightTest("global-fetch.spec.ts", "Playwright.APIRequest proxy is used for fetches")]
        [Test]
        [Timeout(30_000)]
        public async Task StandaloneProxyShouldBeUsedForFetches()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { Timeout = 2000, Proxy = new Proxy { Server = "http://127.0.0.1:1" } }).ConfigureAwait(false);
            Exception ex = Assert.CatchAsync(
                () => request.GetAsync(TestConstants.ServerUrl + "/empty.html"));
            Assert.That(ex, Is.Not.Null);
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest JsonAsync T deserializes the body")]
        [Test]
        [Timeout(30_000)]
        public async Task JsonAsyncShouldDeserializeToType()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-json-t", http =>
            {
                http.Response.ContentType = "application/json";
                return http.Response.WriteAsync("{\"n\":157}");
            });

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(
                TestConstants.ServerUrl + "/api-json-t").ConfigureAwait(false);
            Dictionary<string, int> body = await response.JsonAsync<Dictionary<string, int>>().ConfigureAwait(false);
            Assert.That(body, Is.Not.Null);
            Assert.That(body["n"], Is.EqualTo(157));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest PostAsync dataBytes sends a binary body")]
        [Test]
        [Timeout(30_000)]
        public async Task PostShouldSendDataBytes()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-bytes", async http =>
            {
                using MemoryStream buffer = new MemoryStream();
                await http.Request.Body.CopyToAsync(buffer).ConfigureAwait(false);
                http.Response.ContentType = "application/octet-stream";
                await http.Response.Body.WriteAsync(buffer.ToArray()).ConfigureAwait(false);
            });

            byte[] payload = { 1, 2, 3, 158 };
            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await request.PostAsync(TestConstants.ServerUrl + "/api-bytes", new() { DataByte = payload }).ConfigureAwait(false);
            Assert.That(await response.BodyAsync().ConfigureAwait(false), Is.EqualTo(payload));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest POST form Set and Append long")]
        [Test]
        [Timeout(30_000)]
        public async Task PostFormShouldSendLong()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-form-long", async http =>
            {
                using StreamReader reader = new StreamReader(http.Request.Body);
                string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync(body).ConfigureAwait(false);
            });

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            IFormData form = request.CreateFormData();
            form.Set("wave", 3000000000L).Append("extra", 159L);

            IAPIResponse response = await request.PostAsync(TestConstants.ServerUrl + "/api-form-long", new() { Form = form }).ConfigureAwait(false);
            string text = await response.TextAsync().ConfigureAwait(false);
            Assert.That(text, Does.Contain("wave=3000000000"));
            Assert.That(text, Does.Contain("extra=159"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest POST form Set and Append double")]
        [Test]
        [Timeout(30_000)]
        public async Task PostFormShouldSendDouble()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-form-double", async http =>
            {
                using StreamReader reader = new StreamReader(http.Request.Body);
                string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync(body).ConfigureAwait(false);
            });

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            IFormData form = request.CreateFormData();
            form.Set("wave", 160.5).Append("extra", 1.25);

            IAPIResponse response = await request.PostAsync(TestConstants.ServerUrl + "/api-form-double", new() { Form = form }).ConfigureAwait(false);
            string text = await response.TextAsync().ConfigureAwait(false);
            Assert.That(text, Does.Contain("wave=160.5"));
            Assert.That(text, Does.Contain("extra=1.25"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest POST form Set and Append decimal")]
        [Test]
        [Timeout(30_000)]
        public async Task PostFormShouldSendDecimal()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-form-decimal", async http =>
            {
                using StreamReader reader = new StreamReader(http.Request.Body);
                string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync(body).ConfigureAwait(false);
            });

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            IFormData form = request.CreateFormData();
            form.Set("wave", 180.75m).Append("extra", 2.5m);

            IAPIResponse response = await request.PostAsync(TestConstants.ServerUrl + "/api-form-decimal", new() { Form = form }).ConfigureAwait(false);
            string text = await response.TextAsync().ConfigureAwait(false);
            Assert.That(text, Does.Contain("wave=180.75"));
            Assert.That(text, Does.Contain("extra=2.5"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest POST form Set and Append float")]
        [Test]
        [Timeout(30_000)]
        public async Task PostFormShouldSendFloat()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-form-float", async http =>
            {
                using StreamReader reader = new StreamReader(http.Request.Body);
                string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync(body).ConfigureAwait(false);
            });

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            IFormData form = request.CreateFormData();
            form.Set("wave", 204.5f).Append("extra", 1.25f);

            IAPIResponse response = await request.PostAsync(TestConstants.ServerUrl + "/api-form-float", new() { Form = form }).ConfigureAwait(false);
            string text = await response.TextAsync().ConfigureAwait(false);
            Assert.That(text, Does.Contain("wave=204.5"));
            Assert.That(text, Does.Contain("extra=1.25"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "APIRequest Put Patch Delete dataBytes send a binary body")]
        [Test]
        [Timeout(30_000)]
        public async Task PutPatchDeleteShouldSendDataBytes()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-bytes-put", EchoBytesAsync);
            Server.SetRoute("/api-bytes-patch", EchoBytesAsync);
            Server.SetRoute("/api-bytes-delete", EchoBytesAsync);

            byte[] putPayload = { 1, 2, 166 };
            byte[] patchPayload = { 3, 4, 166 };
            byte[] deletePayload = { 5, 6, 166 };
            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);

            IAPIResponse put = await request.PutAsync(TestConstants.ServerUrl + "/api-bytes-put", new() { DataByte = putPayload }).ConfigureAwait(false);
            IAPIResponse patch = await request.PatchAsync(TestConstants.ServerUrl + "/api-bytes-patch", new() { DataByte = patchPayload }).ConfigureAwait(false);
            IAPIResponse delete = await request.DeleteAsync(TestConstants.ServerUrl + "/api-bytes-delete", new() { DataByte = deletePayload }).ConfigureAwait(false);

            Assert.That(await put.BodyAsync().ConfigureAwait(false), Is.EqualTo(putPayload));
            Assert.That(await patch.BodyAsync().ConfigureAwait(false), Is.EqualTo(patchPayload));
            Assert.That(await delete.BodyAsync().ConfigureAwait(false), Is.EqualTo(deletePayload));
        }
    }
}
