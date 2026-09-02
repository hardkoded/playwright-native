/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Expect ToHaveScreenshot animations, caret, omitBackground, and mask.
    /// </summary>
    [TestFixture]
    public class ExpectScreenshotDecorationsTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "animations disabled matches after a delay")]
        [Test]
        [Timeout(30_000)]
        public async Task AnimationsDisabledShouldMatchAfterADelay()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(120, 80).ConfigureAwait(false);
            await page.SetContentAsync(
                "<style>@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }" +
                "div { width: 60px; height: 60px; background: #c00; animation: spin 1s linear infinite; }</style>" +
                "<div id=\"box\"></div>").ConfigureAwait(false);

            byte[] expected = await page.Locator("#box").ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }).ConfigureAwait(false);
            await page.WaitForTimeoutAsync(120).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#box"))
                .ToHaveScreenshotAsync(expected, animations: "disabled")
                .ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "caret hide matches an unfocused field")]
        [Test]
        [Timeout(30_000)]
        public async Task CaretHideShouldMatchAnUnfocusedField()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(240, 80).ConfigureAwait(false);
            await page.SetContentAsync(
                "<style>input,input:focus{outline:none;box-shadow:none;border:1px solid #000;caret-color:red;font-size:40px;width:220px;height:50px}</style>" +
                "<input id=\"i\" value=\"hello\" />").ConfigureAwait(false);

            byte[] expected = await page.ScreenshotAsync().ConfigureAwait(false);
            await page.FocusAsync("#i").ConfigureAwait(false);
            await Assertions.Expect(page)
                .ToHaveScreenshotAsync(expected, caret: "hide")
                .ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "omitBackground is applied to the capture")]
        [Test]
        [Timeout(30_000)]
        public async Task OmitBackgroundShouldBeAppliedToTheCapture()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(80, 50).ConfigureAwait(false);
            await page.SetContentAsync(
                "<style>html,body{margin:0;background:transparent}</style>" +
                "<div id=\"box\" style=\"width:40px;height:30px;background:#0a0\"></div>").ConfigureAwait(false);

            byte[] expected = await page.ScreenshotAsync(new() { OmitBackground = true }).ConfigureAwait(false);
            await Assertions.Expect(page)
                .ToHaveScreenshotAsync(expected, omitBackground: true)
                .ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "mask is applied to the capture")]
        [Test]
        [Timeout(30_000)]
        public async Task MaskShouldBeAppliedToTheCapture()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(200, 120).ConfigureAwait(false);
            await page.SetContentAsync(
                "<style>html,body{margin:0;background:#c00}</style>" +
                "<div id=\"secret\" style=\"position:absolute;left:20px;top:20px;width:40px;height:40px;background:#00c\"></div>").ConfigureAwait(false);

            ILocator[] mask = new[] { page.Locator("#secret") };
            byte[] expected = await page.ScreenshotAsync(new() { Mask = mask, MaskColor = "#00FF00" }).ConfigureAwait(false);
            await Assertions.Expect(page)
                .ToHaveScreenshotAsync(expected, mask: mask, maskColor: "#00FF00")
                .ConfigureAwait(false);
        }
    }
}
