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
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.Chromium;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests.Chromium
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
            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(
                () => handle.SetInputFilesAsync(new FilePayload { Name = "x.txt", MimeType = "text/plain", Buffer = new byte[] { 1 } }));
            Assert.That(ex.Message, Does.Contain("file"));
        }
    }
}
