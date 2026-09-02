/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>locator.drop()</c>.
    /// </summary>
    [TestFixture]
    public class LocatorDropTests : PageTestEx
    {
        [PlaywrightTest("page-drop.spec.ts", "Drop dispatches text/plain data")]
        [Test]
        [Timeout(30_000)]
        public async Task DropShouldDispatchTextPlainData()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"z\">drop</div>").ConfigureAwait(false);
            await page.EvaluateAsync(@"(() => {
                const z = document.getElementById('z');
                z.addEventListener('drop', e => {
                    e.preventDefault();
                    z.textContent = e.dataTransfer.getData('text/plain');
                });
                z.addEventListener('dragover', e => e.preventDefault());
            })()").ConfigureAwait(false);

            await page.Locator("#z").DropAsync(new DropPayload
            {
                Data = new Dictionary<string, string>
                {
                    ["text/plain"] = "hello-drop",
                },
            }).ConfigureAwait(false);

            Assert.That(await page.Locator("#z").TextContentAsync().ConfigureAwait(false), Is.EqualTo("hello-drop"));
        }

        [PlaywrightTest("page-drop.spec.ts", "Drop dispatches files")]
        [Test]
        [Timeout(30_000)]
        public async Task DropShouldDispatchFiles()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"z\">drop</div>").ConfigureAwait(false);
            await page.EvaluateAsync(@"(() => {
                const z = document.getElementById('z');
                z.addEventListener('drop', e => {
                    e.preventDefault();
                    z.textContent = e.dataTransfer.files[0] ? e.dataTransfer.files[0].name : '';
                });
                z.addEventListener('dragover', e => e.preventDefault());
            })()").ConfigureAwait(false);

            await page.Locator("#z").DropAsync(new DropPayload
            {
                Files = new[]
                {
                    new FilePayload
                    {
                        Name = "note.txt",
                        MimeType = "text/plain",
                        Buffer = Encoding.UTF8.GetBytes("hi"),
                    },
                },
            }).ConfigureAwait(false);

            Assert.That(await page.Locator("#z").TextContentAsync().ConfigureAwait(false), Is.EqualTo("note.txt"));
        }
    }
}
