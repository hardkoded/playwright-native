/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.Chromium;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests.Chromium
{
    /// <summary>
    /// Integration tests for <see cref="CRElementHandle.SetInputFilesAsync"/>.
    /// </summary>
    [TestFixture]
    public class CRFileInputTests : CRTestBase
    {
        [PlaywrightTest("page-set-input-files.spec.ts", "should set single file")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetSingleFile()
        {
            await Page.GoToAsync("data:text/html,<input id='f' type='file'>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#f").ConfigureAwait(false);
            await handle.SetInputFilesAsync(new FilePayload
            {
                Name = "hello.txt",
                MimeType = "text/plain",
                Buffer = Encoding.UTF8.GetBytes("hello world"),
            }).ConfigureAwait(false);

            string name = await Page.EvaluateAsync<string>("document.querySelector('#f').files[0].name").ConfigureAwait(false);
            string type = await Page.EvaluateAsync<string>("document.querySelector('#f').files[0].type").ConfigureAwait(false);
            int size = await Page.EvaluateAsync<int>("document.querySelector('#f').files[0].size").ConfigureAwait(false);

            Assert.That(name, Is.EqualTo("hello.txt"));
            Assert.That(type, Is.EqualTo("text/plain"));
            Assert.That(size, Is.EqualTo(11));
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should set multiple files")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetMultipleFiles()
        {
            await Page.GoToAsync("data:text/html,<input id='f' type='file' multiple>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#f").ConfigureAwait(false);
            await handle.SetInputFilesAsync(new[]
            {
                new FilePayload { Name = "a.txt", MimeType = "text/plain", Buffer = Encoding.UTF8.GetBytes("a") },
                new FilePayload { Name = "b.txt", MimeType = "text/plain", Buffer = Encoding.UTF8.GetBytes("bb") },
            }).ConfigureAwait(false);

            int count = await Page.EvaluateAsync<int>("document.querySelector('#f').files.length").ConfigureAwait(false);
            string names = await Page.EvaluateAsync<string>(
                "Array.from(document.querySelector('#f').files).map(f => f.name).join(',')").ConfigureAwait(false);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(names, Is.EqualTo("a.txt,b.txt"));
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should fire change event")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFireChangeEvent()
        {
            await Page.GoToAsync(@"data:text/html,<input id='f' type='file'>
                <script>
                window.events = [];
                document.getElementById('f').addEventListener('change', () => window.events.push('change'));
                document.getElementById('f').addEventListener('input', () => window.events.push('input'));
                </script>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#f").ConfigureAwait(false);
            await handle.SetInputFilesAsync(new FilePayload
            {
                Name = "x.txt",
                MimeType = "text/plain",
                Buffer = Encoding.UTF8.GetBytes("x"),
            }).ConfigureAwait(false);

            string json = await Page.EvaluateAsync<string>("JSON.stringify(window.events)").ConfigureAwait(false);
            Assert.That(json, Is.EqualTo("[\"input\",\"change\"]"));
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should throw on non file input")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowOnNonFileInput()
        {
            await Page.GoToAsync("data:text/html,<input id='t' type='text'>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#t").ConfigureAwait(false);
            PlaywrightSharpException ex = Assert.ThrowsAsync<PlaywrightSharpException>(
                () => handle.SetInputFilesAsync(new FilePayload { Name = "x.txt", MimeType = "text/plain", Buffer = new byte[] { 1 } }));
            Assert.That(ex.Message, Does.Contain("file"));
        }
    }
}
