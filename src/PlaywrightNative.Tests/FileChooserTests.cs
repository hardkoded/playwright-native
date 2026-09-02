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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IPage.FileChooser"/> and
    /// <see cref="IPage.WaitForFileChooserAsync"/>.
    /// </summary>
    [TestFixture]
    public class FileChooserTests : PageTestEx
    {
        [PlaywrightTest("page-filechooser.spec.ts", "wait then click sets files")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSetFilesFromChooser()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=\"file\" id=\"f\">").ConfigureAwait(false);

            Task<IFileChooser> waitTask = page.WaitForFileChooserAsync();
            await page.ClickAsync("#f", new() { NoWaitAfter = true }).ConfigureAwait(false);
            IFileChooser chooser = await waitTask.ConfigureAwait(false);

            Assert.That(chooser, Is.Not.Null);
            Assert.That(chooser.Page, Is.SameAs(page));
            Assert.That(chooser.Element, Is.Not.Null);
            Assert.That(chooser.IsMultiple, Is.False);

            await chooser.SetFilesAsync(TestConstants.FileToUpload).ConfigureAwait(false);
            string name = await page.EvaluateAsync<string>("document.querySelector('#f').files[0].name").ConfigureAwait(false);
            Assert.That(name, Does.Contain("file-to-upload"));
        }

        [PlaywrightTest("page-filechooser.spec.ts", "RunAndWaitForFileChooserAsync waits for click")]
        [Test]
        [Timeout(30_000)]
        public async Task RunAndWaitForFileChooserAsyncShouldReturnTheChooser()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=\"file\" id=\"f\">").ConfigureAwait(false);

            IFileChooser chooser = await page.RunAndWaitForFileChooserAsync(
                () => page.ClickAsync("#f", new() { NoWaitAfter = true })).ConfigureAwait(false);

            Assert.That(chooser, Is.Not.Null);
            Assert.That(chooser.Page, Is.SameAs(page));
            await chooser.SetFilesAsync(TestConstants.FileToUpload).ConfigureAwait(false);
        }

        [PlaywrightTest("page-filechooser.spec.ts", "multiple input reports IsMultiple")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportMultiple()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=\"file\" id=\"f\" multiple>").ConfigureAwait(false);

            Task<IFileChooser> waitTask = page.WaitForFileChooserAsync();
            await page.ClickAsync("#f", new() { NoWaitAfter = true }).ConfigureAwait(false);
            IFileChooser chooser = await waitTask.ConfigureAwait(false);

            Assert.That(chooser.IsMultiple, Is.True);
        }

        [PlaywrightTest("page-filechooser.spec.ts", "WaitForFileChooserAsync honors a predicate")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForFileChooserShouldHonorPredicate()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=\"file\" id=\"f\" multiple>").ConfigureAwait(false);

            Task<IFileChooser> waitTask = page.WaitForFileChooserAsync(chooser => chooser.IsMultiple);
            await page.ClickAsync("#f", new() { NoWaitAfter = true }).ConfigureAwait(false);
            IFileChooser chooser = await waitTask.ConfigureAwait(false);

            Assert.That(chooser.IsMultiple, Is.True);
        }

        [PlaywrightTest("page-filechooser.spec.ts", "wait times out")]
        [Test]
        [Timeout(30_000)]
        public void ShouldTimeoutWaitingForFileChooser()
        {
            Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.WaitForFileChooserAsync(new() { Timeout = 200 }).ConfigureAwait(false);
            });
        }
    }
}
