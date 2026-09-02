/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Response body / JSON / finished plus request post-data and redirect chain
    /// on the public <see cref="IResponse"/> / <see cref="IRequest"/> implementations.
    /// </summary>
    [TestFixture]
    public class ResponseBodyTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("page-network-request.spec.ts", "should return response body text")]
        [Test]
        [Timeout(30_000)]
        public async Task GetTextAsyncShouldReturnBody()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            string expected = "<html>Hello, Wave 41!</html>";
            Server.SetRoute("/direct-body-text.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync(expected);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IResponse> waitTask = page.WaitForResponseAsync(r => r.Url.Contains("/direct-body-text.html", StringComparison.Ordinal));
            await page.GoToAsync($"{TestConstants.ServerUrl}/direct-body-text.html").ConfigureAwait(false);
            IResponse response = await waitTask.ConfigureAwait(false);

            string text = await response.GetTextAsync().ConfigureAwait(false);
            Assert.That(text, Is.EqualTo(expected));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo(expected));
            byte[] bytes = await response.GetBodyAsync().ConfigureAwait(false);
            Assert.That(Encoding.UTF8.GetString(bytes), Is.EqualTo(expected));
            Assert.That(await response.BodyAsync().ConfigureAwait(false), Is.EqualTo(bytes));
            string finished = await response.GetFinishedAsync().ConfigureAwait(false);
            Assert.That(finished, Is.Empty);
            Assert.That(await response.FinishedAsync().ConfigureAwait(false), Is.Empty);
        }

        [PlaywrightTest("page-network-request.spec.ts", "should parse json body")]
        [Test]
        [Timeout(30_000)]
        public async Task GetJsonAsyncShouldParseBody()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/direct-body.json", context =>
            {
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync("{\"foo\":42}");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IResponse> waitTask = page.WaitForResponseAsync(r => r.Url.Contains("/direct-body.json", StringComparison.Ordinal));
            await page.GoToAsync($"{TestConstants.ServerUrl}/direct-body.json").ConfigureAwait(false);
            IResponse response = await waitTask.ConfigureAwait(false);

            using JsonDocument document = await response.GetJsonAsync().ConfigureAwait(false);
            Assert.That(document.RootElement.GetProperty("foo").GetInt32(), Is.EqualTo(42));
            using JsonDocument alias = await response.GetJsonAsync().ConfigureAwait(false);
            Assert.That(alias.RootElement.GetProperty("foo").GetInt32(), Is.EqualTo(42));
        }

        [PlaywrightTest("page-network-request.spec.ts", "JsonAsync T aliases GetJsonAsync T")]
        [Test]
        [Timeout(30_000)]
        public async Task JsonAsyncTShouldAliasGetJsonAsyncT()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/direct-body-t.json", context =>
            {
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync("{\"foo\":42}");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IResponse> waitTask = page.WaitForResponseAsync(r => r.Url.Contains("/direct-body-t.json", StringComparison.Ordinal));
            await page.GoToAsync($"{TestConstants.ServerUrl}/direct-body-t.json").ConfigureAwait(false);
            IResponse response = await waitTask.ConfigureAwait(false);

            Dictionary<string, JsonElement> viaGet = await response.GetJsonAsync<Dictionary<string, JsonElement>>().ConfigureAwait(false);
            Dictionary<string, JsonElement> viaAlias = await response.JsonAsync<Dictionary<string, JsonElement>>().ConfigureAwait(false);
            Assert.That(viaGet["foo"].GetInt32(), Is.EqualTo(42));
            Assert.That(viaAlias["foo"].GetInt32(), Is.EqualTo(42));
        }

        [PlaywrightTest("page-network-request.spec.ts", "should expose post data buffer and json")]
        [Test]
        [Timeout(30_000)]
        public async Task PostDataBufferAndPayloadAsJsonShouldRoundTrip()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/direct-post-json", context =>
            {
                context.Response.ContentType = "text/plain";
                return context.Response.WriteAsync("ok");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Task<IRequest> waitTask = page.WaitForRequestAsync(r => r.Url.Contains("/direct-post-json", StringComparison.Ordinal));
            await page.EvaluateAsync(
                "fetch('/direct-post-json', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ n: 7 }) })")
                .ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);

            Assert.That(request.PostData, Does.Contain("\"n\":7").Or.Contain("\"n\": 7"));
            Assert.That(request.PostDataBuffer, Is.Not.Null);
            Assert.That(Encoding.UTF8.GetString(request.PostDataBuffer), Is.EqualTo(request.PostData));
            using JsonDocument payload = request.GetPayloadAsJson();
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.RootElement.GetProperty("n").GetInt32(), Is.EqualTo(7));
            using JsonDocument alias = request.GetPayloadAsJson();
            Assert.That(alias, Is.Not.Null);
            Assert.That(alias.RootElement.GetProperty("n").GetInt32(), Is.EqualTo(7));
        }

        [PlaywrightTest("page-network-request.spec.ts", "should report redirectedFrom")]
        [Test]
        [Timeout(30_000)]
        public async Task RedirectedFromShouldPointAtOriginalRequest()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRedirect("/direct-redir", "/direct-redir-dest.html");
            Server.SetRoute("/direct-redir-dest.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync("<html><body>dest</body></html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> waitTask = page.WaitForRequestAsync(r => r.Url.Contains("/direct-redir-dest.html", StringComparison.Ordinal));
            await page.GoToAsync($"{TestConstants.ServerUrl}/direct-redir").ConfigureAwait(false);
            IRequest dest = await waitTask.ConfigureAwait(false);

            Assert.That(dest.RedirectedFrom, Is.Not.Null);
            Assert.That(dest.RedirectedFrom.Url, Does.Contain("/direct-redir"));
            Assert.That(dest.RedirectedFrom.RedirectedTo, Is.Not.Null);
            Assert.That(dest.RedirectedFrom.RedirectedTo.Url, Does.Contain("/direct-redir-dest.html"));
        }
    }
}
