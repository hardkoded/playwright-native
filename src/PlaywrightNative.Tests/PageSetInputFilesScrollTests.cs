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
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>scroll</c> option on <see cref="IPage.SetInputFilesAsync"/>.
    /// </summary>
    [TestFixture]
    public class PageSetInputFilesScrollTests : PageTestEx
    {
        [PlaywrightTest("page-set-input-files.spec.ts", "SetInputFiles Auto scrolls the target into view")]
        [Test]
        [Timeout(30_000)]
        public async Task AutoShouldScrollTheTargetIntoView()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
                <div id='box' style='width:240px;height:120px;overflow:auto;border:1px solid #000'>
                  <div style='height:600px'></div>
                  <input id='btn' type='file'>
                </div>").ConfigureAwait(false);

            double before = await page.EvaluateAsync<double>("document.getElementById('box').scrollTop").ConfigureAwait(false);
            await page.SetInputFilesAsync("#btn", new[]
            {
                new FilePayload
                {
                    Name = "wave431.txt",
                    MimeType = "text/plain",
                    Buffer = Encoding.UTF8.GetBytes("hello"),
                },
            }).ConfigureAwait(false);
            double after = await page.EvaluateAsync<double>("document.getElementById('box').scrollTop").ConfigureAwait(false);
            string name = await page.EvaluateAsync<string>("document.getElementById('btn').files[0].name").ConfigureAwait(false);

            Assert.That(before, Is.EqualTo(0));
            Assert.That(after, Is.GreaterThan(0));
            Assert.That(name, Is.EqualTo("wave431.txt"));
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "SetInputFiles scroll None does not scroll")]
        [Test]
        [Timeout(30_000)]
        public async Task ScrollNoneShouldNotScrollThePage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
                <div id='box' style='width:240px;height:120px;overflow:auto;border:1px solid #000'>
                  <div style='height:600px'></div>
                  <input id='btn' type='file'>
                </div>").ConfigureAwait(false);

            await page.SetInputFilesAsync(
                "#btn",
                new[]
                {
                    new FilePayload
                    {
                        Name = "wave431.txt",
                        MimeType = "text/plain",
                        Buffer = Encoding.UTF8.GetBytes("hello"),
                    },
                },
                force: true,
                scroll: ActionScroll.None).ConfigureAwait(false);
            double after = await page.EvaluateAsync<double>("document.getElementById('box').scrollTop").ConfigureAwait(false);
            string name = await page.EvaluateAsync<string>("document.getElementById('btn').files[0].name").ConfigureAwait(false);
            Assert.That(after, Is.EqualTo(0));
            Assert.That(name, Is.EqualTo("wave431.txt"));
        }
    }
}
