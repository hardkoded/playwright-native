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
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-set-input-files.spec.ts</c> parity for
    /// <see cref="IPage.SetInputFilesAsync(string, string, bool?, float?, bool?)"/>
    /// and element/locator path uploads.
    /// Skipped (Android / frozen WebKit):
    /// <c>should upload a folder</c>,
    /// <c>should upload a folder and throw for multiple directories</c>,
    /// <c>should throw if a directory and files are passed</c>,
    /// <c>should throw when uploading a folder in a normal file upload input</c>,
    /// <c>should throw when uploading a file in a directory upload input</c>,
    /// <c>should upload large file</c>,
    /// <c>should upload large file with relative path</c>
    /// are Android-only skips here (frozen WebKit is not this stack).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageSetInputFilesParityTests : PageTestEx
    {
        private const int SlowTestTimeout = 180_000;

        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19780;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    string origin = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    Prefix = origin;
                    EmptyPage = origin + "/empty.html";
                    return;
                }
                catch (Exception)
                {
                }
            }

            Assert.Ignore("Test server is unavailable.");
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }
        }

        [TearDown]
        public void ResetServerRoutes()
        {
            Server?.Reset();
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should upload the file")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUploadTheFile()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/fileupload.html").ConfigureAwait(false);
                string filePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), Asset("file-to-upload.txt"));
                IElementHandle input = await page.QuerySelectorAsync("input").ConfigureAwait(false);
                await input.SetInputFilesAsync(filePath).ConfigureAwait(false);
                Assert.That(
                    await page.EvaluateAsync<string>("e => e.files[0].name", input).ConfigureAwait(false),
                    Is.EqualTo("file-to-upload.txt"));
                Assert.That(await ReadFirstFileTextAsync(page, input).ConfigureAwait(false), Is.EqualTo("contents of the file"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should upload a folder")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUploadAFolder()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/folderupload.html").ConfigureAwait(false);
                IElementHandle input = await page.QuerySelectorAsync("input").ConfigureAwait(false);
                string dir = Path.Combine(Path.GetTempPath(), "pw-set-input-" + Guid.NewGuid().ToString("N"), "file-upload-test");
                Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(Path.Combine(dir, "file1.txt"), "file1 content").ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(dir, "file2"), "file2 content").ConfigureAwait(false);
                Directory.CreateDirectory(Path.Combine(dir, "sub-dir"));
                await File.WriteAllTextAsync(Path.Combine(dir, "sub-dir", "really.txt"), "sub-dir file content").ConfigureAwait(false);
                try
                {
                    await input.SetInputFilesAsync(dir).ConfigureAwait(false);
                    string[] relativePaths = await page.EvaluateAsync<string[]>(
                        "e => [...e.files].map(f => f.webkitRelativePath)",
                        input).ConfigureAwait(false);
                    Assert.That(
                        relativePaths,
                        Is.EquivalentTo(new[]
                        {
                            "file-upload-test/sub-dir/really.txt",
                            "file-upload-test/file1.txt",
                            "file-upload-test/file2",
                        }));
                    for (int i = 0; i < relativePaths.Length; i++)
                    {
                        string content = await input.EvaluateAsync<string>(
                            @"(e, i) => {
                                const reader = new FileReader();
                                const promise = new Promise(fulfill => reader.onload = fulfill);
                                reader.readAsText(e.files[i]);
                                return promise.then(() => reader.result);
                            }",
                            i).ConfigureAwait(false);
                        string expected = await File.ReadAllTextAsync(Path.Combine(dir, "..", relativePaths[i])).ConfigureAwait(false);
                        Assert.That(content, Is.EqualTo(expected));
                    }
                }
                finally
                {
                    TryDeleteDirectory(Path.GetDirectoryName(dir));
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should upload a folder and throw for multiple directories")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUploadAFolderAndThrowForMultipleDirectories()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/folderupload.html").ConfigureAwait(false);
                IElementHandle input = await page.QuerySelectorAsync("input").ConfigureAwait(false);
                string dir = Path.Combine(Path.GetTempPath(), "pw-set-input-" + Guid.NewGuid().ToString("N"), "file-upload-test");
                Directory.CreateDirectory(Path.Combine(dir, "folder1"));
                await File.WriteAllTextAsync(Path.Combine(dir, "folder1", "file1.txt"), "file1 content").ConfigureAwait(false);
                Directory.CreateDirectory(Path.Combine(dir, "folder2"));
                await File.WriteAllTextAsync(Path.Combine(dir, "folder2", "file2.txt"), "file2 content").ConfigureAwait(false);
                try
                {
                    PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                        () => input.SetInputFilesAsync(new[]
                        {
                            Path.Combine(dir, "folder1"),
                            Path.Combine(dir, "folder2"),
                        }));
                    Assert.That(error.Message, Does.Contain("Multiple directories are not supported"));
                }
                finally
                {
                    TryDeleteDirectory(Path.GetDirectoryName(dir));
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should throw if a directory and files are passed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowIfADirectoryAndFilesArePassed()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/folderupload.html").ConfigureAwait(false);
                IElementHandle input = await page.QuerySelectorAsync("input").ConfigureAwait(false);
                string dir = Path.Combine(Path.GetTempPath(), "pw-set-input-" + Guid.NewGuid().ToString("N"), "file-upload-test");
                Directory.CreateDirectory(Path.Combine(dir, "folder1"));
                await File.WriteAllTextAsync(Path.Combine(dir, "folder1", "file1.txt"), "file1 content").ConfigureAwait(false);
                try
                {
                    PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                        () => input.SetInputFilesAsync(new[]
                        {
                            Path.Combine(dir, "folder1"),
                            Path.Combine(dir, "folder1", "file1.txt"),
                        }));
                    Assert.That(error.Message, Does.Contain("File paths must be all files or a single directory"));
                }
                finally
                {
                    TryDeleteDirectory(Path.GetDirectoryName(dir));
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should throw when uploading a folder in a normal file upload input")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowWhenUploadingAFolderInANormalFileUploadInput()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/fileupload.html").ConfigureAwait(false);
                IElementHandle input = await page.QuerySelectorAsync("input").ConfigureAwait(false);
                string dir = Path.Combine(Path.GetTempPath(), "pw-set-input-" + Guid.NewGuid().ToString("N"), "file-upload-test");
                Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(Path.Combine(dir, "file1.txt"), "file1 content").ConfigureAwait(false);
                try
                {
                    PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                        () => input.SetInputFilesAsync(dir));
                    Assert.That(error.Message, Does.Contain("File input does not support directories, pass individual files instead"));
                }
                finally
                {
                    TryDeleteDirectory(Path.GetDirectoryName(dir));
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should throw when uploading a file in a directory upload input")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowWhenUploadingAFileInADirectoryUploadInput()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/folderupload.html").ConfigureAwait(false);
                IElementHandle input = await page.QuerySelectorAsync("input").ConfigureAwait(false);
                PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                    () => input.SetInputFilesAsync(Asset("file to upload.txt")));
                Assert.That(error.Message, Does.Contain("[webkitdirectory] input requires passing a path to a directory"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should upload a file after popup")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUploadAFileAfterPopup()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/fileupload.html").ConfigureAwait(false);
                Task<IPage> popupTask = page.WaitForPopupAsync();
                await page.EvaluateAsync("() => { window['__popup'] = window.open('about:blank'); }").ConfigureAwait(false);
                IPage popup = await popupTask.ConfigureAwait(false);
                await popup.CloseAsync().ConfigureAwait(false);
                string filePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), Asset("file-to-upload.txt"));
                IElementHandle input = await page.QuerySelectorAsync("input").ConfigureAwait(false);
                await input.SetInputFilesAsync(filePath).ConfigureAwait(false);
                Assert.That(
                    await page.EvaluateAsync<string>("e => e.files[0].name", input).ConfigureAwait(false),
                    Is.EqualTo("file-to-upload.txt"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should upload large file")]
        [Test]
        [Timeout(SlowTestTimeout)]
        public async Task ShouldUploadLargeFile()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/fileupload.html").ConfigureAwait(false);
                string outputDir = Path.Combine(Path.GetTempPath(), "pw-set-input-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(outputDir);
                string uploadFile = Path.Combine(outputDir, "200MB.zip");
                CreateLargeFile(uploadFile);
                try
                {
                    ILocator input = page.Locator("input[type=\"file\"]");
                    IJSHandle events = await input.EvaluateHandleAsync(
                        @"e => {
                            const events = [];
                            e.addEventListener('input', () => events.push('input'));
                            e.addEventListener('change', () => events.push('change'));
                            return events;
                        }").ConfigureAwait(false);
                    await input.SetInputFilesAsync(uploadFile).ConfigureAwait(false);
                    Assert.That(
                        await input.EvaluateAsync<string>("e => e.files[0].name").ConfigureAwait(false),
                        Is.EqualTo("200MB.zip"));
                    Assert.That(
                        await events.EvaluateAsync<string[]>("e => e").ConfigureAwait(false),
                        Is.EqualTo(new[] { "input", "change" }));

                    Task<UploadedFile> serverFileTask = WaitForUploadedFileAsync("/upload", "file1");
                    Task clickTask = page.ClickAsync("input[type=submit]");
                    await Task.WhenAll(serverFileTask, clickTask).ConfigureAwait(false);
                    UploadedFile file1 = await serverFileTask.ConfigureAwait(false);
                    Assert.That(file1.FileName, Is.EqualTo("200MB.zip"));
                    Assert.That(file1.Length, Is.EqualTo(200L * 1024 * 1024));
                }
                finally
                {
                    TryDeleteDirectory(outputDir);
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should throw an error if the file does not exist")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowAnErrorIfTheFileDoesNotExist()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/fileupload.html").ConfigureAwait(false);
                IElementHandle input = await page.QuerySelectorAsync("input").ConfigureAwait(false);
                FileNotFoundException error = Assert.CatchAsync<FileNotFoundException>(
                    () => input.SetInputFilesAsync("i actually do not exist.txt"));
                Assert.That(error.Message, Does.Contain("ENOENT: no such file or directory"));
                Assert.That(error.Message, Does.Contain("i actually do not exist.txt"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should upload large file with relative path")]
        [Test]
        [Timeout(SlowTestTimeout)]
        public async Task ShouldUploadLargeFileWithRelativePath()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/fileupload.html").ConfigureAwait(false);
                string outputDir = Path.Combine(Path.GetTempPath(), "pw-set-input-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(outputDir);
                string uploadFile = Path.Combine(outputDir, "200MB.zip");
                CreateLargeFile(uploadFile);
                try
                {
                    ILocator input = page.Locator("input[type=\"file\"]");
                    IJSHandle events = await input.EvaluateHandleAsync(
                        @"e => {
                            const events = [];
                            e.addEventListener('input', () => events.push('input'));
                            e.addEventListener('change', () => events.push('change'));
                            return events;
                        }").ConfigureAwait(false);
                    string relativeUploadPath = Path.GetRelativePath(Directory.GetCurrentDirectory(), uploadFile);
                    Assert.That(Path.IsPathRooted(relativeUploadPath), Is.False);
                    await input.SetInputFilesAsync(relativeUploadPath).ConfigureAwait(false);
                    Assert.That(
                        await input.EvaluateAsync<string>("e => e.files[0].name").ConfigureAwait(false),
                        Is.EqualTo("200MB.zip"));
                    Assert.That(
                        await events.EvaluateAsync<string[]>("e => e").ConfigureAwait(false),
                        Is.EqualTo(new[] { "input", "change" }));

                    Task<UploadedFile> serverFileTask = WaitForUploadedFileAsync("/upload", "file1");
                    Task clickTask = page.ClickAsync("input[type=submit]");
                    await Task.WhenAll(serverFileTask, clickTask).ConfigureAwait(false);
                    UploadedFile file1 = await serverFileTask.ConfigureAwait(false);
                    Assert.That(file1.FileName, Is.EqualTo("200MB.zip"));
                    Assert.That(file1.Length, Is.EqualTo(200L * 1024 * 1024));
                }
                finally
                {
                    TryDeleteDirectory(outputDir);
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should upload the file with spaces in name")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUploadTheFileWithSpacesInName()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/input/fileupload.html").ConfigureAwait(false);
                string filePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), Asset("file to upload.txt"));
                IElementHandle input = await page.QuerySelectorAsync("input").ConfigureAwait(false);
                await input.SetInputFilesAsync(filePath).ConfigureAwait(false);
                Assert.That(
                    await page.EvaluateAsync<string>("e => e.files[0].name", input).ConfigureAwait(false),
                    Is.EqualTo("file to upload.txt"));
                Assert.That(await ReadFirstFileTextAsync(page, input).ConfigureAwait(false), Is.EqualTo("contents of the file"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should work")]
        [PlaywrightTest("page-set-input-files.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<input type=file>").ConfigureAwait(false);
                await page.SetInputFilesAsync("input", Asset("file-to-upload.txt")).ConfigureAwait(false);
                Assert.That(await page.EvalOnSelectorAsync<int>("input", "input => input.files.length").ConfigureAwait(false), Is.EqualTo(1));
                Assert.That(
                    await page.EvalOnSelectorAsync<string>("input", "input => input.files[0].name").ConfigureAwait(false),
                    Is.EqualTo("file-to-upload.txt"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should set from memory")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetFromMemory()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<input type=file>").ConfigureAwait(false);
                await page.SetInputFilesAsync(
                    "input",
                    new FilePayload
                    {
                        Name = "test.txt",
                        MimeType = "text/plain",
                        Buffer = Encoding.UTF8.GetBytes("this is a test"),
                    }).ConfigureAwait(false);
                Assert.That(await page.EvalOnSelectorAsync<int>("input", "input => input.files.length").ConfigureAwait(false), Is.EqualTo(1));
                Assert.That(
                    await page.EvalOnSelectorAsync<string>("input", "input => input.files[0].name").ConfigureAwait(false),
                    Is.EqualTo("test.txt"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should work with CSP")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithCsp()
        {
            EnsureServer();
            Server.SetCSP("/empty.html", "default-src \"none\"");
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.SetContentAsync("<input type=file>").ConfigureAwait(false);
                await page.SetInputFilesAsync("input", Asset("file-to-upload.txt")).ConfigureAwait(false);
                Assert.That(await page.EvalOnSelectorAsync<int>("input", "input => input.files.length").ConfigureAwait(false), Is.EqualTo(1));
                Assert.That(
                    await page.EvalOnSelectorAsync<string>("input", "input => input.files[0].name").ConfigureAwait(false),
                    Is.EqualTo("file-to-upload.txt"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should detect mime type")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDetectMimeType()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Task<IReadOnlyDictionary<string, UploadedFile>> uploaded = WaitForUploadedFormAsync("/upload");
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.SetContentAsync(
                    "<form action=\"/upload\" method=\"post\" enctype=\"multipart/form-data\">" +
                    "<input type=\"file\" name=\"file1\">" +
                    "<input type=\"file\" name=\"file2\">" +
                    "<input type=\"submit\" value=\"Submit\">" +
                    "</form>").ConfigureAwait(false);
                await (await page.QuerySelectorAsync("input[name=file1]").ConfigureAwait(false))
                    .SetInputFilesAsync(Asset("file-to-upload.txt")).ConfigureAwait(false);
                await (await page.QuerySelectorAsync("input[name=file2]").ConfigureAwait(false))
                    .SetInputFilesAsync(Asset("pptr.png")).ConfigureAwait(false);
                await Task.WhenAll(page.ClickAsync("input[type=submit]"), uploaded).ConfigureAwait(false);
                IReadOnlyDictionary<string, UploadedFile> files = await uploaded.ConfigureAwait(false);
                UploadedFile file1 = files["file1"];
                UploadedFile file2 = files["file2"];
                Assert.That(file1.FileName, Is.EqualTo("file-to-upload.txt"));
                Assert.That(file1.ContentType, Is.EqualTo("text/plain"));
                Assert.That(Encoding.UTF8.GetString(file1.Bytes), Is.EqualTo(await File.ReadAllTextAsync(Asset("file-to-upload.txt")).ConfigureAwait(false)));
                Assert.That(file2.FileName, Is.EqualTo("pptr.png"));
                Assert.That(file2.ContentType, Is.EqualTo("image/png"));
                Assert.That(file2.Bytes, Is.EqualTo(await File.ReadAllBytesAsync(Asset("pptr.png")).ConfigureAwait(false)));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should not trim big uploaded files")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotTrimBigUploadedFiles()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Task<IReadOnlyDictionary<string, UploadedFile>> uploaded = WaitForUploadedFormAsync("/upload");
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                int dataSize = 1 << 20;
                await Task.WhenAll(
                    page.EvaluateAsync(
                        @"(size) => {
                            const body = new FormData();
                            body.set('file', new Blob([new Uint8Array(size)]));
                            return fetch('/upload', { method: 'POST', body });
                        }",
                        dataSize),
                    uploaded).ConfigureAwait(false);
                IReadOnlyDictionary<string, UploadedFile> files = await uploaded.ConfigureAwait(false);
                Assert.That(files["file"].Length, Is.EqualTo(dataSize));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should emit input and change events")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmitInputAndChangeEvents()
        {
            await WithPageAsync(async page =>
            {
                List<string> events = new List<string>();
                await page.ExposeFunctionAsync(
                    "eventHandled",
                    (JsonElement e) => events.Add(e.GetProperty("type").GetString())).ConfigureAwait(false);
                await page.SetContentAsync(
                    "<input id=input type=file></input>" +
                    "<script>" +
                    "input.addEventListener('input', e => eventHandled({ type: e.type }));" +
                    "input.addEventListener('change', e => eventHandled({ type: e.type }));" +
                    "</script>").ConfigureAwait(false);
                await (await page.QuerySelectorAsync("input").ConfigureAwait(false))
                    .SetInputFilesAsync(Asset("file-to-upload.txt")).ConfigureAwait(false);
                Assert.That(events, Has.Count.EqualTo(2));
                Assert.That(events[0], Is.EqualTo("input"));
                Assert.That(events[1], Is.EqualTo("change"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "input event.composed should be true and cross shadow dom boundary")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task InputEventComposedShouldBeTrueAndCrossShadowDomBoundary()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.SetContentAsync(
                    "<body><script>" +
                    "const div = document.createElement('div');" +
                    "const shadowRoot = div.attachShadow({mode: 'open'});" +
                    "shadowRoot.innerHTML = '<input type=file></input>';" +
                    "document.body.appendChild(div);" +
                    "</script></body>").ConfigureAwait(false);
                await page.Locator("body").EvaluateAsync<object>(
                    @"select => {
                        window.firedBodyEvents = [];
                        for (const event of ['input', 'change']) {
                            select.addEventListener(event, e => {
                                window.firedBodyEvents.push(e.type + ':' + e.composed);
                            }, false);
                        }
                    }").ConfigureAwait(false);
                await page.Locator("input").EvaluateAsync<object>(
                    @"select => {
                        window.firedEvents = [];
                        for (const event of ['input', 'change']) {
                            select.addEventListener(event, e => {
                                window.firedEvents.push(e.type + ':' + e.composed);
                            }, false);
                        }
                    }").ConfigureAwait(false);
                await page.Locator("input").SetInputFilesAsync(new FilePayload
                {
                    Name = "test.txt",
                    MimeType = "text/plain",
                    Buffer = Encoding.UTF8.GetBytes("this is a test"),
                }).ConfigureAwait(false);
                Assert.That(
                    await page.EvaluateAsync<string[]>("() => window['firedEvents']").ConfigureAwait(false),
                    Is.EqualTo(new[] { "input:true", "change:false" }));
                Assert.That(
                    await page.EvaluateAsync<string[]>("() => window['firedBodyEvents']").ConfigureAwait(false),
                    Is.EqualTo(new[] { "input:true" }));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "input should trigger events when files changed second time")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task InputShouldTriggerEventsWhenFilesChangedSecondTime()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<input type=file multiple=true/>").ConfigureAwait(false);
                ILocator input = page.Locator("input");
                IJSHandle events = await input.EvaluateHandleAsync(
                    @"e => {
                        const events = [];
                        e.addEventListener('input', () => events.push('input'));
                        e.addEventListener('change', () => events.push('change'));
                        return events;
                    }").ConfigureAwait(false);
                await input.SetInputFilesAsync(Asset("file-to-upload.txt")).ConfigureAwait(false);
                Assert.That(
                    await input.EvaluateAsync<string>("e => e.files[0].name").ConfigureAwait(false),
                    Is.EqualTo("file-to-upload.txt"));
                Assert.That(
                    await events.EvaluateAsync<string[]>("e => e").ConfigureAwait(false),
                    Is.EqualTo(new[] { "input", "change" }));
                await events.EvaluateAsync("e => { e.length = 0; }").ConfigureAwait(false);
                await input.SetInputFilesAsync(Asset("pptr.png")).ConfigureAwait(false);
                Assert.That(
                    await input.EvaluateAsync<string>("e => e.files[0].name").ConfigureAwait(false),
                    Is.EqualTo("pptr.png"));
                Assert.That(
                    await events.EvaluateAsync<string[]>("e => e").ConfigureAwait(false),
                    Is.EqualTo(new[] { "input", "change" }));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should preserve lastModified timestamp")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPreserveLastModifiedTimestamp()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<input type=file multiple=true/>").ConfigureAwait(false);
                ILocator input = page.Locator("input");
                string[] files = { "file-to-upload.txt", "file-to-upload-2.txt" };
                await input.SetInputFilesAsync(new[] { Asset(files[0]), Asset(files[1]) }).ConfigureAwait(false);
                Assert.That(
                    await input.EvaluateAsync<string[]>("e => [...e.files].map(f => f.name)").ConfigureAwait(false),
                    Is.EqualTo(files));
                long[] timestamps = await input.EvaluateAsync<long[]>("e => [...e.files].map(f => f.lastModified)").ConfigureAwait(false);
                long[] expected = new long[files.Length];
                for (int i = 0; i < files.Length; i++)
                {
                    DateTime utc = File.GetLastWriteTimeUtc(Asset(files[i]));
                    expected[i] = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
                    Assert.That(
                        Math.Abs(timestamps[i] - expected[i]),
                        Is.LessThanOrEqualTo(1000),
                        "expected: " + string.Join(",", expected) + "; actual: " + string.Join(",", timestamps));
                }
            }).ConfigureAwait(false);
        }

        private static string Asset(string name) => TestUtils.GetWebServerFile(name);

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            Server.Reset();
        }

        private static async Task WithPageAsync(Func<IPage, Task> body)
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await body(page).ConfigureAwait(false);
        }

        private static async Task<string> ReadFirstFileTextAsync(IPage page, IElementHandle input)
        {
            return await page.EvaluateAsync<string>(
                @"e => {
                    const reader = new FileReader();
                    const promise = new Promise(fulfill => reader.onload = fulfill);
                    reader.readAsText(e.files[0]);
                    return promise.then(() => reader.result);
                }",
                input).ConfigureAwait(false);
        }

        private static void CreateLargeFile(string path)
        {
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            stream.SetLength(200L * 1024 * 1024);
        }

        private static Task<IReadOnlyDictionary<string, UploadedFile>> WaitForUploadedFormAsync(string path)
        {
            TaskCompletionSource<IReadOnlyDictionary<string, UploadedFile>> tcs =
                new TaskCompletionSource<IReadOnlyDictionary<string, UploadedFile>>(TaskCreationOptions.RunContinuationsAsynchronously);
            Server.SetRoute(path, async context =>
            {
                IReadOnlyDictionary<string, UploadedFile> files = await SnapshotFormFilesAsync(context.Request).ConfigureAwait(false);
                tcs.TrySetResult(files);
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync("okay").ConfigureAwait(false);
            });
            return tcs.Task;
        }

        private static async Task<UploadedFile> WaitForUploadedFileAsync(string path, string fieldName)
        {
            IReadOnlyDictionary<string, UploadedFile> files = await WaitForUploadedFormAsync(path).ConfigureAwait(false);
            return files[fieldName];
        }

        private static Task<IFormCollection> ReadFormAsync(HttpRequest request)
        {
            FormOptions options = new FormOptions
            {
                MultipartBodyLengthLimit = 512L * 1024 * 1024,
                ValueLengthLimit = int.MaxValue,
                MemoryBufferThreshold = 1024 * 1024,
            };
            request.HttpContext.Features.Set<IFormFeature>(new FormFeature(request, options));
            return request.ReadFormAsync();
        }

        private static async Task<IReadOnlyDictionary<string, UploadedFile>> SnapshotFormFilesAsync(HttpRequest request)
        {
            IFormCollection form = await ReadFormAsync(request).ConfigureAwait(false);
            Dictionary<string, UploadedFile> files = new Dictionary<string, UploadedFile>(StringComparer.Ordinal);
            foreach (IFormFile file in form.Files)
            {
                using Stream stream = file.OpenReadStream();
                using MemoryStream memory = new MemoryStream();
                await stream.CopyToAsync(memory).ConfigureAwait(false);
                files[file.Name] = new UploadedFile
                {
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    Length = file.Length,
                    Bytes = memory.ToArray(),
                };
            }

            return files;
        }

        private sealed class UploadedFile
        {
            public string FileName { get; set; }

            public string ContentType { get; set; }

            public long Length { get; set; }

            public byte[] Bytes { get; set; }
        }

        private static void TryDeleteDirectory(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
