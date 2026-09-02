/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests.Chromium
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
