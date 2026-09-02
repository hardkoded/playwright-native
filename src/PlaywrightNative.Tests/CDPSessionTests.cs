/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="ICDPSession"/>.
    /// </summary>
    [TestFixture]
    public class CDPSessionTests : PageTestEx
    {
        [PlaywrightTest("session.spec.ts", "page NewCDPSession evaluates via Runtime")]
        [Test]
        [Timeout(30_000)]
        public async Task PageSessionShouldEvaluate()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("CDP sessions are Chromium-only.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            ICDPSession session = await page.NewCDPSessionAsync().ConfigureAwait(false);
            JsonElement? response = await session.SendAsync("Runtime.evaluate", new
            {
                expression = "7 + 8",
                returnByValue = true,
            }).ConfigureAwait(false);

            Assert.That(response.HasValue, Is.True);
            JsonElement result = response.Value.GetProperty("result").GetProperty("value");
            Assert.That(result.GetInt32(), Is.EqualTo(15));

            int pageValue = await page.EvaluateAsync<int>("1 + 1").ConfigureAwait(false);
            Assert.That(pageValue, Is.EqualTo(2));
        }

        [PlaywrightTest("session.spec.ts", "context NewCDPSession matches page session")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextSessionShouldTalkToThePage()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("CDP sessions are Chromium-only.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            ICDPSession session = await context.NewCDPSessionAsync(page).ConfigureAwait(false);
            JsonElement? response = await session.SendAsync("Runtime.evaluate", new
            {
                expression = "21 * 2",
                returnByValue = true,
            }).ConfigureAwait(false);

            Assert.That(response.Value.GetProperty("result").GetProperty("value").GetInt32(), Is.EqualTo(42));
        }

        [PlaywrightTest("session.spec.ts", "browser NewBrowserCDPSession reports version")]
        [Test]
        [Timeout(30_000)]
        public async Task BrowserSessionShouldReportVersion()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("CDP sessions are Chromium-only.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            ICDPSession session = await browser.NewBrowserCDPSessionAsync().ConfigureAwait(false);
            JsonElement? response = await session.SendAsync("Browser.getVersion").ConfigureAwait(false);

            Assert.That(response.HasValue, Is.True);
            string product = response.Value.GetProperty("product").GetString();
            Assert.That(product, Does.Contain("Chrome").Or.Contain("Chromium").Or.Contain("Headless"));
        }

        [PlaywrightTest("session.spec.ts", "Detach prevents further Send")]
        [Test]
        [Timeout(30_000)]
        public async Task DetachShouldPreventFurtherSend()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("CDP sessions are Chromium-only.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            ICDPSession session = await page.NewCDPSessionAsync().ConfigureAwait(false);
            await session.DetachAsync().ConfigureAwait(false);

            TargetClosedException ex = Assert.ThrowsAsync<TargetClosedException>(
                async () => await session.SendAsync("Runtime.evaluate", new { expression = "1" }).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("Session closed").Or.Contain("closed"));
        }

        [PlaywrightTest("session.spec.ts", "WebKit NewCDPSession throws")]
        [Test]
        [Timeout(30_000)]
        public async Task WebKitShouldRejectCdpSession()
        {
            if (!TestConstants.IsWebKit)
            {
                Assert.Ignore("This assertion is WebKit-only.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            PlaywrightNativeException pageEx = Assert.ThrowsAsync<PlaywrightNativeException>(
                async () => await page.NewCDPSessionAsync().ConfigureAwait(false));
            Assert.That(pageEx.Message, Does.Contain("Chromium"));

            PlaywrightNativeException browserEx = Assert.ThrowsAsync<PlaywrightNativeException>(
                async () => await browser.NewBrowserCDPSessionAsync().ConfigureAwait(false));
            Assert.That(browserEx.Message, Does.Contain("Chromium"));
        }

        [PlaywrightTest("session.spec.ts", "context NewCDPSession on a frame")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameSessionShouldEvaluate()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("CDP sessions are Chromium-only.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<iframe src=\"about:blank\"></iframe>").ConfigureAwait(false);

            IElementHandle iframe = await page.QuerySelectorAsync("iframe").ConfigureAwait(false);
            IFrame frame = await iframe.ContentFrameAsync().ConfigureAwait(false);
            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(
                async () => await context.NewCDPSessionAsync(frame).ConfigureAwait(false));
            Assert.That(
                ex.Message,
                Does.Contain("This frame does not have a separate CDP session, it is a part of the parent frame's session"));
        }
    }
}
