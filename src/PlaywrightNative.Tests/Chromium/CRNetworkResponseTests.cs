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
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.Chromium;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests.Chromium
{
    [TestFixture]
    public class CRNetworkResponseTests : CRTestBase
    {
        [PlaywrightTest("page-network-response.spec.ts", "should report response status")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportResponseStatus()
        {
            Server.SetRoute("/status200.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync("<html>OK</html>");
            });

            TaskCompletionSource<CRResponse> responseTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            Page.ResponseReceived += (sender, response) =>
            {
                if (response.Url.Contains("/status200.html"))
                {
                    responseTcs.TrySetResult(response);
                }
            };

            await Page.GoToAsync(TestConstants.ServerUrl + "/status200.html").ConfigureAwait(false);

            CRResponse capturedResponse = await responseTcs.Task.ConfigureAwait(false);

            Assert.That(capturedResponse.Status, Is.EqualTo(200));
            Assert.That(capturedResponse.Ok, Is.True);
            Assert.That(capturedResponse.Url, Does.Contain("/status200.html"));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should report response headers")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportResponseHeaders()
        {
            Server.SetRoute("/custom-header.html", context =>
            {
                context.Response.ContentType = "text/html";
                context.Response.Headers["X-Custom-Header"] = "custom-value";
                return context.Response.WriteAsync("<html>headers</html>");
            });

            TaskCompletionSource<CRResponse> responseTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            Page.ResponseReceived += (sender, response) =>
            {
                if (response.Url.Contains("/custom-header.html"))
                {
                    responseTcs.TrySetResult(response);
                }
            };

            await Page.GoToAsync(TestConstants.ServerUrl + "/custom-header.html").ConfigureAwait(false);

            CRResponse capturedResponse = await responseTcs.Task.ConfigureAwait(false);

            Assert.That(capturedResponse.Headers, Does.ContainKey("x-custom-header"));
            Assert.That(capturedResponse.Headers["x-custom-header"], Is.EqualTo("custom-value"));
        }

        [PlaywrightTest("page-network-response.spec.ts", "should return response body")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnResponseBody()
        {
            string expectedBody = "<html>Hello, World!</html>";

            Server.SetRoute("/body-test.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync(expectedBody);
            });

            TaskCompletionSource<CRResponse> responseTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            Page.ResponseReceived += (sender, response) =>
            {
                if (response.Url.Contains("/body-test.html"))
                {
                    responseTcs.TrySetResult(response);
                }
            };

            await Page.GoToAsync(TestConstants.ServerUrl + "/body-test.html").ConfigureAwait(false);

            CRResponse capturedResponse = await responseTcs.Task.ConfigureAwait(false);

            string body = await capturedResponse.GetBodyAsync().ConfigureAwait(false);
            Assert.That(body, Is.EqualTo(expectedBody));
        }
    }
}
