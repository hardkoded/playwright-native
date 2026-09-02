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
    /// Official <c>jshandle-as-element.spec.ts</c>.
    /// </summary>
    [TestFixture]
    public class JSHandleAsElementTests : PageTestEx
    {
        [PlaywrightTest("jshandle-as-element.spec.ts", "should work")]
        [PlaywrightTest("jshandle-as-element.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle aHandle = await page.EvaluateHandleAsync("() => document.body").ConfigureAwait(false);
            Assert.That(aHandle.AsElement(), Is.Not.Null);
        }

        [PlaywrightTest("jshandle-as-element.spec.ts", "should return null for non-elements")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnNullForNonElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle aHandle = await page.EvaluateHandleAsync("() => 2").ConfigureAwait(false);
            Assert.That(aHandle.AsElement(), Is.Null);
        }

        [PlaywrightTest("jshandle-as-element.spec.ts", "should return ElementHandle for TextNodes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnElementHandleForTextNodes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>ee!</div>").ConfigureAwait(false);
            IJSHandle aHandle = await page.EvaluateHandleAsync("() => document.querySelector('div').firstChild").ConfigureAwait(false);
            IElementHandle element = aHandle.AsElement();
            Assert.That(element, Is.Not.Null);
            bool isTextNode = await page.EvaluateAsync<bool>("e => e.nodeType === Node.TEXT_NODE", element).ConfigureAwait(false);
            Assert.That(isTextNode, Is.True);
        }

        [PlaywrightTest("jshandle-as-element.spec.ts", "should work with nullified Node")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithNullifiedNode()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<section>test</section>").ConfigureAwait(false);
            await page.EvaluateAsync("delete Node").ConfigureAwait(false);
            IJSHandle handle = await page.EvaluateHandleAsync("() => document.querySelector('section')").ConfigureAwait(false);
            Assert.That(handle.AsElement(), Is.Not.Null);
        }
    }
}
