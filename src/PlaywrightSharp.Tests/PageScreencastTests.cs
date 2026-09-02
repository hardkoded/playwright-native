/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page.screencast</c> start / stop.
    /// </summary>
    [TestFixture]
    public class PageScreencastTests : PageTestEx
    {
        [PlaywrightTest("screencast.spec.ts", "start delivers frames via onFrame callback")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDeliverFramesViaOnFrame()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("WebKit has no Page.startScreencast; Chromium CDP only.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 400).ConfigureAwait(false);

            TaskCompletionSource<ScreencastFrame> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await page.Screencast.StartAsync(frame =>
            {
                tcs.TrySetResult(frame);
                return Task.CompletedTask;
            }).ConfigureAwait(false);

            await page.SetContentAsync("<div style=\"width:100%;height:100%;background:#c00\">cast</div>").ConfigureAwait(false);
            Task winner = await Task.WhenAny(tcs.Task, Task.Delay(10_000)).ConfigureAwait(false);
            Assert.That(winner, Is.SameAs(tcs.Task));

            ScreencastFrame frame = await tcs.Task.ConfigureAwait(false);
            await page.Screencast.StopAsync().ConfigureAwait(false);

            Assert.That(frame.Data, Is.Not.Null);
            Assert.That(frame.Data.Length, Is.GreaterThan(2));
            Assert.That(frame.Data[0], Is.EqualTo(0xFF));
            Assert.That(frame.Data[1], Is.EqualTo(0xD8));
            Assert.That(frame.ViewportWidth, Is.EqualTo(500));
            Assert.That(frame.ViewportHeight, Is.EqualTo(400));
        }

        [PlaywrightTest("screencast.spec.ts", "start throws if screencast is already started")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowIfScreencastIsAlreadyStarted()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("WebKit has no Page.startScreencast; Chromium CDP only.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.Screencast.StartAsync(_ => Task.CompletedTask).ConfigureAwait(false);
            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Screencast.StartAsync(_ => Task.CompletedTask));
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("already started"));
            await page.Screencast.StopAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("screencast.spec.ts", "start is rejected on WebKit")]
        [Test]
        [Timeout(30_000)]
        public async Task StartShouldThrowOnWebKit()
        {
            if (!TestConstants.IsWebKit)
            {
                Assert.Ignore("Chromium implements Page.startScreencast.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Screencast.StartAsync(_ => Task.CompletedTask));
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("not supported"));
        }
    }
}
