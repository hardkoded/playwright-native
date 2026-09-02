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
