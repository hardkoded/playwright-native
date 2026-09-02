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
    /// Integration tests for <c>CRPage.AddScriptTagAsync</c>.
    /// </summary>
    [TestFixture]
    public class CRScriptTagTests : CRTestBase
    {
        [PlaywrightTest("page-add-script-tag.spec.ts", "should add script with inline content")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAddScriptWithInlineContent()
        {
            await Page.GoToAsync("data:text/html,<div>placeholder</div>").ConfigureAwait(false);

            await Page.AddScriptTagAsync(content: "window.__marker = 42;").ConfigureAwait(false);

            int marker = await Page.EvaluateAsync<int>("window.__marker").ConfigureAwait(false);
            Assert.That(marker, Is.EqualTo(42));
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "should add script with url")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAddScriptWithUrl()
        {
            Server.SetRoute("/marker-script.js", context =>
            {
                context.Response.ContentType = "application/javascript";
                return context.Response.WriteAsync("window.__fromUrl = 'yes';");
            });

            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await Page.AddScriptTagAsync(url: TestConstants.ServerUrl + "/marker-script.js").ConfigureAwait(false);

            string v = await Page.EvaluateAsync<string>("window.__fromUrl").ConfigureAwait(false);
            Assert.That(v, Is.EqualTo("yes"));
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "should throw when neither url nor content")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowWhenNeitherUrlNorContent()
        {
            System.ArgumentException ex = Assert.ThrowsAsync<System.ArgumentException>(
                () => Page.AddScriptTagAsync());
            Assert.That(ex.Message, Does.Contain("url").Or.Contain("content"));
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "should throw when both url and content")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowWhenBothUrlAndContent()
        {
            System.ArgumentException ex = Assert.ThrowsAsync<System.ArgumentException>(
                () => Page.AddScriptTagAsync(url: "http://a", content: "x"));
            Assert.That(ex.Message, Does.Contain("not both").Or.Contain("one"));
        }
    }
}
