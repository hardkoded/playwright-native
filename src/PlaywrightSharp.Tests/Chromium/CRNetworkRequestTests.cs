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
using System;
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
    public class CRNetworkRequestTests : CRTestBase
    {
        [PlaywrightTest("page-network-request.spec.ts", "should report request url")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportRequestUrl()
        {
            TaskCompletionSource<CRRequest> requestTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            Page.RequestCreated += (sender, request) =>
            {
                if (request.Url.Contains("/empty.html"))
                {
                    requestTcs.TrySetResult(request);
                }
            };

            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            CRRequest capturedRequest = await requestTcs.Task.ConfigureAwait(false);

            Assert.That(capturedRequest.Url, Is.EqualTo(TestConstants.EmptyPage));
            Assert.That(capturedRequest.Method, Is.EqualTo("GET"));
            Assert.That(capturedRequest.IsNavigationRequest, Is.True);
        }

        [PlaywrightTest("page-network-request.spec.ts", "should report post data")]
        [Test, Timeout(30_000)]
        public async Task ShouldReportPostData()
        {
            Server.SetRoute("/post-endpoint", _ => Task.CompletedTask);

            var requests = new List<CRRequest>();

            Page.RequestCreated += (_, e) => requests.Add(e);

            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await Page.EvaluateAsync<string>("fetch('/post-endpoint', { method: 'POST', body: JSON.stringify({ foo: 'bar' }) }).then(r => r.text())").ConfigureAwait(false);

            await Task.Delay(500).ConfigureAwait(false);

            CRRequest postRequest = requests.First(r => r.Url.Contains("/post-endpoint"));

            Assert.That(postRequest.PostData, Does.Contain("foo"));
            Assert.That(postRequest.Method, Is.EqualTo("POST"));
        }

        [PlaywrightTest("page-network-request.spec.ts", "should report resource type")]
        [Test, Timeout(30_000)]
        public async Task ShouldReportResourceType()
        {
            Server.SetRoute("/resource-type.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync("<html><script src='/script.js'></script></html>");
            });

            Server.SetRoute("/script.js", context =>
            {
                context.Response.ContentType = "application/javascript";
                return context.Response.WriteAsync("console.log('hello')");
            });

            var requests = new List<CRRequest>();

            Page.RequestCreated += (_, e) => requests.Add(e);

            await Page.GoToAsync(TestConstants.ServerUrl + "/resource-type.html").ConfigureAwait(false);

            CRRequest documentRequest = requests.First(r => r.Url.Contains("/resource-type.html"));
            CRRequest scriptRequest = requests.First(r => r.Url.Contains("/script.js"));

            Assert.That(documentRequest.ResourceType, Is.EqualTo("Document"));
            Assert.That(scriptRequest.ResourceType, Is.EqualTo("Script"));
        }

        [PlaywrightTest("page-network-request.spec.ts", "should report request headers")]
        [Test, Timeout(30_000)]
        public async Task ShouldReportRequestHeaders()
        {
            TaskCompletionSource<CRRequest> requestTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            Page.RequestCreated += (_, request) =>
            {
                if (request.Url.Contains("/empty.html"))
                {
                    requestTcs.TrySetResult(request);
                }
            };

            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            CRRequest capturedRequest = await requestTcs.Task.ConfigureAwait(false);

            Assert.That(
                capturedRequest.Headers.Keys.Any(k => k.Equals("user-agent", StringComparison.OrdinalIgnoreCase)),
                Is.True);
        }

        [PlaywrightTest("page-network-request.spec.ts", "should report request frame")]
        [Test, Timeout(30_000)]
        public async Task ShouldReportRequestFrame()
        {
            TaskCompletionSource<CRRequest> requestTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            Page.RequestCreated += (_, request) =>
            {
                if (request.Url.Contains("/empty.html"))
                {
                    requestTcs.TrySetResult(request);
                }
            };

            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            CRRequest capturedRequest = await requestTcs.Task.ConfigureAwait(false);

            Assert.That(capturedRequest.Frame, Is.Not.Null);
            Assert.That(capturedRequest.Frame.FrameId, Is.EqualTo(Page.MainFrame.FrameId));
        }
    }
}
