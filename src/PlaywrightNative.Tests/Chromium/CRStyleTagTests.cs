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
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests.Chromium
{
    /// <summary>
    /// Integration tests for <c>CRPage.AddStyleTagAsync</c>.
    /// </summary>
    [TestFixture]
    public class CRStyleTagTests : CRTestBase
    {
        [PlaywrightTest("page-add-style-tag.spec.ts", "should add style with inline content")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAddStyleWithInlineContent()
        {
            await Page.GoToAsync("data:text/html,<div id='d'>text</div>").ConfigureAwait(false);

            await Page.AddStyleTagAsync(content: "#d { color: rgb(255, 0, 0); }").ConfigureAwait(false);

            string color = await Page.EvaluateAsync<string>(
                "getComputedStyle(document.getElementById('d')).color").ConfigureAwait(false);
            Assert.That(color, Is.EqualTo("rgb(255, 0, 0)"));
        }

        [PlaywrightTest("page-add-style-tag.spec.ts", "should add style with url")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAddStyleWithUrl()
        {
            Server.SetRoute("/red.css", context =>
            {
                context.Response.ContentType = "text/css";
                return context.Response.WriteAsync("#d { color: rgb(0, 128, 0); }");
            });

            await Page.GoToAsync(TestConstants.ServerUrl + "/empty.html").ConfigureAwait(false);
            await Page.SetContentAsync("<div id='d'>text</div>").ConfigureAwait(false);
            await Page.AddStyleTagAsync(url: TestConstants.ServerUrl + "/red.css").ConfigureAwait(false);

            string color = await Page.EvaluateAsync<string>(
                "getComputedStyle(document.getElementById('d')).color").ConfigureAwait(false);
            Assert.That(color, Is.EqualTo("rgb(0, 128, 0)"));
        }

        [PlaywrightTest("page-add-style-tag.spec.ts", "should throw when neither url nor content")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowWhenNeitherUrlNorContent()
        {
            System.ArgumentException ex = Assert.ThrowsAsync<System.ArgumentException>(
                () => Page.AddStyleTagAsync());
            Assert.That(ex.Message, Does.Contain("url").Or.Contain("content"));
        }
    }
}
