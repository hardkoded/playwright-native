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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.Chromium;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests.Chromium
{
    [TestFixture]
    public class CRRouteTests : CRTestBase
    {
        [PlaywrightTest("page-route.spec.ts", "should intercept @smoke")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIntercept()
        {
            bool intercepted = false;

            Server.SetRoute("/intercept.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync("<html><body>intercepted</body></html>");
            });

            await Page.RouteAsync("**/intercept.html", async route =>
            {
                intercepted = true;
                await route.ContinueAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

            await Page.GoToAsync(TestConstants.ServerUrl + "/intercept.html").ConfigureAwait(false);

            Assert.That(intercepted, Is.True);
        }

        [PlaywrightTest("page-route.spec.ts", "should receive fetch events")]
        [Test, Timeout(15_000)]
        public async Task ShouldReceiveFetchEvents()
        {
            // Test raw CDP Fetch: enable it, listen for requestPaused, manually continue.
            var fetchEvents = new List<string>();
            object fetchEventsGate = new object();
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            Page.Session.MessageReceived += (method, param) =>
            {
                lock (fetchEventsGate)
                {
                    fetchEvents.Add(method);
                }

                if (method == "Fetch.requestPaused" && param.HasValue)
                {
                    string interceptionId = param.Value.GetProperty("requestId").GetString();
                    // Continue the request manually
                    _ = Page.Session.SendAsync("Fetch.continueRequest", new { requestId = interceptionId });
                    tcs.TrySetResult(true);
                }
            };

            await Page.Session.SendAsync("Fetch.enable", new
            {
                handleAuthRequests = false,
                patterns = new[] { new { urlPattern = "*" } },
            }).ConfigureAwait(false);

            Server.SetRoute("/fetch-test.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync("<html>fetch-ok</html>");
            });

            // Navigate in background — it will block until we continue the paused request.
            Task navTask = Page.GoToAsync(TestConstants.ServerUrl + "/fetch-test.html", timeout: 10_000);

            // Wait for the Fetch event or timeout.
            using var cts = new System.Threading.CancellationTokenSource(5_000);
            cts.Token.Register(() => tcs.TrySetResult(false));
            bool received = await tcs.Task.ConfigureAwait(false);

            if (received)
            {
                await navTask.ConfigureAwait(false);
            }

            string[] snapshot;
            lock (fetchEventsGate)
            {
                snapshot = fetchEvents.ToArray();
            }

            string fetchEventsStr = string.Join(", ", snapshot.Where(e => e.StartsWith("Fetch.") || e.StartsWith("Network.request")));
            TestContext.Out.WriteLine($"Relevant events: {fetchEventsStr}");
            Assert.That(received, Is.True, $"Fetch.requestPaused not received. Events: [{fetchEventsStr}]");
        }

        [PlaywrightTest("page-route.spec.ts", "should fulfill with custom response")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFulfillWithCustomResponse()
        {
            await Page.RouteAsync("**/empty.html", async route =>
            {
                await route.FulfillAsync(200, "custom body", "text/html").ConfigureAwait(false);
            }).ConfigureAwait(false);

            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            string body = await Page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
            Assert.That(body, Is.EqualTo("custom body"));
        }

        [PlaywrightTest("page-route.spec.ts", "should abort request")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAbortRequest()
        {
            List<CRRequest> failedRequests = new();

            Page.RequestFailed += (sender, request) =>
            {
                failedRequests.Add(request);
            };

            await Page.RouteAsync("**/abort.html", async route =>
            {
                await route.AbortAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

            Server.SetRoute("/abort.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync("<html><body>should not reach</body></html>");
            });

            bool navigationFailed = false;
            try
            {
                await Page.GoToAsync(TestConstants.ServerUrl + "/abort.html").ConfigureAwait(false);
            }
            catch (NavigationException)
            {
                navigationFailed = true;
            }
            catch (PlaywrightNativeException)
            {
                navigationFailed = true;
            }

            Assert.That(navigationFailed, Is.True);
            Assert.That(failedRequests, Has.Count.GreaterThanOrEqualTo(1));
        }

        [PlaywrightTest("page-route.spec.ts", "should continue with modified headers")]
        [Test, Timeout(30_000)]
        public async Task ShouldContinueWithModifiedHeaders()
        {
            Server.SetRoute("/check-header.html", context =>
            {
                string customValue = context.Request.Headers["x-custom-header"];
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync(customValue ?? "no-header");
            });

            await Page.RouteAsync("**/check-header.html", async route =>
            {
                var headers = new Dictionary<string, string>(route.Request.Headers, StringComparer.OrdinalIgnoreCase)
                {
                    ["x-custom-header"] = "injected-value",
                };
                await route.ContinueAsync(headers: headers).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await Page.GoToAsync(TestConstants.ServerUrl + "/check-header.html").ConfigureAwait(false);

            string body = await Page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
            Assert.That(body, Is.EqualTo("injected-value"));
        }

        [PlaywrightTest("page-route.spec.ts", "should fulfill with custom status")]
        [Test, Timeout(30_000)]
        public async Task ShouldFulfillWithCustomStatus()
        {
            var responses = new List<CRResponse>();
            Page.ResponseReceived += (_, e) => responses.Add(e);

            await Page.RouteAsync("**/custom-status", async route =>
            {
                await route.FulfillAsync(
                    404,
                    "Not Found Custom",
                    "text/plain").ConfigureAwait(false);
            }).ConfigureAwait(false);

            await Page.GoToAsync(TestConstants.ServerUrl + "/custom-status").ConfigureAwait(false);

            string content = await Page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
            Assert.That(content, Is.EqualTo("Not Found Custom"));

            CRResponse interceptedResponse = responses.FirstOrDefault(r => r.Url.Contains("/custom-status"));
            Assert.That(interceptedResponse, Is.Not.Null, "ResponseReceived should have fired for the fulfilled request");
            Assert.That(interceptedResponse.Status, Is.EqualTo(404));
        }

        [PlaywrightTest("page-route.spec.ts", "should not intercept non matching urls")]
        [Test, Timeout(30_000)]
        public async Task ShouldNotInterceptNonMatchingUrls()
        {
            Server.SetRoute("/not-intercepted.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync("original-content");
            });

            bool intercepted = false;
            await Page.RouteAsync("**/other-page.html", async route =>
            {
                intercepted = true;
                await route.ContinueAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

            await Page.GoToAsync(TestConstants.ServerUrl + "/not-intercepted.html").ConfigureAwait(false);

            Assert.That(intercepted, Is.False);
            string content = await Page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
            Assert.That(content, Is.EqualTo("original-content"));

            // Confirm the handler is actually wired — navigate to a matching URL.
            Server.SetRoute("/other-page.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync("<html>other</html>");
            });

            await Page.GoToAsync(TestConstants.ServerUrl + "/other-page.html").ConfigureAwait(false);
            Assert.That(intercepted, Is.True, "handler should fire for matching URL");
        }

        [PlaywrightTest("page-route.spec.ts", "should fulfill with json body")]
        [Test, Timeout(30_000)]
        public async Task ShouldFulfillWithJsonBody()
        {
            await Page.RouteAsync("**/api/data", async route =>
            {
                await route.FulfillAsync(
                    200,
                    "{\"key\":\"value\"}",
                    "application/json").ConfigureAwait(false);
            }).ConfigureAwait(false);

            await Page.GoToAsync(TestConstants.ServerUrl + "/empty.html").ConfigureAwait(false);

            string json = await Page.EvaluateAsync<string>(
                "fetch('/api/data').then(r => r.text())").ConfigureAwait(false);
            Assert.That(json, Is.EqualTo("{\"key\":\"value\"}"));
        }

        [PlaywrightTest("page-route.spec.ts", "should intercept at context level")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInterceptAtContextLevel()
        {
            bool intercepted = false;
            await Context.RouteAsync("**/context-route.html", async route =>
            {
                intercepted = true;
                await route.FulfillAsync(200, "context-fulfilled", "text/html").ConfigureAwait(false);
            }).ConfigureAwait(false);

            await Page.GoToAsync(TestConstants.ServerUrl + "/context-route.html").ConfigureAwait(false);

            Assert.That(intercepted, Is.True);
            string content = await Page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
            Assert.That(content, Is.EqualTo("context-fulfilled"));
        }

        [PlaywrightTest("page-route.spec.ts", "Context route should apply to new pages")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContextRouteShouldApplyToNewPages()
        {
            await Context.RouteAsync("**/new-page-route.html", async route =>
            {
                await route.FulfillAsync(200, "routed-in-new-page", "text/html").ConfigureAwait(false);
            }).ConfigureAwait(false);

            CRPage newPage = await Context.NewPageAsync().ConfigureAwait(false);
            await newPage.InitializedTask.ConfigureAwait(false);

            await newPage.GoToAsync(TestConstants.ServerUrl + "/new-page-route.html").ConfigureAwait(false);

            string content = await newPage.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
            Assert.That(content, Is.EqualTo("routed-in-new-page"));

            await newPage.ClosePageAsync(runBeforeUnload: false).ConfigureAwait(false);
        }
    }
}
