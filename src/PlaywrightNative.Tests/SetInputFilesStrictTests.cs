/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>setInputFiles({ strict })</c>.
    /// </summary>
    [TestFixture]
    public class SetInputFilesStrictTests : PageTestEx
    {
        private static FilePayload WaveFile()
            => new FilePayload
            {
                Name = "wave642.txt",
                MimeType = "text/plain",
                Buffer = Encoding.UTF8.GetBytes("wave642"),
            };

        [PlaywrightTest("page-set-input-files.spec.ts", "strict true throws when two inputs match")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictTrueShouldThrowWhenTwoInputsMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type='file'><input type='file'>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.SetInputFilesAsync("input", WaveFile(), strict: true));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
            Assert.That(ex.Message, Does.Contain("2 elements"));
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "strict true accepts a unique selector")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictTrueShouldAcceptAUniqueSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id='only' type='file'><input type='file'>").ConfigureAwait(false);

            await page.SetInputFilesAsync("#only", WaveFile(), strict: true).ConfigureAwait(false);
            string name = await page.EvaluateAsync<string>("document.getElementById('only').files[0].name").ConfigureAwait(false);
            Assert.That(name, Is.EqualTo("wave642.txt"));
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "strict false overrides context StrictSelectors")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictFalseShouldOverrideContextStrictSelectors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                StrictSelectors = true,
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id='first' type='file'><input id='second' type='file'>").ConfigureAwait(false);

            await page.SetInputFilesAsync("input", WaveFile(), strict: false).ConfigureAwait(false);
            string name = await page.EvaluateAsync<string>("document.getElementById('first').files[0].name").ConfigureAwait(false);
            Assert.That(name, Is.EqualTo("wave642.txt"));
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "frame SetInputFiles honors strict")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameSetInputFilesShouldHonorStrict()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<iframe></iframe>").ConfigureAwait(false);
            IFrame frame = null;
            foreach (IFrame child in page.MainFrame.ChildFrames)
            {
                frame = child;
                break;
            }

            Assert.That(frame, Is.Not.Null);
            await frame.SetContentAsync("<input type='file'><input type='file'>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => frame.SetInputFilesAsync("input", WaveFile(), strict: true));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
