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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.Chromium;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests.Chromium
{
    [TestFixture]
    public class CRNetworkEventTests : CRTestBase
    {
        [PlaywrightTest("page-event-network.spec.ts", "should fire request and response events")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFireRequestAndResponseEvents()
        {
            Server.SetRoute("/network-event-test.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync("<html>Network event test</html>");
            });

            List<CRRequest> requests = new();
            List<CRResponse> responses = new();

            Page.RequestCreated += (_, e) => requests.Add(e);
            Page.ResponseReceived += (_, e) => responses.Add(e);

            await Page.GoToAsync(TestConstants.ServerUrl + "/network-event-test.html").ConfigureAwait(false);

            CRRequest matchingRequest = requests.FirstOrDefault(r => r.Url.Contains("/network-event-test.html"));
            Assert.That(matchingRequest, Is.Not.Null, "Expected a RequestCreated event for the navigated URL");
            Assert.That(matchingRequest.Url, Does.Contain("/network-event-test.html"));

            CRResponse matchingResponse = responses.FirstOrDefault(r => r.Url.Contains("/network-event-test.html"));
            Assert.That(matchingResponse, Is.Not.Null, "Expected a ResponseReceived event for the navigated URL");
            Assert.That(matchingResponse.Url, Does.Contain("/network-event-test.html"));
        }

        [PlaywrightTest("page-event-network.spec.ts", "should fire request finished event")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFireRequestFinishedEvent()
        {
            Server.SetRoute("/finished-test.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync("<html>Finished</html>");
            });

            List<CRRequest> finishedRequests = new();

            Page.RequestFinished += (_, e) => finishedRequests.Add(e);

            await Page.GoToAsync(TestConstants.ServerUrl + "/finished-test.html").ConfigureAwait(false);
            await Task.Delay(500).ConfigureAwait(false);

            CRRequest matchingRequest = finishedRequests.FirstOrDefault(r => r.Url.Contains("/finished-test.html"));
            Assert.That(matchingRequest, Is.Not.Null, "Expected a RequestFinished event for the navigated URL");
            Assert.That(matchingRequest.Url, Does.Contain("/finished-test.html"));
        }

        [PlaywrightTest("page-event-network.spec.ts", "should fire request failed event")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFireRequestFailedEvent()
        {
            await Page.RouteAsync("**/will-fail", async route =>
            {
                await route.AbortAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

            List<CRRequest> failed = new();
            Page.RequestFailed += (_, e) => failed.Add(e);

            await Page.GoToAsync(TestConstants.ServerUrl + "/empty.html").ConfigureAwait(false);

            try
            {
                await Page.EvaluateAsync<string>("fetch('/will-fail').catch(e => 'failed')")
                    .ConfigureAwait(false);
            }
            catch
            {
                // Fetch may throw.
            }

            await Task.Delay(500).ConfigureAwait(false);

            CRRequest failedRequest = failed.FirstOrDefault(r => r.Url.Contains("/will-fail"));
            Assert.That(failedRequest, Is.Not.Null, "Expected a RequestFailed event for /will-fail");
            Assert.That(failedRequest.FailureText, Is.Not.Null.And.Not.Empty);
        }

        [PlaywrightTest("page-event-network.spec.ts", "should link response to request")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldLinkResponseToRequest()
        {
            Server.SetRoute("/link-test.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync("<html>Link test</html>");
            });

            List<CRResponse> responses = new();

            Page.ResponseReceived += (_, e) => responses.Add(e);

            await Page.GoToAsync(TestConstants.ServerUrl + "/link-test.html").ConfigureAwait(false);

            CRResponse matchingResponse = responses.FirstOrDefault(r => r.Url.Contains("/link-test.html"));
            Assert.That(matchingResponse, Is.Not.Null, "Expected a ResponseReceived event for the navigated URL");
            Assert.That(matchingResponse.Request, Is.Not.Null, "Response should have an associated request");
            Assert.That(matchingResponse.Request.Url, Does.Contain("/link-test.html"));
            Assert.That(matchingResponse.Request.Response, Is.SameAs(matchingResponse), "Request.Response should reference the same response instance");
        }

        [PlaywrightTest("page-event-network.spec.ts", "should handle redirects")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHandleRedirects()
        {
            Server.SetRoute("/redirect-target.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync("<html>Redirect target</html>");
            });

            Server.SetRedirect("/redirect-source", "/redirect-target.html");

            List<CRRequest> requests = new();

            Page.RequestCreated += (_, e) => requests.Add(e);

            await Page.GoToAsync(TestConstants.ServerUrl + "/redirect-source").ConfigureAwait(false);

            CRRequest targetRequest = requests.FirstOrDefault(r => r.Url.Contains("/redirect-target.html"));
            Assert.That(targetRequest, Is.Not.Null, "Expected a request for the redirect target");
            Assert.That(targetRequest.RedirectedFrom, Is.Not.Null, "Target request should have a RedirectedFrom");
            Assert.That(targetRequest.RedirectedFrom.Url, Does.Contain("/redirect-source"));
        }

        [PlaywrightTest("page-event-network.spec.ts", "should capture multiple requests")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCaptureMultipleRequests()
        {
            Server.SetRoute("/multi-request.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync(
                    "<html><head>" +
                    "<script src='/script1.js'></script>" +
                    "<script src='/script2.js'></script>" +
                    "</head><body>Multi</body></html>");
            });

            Server.SetRoute("/script1.js", context =>
            {
                context.Response.ContentType = "application/javascript";
                return context.Response.WriteAsync("// script 1");
            });

            Server.SetRoute("/script2.js", context =>
            {
                context.Response.ContentType = "application/javascript";
                return context.Response.WriteAsync("// script 2");
            });

            List<CRRequest> requests = new();

            Page.RequestCreated += (_, e) => requests.Add(e);

            await Page.GoToAsync(TestConstants.ServerUrl + "/multi-request.html").ConfigureAwait(false);
            await Task.Delay(500).ConfigureAwait(false);

            // Filter out favicon requests for a clean count.
            List<CRRequest> nonFaviconRequests = requests.Where(r => !r.IsFavicon).ToList();

            Assert.That(nonFaviconRequests, Has.Count.GreaterThanOrEqualTo(3), "Expected at least 3 requests: HTML + 2 scripts");
        }

        [PlaywrightTest("page-event-network.spec.ts", "Go to should wait for network idle")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GoToShouldWaitForNetworkIdle()
        {
            Server.SetRoute("/idle-page.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync(
                    "<html><script src='/delayed-script.js'></script><body>idle-test</body></html>");
            });
            Server.SetRoute("/delayed-script.js", async context =>
            {
                await Task.Delay(300).ConfigureAwait(false);
                context.Response.ContentType = "application/javascript";
                await context.Response.WriteAsync("window.__loaded = true;").ConfigureAwait(false);
            });

            await Page.GoToAsync(
                TestConstants.ServerUrl + "/idle-page.html",
                waitUntil: WaitUntilState.NetworkIdle).ConfigureAwait(false);

            bool loaded = await Page.EvaluateAsync<bool>("window.__loaded === true").ConfigureAwait(false);
            Assert.That(loaded, Is.True);
        }
    }
}
