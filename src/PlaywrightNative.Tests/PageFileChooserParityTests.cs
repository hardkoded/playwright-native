/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-filechooser.spec.ts</c> parity for <see cref="IPage.FileChooser"/>
    /// and <see cref="IPage.WaitForFileChooserAsync"/>.
    /// File-level Android skip is Android-only and is not applied.
    /// Electron-only skip on <c>should trigger listener added before navigation</c> is not applied.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class PageFileChooserParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19333;
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
                    CrossProcessPrefix = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
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

        private static async Task<IFrame> AttachFrameAsync(IPage page, string name, string url)
        {
            string nameJson = System.Text.Json.JsonSerializer.Serialize(name);
            string urlJson = System.Text.Json.JsonSerializer.Serialize(url);
            string script =
                "(() => { const f = document.createElement('iframe'); f.name = " +
                nameJson +
                "; f.id = " +
                nameJson +
                "; f.src = " +
                urlJson +
                "; document.body.appendChild(f); })()";
            await page.EvaluateAsync<object>(script).ConfigureAwait(false);

            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                IFrame named = page.Frame(name);
                if (named != null)
                {
                    return named;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.Fail("Timed out waiting for frame " + name);
            return null;
        }

        private static IFrame FirstChild(IFrame frame)
        {
            foreach (IFrame child in frame.ChildFrames)
            {
                return child;
            }

            return null;
        }

        private static async Task<IFileChooser> WaitAndClickAsync(IPage page, string selector)
        {
            Task<IFileChooser> waitTask = page.WaitForFileChooserAsync();
            await page.ClickAsync(selector, new() { NoWaitAfter = true }).ConfigureAwait(false);
            return await waitTask.ConfigureAwait(false);
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should upload multiple large files")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldUploadMultipleLargeFiles()
        {
            const int filesCount = 10;
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/fileupload-multi.html").ConfigureAwait(false);

            string uploadFile = Path.Combine(Path.GetTempPath(), "50MB_1.zip");
            byte[] chunk = Encoding.UTF8.GetBytes(new string('A', 1024));
            using (FileStream stream = File.OpenWrite(uploadFile))
            {
                for (int i = 0; i < 49 * 1024; i++)
                {
                    await stream.WriteAsync(chunk).ConfigureAwait(false);
                }
            }

            ILocator input = page.Locator("input[type=\"file\"]");
            List<string> uploadFiles = new List<string> { uploadFile };
            for (int i = 2; i <= filesCount; i++)
            {
                string dstFile = Path.Combine(Path.GetTempPath(), "50MB_" + i.ToString(CultureInfo.InvariantCulture) + ".zip");
                File.Copy(uploadFile, dstFile, overwrite: true);
                uploadFiles.Add(dstFile);
            }

            try
            {
                Task<IFileChooser> chooserTask = page.WaitForFileChooserAsync();
                await input.ClickAsync(new() { NoWaitAfter = true }).ConfigureAwait(false);
                IFileChooser fileChooser = await chooserTask.ConfigureAwait(false);
                await fileChooser.SetFilesAsync(uploadFiles).ConfigureAwait(false);
                int filesLen = await page.EvaluateAsync<int>("document.getElementsByTagName(\"input\")[0].files.length").ConfigureAwait(false);
                Assert.That(fileChooser.IsMultiple, Is.True);
                Assert.That(filesLen, Is.EqualTo(filesCount));
            }
            finally
            {
                foreach (string path in uploadFiles)
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
            }
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should emit event once")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEmitEventOnce()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=file>").ConfigureAwait(false);

            TaskCompletionSource<IFileChooser> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            void Handler(object sender, IFileChooser chooser)
            {
                page.FileChooser -= Handler;
                tcs.TrySetResult(chooser);
            }

            page.FileChooser += Handler;
            await page.ClickAsync("input", new() { NoWaitAfter = true }).ConfigureAwait(false);
            IFileChooser result = await tcs.Task.ConfigureAwait(false);
            Assert.That(result, Is.Not.Null);
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should emit event via prepend")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEmitEventViaPrepend()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=file>").ConfigureAwait(false);

            TaskCompletionSource<IFileChooser> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.FileChooser += (_, chooser) => tcs.TrySetResult(chooser);
            await page.ClickAsync("input", new() { NoWaitAfter = true }).ConfigureAwait(false);
            IFileChooser result = await tcs.Task.ConfigureAwait(false);
            Assert.That(result, Is.Not.Null);
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should emit event for iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEmitEventForIframe()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame frame = await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            await frame.SetContentAsync("<input type=file>").ConfigureAwait(false);

            TaskCompletionSource<IFileChooser> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            void Handler(object sender, IFileChooser chooser)
            {
                page.FileChooser -= Handler;
                tcs.TrySetResult(chooser);
            }

            page.FileChooser += Handler;
            await frame.ClickAsync("input", new() { NoWaitAfter = true }).ConfigureAwait(false);
            IFileChooser result = await tcs.Task.ConfigureAwait(false);
            Assert.That(result, Is.Not.Null);
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should emit event on/off")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEmitEventOnOff()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=file>").ConfigureAwait(false);

            TaskCompletionSource<IFileChooser> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            void Listener(object sender, IFileChooser chooser)
            {
                page.FileChooser -= Listener;
                tcs.TrySetResult(chooser);
            }

            page.FileChooser += Listener;
            await page.ClickAsync("input", new() { NoWaitAfter = true }).ConfigureAwait(false);
            IFileChooser result = await tcs.Task.ConfigureAwait(false);
            Assert.That(result, Is.Not.Null);
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should emit event addListener/removeListener")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEmitEventAddListenerRemoveListener()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=file>").ConfigureAwait(false);

            TaskCompletionSource<IFileChooser> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            void Listener(object sender, IFileChooser chooser)
            {
                page.FileChooser -= Listener;
                tcs.TrySetResult(chooser);
            }

            page.FileChooser += Listener;
            await page.ClickAsync("input", new() { NoWaitAfter = true }).ConfigureAwait(false);
            IFileChooser result = await tcs.Task.ConfigureAwait(false);
            Assert.That(result, Is.Not.Null);
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should work when file input is attached to DOM")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWhenFileInputIsAttachedToDOM()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=file>").ConfigureAwait(false);
            IFileChooser chooser = await WaitAndClickAsync(page, "input").ConfigureAwait(false);
            Assert.That(chooser, Is.Not.Null);
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should work when file input is not attached to DOM")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWhenFileInputIsNotAttachedToDOM()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            async Task SetFilesAsync()
            {
                IFileChooser chooser = await page.WaitForFileChooserAsync().ConfigureAwait(false);
                await chooser.SetFilesAsync(TestConstants.FileToUpload).ConfigureAwait(false);
            }

            Task setFiles = SetFilesAsync();
            string content = await page.EvaluateAsync<string>(@"(async () => {
                const el = document.createElement('input');
                el.type = 'file';
                el.click();
                await new Promise(x => el.oninput = x);
                const reader = new FileReader();
                const promise = new Promise(fulfill => reader.onload = fulfill);
                reader.readAsText(el.files[0]);
                return promise.then(() => reader.result);
            })()").ConfigureAwait(false);
            await setFiles.ConfigureAwait(false);
            Assert.That(content, Is.EqualTo("contents of the file"));
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should not throw when filechooser belongs to iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotThrowWhenFilechooserBelongsToIframe()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);
            IFrame frame = FirstChild(page.MainFrame);
            Assert.That(frame, Is.Not.Null);
            await frame.SetContentAsync(@"
    <div>Click me</div>
    <script>
      document.querySelector('div').addEventListener('click', () => {
        const input = document.createElement('input');
        input.type = 'file';
        input.click();
        window.parent.__done = true;
      });
    </script>
  ").ConfigureAwait(false);

            Task<IFileChooser> chooserTask = page.WaitForFileChooserAsync();
            await frame.ClickAsync("div", new() { NoWaitAfter = true }).ConfigureAwait(false);
            await chooserTask.ConfigureAwait(false);
            await page.WaitForFunctionAsync("() => window.__done").ConfigureAwait(false);
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should not throw when frame is detached immediately")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotThrowWhenFrameIsDetachedImmediately()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);
            IFrame frame = FirstChild(page.MainFrame);
            Assert.That(frame, Is.Not.Null);
            await frame.SetContentAsync(@"
    <div>Click me</div>
    <script>
      document.querySelector('div').addEventListener('click', () => {
        const input = document.createElement('input');
        input.type = 'file';
        input.click();
        window.parent.__done = true;
        const iframe = window.parent.document.querySelector('iframe');
        iframe.remove();
      });
    </script>
  ").ConfigureAwait(false);

            page.FileChooser += (_, _) => { };
            await frame.ClickAsync("div", new() { NoWaitAfter = true }).ConfigureAwait(false);
            await page.WaitForFunctionAsync("() => window.__done").ConfigureAwait(false);
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should respect timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRespectTimeout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Assert.ThrowsAsync<TimeoutException>(
                async () => await page.WaitForFileChooserAsync(new() { Timeout = 1 }).ConfigureAwait(false));
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should respect default timeout when there is no custom timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRespectDefaultTimeoutWhenThereIsNoCustomTimeout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            page.SetDefaultTimeout(1);
            Assert.ThrowsAsync<TimeoutException>(
                async () => await page.WaitForFileChooserAsync().ConfigureAwait(false));
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should prioritize exact timeout over default timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldPrioritizeExactTimeoutOverDefaultTimeout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            page.SetDefaultTimeout(0);
            Assert.ThrowsAsync<TimeoutException>(
                async () => await page.WaitForFileChooserAsync(new() { Timeout = 1 }).ConfigureAwait(false));
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should work with no timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithNoTimeout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IFileChooser> waitTask = page.WaitForFileChooserAsync(new() { Timeout = 0 });
            await page.EvaluateAsync<object>(@"(() => {
                setTimeout(() => {
                    const el = document.createElement('input');
                    el.type = 'file';
                    el.click();
                }, 50);
            })()").ConfigureAwait(false);
            IFileChooser chooser = await waitTask.ConfigureAwait(false);
            Assert.That(chooser, Is.Not.Null);
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should abort with signal")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAbortWithSignal()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            using CancellationTokenSource cts = new();
            Task<IFileChooser> promise = page.WaitForFileChooserAsync(new PageWaitForFileChooserOptions { CancellationToken = cts.Token });
            cts.Cancel();
            Assert.ThrowsAsync<OperationCanceledException>(
                async () => await promise.ConfigureAwait(false));
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should return the same file chooser when there are many watchdogs simultaneously")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnTheSameFileChooserWhenThereAreManyWatchdogsSimultaneously()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=file>").ConfigureAwait(false);
            Task<IFileChooser> first = page.WaitForFileChooserAsync();
            Task<IFileChooser> second = page.WaitForFileChooserAsync();
            await page.EvalOnSelectorAsync<object>("input", "input => input.click()").ConfigureAwait(false);
            IFileChooser fileChooser1 = await first.ConfigureAwait(false);
            IFileChooser fileChooser2 = await second.ConfigureAwait(false);
            Assert.That(fileChooser1, Is.SameAs(fileChooser2));
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should accept single file")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAcceptSingleFile()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=file oninput='javascript:console.timeStamp()'>").ConfigureAwait(false);
            IFileChooser fileChooser = await WaitAndClickAsync(page, "input").ConfigureAwait(false);
            Assert.That(fileChooser.Page, Is.SameAs(page));
            Assert.That(fileChooser.Element, Is.Not.Null);
            await fileChooser.SetFilesAsync(TestConstants.FileToUpload).ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<int>("input", "input => input.files.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAsync<string>("input", "input => input.files[0].name").ConfigureAwait(false), Is.EqualTo("file-to-upload.txt"));
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should not trim big uploaded files")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotTrimBigUploadedFiles()
        {
            Assert.That(Server, Is.Not.Null);
            const int dataSize = 1 << 20;
            TaskCompletionSource<long> sizeTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Server.SetRoute("/upload", async context =>
            {
                IFormCollection form = await context.Request.ReadFormAsync().ConfigureAwait(false);
                IFormFile file = form.Files["file"];
                sizeTcs.TrySetResult(file == null ? 0 : file.Length);
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string script =
                "(async () => {" +
                "  const size = " + dataSize.ToString(CultureInfo.InvariantCulture) + ";" +
                "  const body = new FormData();" +
                "  body.set('file', new Blob([new Uint8Array(size)]));" +
                "  await fetch('/upload', { method: 'POST', body });" +
                "})()";
            await Task.WhenAll(
                page.EvaluateAsync<object>(script),
                sizeTcs.Task).ConfigureAwait(false);
            Assert.That(sizeTcs.Task.Result, Is.EqualTo(dataSize));
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should be able to read selected file")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldBeAbleToReadSelectedFile()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=file>").ConfigureAwait(false);

            async Task SetFilesAsync()
            {
                IFileChooser fileChooser = await page.WaitForFileChooserAsync().ConfigureAwait(false);
                await fileChooser.SetFilesAsync(TestConstants.FileToUpload).ConfigureAwait(false);
            }

            Task setFiles = SetFilesAsync();
            string content = await page.EvalOnSelectorAsync<string>("input", @"async picker => {
                picker.click();
                await new Promise(x => picker.oninput = x);
                const reader = new FileReader();
                const promise = new Promise(fulfill => reader.onload = fulfill);
                reader.readAsText(picker.files[0]);
                return promise.then(() => reader.result);
            }").ConfigureAwait(false);
            await setFiles.ConfigureAwait(false);
            Assert.That(content, Is.EqualTo("contents of the file"));
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should be able to reset selected files with empty file list")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldBeAbleToResetSelectedFilesWithEmptyFileList()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=file>").ConfigureAwait(false);

            async Task SetFilesAsync(IEnumerable<string> files)
            {
                IFileChooser fileChooser = await page.WaitForFileChooserAsync().ConfigureAwait(false);
                await fileChooser.SetFilesAsync(files).ConfigureAwait(false);
            }

            Task setFirst = SetFilesAsync(new[] { TestConstants.FileToUpload });
            int fileLength1 = await page.EvalOnSelectorAsync<int>("input", @"async picker => {
                picker.click();
                await new Promise(x => picker.oninput = x);
                return picker.files.length;
            }").ConfigureAwait(false);
            await setFirst.ConfigureAwait(false);
            Assert.That(fileLength1, Is.EqualTo(1));

            Task setSecond = SetFilesAsync(Array.Empty<string>());
            int fileLength2 = await page.EvalOnSelectorAsync<int>("input", @"async picker => {
                picker.click();
                await new Promise(x => picker.oninput = x);
                return picker.files.length;
            }").ConfigureAwait(false);
            await setSecond.ConfigureAwait(false);
            Assert.That(fileLength2, Is.EqualTo(0));
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should work for single file pick")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForSingleFilePick()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=file>").ConfigureAwait(false);
            IFileChooser fileChooser = await WaitAndClickAsync(page, "input").ConfigureAwait(false);
            Assert.That(fileChooser.IsMultiple, Is.False);
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should work for \"multiple\"")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForMultiple()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input multiple type=file>").ConfigureAwait(false);
            IFileChooser fileChooser = await WaitAndClickAsync(page, "input").ConfigureAwait(false);
            Assert.That(fileChooser.IsMultiple, Is.True);
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should work for \"webkitdirectory\"")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForWebkitdirectory()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input multiple webkitdirectory type=file>").ConfigureAwait(false);
            IFileChooser fileChooser = await WaitAndClickAsync(page, "input").ConfigureAwait(false);
            Assert.That(fileChooser.IsMultiple, Is.True);
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should emit event after navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEmitEventAfterNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            List<string> logs = new List<string>();
            page.FileChooser += (_, _) => logs.Add("filechooser");
            await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            await page.SetContentAsync("<input type=file>").ConfigureAwait(false);
            await WaitAndClickAsync(page, "input").ConfigureAwait(false);
            await page.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            await page.SetContentAsync("<input type=file>").ConfigureAwait(false);
            await WaitAndClickAsync(page, "input").ConfigureAwait(false);
            Assert.That(logs, Is.EqualTo(new[] { "filechooser", "filechooser" }));
        }

        [PlaywrightTest("page-filechooser.spec.ts", "should trigger listener added before navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTriggerListenerAddedBeforeNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<IFileChooser> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            void Handler(object sender, IFileChooser chooser)
            {
                page.FileChooser -= Handler;
                tcs.TrySetResult(chooser);
            }

            page.FileChooser += Handler;
            await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            await page.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            await page.SetContentAsync("<input type=file>").ConfigureAwait(false);
            await page.ClickAsync("input", new() { NoWaitAfter = true }).ConfigureAwait(false);
            IFileChooser chooser = await tcs.Task.ConfigureAwait(false);
            Assert.That(chooser, Is.Not.Null);
        }
    }
}
