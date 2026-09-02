/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Integration tests for the final DirectElementHandle overloads: path-based
    /// <see cref="IElementHandle.SetInputFilesAsync(string, bool?, float?)"/>,
    /// multi-path <see cref="IElementHandle.SetInputFilesAsync(System.Collections.Generic.IEnumerable{string}, bool?, float?)"/>,
    /// element-handle <see cref="IElementHandle.SelectOptionAsync(IElementHandle, bool?, float?)"/>,
    /// and failure mode when a given path does not exist.
    /// </summary>
    [TestFixture]
    public class ElementHandleFinalOverloadsTests : PageTestEx
    {
        [PlaywrightTest("elementhandle-misc.spec.ts", "SetInputFilesAsync path uploads file")]
        [Test]
        [Timeout(30_000)]
        public async Task SetInputFilesAsyncPathUploadsFile()
        {
            BrowserLauncher.SkipUnlessChromium();

            string tempFile = Path.Combine(Path.GetTempPath(), $"playwright-upload-{Guid.NewGuid():N}.txt");
            byte[] content = Encoding.UTF8.GetBytes("hello direct upload");
            File.WriteAllBytes(tempFile, content);

            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);

                await page.GoToAsync("data:text/html,<input id='f' type='file' />").ConfigureAwait(false);
                IElementHandle handle = await page.QuerySelectorAsync("#f").ConfigureAwait(false);

                await handle.SetInputFilesAsync(tempFile).ConfigureAwait(false);

                string name = await page.EvaluateAsync<string>("document.querySelector('#f').files[0].name").ConfigureAwait(false);
                int size = await page.EvaluateAsync<int>("document.querySelector('#f').files[0].size").ConfigureAwait(false);

                Assert.That(name, Is.EqualTo(Path.GetFileName(tempFile)));
                Assert.That(size, Is.EqualTo(content.Length));
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [PlaywrightTest("elementhandle-misc.spec.ts", "SetInputFilesAsync multiple paths")]
        [Test]
        [Timeout(30_000)]
        public async Task SetInputFilesAsyncMultiplePaths()
        {
            BrowserLauncher.SkipUnlessChromium();

            string tempA = Path.Combine(Path.GetTempPath(), $"playwright-upload-a-{Guid.NewGuid():N}.txt");
            string tempB = Path.Combine(Path.GetTempPath(), $"playwright-upload-b-{Guid.NewGuid():N}.txt");
            File.WriteAllBytes(tempA, Encoding.UTF8.GetBytes("a"));
            File.WriteAllBytes(tempB, Encoding.UTF8.GetBytes("bb"));

            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);

                await page.GoToAsync("data:text/html,<input id='f' type='file' multiple />").ConfigureAwait(false);
                IElementHandle handle = await page.QuerySelectorAsync("#f").ConfigureAwait(false);

                await handle.SetInputFilesAsync(new[] { tempA, tempB }).ConfigureAwait(false);

                int count = await page.EvaluateAsync<int>("document.querySelector('#f').files.length").ConfigureAwait(false);
                string names = await page.EvaluateAsync<string>(
                    "Array.from(document.querySelector('#f').files).map(f => f.name).join(',')").ConfigureAwait(false);

                Assert.That(count, Is.EqualTo(2));
                Assert.That(names, Is.EqualTo($"{Path.GetFileName(tempA)},{Path.GetFileName(tempB)}"));
            }
            finally
            {
                if (File.Exists(tempA))
                {
                    File.Delete(tempA);
                }

                if (File.Exists(tempB))
                {
                    File.Delete(tempB);
                }
            }
        }

        [PlaywrightTest("elementhandle-misc.spec.ts", "SelectOptionAsync with element handle")]
        [Test]
        [Timeout(30_000)]
        public async Task SelectOptionAsyncWithElementHandle()
        {
            BrowserLauncher.SkipUnlessChromium();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(@"data:text/html,<select id='s'>
                <option value='a'>Alpha</option>
                <option value='b' id='opt-b'>Beta</option>
                <option value='c'>Gamma</option>
            </select>").ConfigureAwait(false);

            IElementHandle select = await page.QuerySelectorAsync("#s").ConfigureAwait(false);
            IElementHandle option = await page.QuerySelectorAsync("#opt-b").ConfigureAwait(false);

            System.Collections.Generic.IReadOnlyList<string> result = await select.SelectOptionAsync(option).ConfigureAwait(false);

            Assert.That(result, Is.EqualTo(new[] { "b" }));
            string value = await page.EvaluateAsync<string>("document.querySelector('#s').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("b"));
        }

        [PlaywrightTest("elementhandle-misc.spec.ts", "SetInputFilesAsync missing path throws")]
        [Test]
        [Timeout(30_000)]
        public async Task SetInputFilesAsyncMissingPathThrows()
        {
            BrowserLauncher.SkipUnlessChromium();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<input id='f' type='file' />").ConfigureAwait(false);
            IElementHandle handle = await page.QuerySelectorAsync("#f").ConfigureAwait(false);

            string missing = Path.Combine(Path.GetTempPath(), $"playwright-missing-{Guid.NewGuid():N}.txt");
            Assert.That(File.Exists(missing), Is.False);

            Assert.ThrowsAsync<FileNotFoundException>(() => handle.SetInputFilesAsync(missing));
        }

    }
}
