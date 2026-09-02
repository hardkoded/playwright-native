/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-drop.spec.ts</c> parity for <see cref="IPage.DropAsync"/>.
    /// File-level Android skip is Android-only and is not applied.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class PageDropTests : PageTestEx
    {
        private const string DropzoneSetup = @"
            <style>#dropzone { width: 300px; height: 200px; border: 2px dashed #888; }</style>
            <div id='dropzone'></div>
            <script>
              window.__dropInfo = null;
              const zone = document.getElementById('dropzone');
              zone.addEventListener('dragenter', e => e.preventDefault());
              zone.addEventListener('dragover', e => e.preventDefault());
              zone.addEventListener('drop', async e => {
                e.preventDefault();
                const files = [];
                for (const file of e.dataTransfer.files)
                  files.push({ name: file.name, type: file.type, size: file.size, text: await file.text() });
                const data = {};
                for (const t of e.dataTransfer.types) {
                  if (t !== 'Files')
                    data[t] = e.dataTransfer.getData(t);
                }
                window.__dropInfo = { files, data };
              });
            </script>";

        private static async Task<DropInfo> GetDropInfoAsync(IPage page)
        {
            await page.WaitForFunctionAsync("() => window.__dropInfo").ConfigureAwait(false);
            return await page.EvaluateAsync<DropInfo>("(() => window.__dropInfo)()").ConfigureAwait(false);
        }

        [PlaywrightTest("page-drop.spec.ts", "should drop a file payload")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDropAFilePayload()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(DropzoneSetup).ConfigureAwait(false);
            await page.DropAsync("#dropzone", new DropPayload
            {
                Files = new[]
                {
                    new FilePayload
                    {
                        Name = "note.txt",
                        MimeType = "text/plain",
                        Buffer = Encoding.UTF8.GetBytes("hello"),
                    },
                },
            }).ConfigureAwait(false);

            DropInfo info = await GetDropInfoAsync(page).ConfigureAwait(false);
            Assert.That(info.Files, Has.Exactly(1).Items);
            Assert.That(info.Files[0].Name, Is.EqualTo("note.txt"));
            Assert.That(info.Files[0].Type, Is.EqualTo("text/plain"));
            Assert.That(info.Files[0].Size, Is.EqualTo(5));
            Assert.That(info.Files[0].Text, Is.EqualTo("hello"));
            Assert.That(info.Data, Is.Empty);
        }

        [PlaywrightTest("page-drop.spec.ts", "should drop multiple file payloads")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDropMultipleFilePayloads()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(DropzoneSetup).ConfigureAwait(false);
            await page.DropAsync("#dropzone", new DropPayload
            {
                Files = new[]
                {
                    new FilePayload
                    {
                        Name = "a.txt",
                        MimeType = "text/plain",
                        Buffer = Encoding.UTF8.GetBytes("AAA"),
                    },
                    new FilePayload
                    {
                        Name = "b.txt",
                        MimeType = "text/plain",
                        Buffer = Encoding.UTF8.GetBytes("BB"),
                    },
                },
            }).ConfigureAwait(false);

            DropInfo info = await GetDropInfoAsync(page).ConfigureAwait(false);
            Assert.That(info.Files, Has.Exactly(2).Items);
            Assert.That(info.Files[0].Name, Is.EqualTo("a.txt"));
            Assert.That(info.Files[0].Text, Is.EqualTo("AAA"));
            Assert.That(info.Files[1].Name, Is.EqualTo("b.txt"));
            Assert.That(info.Files[1].Text, Is.EqualTo("BB"));
        }

        [PlaywrightTest("page-drop.spec.ts", "should drop a file by local path")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDropAFileByLocalPath()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(DropzoneSetup).ConfigureAwait(false);

            string filePath = Path.Combine(Path.GetTempPath(), "pw-drop-" + Guid.NewGuid().ToString("N"), "hello.txt");
            string directory = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(filePath, "path-content").ConfigureAwait(false);
            await page.DropAsync("#dropzone", new DropPayload
            {
                FilePaths = new[] { filePath },
            }).ConfigureAwait(false);

            DropInfo info = await GetDropInfoAsync(page).ConfigureAwait(false);
            Assert.That(info.Files, Has.Exactly(1).Items);
            Assert.That(info.Files[0].Name, Is.EqualTo("hello.txt"));
            Assert.That(info.Files[0].Text, Is.EqualTo("path-content"));
        }

        [PlaywrightTest("page-drop.spec.ts", "should drop clipboard-like data")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDropClipboardLikeData()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(DropzoneSetup).ConfigureAwait(false);
            await page.DropAsync("#dropzone", new DropPayload
            {
                Data = new Dictionary<string, string>
                {
                    ["text/plain"] = "hello world",
                    ["text/uri-list"] = "https://example.com",
                },
            }).ConfigureAwait(false);

            DropInfo info = await GetDropInfoAsync(page).ConfigureAwait(false);
            Assert.That(info.Files, Is.Empty);
            Assert.That(info.Data["text/plain"], Is.EqualTo("hello world"));
            Assert.That(info.Data["text/uri-list"], Is.EqualTo("https://example.com"));
        }

        [PlaywrightTest("page-drop.spec.ts", "should drop files and data together")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDropFilesAndDataTogether()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(DropzoneSetup).ConfigureAwait(false);
            await page.DropAsync("#dropzone", new DropPayload
            {
                Files = new[]
                {
                    new FilePayload
                    {
                        Name = "mix.txt",
                        MimeType = "text/plain",
                        Buffer = Encoding.UTF8.GetBytes("mix"),
                    },
                },
                Data = new Dictionary<string, string>
                {
                    ["text/plain"] = "label",
                },
            }).ConfigureAwait(false);

            DropInfo info = await GetDropInfoAsync(page).ConfigureAwait(false);
            Assert.That(info.Files[0].Text, Is.EqualTo("mix"));
            Assert.That(info.Data["text/plain"], Is.EqualTo("label"));
        }

        [PlaywrightTest("page-drop.spec.ts", "should throw when target does not accept drop")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowWhenTargetDoesNotAcceptDrop()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id='dropzone' style='width: 200px; height: 100px;'></div>")
                .ConfigureAwait(false);

            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.DropAsync("#dropzone", new DropPayload
                {
                    Data = new Dictionary<string, string> { ["text/plain"] = "nope" },
                }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Match("(?i)drop target did not accept the drop"));
        }

        [PlaywrightTest("page-drop.spec.ts", "should throw when neither files nor data provided")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowWhenNeitherFilesNorDataProvided()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(DropzoneSetup).ConfigureAwait(false);

            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.DropAsync("#dropzone", new DropPayload()));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("At least one of \"files\" or \"data\""));
        }

        private sealed class DropInfo
        {
            [JsonPropertyName("files")]
            public DropFile[] Files { get; set; } = Array.Empty<DropFile>();

            [JsonPropertyName("data")]
            public Dictionary<string, string> Data { get; set; } = new Dictionary<string, string>();
        }

        private sealed class DropFile
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("type")]
            public string Type { get; set; } = string.Empty;

            [JsonPropertyName("size")]
            public int Size { get; set; }

            [JsonPropertyName("text")]
            public string Text { get; set; } = string.Empty;
        }
    }
}
