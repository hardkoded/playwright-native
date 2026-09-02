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
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.Chromium;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests.Chromium
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
