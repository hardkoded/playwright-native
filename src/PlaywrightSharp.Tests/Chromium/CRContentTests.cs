/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests.Chromium
{
    /// <summary>
    /// Integration tests for <c>CRPage.SetContentAsync</c> and <c>CRPage.ContentAsync</c>.
    /// </summary>
    [TestFixture]
    public class CRContentTests : CRTestBase
    {
        [PlaywrightTest("page-set-content.spec.ts", "should set simple content")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetSimpleContent()
        {
            await Page.SetContentAsync("<div id='d'>hello</div>").ConfigureAwait(false);

            string text = await Page.EvaluateAsync<string>("document.querySelector('#d').textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("hello"));
        }

        [PlaywrightTest("page-set-content.spec.ts", "should set content with script")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetContentWithScript()
        {
            await Page.SetContentAsync(@"<div id='out'></div><script>
                document.getElementById('out').textContent = 'scripted';
            </script>").ConfigureAwait(false);

            string text = await Page.EvaluateAsync<string>("document.getElementById('out').textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("scripted"));
        }

        [PlaywrightTest("page-set-content.spec.ts", "ContentAsync should return set content")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContentAsyncShouldReturnSetContent()
        {
            await Page.SetContentAsync("<div id='d'>marker</div>").ConfigureAwait(false);

            string content = await Page.ContentAsync().ConfigureAwait(false);
            Assert.That(content, Does.Contain("<div id=\"d\">marker</div>"));
        }

        [PlaywrightTest("page-set-content.spec.ts", "should overwrite previous content")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOverwritePreviousContent()
        {
            await Page.SetContentAsync("<div>first</div>").ConfigureAwait(false);
            await Page.SetContentAsync("<div>second</div>").ConfigureAwait(false);

            string text = await Page.EvaluateAsync<string>("document.querySelector('div').textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("second"));
        }
    }
}
