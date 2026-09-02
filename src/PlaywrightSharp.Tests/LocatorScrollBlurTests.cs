/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// ScrollIntoViewIfNeeded, Blur, and SelectText on <see cref="ILocator"/>.
    /// </summary>
    [TestFixture]
    public class LocatorScrollBlurTests : PageTestEx
    {
        [PlaywrightTest("locator-convenience.spec.ts", "ScrollIntoViewIfNeeded brings an offscreen element into view")]
        [Test]
        [Timeout(30_000)]
        public async Task ScrollIntoViewIfNeededShouldBringOffscreenElementIntoView()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 400).ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"height:2000px\"></div><div id=\"t\">target</div>").ConfigureAwait(false);

            ILocator target = page.Locator("#t");
            var before = await target.BoundingBoxAsync().ConfigureAwait(false);
            Assert.That(before, Is.Not.Null);
            Assert.That(before.Y, Is.GreaterThan(400));

            await target.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);

            var after = await target.BoundingBoxAsync().ConfigureAwait(false);
            Assert.That(after, Is.Not.Null);
            Assert.That(after.Y, Is.GreaterThanOrEqualTo(0f));
            Assert.That(after.Y, Is.LessThan(400f));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "Blur removes focus")]
        [Test]
        [Timeout(30_000)]
        public async Task BlurShouldRemoveFocus()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"name\" /><input id=\"other\" />").ConfigureAwait(false);

            ILocator name = page.Locator("#name");
            await name.FocusAsync().ConfigureAwait(false);
            string focused = await page.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);
            Assert.That(focused, Is.EqualTo("name"));

            await name.BlurAsync().ConfigureAwait(false);

            string after = await page.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);
            Assert.That(after, Is.Not.EqualTo("name"));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "SelectText selects input text")]
        [Test]
        [Timeout(30_000)]
        public async Task SelectTextShouldSelectInputText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"n\" value=\"hello\" />").ConfigureAwait(false);

            await page.Locator("#n").SelectTextAsync().ConfigureAwait(false);

            Assert.That(await page.EvalOnSelectorAsync<int>("#n", "el => el.selectionStart").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await page.EvalOnSelectorAsync<int>("#n", "el => el.selectionEnd").ConfigureAwait(false), Is.EqualTo(5));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "SelectText is strict")]
        [Test]
        [Timeout(30_000)]
        public async Task SelectTextShouldThrowWhenTwoMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input class=\"x\" value=\"a\" /><input class=\"x\" value=\"b\" />").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Locator(".x").SelectTextAsync());

            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
