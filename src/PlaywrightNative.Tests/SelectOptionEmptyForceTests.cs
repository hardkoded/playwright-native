/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page.selectOption(selector, { force })</c> with omitted values.
    /// </summary>
    [TestFixture]
    public class SelectOptionEmptyForceTests : PageTestEx
    {
        [PlaywrightTest("page-select-option.spec.ts", "force true runs on a hidden select")]
        [Test]
        [Timeout(30_000)]
        public async Task ForceTrueShouldRunOnAHiddenSelect()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<select id='only' style='display:none'><option value='wave682' selected>a</option></select>").ConfigureAwait(false);

            await page.SelectOptionAsync("#only", System.Array.Empty<string>(), new() { Force = true }).ConfigureAwait(false);
            string actual = await page.EvaluateAsync<string>("document.getElementById('only').id").ConfigureAwait(false);
            Assert.That(actual, Is.EqualTo("only"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "omitted force times out on a hidden select")]
        [Test]
        [Timeout(30_000)]
        public async Task OmittedForceShouldTimeoutOnAHiddenSelect()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<select id='only' style='display:none'><option value='wave682' selected>a</option></select>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.SelectOptionAsync("#only", System.Array.Empty<string>(), new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "force true accepts a visible unique selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ForceTrueShouldAcceptAVisibleUniqueSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<select id='only'><option value='wave682' selected>a</option></select>").ConfigureAwait(false);

            await page.SelectOptionAsync("#only", System.Array.Empty<string>(), new() { Force = true }).ConfigureAwait(false);
            string actual = await page.EvaluateAsync<string>("document.getElementById('only').id").ConfigureAwait(false);
            Assert.That(actual, Is.EqualTo("only"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "frame honors force")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameShouldHonorForce()
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
            await frame.SetContentAsync("<select id='only' style='display:none'><option value='wave682' selected>a</option></select>").ConfigureAwait(false);

            await frame.SelectOptionAsync("#only", System.Array.Empty<string>(), new() { Force = true }).ConfigureAwait(false);
            string actual = await frame.EvaluateAsync<string>("document.getElementById('only').id").ConfigureAwait(false);
            Assert.That(actual, Is.EqualTo("only"));
        }
    }
}
